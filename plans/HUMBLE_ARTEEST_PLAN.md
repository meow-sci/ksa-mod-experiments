# Humble Arteest — Part Painting Deep Dive Analysis & Implementation Plan

## Executive Summary

**Can we "paint" vehicle parts at runtime in KSA? YES — but with important caveats.**

After a comprehensive analysis of the KSA decompiled source code (rendering pipeline, shaders, material system, part data model, existing mods), we've identified multiple viable approaches. The game has NO existing paint/color/variant/livery system for parts, but the rendering architecture has clear injection points. The primary challenge is that as a **mod** (not a game rebuild), we can't directly modify compiled C# structs or shader binaries — we must work through Harmony patching and potentially shader file replacement.

### Key Findings At A Glance

| Finding | Detail |
|---------|--------|
| **Existing paint system?** | ❌ None — colors are baked into texture atlases |
| **MaterialData.AlbedoColor?** | ✅ Exists, multiplied with textures — but only in **legacy** shader path (ModelPbr.frag), NOT the modern indirect path (MeshIndirect.frag) |
| **Per-instance color field?** | ❌ Not present — PerInstanceData only has transform + StateBitFlag (+ Temperature/TFI for dynamic parts) |
| **Padding bytes available?** | ✅ 12 bytes of padding in PerInstanceData (3 ints) — could pack RGBA color |
| **Shader files on disk?** | ✅ GLSL source files in Content/Core/Shaders/ — potentially replaceable |
| **Highlight color mixing?** | ✅ Fragment shader already does `mix(color, tintColor, 0.5)` for Highlighted/Grabbed states |
| **Temperature per-instance?** | ✅ FxTemperature demonstrates per-instance visual effects flowing to GPU |
| **Harmony patch points?** | ✅ `PartModelModule.UpdateRenderData()` already patched by blinky mod |
| **Shared materials?** | ⚠️ Materials are shared per part family — modifying one affects ALL parts in that family |

---

## Architecture Overview

### Rendering Pipeline Flow

```
Part Instance (in Vehicle)
    │
    ▼
PartModelModule.UpdateRenderData()           ← HARMONY PATCH POINT
    │  Creates PerInstanceData { ModelMatrix, StateBitFlag }
    ▼
PartModel.AddInstance(instanceData, viewport, frameIndex)
    │  Stages to per-viewport list
    ▼
PartModel.WriteInstancesToGpu()
    │  Writes to GPU storage buffers:
    │    - PerInstanceDataVectors (Set 3, Binding 0) → Vertex Shader
    │    - PerDrawDataVectors (Set 2, Binding 0)     → Fragment Shader
    │    - DrawCommandVectors (indirect draws)
    ▼
PartModelRenderer.WriteCommands()
    │  Binds pipeline + descriptor sets + push constants
    ▼
vkCmdDrawIndexedIndirect()
    │
    ▼
Vertex Shader (MeshIndirect.vert)
    │  Reads InstanceStorage.Data[gl_InstanceIndex]
    │  Passes StateBitFlag → outHighlighted
    ▼
Fragment Shader (MeshIndirect.frag)
    │  Reads PerDrawData for texture indices
    │  Samples textures via bindless handles
    │  Applies highlight color mixing via StateBitFlag
    ▼
Output Color
```

### Three Part Rendering Paths

| Path | Module | PerInstanceData Fields | Shader |
|------|--------|----------------------|--------|
| **Static** | `PartModelModule` | ModelMatrix, StateBitFlag, *3 padding ints* | MeshIndirect.vert/frag |
| **Dynamic** | `PartModelDynamicModule` | ModelMatrix, StateBitFlag, Temperature, TfiThickness, *1 padding int* | DynamicMeshIndirect.vert/frag |
| **Glass** | `PartModelGlassModule` | ModelMatrix, StateBitFlag, *3 padding ints* | MeshGlassIndirect.frag |

### Key Data Structures

**PerInstanceData (Static — 80 bytes):**
```csharp
public struct PerInstanceData
{
    public float4x4 ModelMatrix;      // 64 bytes — transform
    public int StateBitFlag;          //  4 bytes — visual state bits 0-5
    private int packing1;             //  4 bytes — AVAILABLE
    private int packing2;             //  4 bytes — AVAILABLE
    private int packing3;             //  4 bytes — AVAILABLE
}
```

**PerInstanceData (Dynamic — 80 bytes):**
```csharp
public struct PerInstanceData
{
    public float4x4 ModelMatrix;      // 64 bytes
    public int StateBitFlag;          //  4 bytes
    public float Temperature;         //  4 bytes — heat glow
    public float TfiThickness;        //  4 bytes — thin film
    private int packing1;             //  4 bytes — AVAILABLE
}
```

**PerDrawData (20 bytes):**
```csharp
public struct PerDrawData
{
    public int DiffuseTextureIndex;   // Bindless texture handle
    public int NormalTextureIndex;
    public int PbrTextureIndex;
    public int EmissiveTextureIndex;
    public int TfiTextureIndex;
}
```

**StateBitFlag encoding (in fragment shader):**
```glsl
Bit 0: Highlighted  → mix(color, RED,    0.5)
Bit 1: Grabbed      → mix(color, YELLOW, 0.5)
Bit 2: FakeTranslucent → mix(color, transparent, 0.75)
Bit 3: Selected     → writes to selectedMap texture
Bit 4: IsEditedVehicle
Bit 5: IVA Mode
Bits 6-31: UNUSED
```

### Material System

- Materials defined per **part family** (e.g., `CoreCommandA_Material`)
- All parts in a family share ONE material with ONE texture atlas
- `MaterialData.AlbedoColor` (float4) exists but is set to `float4.One` (white/no tint) and only used in the **legacy** ModelPbr.frag path
- The **modern indirect path** (`MeshIndirect.frag`) reads texture indices from `PerDrawData` directly and does NOT apply `AlbedoColor`
- ~10 part families, each with Diffuse, Normal, PBR, Emissive, TFI texture atlases

---

## Viable Approaches (Ranked)

### 🥇 Approach A: Shader Replacement + PerInstanceData Padding Hijack (RECOMMENDED)

**Concept:** Replace GLSL shader source files on disk with modified versions that read a color tint from the PerInstanceData padding bytes. Harmony-patch the C# side to write color data into those padding slots.

**Why this is best:**
- Per-instance granularity (each part gets its own color)
- Uses existing GPU data flow (no new buffers or descriptor sets)
- 12 bytes of padding available (enough for packed RGBA or RGB floats)
- Shader already demonstrates the `mix()` color overlay pattern
- Harmony patching `UpdateRenderData()` is a proven pattern (blinky does it)

**C# Side (Harmony patch):**
```csharp
// Patch PartModelModule.UpdateRenderData() to pack color into padding
[HarmonyPatch(typeof(PartModelModule), nameof(PartModelModule.UpdateRenderData))]
public static class PaintPatch
{
    public static bool Prefix(PartModelModule __instance, ...)
    {
        // ... compute standard instanceData ...
        
        // Pack paint color into padding bytes
        float4 paintColor = PaintManager.GetPaintColor(__instance.Parent);
        // Write paintColor.R into packing1, paintColor.G into packing2, etc.
        // via unsafe pointer or reflection on the struct
        
        PartModel.AddInstance(instanceData, viewport, frameIndex);
        return false; // Skip original
    }
}
```

**Shader Side (modified MeshIndirect.frag):**
```glsl
// Read paint color from what was padding in PerInstanceData
struct InstanceData {
    mat4 WorldMatrix;
    int Highlighted;
    float PaintR;      // was packing1
    float PaintG;      // was packing2
    float PaintB;      // was packing3
};

// In fragment shader, after albedo sampling:
vec3 paintColor = vec3(inPaintR, inPaintG, inPaintB);
if (paintColor != vec3(0.0)) {
    sampledColor *= paintColor;  // Multiply tint
}
```

**Risks & Unknowns:**
- ⚠️ **Does the game load shaders from GLSL source files at runtime?** (Must test — if pre-compiled to SPIR-V, this won't work)
- ⚠️ **Will the game overwrite shader files on update?** (Need to copy/manage shader overrides)
- ⚠️ **Struct alignment between C# and GLSL** — must verify the padding bytes map exactly to expected shader locations
- ⚠️ **Need to handle all three paths** (static, dynamic, glass) with consistent struct layouts
- ⚠️ **Game is launched via StarMap mod loader** — `Process.MainModule` points to `C:\StarMap`, NOT the KSA game directory. Must resolve the game install path from the `KSA.dll` assembly location or fall back to the well-known path `C:\Program Files\Kitten Space Agency\`.

**Required Tests:**
1. Verify shader files are loaded from disk at runtime
2. Verify a trivially modified shader (e.g., change highlight color) takes effect
3. Verify padding bytes in PerInstanceData are passed through to the shader unchanged
4. Test struct alignment between C# `PerInstanceData` and GLSL `InstanceData`

---

### 🥈 Approach B: Runtime Material Cloning via GPU Buffer

**Concept:** Create new `MaterialData` entries in the GPU material storage buffer with modified `AlbedoColor`, then redirect parts to use the cloned material.

**Why this could work:**
- `MaterialData.AlbedoColor` is already multiplied with albedo texture in `MaterialSet.glsl`
- `GpuMaterialSystem.CreateObject()` provides the allocation mechanism
- `VkUtils.StageAndUploadToBuffer()` handles GPU uploads
- No shader modifications needed (if the legacy path is used)

**Critical Problem:**
- The **modern indirect rendering path** (`MeshIndirect.frag`) does NOT use `MaterialSet.glsl` or `AlbedoColor` — it reads texture indices from `PerDrawData` directly
- This approach ONLY works if parts use the legacy `ModelPbr.frag` path
- Need to verify which rendering path is actually active for vehicle parts

**If legacy path IS used (or can be forced):**
1. Clone material via `GpuMaterialSystem.CreateObject()` with new AssetName
2. Set `AlbedoColor = desiredTintColor` on the clone
3. Harmony-patch `PartModel.Get()` or constructor to assign cloned material per-part
4. Potentially patch `WriteInstancesToGpu()` to use clone's texture handles in PerDrawData

**Risks:**
- ⚠️ Likely does NOT affect the indirect rendering path (which is the modern/primary one)
- ⚠️ GPU memory overhead from material clones
- ⚠️ Need to manage material lifecycle and avoid leaks
- ⚠️ Complex reflection chain to access GpuMaterialSystem internals

**Required Tests:**
1. Determine which shader path (legacy vs indirect) is used for vehicle parts
2. Test if modifying an existing material's AlbedoColor in the GPU buffer changes appearance
3. Test if creating a new MaterialData object at runtime works

---

### 🥉 Approach C: Temperature LUT Hijack (Limited but Simple)

**Concept:** Replace the temperature color lookup table texture with a custom color palette, then use the existing `Temperature` field on dynamic parts to index into desired colors.

**Why this is interesting:**
- NO shader modifications needed
- NO struct modifications needed
- Temperature field already flows per-instance to fragment shader
- Just need to:
  1. Replace the temperature LUT texture
  2. Harmony-patch `PartModelDynamicModule.UpdateRenderData()` to set custom Temperature values

**Limitations:**
- Only works for parts using `PartModelDynamic` (parts with temperature effects)
- Color is ADDITIVE (heat glow is added on top, not multiplied)
- Limited color range (1D palette, indexed by single float)
- Can't have zero tint (0.0 temperature = no glow, but any tint adds glow)
- All standard parts use `PartModel` (static), NOT `PartModelDynamic`

**Verdict:** Too limited for a general painting system, but could be a quick proof-of-concept for demonstrating per-instance color variation on dynamic parts.

---

### Approach D: Post-Processing Compute Overlay

**Concept:** Create a compute shader pass similar to `PartSelectedRenderer` that applies per-part color overlays.

**Why it's architecturally clean:**
- `PartSelectedRenderer` already demonstrates per-pixel object identification + color overlay
- Uses compute shaders that read from a selection image and write to the main color target
- Could extend this to support arbitrary per-part colors

**Challenges:**
- Requires creating new Vulkan resources (image, descriptor sets, compute pipeline)
- Need to create or modify a compute shader
- Need to write part IDs to a buffer during rendering (requires render pass changes)
- High complexity for a mod

**Verdict:** Elegant but too complex for initial implementation. Consider for v2 if simpler approaches work.

---

### Approach E: Highlight System Extension (Quick & Dirty)

**Concept:** Use unused StateBitFlag bits (6-31) to encode color information, then modify the shader to decode and apply colors from those bits.

**How:**
- Pack a color palette index into bits 6-13 (256 colors)
- Modify fragment shader to decode palette index and apply tint
- Harmony-patch `UpdateRenderData()` to set the bits

**Pros:** Minimal struct changes, uses existing data flow
**Cons:** Still requires shader replacement, limited to 256-color palette with bit packing, and bits are integer-only (no smooth gradients without a palette)

**Verdict:** Viable if shader replacement works, but Approach A gives full RGB with the same prerequisites.

---

## Recommended Strategy: Phased Implementation

### Phase 0: Feasibility Validation (CRITICAL)

Before any real implementation, these experiments must be performed to de-risk the approach:

#### Experiment 0.1: Shader File Loading Test
**Goal:** Determine if KSA loads shader GLSL source files from `Content/Core/Shaders/` at runtime.
**Method:**
1. Make a trivial change to `MeshIndirect.frag` — change the Highlighted color from red `(1.0, 0.0, 0.0)` to bright green `(0.0, 1.0, 0.0)`
2. Launch the game
3. Hover over a part in the editor (triggers highlight)
4. Observe if highlight is green (shader loaded from disk) or red (pre-compiled)

**✅ RESULT: PASSED** — Part highlights turned green in the vehicle editor. KSA loads GLSL shader source files from `Content/Core/Shaders/` at runtime and compiles them. **Approach A is viable.**

**If shaders load from disk:** Approach A is viable → proceed to Phase 1
**If shaders are pre-compiled:** Approach A is blocked → investigate SPIR-V replacement or pivot to Approach B

#### Experiment 0.2: PerInstanceData Padding Passthrough Test
**Goal:** Verify that padding bytes in PerInstanceData are passed through to the shader unchanged.
**Method:**
1. Harmony-patch `PartModelModule.UpdateRenderData()` to write known values into `packing1` (e.g., `1065353216` which is `1.0f` as int bits)
2. Modify `MeshIndirect.frag` to read the value and use it as a color component (e.g., make all parts red if the value matches)
3. This confirms the C# struct layout matches the GLSL struct layout

#### Experiment 0.3: Material AlbedoColor Path Test
**Goal:** Determine if the indirect rendering path uses `MaterialData.AlbedoColor`.
**Method:**
1. Use reflection to access `GpuMaterialSystem` and find a material handle
2. Modify the `AlbedoColor` in the GPU buffer to a bright red
3. Observe if parts using that material turn red
4. If yes: Approach B (material cloning) is viable as a simpler alternative
5. If no: Confirms the indirect path ignores AlbedoColor

#### Experiment 0.4: Temperature Visual Test
**Goal:** Quick proof that per-instance visual modification works through existing fields.
**Method:**
1. Harmony-patch `PartModelDynamicModule.UpdateRenderData()` to set `Temperature = 1.0f` on all dynamic parts
2. Observe if parts glow (confirms per-instance data flows to shader correctly)
3. Try replacing the temperature LUT texture with a custom gradient

### Phase 1: Minimal Viable Paint (MVP)

Assuming Phase 0 validates shader replacement + padding passthrough:

#### Step 1.1: Shader Modifications
Modify these shader files to read paint color from PerInstanceData padding:
- `Mesh/MeshIndirect.vert` — pass paint color from storage buffer to fragment
- `Mesh/MeshIndirect.frag` — apply paint tint to albedo before lighting
- `Mesh/DynamicMeshIndirect.vert` — same for dynamic parts
- `Mesh/DynamicMeshIndirect.frag` — same for dynamic parts
- `Mesh/MeshGlassIndirect.frag` — same for glass parts

Tint application in fragment shader:
```glsl
// After albedo sampling:
vec3 paintTint = vec3(inPaintR, inPaintG, inPaintB);
float paintStrength = length(paintTint);  // 0 = no paint
if (paintStrength > 0.001) {
    // Normalize + apply as multiplicative tint
    sampledColor.rgb *= paintTint;
}
```

#### Step 1.2: Harmony Patches
- Patch `PartModelModule.UpdateRenderData()` — pack color into padding
- Patch `PartModelDynamicModule.UpdateRenderData()` — pack color into its padding slot
- Patch `PartModelGlassModule.UpdateRenderData()` — pack color into padding
- Use `HotkeyGuard.Patch()` per mod requirements

#### Step 1.3: Paint State Management
- `PaintManager` class in `humble-arteest.lib`:
  - `Dictionary<uint, float4>` mapping Part InstanceId → paint color
  - Thread-safe reads (UpdateRenderData runs on game thread)
  - Methods: `SetPaintColor(Part, float4)`, `GetPaintColor(Part)`, `ClearPaint(Part)`

#### Step 1.4: Basic UI
- ImGui window with:
  - Vehicle selector (filterable combo)
  - Part list for selected vehicle (tree view)
  - RGB color picker (`ImGui.ColorEdit3`)
  - "Paint Selected Part" button
  - "Paint All Parts" button
  - "Clear Paint" button

### Phase 2: Persistence & UX Polish

#### Step 2.1: Save/Load Paint Schemes
- Serialize paint colors to TOML file per vehicle
- Key by part template ID or instance ID
- Load on game start / vehicle load

#### Step 2.2: Color Presets & Palettes
- Built-in color palette (XKCD colors like zippo uses)
- Custom palette editor
- "Paint bucket" mode: click parts to paint in 3D viewport (if feasible)

#### Step 2.3: Grant Supermod Integration
- Create `HumbleArteestSubmod` implementing `ISubmod`
- Register in grant's submod list
- Shader files managed/installed by the mod

### Phase 3: Advanced Features (Future)

- Pattern/gradient painting (requires more shader work)
- Per-part roughness/metallic overrides
- Decal system (texture overlay)
- Team/organization liveries
- Paint scheme sharing (export/import)

---

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Shaders are pre-compiled (not loaded from GLSL) | 🔴 Blocks Approach A entirely | Medium | Test in Phase 0; pivot to Approach B or SPIR-V replacement |
| Struct alignment mismatch C#/GLSL | 🟡 Colors render incorrectly | Low | Phase 0 padding test validates this |
| Game updates overwrite modified shaders | 🟡 Paint breaks on update | High | Mod installer copies shaders; detect+reapply on launch |
| GPU performance impact | 🟢 Negligible | Very Low | One multiply per fragment, already done for highlights |
| Material sharing causes family-wide tint | 🟡 Unintended parts colored | Medium (Approach B only) | Approach A avoids this entirely (per-instance) |
| Vulkan validation errors from struct mismatch | 🔴 Crashes | Low | Careful alignment testing in Phase 0 |

---

## Experimental Test Implementations

These should be built as small, isolated test mods within the humble-arteest project:

### Test 1: Shader Load Verification (`ShaderLoadTest`)
```
File: humble-arteest.lib/Experiments/ShaderLoadTest.cs

Purpose: Modify a shader file on disk, observe if game picks up the change.
Implementation:
  1. On mod load, backup Content/Core/Shaders/Mesh/MeshIndirect.frag
  2. Replace the highlight color constant (1.0, 0.0, 0.0) → (0.0, 1.0, 0.0)
  3. Log that the shader was modified
  4. After testing, provide a "restore" button to revert
  5. Requires game restart to see effect (shaders compiled at startup)

Expected Result: If highlighting turns green, shaders load from source files.
```

### Test 2: Padding Passthrough Test (`PaddingTest`)
```
File: humble-arteest.lib/Experiments/PaddingTest.cs

Purpose: Write known float values into PerInstanceData padding bytes and verify 
         they arrive at the shader correctly.
Implementation:
  1. Harmony prefix on PartModelModule.UpdateRenderData()
  2. After creating PerInstanceData, use unsafe code or reflection to set:
     - packing1 = BitConverter.SingleToInt32Bits(1.0f)  // R
     - packing2 = BitConverter.SingleToInt32Bits(0.0f)  // G
     - packing3 = BitConverter.SingleToInt32Bits(0.0f)  // B
  3. Modify MeshIndirect.vert to pass these as float outputs
  4. Modify MeshIndirect.frag to multiply albedo by these values
  5. If all parts turn red, the passthrough works

Prerequisite: Test 1 passes (shaders loadable from disk)
Expected Result: All static parts render with red tint.
```

### Test 3: Material AlbedoColor Test (`MaterialColorTest`)
```
File: humble-arteest.lib/Experiments/MaterialColorTest.cs

Purpose: Determine if modifying MaterialData.AlbedoColor in the GPU buffer 
         affects the indirect rendering path.
Implementation:
  1. Use reflection to access Program.Instance (or similar singleton)
  2. Navigate to GpuMaterialSystem
  3. Find a material handle (e.g., CoreCommandA_Material)
  4. Calculate buffer offset: handle * sizeof(MaterialData)
  5. Modify AlbedoColor bytes at that offset (set to bright red)
  6. Observe if command module parts turn red

Expected Result: If parts turn red → material cloning approach is viable.
                 If no change → indirect path ignores AlbedoColor (expected).
```

### Test 4: Temperature Tint Test (`TemperatureTintTest`)
```
File: humble-arteest.lib/Experiments/TemperatureTintTest.cs

Purpose: Quick proof that per-instance visual data flows correctly.
Implementation:
  1. Harmony prefix on PartModelDynamicModule.UpdateRenderData()
  2. Set Temperature = 0.8f on all dynamic parts
  3. Observe if parts glow with heat effect
  4. This validates per-instance data flow without any shader changes

Expected Result: Dynamic parts glow with heat color.
Note: Only affects PartModelDynamic parts (engines, etc.), not standard parts.
```

### Test 5: Full Paint Proof-of-Concept (`PaintPOC`)
```
File: humble-arteest.lib/Experiments/PaintPOC.cs

Purpose: End-to-end proof that per-part coloring works via the recommended approach.
Implementation:
  1. Install modified shaders (from Test 2, but with proper tint logic)
  2. Harmony-patch all three UpdateRenderData methods
  3. Simple dictionary mapping Part InstanceId → float3 color
  4. ImGui UI: vehicle selector + color picker + "Paint All" button
  5. Set paint color for all parts on selected vehicle
  6. Verify parts render with chosen color tint

Prerequisites: Tests 1 + 2 pass
Expected Result: Vehicle parts tinted with user-chosen color.
```

---

## File Structure Plan

```
humble-arteest/
├── Mod.cs                    — Main mod lifecycle + ImGui window
├── Patcher.cs                — Harmony setup + HotkeyGuard
├── humble-arteest.csproj     — Project references
├── mod.toml                  — Mod metadata
└── README.md                 — Mod documentation

humble-arteest.lib/
├── HumbleArteestSubmod.cs    — ISubmod implementation for grant integration
├── PaintManager.cs           — Per-part color state management
├── PaintPatches.cs           — Harmony patches for UpdateRenderData (all 3 paths)
├── ShaderInstaller.cs        — Copies modified shaders to Content/Core/Shaders/
├── PaintSerializer.cs        — Save/load paint schemes to TOML
├── Experiments/              — Phase 0 test implementations
│   ├── ShaderLoadTest.cs
│   ├── PaddingTest.cs
│   ├── MaterialColorTest.cs
│   ├── TemperatureTintTest.cs
│   └── PaintPOC.cs
└── Shaders/                  — Modified shader source files (stored in mod)
    ├── MeshIndirect.vert
    ├── MeshIndirect.frag
    ├── DynamicMeshIndirect.vert
    ├── DynamicMeshIndirect.frag
    ├── MeshGlassIndirect.vert
    └── MeshGlassIndirect.frag
```

---

## Key Code References (Decomp)

| File | Key Content |
|------|-------------|
| `decomp/ksa/KSA/PartModelModule.cs` | `UpdateRenderData()` — primary Harmony patch target |
| `decomp/ksa/KSA/PartModelDynamicModule.cs` | Dynamic variant with Temperature/TFI fields |
| `decomp/ksa/KSA/PartModelGlassModule.cs` | Glass variant |
| `decomp/ksa/KSA/PartModel.cs` | `PerInstanceData` struct (lines 340-351), `AddInstance()`, `WriteInstancesToGpu()` |
| `decomp/ksa/KSA/PartModelDynamic.cs` | Dynamic `PerInstanceData` struct (lines 350-361) |
| `decomp/ksa/KSA/Part.cs` | Part properties: Highlighted, Grabbed, Selected, FakeTranslucent |
| `decomp/ksa/KSA/PartModelRenderer.cs` | Pipeline setup, descriptor set layout, WriteCommands() |
| `decomp/ksa/KSA/MaterialData.cs` | MaterialData struct with AlbedoColor field |
| `decomp/ksa/KSA/GpuMaterialSystem.cs` | Material GPU buffer management, CreateAsset() |
| `decomp/ksa/KSA/PartSelectedRenderer.cs` | Post-processing overlay pattern (reference for Approach D) |
| `decomp/ksa/KSA/FxTemperature.cs` | Per-instance visual state pattern (model for paint state) |
| `decomp/ksa/Content/Core/Shaders/Mesh/MeshIndirect.frag` | Primary fragment shader — highlight mixing at lines 228-240 |
| `decomp/ksa/Content/Core/Shaders/Mesh/MeshIndirect.vert` | Vertex shader — InstanceData struct definition |
| `decomp/ksa/Content/Core/Shaders/Mesh/DynamicMeshIndirect.frag` | Dynamic fragment shader with Temperature LUT |
| `decomp/ksa/Content/Core/Shaders/Common/MaterialSet.glsl` | Material buffer access — `albedoColor * texture(...)` pattern |

### Existing Mod Patterns to Follow

| Mod | Pattern | Relevance |
|-----|---------|-----------|
| **blinky.lib** | Harmony prefix on `PartModelModule.UpdateRenderData()` returning false | Exact same patch point we need |
| **zippo.lib** | Reflection-based component discovery + color property modification | Part traversal + color setting pattern |
| **i-feel-seen.lib** | Vehicle render override via Harmony prefix on `Vehicle.UpdateRenderData()` | Alternative patch point at vehicle level |
| **inanimate-carbon-rod.lib** | `PartModelRenderer.ColorData` access + Vulkan rendering context | Understanding the rendering pipeline |
| **ksa-abstractions.lib** | `PartHelpers.GetAllParts()` + `ReflectionHelpers` | Part traversal and reflection utilities |

---

## Decision Points for User

1. **Shader replacement comfort level:** Approach A requires placing modified shader files in the game's Content directory. Is this acceptable, or should we restrict to C#-only approaches?

2. **Scope of initial implementation:** Should we start with just the Phase 0 experiments, or build the full MVP infrastructure alongside?

3. **Color model:** Should painting be:
   - **Multiplicative tint** (darkens/saturates — good for team colors)
   - **Additive overlay** (adds color — good for glowing effects)
   - **Mix/lerp blend** (blends toward target color — most natural for "paint")
   - **All three with a mode selector** (most flexible)

4. **Persistence:** Should paint colors persist across game sessions from the start, or is runtime-only OK for initial testing?

---

## Summary

The most promising path is **Approach A: Shader Replacement + PerInstanceData Padding Hijack**, which gives true per-part RGB coloring with minimal performance overhead. However, this depends on the game loading GLSL shader sources from disk, which **must be validated first** (Experiment 0.1).

If shader replacement isn't viable, **Approach B: Runtime Material Cloning** is the fallback, though it may only work on the legacy rendering path and gives per-material-family coloring rather than per-part.

**Recommended next step:** Implement the Phase 0 experiments (Tests 1-4) to validate feasibility before committing to a full implementation.
