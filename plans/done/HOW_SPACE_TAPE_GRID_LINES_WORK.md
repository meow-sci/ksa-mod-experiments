# How Space-Tape Grid Lines Work

> Researched from: `space-tape.lib/CameraSnapController.cs`, `decomp/ksa/KSA/GizmosRenderer.cs`,
> `decomp/ksa/KSA/GizmoPass.cs`, `decomp/ksa/Content/Core/Shaders/Gizmos/LineGizmo.vert`,
> `decomp/ksa/Content/Core/Shaders/Gizmos/Gizmo.frag`

---

## Summary

The grid lines are rendered via **KSA's `GizmosRenderer`** — a dedicated debug/gizmo rendering system built on **Vulkan** that accumulates line (and sphere) draw calls each frame and then flushes them to the GPU in a single batch draw. The grid is drawn once per frame during the scene update, not during the ImGui UI pass.

---

## Step-by-Step Pipeline

### 1. Entry Point: `SpaceTapeSubmod.UpdateScene(Viewport viewport)`

`UpdateScene` is called once per frame from a game render-loop patch (via `PartRenderHelper.Patch()`). After updating gizmos and interaction, it calls:

```csharp
_cameraSnap.DrawGrid(viewport, _scene);
```

### 2. `CameraSnapController.DrawGrid()` — Line Submission

`DrawGrid` guards on `GridVisible && ActiveMode != None && scene.IsActive`, then:

1. Gets the current **Asmb2Ego** transform matrix (assembly-to-ego = world space relative to the camera).
2. Determines the **grid plane** based on the active camera snap mode:
   - Front/Back → YZ plane (axisU = Z, axisV = Y)
   - Left/Right → XZ plane (axisU = X, axisV = Z)
   - Top/Bottom → XY plane (axisU = X, axisV = Y)
3. Loops over all horizontal and vertical grid lines, computes start/end points in assembly space, transforms them to ego space, and calls:

```csharp
Program.GizmosRenderer.DrawLine(startEgo, endEgo, color);
```

The color used is either `GridColor` (translucent gray) or `GridAxisColor` (brighter yellow) for lines that pass through the origin. Both are `float4` values (RGBA, 0-1 range).

### 3. `GizmosRenderer.DrawLine()` — CPU-Side Accumulation

`GizmosRenderer` is a class owned by `Program` (the KSA game host). It maintains a flat array:

```csharp
private LineGizmoVertex[] _lineInstances = new LineGizmoVertex[131072]; // max ~65k lines
```

where each `LineGizmoVertex` is:

```csharp
public struct LineGizmoVertex {
    public float3 position;  // ego-space endpoint
    public uint color;       // packed RGBA (see EncodeColor)
}
```

`DrawLine` pushes two vertices (start + end) and increments `_instanceCounts[1]` by 2. This is a **CPU-side buffer only** at this point — no GPU work yet.

The color packing in `EncodeColor`:

```csharp
private uint EncodeColor(float4 color) {
    byte r = (byte)(color.X * 255f);
    byte g = (byte)(color.Y * 255f);
    byte b = (byte)(color.Z * 255f);
    byte a = (byte)(color.W * 255f);
    return (uint)((a << 24) | (b << 16) | (g << 8) | r);
}
```

Alpha **is** preserved in the packed integer at this stage.

### 4. `GizmoPass` — Render Pass Structure

Gizmos render in a dedicated Vulkan render pass (`GizmoPass`) separate from the main scene. It has **2 subpasses**:
- Subpass 0: render gizmos into an offscreen color+depth buffer
- Subpass 1: composite the result back into the main scene color buffer

The depth buffer starts cleared each frame (LoadOp = Clear), so gizmos interact with the 3D scene through depth testing only.

### 5. `GizmosRenderer.RenderLines()` — GPU Draw

At render time, `GizmosRenderer.Render()` is called from the `GizmoPass`. For lines:

```csharp
public void RenderLines(CommandBuffer commandBuffer, Viewport viewport, int frameIndex) {
    // Copy accumulated CPU data to mapped GPU memory
    _lineInstances.CopyTo(_lineInstanceMemory.AsSpan<LineGizmoVertex>());
    
    // Bind the line vertex buffer
    commandBuffer.BindVertexBuffer(0, _lineInstanceBuffer, ...);
    
    // Bind the line pipeline (LineGizmo.vert + Gizmo.frag)
    commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipelines[1]);
    
    // One draw call for all accumulated lines
    commandBuffer.Draw(_instanceCounts[1], 1, 0, 0);
}
```

The pipeline was created with:
- Topology: `Presets.InputAssembly.LineList` (pairs of vertices = one line each)
- Rasterization: `Presets.Rasterization.Fill.CullNone`
- Depth: `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` — depth tested **and written**
- Blend: `Presets.BlendState.BlendColorAlpha` — alpha blending IS enabled at the pipeline level

### 6. `LineGizmo.vert` — Vertex Shader

Source: `Content/Core/Shaders/Gizmos/LineGizmo.vert` (loaded as `"DebugGizmoLineVert"`)

The entire `LineGizmoVertex` struct is read as a single `vec4`:

```glsl
layout(location = 0) in vec4 position_color;  // xyz = ego-space position, w = packed color
```

Color is decoded via `ReinterpretColor()`:

```glsl
vec4 ReinterpretColor(float packedColor) {
    uint colorInt = floatBitsToUint(packedColor);
    uint r = colorInt & 0xFFu;
    uint g = (colorInt >> 8u) & 0xFFu;
    uint b = (colorInt >> 16u) & 0xFFu;
    uint a = (colorInt >> 24u) & 0xFFu;
    return vec4(float(r)/255.0, float(g)/255.0, float(b)/255.0, float(a)/255.0);
}
```

Alpha **is** still preserved here. It outputs the decoded color as `gizmoColor` at `location = 2`, along with `normal = normalize(position_color.xyz)` and `worldPosition = position_color.xyz` (the ego-space position doubles as the "normal" — this is a notable quirk for lines vs. spheres).

### 7. `Gizmo.frag` — Fragment Shader  ⚠️ ROOT CAUSE OF THE COLOR PROBLEM

Source: `Content/Core/Shaders/Gizmos/Gizmo.frag` (loaded as `"DebugGizmoFrag"`)

**This shader is shared between lines and spheres.**

```glsl
layout(location = 2) in vec4 color;   // received from LineGizmo.vert as gizmoColor

void main() {
    vec3 nrm = normalize(normal);
    vec3 camForward = -normalize(vec3(global.camera.inverseView[2]));
    
    vec3 viewDir = normalize(worldPosition);
    
    // Fresnel effect
    float fresnel = pow(1.0 - max(0.0, dot(viewDir, -nrm)), 0.75f);
    fresnel *= 0.5f;
    
    vec3 baseColor = color.xyz;
    vec3 fresnelColor = vec3(1.0f, 1.0f, 1.0f) * fresnel;
    
    outColor = vec4(mix(baseColor, fresnelColor, fresnel), 1);   // ⚠️ alpha HARDCODED to 1
}
```

There are **two problems** here:

#### Problem 1: Alpha is hardcoded to `1`

The final `outColor.w` is always `1` (fully opaque), regardless of what alpha value was passed in `color.w`. Even though the Vulkan pipeline has `BlendColorAlpha` alpha-blending enabled, it has no effect because every fragment outputs `alpha = 1.0`. This means **opacity/transparency control via `GridColor.W` or `GridAxisColor.W` does not work.**

#### Problem 2: Fresnel effect distorts RGB

The fresnel calculation uses `worldPosition` (which, for line vertices, is the ego-space position, not a surface normal) and `normal` (which is `normalize(ego-space-position)` — the normalized direction from the camera to the vertex endpoint). This means:

- Lines whose endpoints are far from the camera origin (large ego-space position magnitude) produce `normal ≈ normalize(bigVector)` which behaves differently than lines near the origin.
- The RGB color is blended toward white based on `fresnel`, so lines do not render as the exact color set via `GridColor`/`GridAxisColor`.
- The fresnel effect was designed for sphere gizmos (where a surface normal makes geometric sense), and produces **undefined/incidental behavior** on line endpoints.

---

## Why Color Settings "Don't Work" — Root Cause Summary

| Setting | What we pass | What shader uses | Effect |
|---|---|---|---|
| `GridColor.W` (alpha) | e.g. `0.4f` | Ignored — overridden by `1` in frag shader | No transparency |
| `GridColor.XYZ` (RGB) | e.g. gray `(0.5, 0.5, 0.5)` | Fresnel-modified toward white | Colors appear brighter/different than set |
| `GridAxisColor` | e.g. yellow `(0.8, 0.8, 0.2)` | Fresnel-modified | Colors appear lighter/washed out |

The pipeline **infrastructure** supports alpha blending, and the **C# `EncodeColor`** and **`LineGizmo.vert`** both preserve the alpha value correctly through to the fragment shader — but `Gizmo.frag` throws it away.

---

## Questions for the KSA Developers

The goal is to understand if/how we could control color and opacity of lines drawn via `GizmosRenderer.DrawLine()`:

1. **Is the hardcoded `alpha = 1` in `Gizmo.frag` intentional for gizmos, or is it an oversight?**
   Since the Vulkan pipeline already uses `BlendColorAlpha`, it seems like the infrastructure was built to support transparency, but the shader doesn't use it.

2. **Would it be possible to change `Gizmo.frag` to use `color.w` instead of hardcoding `1`?**
   Specifically: `outColor = vec4(mix(baseColor, fresnelColor, fresnel), color.w);`  
   This would be a minimal change that would make the alpha channel that's already being passed through actually take effect.

3. **Is there a way to opt out of the fresnel effect for lines?** The fresnel effect makes sense for sphere gizmos but produces incidental/misleading results for line gizmos because line endpoints don't have meaningful surface normals. A plain `outColor = color;` mode for lines would give predictable color output.

4. **Are there plans to expose a separate fragment shader for line gizmos** (e.g. `DebugGizmoLineFrag`) that could output color directly without the fresnel modification?

5. **Is there a separate `DrawLine` overload or API we may have missed** that allows for exact color/opacity control?

6. **Where exactly in the frame is `GizmoPass.Render()` called** relative to bloom, tone-mapping, and other post-processing effects? If gizmos are rendered before post-processing, the effective visual result may be tone-mapped, which further changes perceived brightness/color.

---

## File Map

| File | Role |
|---|---|
| `space-tape.lib/CameraSnapController.cs` | Grid state + submits `DrawLine` calls per frame |
| `space-tape.lib/SpaceTapeSubmod.cs` | Calls `_cameraSnap.DrawGrid()` each frame in `UpdateScene()` |
| `decomp/ksa/KSA/GizmosRenderer.cs` | CPU accumulation buffer + Vulkan pipeline setup + GPU render |
| `decomp/ksa/KSA/GizmoPass.cs` | Render pass structure (offscreen target + composite back to scene) |
| `decomp/ksa/Content/Core/Shaders/Gizmos/LineGizmo.vert` | Vertex shader: unpacks position + color, outputs to frag |
| `decomp/ksa/Content/Core/Shaders/Gizmos/Gizmo.frag` | Fragment shader: applies fresnel, **ignores alpha (hardcodes 1)** |
