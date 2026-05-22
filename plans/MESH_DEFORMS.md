# Dynamic Per-Instance Mesh Deformation Plan

> **Status:** Research complete — feasibility confirmed with caveats.  
> **Goal:** Enable dynamic, per-instance mesh deformation (e.g., collision denting) on vehicle parts, applied uniquely per-Part even when multiple Parts share the same `PartModel` template.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Key Architectural Findings](#key-architectural-findings)
3. [Reference Implementations in Our Mods](#reference-implementations-in-our-mods)
4. [Collision Detection Reality Check](#collision-detection-reality-check)
5. [Chosen Strategy: GPU Shader Deformation](#chosen-strategy-gpu-shader-deformation)
6. [Implementation Roadmap](#implementation-roadmap)
7. [Detailed Task List](#detailed-task-list)
8. [Code Patterns & Examples](#code-patterns--examples)
9. [Known Limitations & Risks](#known-limitations--risks)
10. [Appendix: Relevant Source Links](#appendix-relevant-source-links)

---

## Executive Summary

Dynamic mesh deformation **is practically achievable** for KSA mods, but **only via GPU vertex-shader deformation** driven by per-instance data injected at `PartModel.AddInstance`. A CPU-side mesh-cloning approach (copying `MeshReference` / `HostMesh` / `DeviceMeshInterleaved`) is theoretically possible but prohibitively complex because:

- `PartModel` is globally cached by template ID (`PartModel.Get()`).
- `DeviceMeshInterleaved` stores ALL meshes in a single append-only shared GPU buffer.
- `MeshReference.PositionCompare` is used for CPU-side raycasting and would also need syncing.

The **recommended path** mirrors `humble-arteest.lib`'s proven pattern:

1. Inject deformation parameters into the unused padding bytes of `PartModel.PerInstanceData` (or `PartModelDynamic.PerInstanceData`).
2. Patch the vertex shader (`MeshIndirectVert`) at runtime to read those bytes and displace vertices.
3. Rebuild the rendering pipeline (`PartModelRenderer.ColorData.Rebuild()`).
4. Store per-Part deformation state in a mod-managed dictionary keyed by `Part.InstanceId`.

**Important caveat:** The game does **not** implement vehicle-to-vehicle collision physics. Collision detection between crafts must be built from scratch by the mod (e.g. bounding-sphere / `ActionSphere` overlap tests).

---

## Key Architectural Findings

### 1. Rendering Pipeline — How a Part Becomes Pixels

```
Part (per vehicle instance)
  └── PartModelModule (per frame)
        └── PartModel.AddInstance(PerInstanceData) ──► GPU Instance Buffer
  └── PartModel.Template
        ├── MeshReference ──► DeviceMeshInterleaved (shared global GPU buffer)
        │                         ├── VerticesOffset / IndicesOffset (into shared buffer)
        │                         └── HostMesh (MeshAsset — CPU copy)
        └── PbrMaterialReference ──► PerDrawData (texture bindless handles)
```

Every frame, `PartModelModule.UpdateRenderData` computes the part's model matrix and calls `PartModel.AddInstance(instanceData, viewport, frameIndex)`. The `PerInstanceData` struct is:

```csharp
// decomp/ksa/KSA/PartModel.cs
public struct PerInstanceData
{
    public float4x4 ModelMatrix; // 64 bytes
    public int      StateBitFlag;//  4 bytes
    public uint     EmissiveColor;// 4 bytes
    private int     packing1;    //  4 bytes  ← UNUSED PADDING
    private int     packing2;    //  4 bytes  ← UNUSED PADDING
}
```

`PerDrawData` (one per unique `Material` + `Mesh` combo per frame) carries texture handles:

```csharp
public struct PerDrawData
{
    public int DiffuseTextureIndex;
    public int NormalTextureIndex;
    public int PbrTextureIndex;
    public int EmissiveTextureIndex;
    public int TfiTextureIndex;
}
```

The GPU draw command is an **indexed indirect draw** using the global `DeviceMeshInterleaved.Shared` buffers. The vertex shader receives:
- Vertex attributes (position, normal, UV) from the shared vertex buffer
- `PerInstanceData` from a storage buffer indexed by `gl_InstanceIndex`
- `PerDrawData` from another storage buffer indexed by draw command

This means **every Part gets its own `PerInstanceData` slot** even when multiple Parts share the same `PartModel`. Deformation parameters placed in `PerInstanceData` are therefore **per-Part / per-instance**, which satisfies the requirement.

### 2. Mesh Storage — CPU vs GPU

| Layer | Type | Key Properties |
|-------|------|--------------|
| **CPU Mesh** | `MeshAsset` (`RenderCore.MeshAsset`) | Holds `NativeStrideList` vertex buffers by `MeshAttribute` (Position, Normal, Uv0). Loaded from GLTF. Accessible at runtime. |
| **GPU Mesh (interleaved)** | `DeviceMeshInterleaved` | Computes `VerticesOffset` and `IndicesOffset` into `DeviceMeshInterleaved.Shared` giant buffers. Uploaded once at load time via `Bind()`. |
| **GPU Mesh (simple)** | `SimpleVkMesh` | Legacy non-interleaved path; used if `MeshReference.Interleaved == false`. |
| **Collision / Raycast** | `MeshReference.PositionCompare` | `double3[]` array of world-space positions for every index, used by `Part.RayCastEgoSubPart`. |

### 3. PartModel Global Sharing

```csharp
// decomp/ksa/KSA/PartModel.cs
public static PartModel Get(PartModelModule.Template template)
{
    // Returns EXISTING instance if template.Id already seen
    for (int i = 0; i < Instances.Count; i++)
        if (Instances[i].Template.Id.Equals(template.Id))
            return Instances[i];
    return new PartModel(template);
}
```

Because `PartModel` is shared, any approach that tries to modify `PartModel.Template.Mesh` or `PartModel.Template.Material` would affect **every instance** of that part across all vehicles. Per-instance behavior must be injected at `AddInstance` time or by cloning `PartModel` (which requires bypassing this cache and managing GPU buffers manually).

---

## Reference Implementations in Our Mods

### humble-arteest.lib — Per-PartModel Paint via Padding Injection

- **File:** `humble-arteest.lib/VehiclePaintPatches.cs`
- **Mechanism:** Harmony prefix on `PartModel.AddInstance` reinterprets `PerInstanceData` padding as RGB floats using `Unsafe.As`.
- **Shader modification:** `VehiclePaint.ActivateShaders()` compiles modified `MeshIndirectVert` / `MeshIndirectFrag` at runtime, swaps `VkShaderModule` on `ShaderReference`, then calls `PartModelRenderer.ColorData.Rebuild()`.

```csharp
// From humble-arteest.lib/VehiclePaintPatches.cs
private static void AddInstancePrefix(PartModel __instance, ref PartModel.PerInstanceData instanceData)
{
    if (!VehiclePaint.TryGetEffectiveColor(__instance, out var color))
        return;
    ref var paintable = ref Unsafe.As<PartModel.PerInstanceData, PaintablePerInstanceData>(ref instanceData);
    paintable.PaintR = color.X;
    paintable.PaintG = color.Y;
    paintable.PaintB = color.Z;
}
```

### doh.lib — Per-Instance GPU Material Cloning

- **File:** `doh.lib/Materials/MaterialFactory.cs` and `MaterialSystemAccessor.cs`
- **Mechanism:** Creates new named `MaterialData` entries in the game's `GpuMaterialSystem` via reflection (`CreateObject`). Each clone gets a unique bindless handle. Material index arrays on `KittenEva` renderables are rewritten to point to cloned handles.
- **Relevance:** Proves that per-instance material customization is possible, but **not** required for vertex deformation (which lives in the vertex shader, not the material).

---

## Collision Detection Reality Check

**The game has NO vehicle-to-vehicle collision physics.**

- `KinematicStates.GroundContact` and the `Contact` struct are **terrain/ocean only** (BepuPhysics `ConvexContactManifold` against a ground plane/triangle).
- `VehicleUpdateTask` clusters vehicles using `ActionSphere` overlap solely to batch physics updates; it does **not** generate collision impulses or contact manifolds.
- `Vehicle.NearbyVehicles` is populated from this clustering and is used for UI/debug drawing, not physics.

### What a mod must implement for craft-craft "collision":

1. **Proximity detection:** Iterate `Universe.CurrentSystem.Vehicles.GetList()`, test `ActionSphere.OverlapsWith()` or simple distance vs. sum of `BoundingSphereRadiusBody`.
2. **Part-level refinement:** Once vehicles are near, iterate parts of both vehicles, transform their `MeshReference.BoundingSphereRadius` into world space, test overlap.
3. **Contact generation (optional):** If precise contact points are needed, use `Part.RayCastEgo`-like math or the `MeshReference.PositionCompare` arrays in world space.
4. **Deformation trigger:** On overlap, write deformation params into the mod's per-Part state dictionary.

> **Clarification needed from user:** Do you want (a) cosmetic deformation only (visual dent), (b) physics-affecting deformation (alter mass distribution / bounding box), or (c) both? Physics-affecting deformation requires also patching `KinematicStates` recomputation and `PartTree` mass properties, which is far more invasive.

---

## Chosen Strategy: GPU Shader Deformation

### Why this strategy wins

| Approach | Per-Instance? | CPU Cost | GPU Cost | Raycast Sync | Complexity |
|----------|--------------|----------|----------|--------------|------------|
| **A. Shader deformation via `PerInstanceData`** | ✅ Yes — each Part gets its own instance slot | Very low | Very low (vertex ALU) | Requires hook | Low |
| **B. Clone `MeshReference` + modify `HostMesh` CPU vertices** | ✅ Yes | High (memcpy + modify every frame) | High (re-upload GPU buffer) | Automatic | Very high |
| **C. Clone `PartModel` + allocate new `DeviceMeshInterleaved` offset** | ✅ Yes | High (mesh duplication) | High (more buffer memory) | Automatic | Extreme |
| **D. Replace mesh with `ProcGenMesh`** | ✅ Yes | Medium | Medium | Automatic | High (requires mesh generation infrastructure) |

### How it works

```
┌─────────────────────────────────────────┐
│  Mod state: Dictionary<Part, DeformInfo>  │
│       ↓ (read every frame)              │
│  Harmony Prefix on PartModel.AddInstance│
│       ↓                                 │
│  Inject DeformInfo into PerInstanceData │
│  padding bytes (e.g., 8 bytes)          │
│       ↓                                 │
│  Modified MeshIndirectVert shader       │
│  reads padding, displaces vertices:     │
│    pos += deformDir * deformMag *       │
│           attenuation(distToDeformCenter)│
│       ↓                                 │
│  GPU renders deformed mesh per-instance │
└─────────────────────────────────────────┘
```

### Deformation model options

Because we only have ~8 bytes of padding in `PerInstanceData` (plus we could steal from `EmissiveColor` if not used), the deformation model must be **low-parameter**:

1. **Single dent:** `float3 center` (local space) + `float radius` + `float depth` + `float3 direction` = 28 bytes → too big.
2. **Simplified dent (8 bytes):** `float3 direction` packed into two `half` or one `uint` + `float magnitude` + `float radius` = ~8-12 bytes. Could use `packing1`/`packing2` as two `float` fields (8 bytes) plus repurpose `EmissiveColor` bits (4 bytes) for a quantized radius/direction.
3. **Vertex texture lookup:** Store deformation texture coordinates or index in padding, fetch a displacement map from a global texture. Requires shader modification to sample a texture in vertex stage.
4. **Multi-frame accumulation:** Store deformation state CPU-side, but only the *current frame's delta* is sent to GPU. The mesh never truly "remembers" deformation in the geometry; it just receives a time-varying displacement field.

---

## Implementation Roadmap

### Phase 1: Collision Detection Framework
Build the trigger system that detects when two vehicles (or parts) touch, even though the game doesn't natively support it.

### Phase 2: Shader Deformation Infrastructure
Implement the same shader-swapping pipeline that `humble-arteest.lib` uses, but for vertex displacement instead of paint tint.

### Phase 3: Per-Part State & Injection
Manage deformation parameters per-Part and inject them into `PerInstanceData` via a Harmony prefix.

### Phase 4: Raycast Alignment
Ensure that mouse picking (`Part.RayCastEgoSubPart`) respects the visual deformation.

### Phase 5: Polish & Persistence (Optional)
Save/load deformation state with vehicle save data; add ImGui debug overlay.

---

## Detailed Task List

### Task 1 — Create Mod Skeleton
**Context:** Use the standard KSA mod pattern (`[StarMapMod]`, `Patcher.cs`, Harmony).  
**Files:**
- `src/Mod.cs` — lifecycle hooks (`OnFullyLoaded`, `OnAfterGui`, `Unload`).
- `src/Patcher.cs` — Harmony patch application / removal.

**Steps:**
1. Create a new mod project (e.g., `mesh-deform.mod` + `mesh-deform.lib`).
2. Add `StarMap` and `HarmonyLib` references.
3. Add a `StarMapMod` entry class with `[StarMapAllModsLoaded]` calling `Patcher.Patch()`.
4. In `Unload`, call `Patcher.Unload()` and dispose any GPU resources.

**Code example:**
```csharp
using StarMap.API;
using HarmonyLib;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    [StarMapAllModsLoaded] public void OnFullyLoaded() => Patcher.Patch();
    [StarMapUnload]        public void Unload()      => Patcher.Unload();
}
```

---

### Task 2 — Implement Vehicle-to-Vehicle Proximity Detection
**Context:** The game populates `Vehicle.NearbyVehicles` via `VehicleUpdateTask` clustering, but this is coarse and not a true collision event. We need our own system.  
**Sources:** `decomp/ksa/KSA/VehicleUpdateTask.cs`, `decomp/ksa/KSA/Vehicle.cs` (`ActionSphere`, `BoundingSphereRadiusBody`).

**Steps:**
1. In `OnAfterGui` (game thread), iterate `Universe.CurrentSystem?.Vehicles.GetList()`.
2. For each pair of distinct vehicles, check `vehicleA.LastKinematicStates.BoundingSphereRadiusBody + vehicleB.LastKinematicStates.BoundingSphereRadiusBody` against their CCI position distance.
3. If overlapping, store the pair in a mod-internal `List<(Vehicle A, Vehicle B)> ActiveCollisions`.
4. (Optional refinement) Iterate parts of both vehicles: for each `Part`, compute its world-space bounding sphere using `part.ScaleTotal`, `MeshReference.BoundingSphereRadius`, and the vehicle's `MatrixAsmb2Ego` / world matrix.
5. When a new collision pair appears, log it and queue deformation.

**Code example:**
```csharp
public static void DetectCollisions()
{
    var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
    if (vehicles == null) return;

    for (int i = 0; i < vehicles.Count; i++)
    for (int j = i + 1; j < vehicles.Count; j++)
    {
        var a = vehicles[i];
        var b = vehicles[j];
        double dist = (a.GetPositionCci() - b.GetPositionCci()).Length();
        double threshold = a.BoundingSphereRadiusBody + b.BoundingSphereRadiusBody;
        if (dist < threshold)
        {
            // Collision detected — queue deformation on impacted parts
            DeformManager.OnVehicleCollision(a, b);
        }
    }
}
```

---

### Task 3 — Define Deformation Data Structures
**Context:** We need a compact representation of deformation that fits in `PerInstanceData` padding or can be referenced by it.  
**Decision:** Use an 8-byte payload in `packing1`/`packing2` reinterpreted as `half4` or `float2` + `ushort2`.

**Proposed compact format (8 bytes):**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DeformPayload
{
    // Packed into PartModel.PerInstanceData packing1/packing2
    public float DirX;      // -1..1  deformation direction X
    public float DirYZ;     // packed: Y in high 16 bits, Z in low 16 bits (or use half)
    public half  Magnitude; // 0..1  displacement strength in metres
    public half  Radius;    // 0..1  normalized radius of effect (scaled by part bounding sphere)
}
```

Alternative simpler format (8 bytes exact):
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DeformPayload
{
    public float Magnitude; // max displacement in metres
    public float Radius;  // sphere of influence in metres
    // Direction is inferred from collision normal (stored CPU-side only)
}
```

**Steps:**
1. Define `DeformPayload` struct with explicit layout matching `packing1` + `packing2` (8 bytes).
2. Define `PartDeformState` CPU-side struct holding full collision info (`double3 CenterLocal`, `double3 DirectionLocal`, `float Magnitude`, `float Radius`, `float Progress`).
3. Create `DeformManager` singleton with `Dictionary<Part, PartDeformState> States`.
4. Provide `DeformManager.ApplyDeform(Part part, double3 worldHitPoint, double3 worldNormal, float energy)`.

---

### Task 4 — Harmony Patch `PartModel.AddInstance` to Inject Payload
**Context:** Same pattern as `humble-arteest.lib/VehiclePaintPatches.cs`. We patch `PartModel.AddInstance` to write deformation data before the instance is enqueued.  
**Source:** `decomp/ksa/KSA/PartModel.cs` (`AddInstance`).

**Steps:**
1. Create `DeformPatches.Apply(Harmony harmony)`.
2. Use `AccessTools.Method(typeof(PartModel), nameof(PartModel.AddInstance))`.
3. In the prefix, receive `PartModel __instance` and `ref PartModel.PerInstanceData instanceData`.
4. Lookup the `Part` that owns this `PartModelModule`. **Problem:** `AddInstance` does not receive the `Part` directly. We need to find the Part via reverse lookup or patch `PartModelModule.UpdateRenderData` instead.

**Critical design decision:** `PartModel.AddInstance` signature is:
```csharp
public void AddInstance(PerInstanceData instanceData, Viewport viewport, int frameIndex)
```
It does NOT know which Part is calling it. However, `PartModelModule.UpdateRenderData` DOES know (`Parent` is the `Part`). Therefore we should patch **`PartModelModule.UpdateRenderData`** (or wrap `PartModel.AddInstance` from within it) to capture the `Part` → `DeformPayload` mapping.

**Revised Step 4a:**
- Patch `PartModelModule.UpdateRenderData` with a **Postfix** that intercepts the `instanceData` after it is created but before `AddInstance` is called... **No**, that's also hard because the struct is passed by value.

**Better approach:** Patch `PartModel.AddInstance` with a prefix that also receives the `Part` by patching the **caller** `PartModelModule.UpdateRenderData`. Or, use a static `ThreadLocal<Part>` set in a prefix on `PartModelModule.UpdateRenderData` and read in a prefix on `PartModel.AddInstance`.

**Chosen approach (simplest):**
```csharp
// 1. Prefix on PartModelModule.UpdateRenderData to capture the Part
[HarmonyPatch(typeof(PartModelModule), nameof(PartModelModule.UpdateRenderData))]
static class CapturePartPatch
{
    public static readonly ThreadLocal<Part?> CurrentPart = new();

    static void Prefix(PartModelModule __instance)
    {
        CurrentPart.Value = __instance.Parent;
    }
}

// 2. Prefix on PartModel.AddInstance to inject deformation using captured Part
[HarmonyPatch(typeof(PartModel), nameof(PartModel.AddInstance))]
static class AddInstanceDeformPatch
{
    static void Prefix(PartModel __instance, ref PartModel.PerInstanceData instanceData)
    {
        var part = CapturePartPatch.CurrentPart.Value;
        if (part == null) return;
        if (!DeformManager.TryGetPayload(part, out var payload)) return;

        // Write into packing bytes
        ref var deformable = ref Unsafe.As<PartModel.PerInstanceData, DeformablePerInstanceData>(ref instanceData);
        deformable.DeformMagnitude = payload.Magnitude;
        deformable.DeformRadius   = payload.Radius;
        // ... pack direction if space allows
    }
}
```

**Caution:** `ThreadLocal` is safe here because rendering is single-threaded on the game's main thread, but verify with runtime reflection if unsure.

---

### Task 5 — Modify Vertex Shader at Runtime
**Context:** Same pattern as `humble-arteest.lib/VehiclePaint.ActivateShaders()`. We compile modified GLSL at runtime, swap `VkShaderModule` on the `ShaderReference`, and rebuild pipelines.  
**Source files:** `humble-arteest.lib/VehiclePaint.cs` (shader swap logic), game shaders `MeshIndirectVert` / `DynamicMeshIndirectVert`.

**Steps:**
1. Write a modified `MeshIndirectVert.glsl` that:
   - Adds `float DeformMagnitude`, `float DeformRadius` (and optionally direction) to the `InstanceData` struct.
   - After computing `worldPosition` (or before applying `ModelMatrix`), applies displacement:
     ```glsl
     vec3 toVertex = worldPos - deformCenter;
     float dist = length(toVertex);
     float attenuation = smoothstep(deformRadius, 0.0, dist);
     worldPos += deformDirection * deformMagnitude * attenuation;
     ```
   - Note: the shader currently works in **model local space** (before `ModelMatrix` is applied). Deformation should also be in local space to stay attached to the part.
2. Use `ShaderReference.DoLoad` restore + `ShaderModuleUtils.FromFile` compile pattern from `VehiclePaint`.
3. Call `PartModelRenderer.ColorData.Rebuild()` after shader swap.
4. (Optional) Also patch `DynamicMeshIndirectVert` if you want deformation on animated/dynamic parts (e.g., deployables).

**Important:** The original shader source file path must be resolved via `ShaderReference.ModPath` or `GamePaths.GetShaderPath(localPath)`. Write the modified source to a **temporary file in the same directory** (so `#include` directives resolve), compile, then delete the temp file.

---

### Task 6 — Sync Raycasting with Visual Deformation
**Context:** `Part.RayCastEgoSubPart` uses `MeshReference.PositionCompare` which is the **undeformed** CPU-side vertex cache. If the GPU deforms vertices, mouse picking will hit the original mesh.  
**Source:** `decomp/ksa/KSA/Part.cs` lines 1207–1250.

**Options:**

| Option | Effort | Accuracy | Recommendation |
|--------|--------|----------|----------------|
| A. Hook `RayCastEgoSubPart` to apply inverse deformation to ray | Medium | High | **Preferred** |
| B. Update `PositionCompare` array every frame | High | High | Avoid — CPU memcpy cost |
| C. Accept raycast mismatch | None | Low | Not recommended |

**Chosen: Option A.**

**Steps:**
1. Harmony postfix or transpiler on `Part.RayCastEgoSubPart`. Actually, a **Prefix** that modifies the incoming `Ray` by transforming it into "undeformed space" is easier.
2. If deformation is a simple spherical dent, the inverse transform is non-trivial analytically. However, we can approximate:
   - Deformation displaces vertices along a direction by magnitude `attenuation(dist)`.
   - To make raycast conservative, expand the bounding sphere test by `DeformMagnitude`.
   - For the watertight ray-triangle test (`ray.RaycastWatertight`), apply the inverse deformation to the ray origin/direction numerically (e.g., step along ray in small segments, undo deformation).

**Simpler practical approach:**
- In `RayCastEgoSubPart`, after the `BoundingSphere3D` test passes but before `ray.RaycastWatertight`, if the part has deformation active, inflate the bounding sphere radius by `DeformMagnitude`. This ensures the ray enters the watertight test region. Then, after a hit is found, offset the hit point by the deformation vector. This is approximate but cheap.

**Code sketch:**
```csharp
[HarmonyPatch(typeof(Part), nameof(Part.RayCastEgoSubPart))]
static class RaycastDeformPatch
{
    static void Prefix(Part __instance, ref Ray ray, ref double4x4 matrixVehicleAsmb2Ego)
    {
        if (!DeformManager.TryGetState(__instance, out var state)) return;
        // Roughly: if deformation pushes surface outward, shift ray origin slightly
        // along inverse normal so the undeformed mesh intersection is closer to truth.
        // Exact math depends on deformation model chosen in Task 3.
    }
}
```

---

### Task 7 — Hook into Collision Event to Drive Deformation
**Context:** Once Task 2 detects a vehicle-vehicle overlap, we must decide which parts are impacted and how much.  
**Dependency:** Task 2 and Task 3.

**Steps:**
1. In `DeformManager.OnVehicleCollision(Vehicle a, Vehicle b)`, compute relative velocity at impact:
   ```csharp
   double3 relVel = a.GetVelocityCci() - b.GetVelocityCci();
   float energy = (float)relVel.LengthSquared(); // proxy for impact energy
   ```
2. Find the closest parts between the two vehicles. For each part in `a.Parts.Parts` and `b.Parts.Parts`, compute world-space bounding sphere centers. Find the pair with minimum distance.
3. Compute world-space hit point = midpoint between the two closest bounding sphere centers.
4. For the impacted part on vehicle `a`:
   - Transform hit point and `relVel` direction into the part's **local assembly space**.
   - Call `DeformManager.ApplyDeform(part, localHitPoint, localDirection, energy)`.
5. Make deformation decay over time (optional): each frame, reduce `Magnitude` by a recovery rate so dents slowly pop out (if desired).

---

### Task 8 — Add ImGui Debug UI
**Context:** Mod visibility and debugging via ImGui inside the game's menu. Follow `doh.lib/DohSubmod.cs` pattern or `humble-arteest.lib` experiments.  
**Source:** `ksa` skill section on game menus (`game-menus.md`).

**Steps:**
1. Add a top-level menu "Mesh Deform" using the `Program.DrawMenuBar` transpiler pattern (see `docs/game-menus.md` in the repo).
2. In the panel, show:
   - Number of active deformations
   - List of deformed parts (vehicle ID, part ID, magnitude)
   - Sliders to manually apply test deformation to the currently hovered part
   - "Reset All" button to clear `DeformManager.States`
3. Use `ImGui.IsWindowFocused(...) && ImGui.GetIO().WantTextInput` to block game hotkeys when editing.

---

### Task 9 — Cleanup & Persistence (Optional)
**Context:** Ensure mod can be unloaded without leaking GPU state, and optionally save dents.  
**Priority:** Low unless user requests persistence.

**Steps:**
1. In `Patcher.Unload()`, restore original shaders via `ShaderReference.DoLoad` (same as `VehiclePaint.DeactivateShaders`).
2. Remove all Harmony patches.
3. Clear `DeformManager.States`.
4. (Optional persistence) In `Vehicle.SerializeSave`, there's no hook for mods. Alternative: save a sidecar file in `My Games/Kitten Space Agency/mods/mesh-deform/saves/{vehicle.Id}.json` containing `Part.InstanceId` → deform state. Load when vehicle is deserialized or when first seen in `OnAfterGui`.

---

## Code Patterns & Examples

### Pattern A: Reinterpret Padding with `Unsafe.As`

```csharp
[StructLayout(LayoutKind.Sequential)]
struct DeformablePerInstanceData
{
    public float4x4 ModelMatrix; // 64 bytes
    public int        StateBitFlag;// 4
    public uint       EmissiveColor;// 4
    public float      DeformMag;   // 4  ← was packing1
    public float      DeformRad;   // 4  ← was packing2
}

// In prefix:
ref var d = ref Unsafe.As<PartModel.PerInstanceData, DeformablePerInstanceData>(ref instanceData);
d.DeformMag = payload.Magnitude;
d.DeformRad = payload.Radius;
```

### Pattern B: Runtime Shader Compilation & Swap

```csharp
// Same as humble-arteest.lib/VehiclePaint.cs
private static bool CompileAndSwapShader(string shaderId, Func<string,string> modifier, Device device)
{
    var shaderRef = ModLibrary.Get<ShaderReference>(shaderId);
    var modPath = GetShaderModPath(shaderRef); // resolve actual file path
    var source = File.ReadAllText(modPath);
    var modified = modifier(source);
    // write temp, compile, swap VkShaderModule, rebuild pipeline
}
```

### Pattern C: Replace Material Indices (for reference, if later needed)

```csharp
// From doh.lib — if you ever need per-instance textures, not just geometry
var matSet = _materialFactory.CloneAllMaterials(originalHandles, tintColor);
// ... then rewrite int[] MaterialIndices on renderables using HandleMap
```

---

## Known Limitations & Risks

1. **No Native Craft-Craft Collision:** The entire collision trigger layer must be built by the mod. This is CPU work every frame. Mitigation: only run detection when `BoundingSphereRadiusBody` overlap occurs, and throttle to every N frames.

2. **Padding is Limited:** `PerInstanceData` only has ~8 bytes of guaranteed padding (`packing1` + `packing2`). `EmissiveColor` (4 bytes) could be repurposed if you don't need status-light coloring. If you need more params, you must either:
   - Use a global lookup table indexed by a small ID (e.g., `packing1` = deform slot index, actual data in GPU SSBO / CPU dict).
   - Or upgrade to `PartModelDynamic` which has a different padding layout (`Temperature`, `TfiThickness`, `packing1`).

3. **Raycast Approximation:** Unless we update `PositionCompare` arrays or do a complex inverse-deformation ray transform, mouse picking will be approximate. The bounding-sphere inflation trick (Task 6) is recommended as a pragmatic compromise.

4. **Shader Recompilation Cost:** Runtime GLSL compilation + pipeline rebuild takes ~50-200 ms and causes a frame hitch. Do it once at mod load, not per-deformation.

5. **Deformation is Visual Only:** Mass properties, aerodynamics, and physics bounding boxes (`BoundingBoxAsmb`, `BoundingSphereRadiusBody`) remain unchanged unless you also patch `PartTree.ComputeBoundingBoxAsmb()` and `Vehicle.UpdateAfterPartTreeModification()`. This is significantly more invasive.

6. **Shared `DeviceMeshInterleaved` Buffer:** Even if you clone `PartModel`, all meshes still live in the same giant buffer. You cannot remove or resize an existing mesh's allocation. CPU-side cloning to new offsets is possible only at load time (before `Shared.Build()`), not dynamically.

7. **Multiplayer / Save Compatibility:** If deformation is not persisted, reloading a save resets all dents. If persisted via sidecar files, players without the mod won't see dents (cosmetic-only side effect).

---

## Appendix: Relevant Source Links

### Decompiled Game Sources
- `decomp/ksa/KSA/PartModel.cs` — `PerInstanceData`, `PerDrawData`, `AddInstance`, `WriteInstancesToGpu`  
- `decomp/ksa/KSA/PartModelModule.cs` — `UpdateRenderData`, `CreateComponents`, calls `PartModel.AddInstance`  
- `decomp/ksa/KSA/PartModelDynamic.cs` — Dynamic variant with `Temperature`/`TfiThickness` padding  
- `decomp/ksa/KSA/PartModelRenderer.cs` — `ColorData.BuildPipelineModel`, `ColorData.Rebuild()`, shader bindings  
- `decomp/ksa/KSA/Part.cs` — `RayCastEgoSubPart` (line ~1228), `PositionCompare` usage, `MeshViewModule` access  
- `decomp/ksa/KSA/MeshReference.cs` — `HostMesh`, `DeviceMeshInterleaved`, `PositionCompare`, `Load()`, `Bind()`  
- `decomp/ksa/KSA/DeviceMeshInterleaved.cs` — Shared global buffer allocation, `VerticesOffset`, `IndicesOffset`, interleaved layout  
- `decomp/ksa/RenderCore/MeshAsset.cs` — CPU-side mesh data container extending `Mesh<MeshAttribute>`  
- `decomp/ksa/RenderCore.Mesh/SimpleVkMesh.cs` — GPU buffer upload helper (non-interleaved path)  
- `decomp/ksa/KSA/VehicleUpdateTask.cs` — `ActionSphere` clustering, `NearbyVehicles` population  
- `decomp/ksa/KSA/KinematicStates.cs` — `BoundingSphereRadiusBody`, `BoundingBoxAsmb`, `GroundContact` (terrain only)  
- `decomp/ksa/KSA/Contact.cs` — BepuPhysics contact manifold (terrain/ocean, NOT vehicle-vehicle)

### Existing Mod Reference Implementations
- `humble-arteest.lib/VehiclePaint.cs` — Runtime shader modification, temp-file compile, `VkShaderModule` swap  
- `humble-arteest.lib/VehiclePaintPatches.cs` — `Unsafe.As` padding injection into `PartModel.AddInstance`  
- `doh.lib/Materials/MaterialFactory.cs` — `GpuMaterialSystem` cloning via reflection, `MaterialData` construction  
- `doh.lib/Materials/MaterialSystemAccessor.cs` — Reflection bridge to `GpuMaterialSystem`, `BigBuffer` GPU writes  
- `doh.lib/Spawning/KittenSpawner.cs` — Full KittenEva instantiation + per-instance material replacement

### KSA Skill / Docs
- `.agents/skills/ksa/SKILL.md` — Mod lifecycle, `PartTree.Merge()`, vehicle teleport, `PerInstanceData`  
- `.agents/skills/ksa/parts.md` — `PartModelModule`, `MeshViewModule`, raycasting workaround  
- `.agents/skills/ksa/debug.md` — Runtime reflection dump strategy (use if decompiled field names mismatch binary)

---

## Questions for the User

Before implementation begins, please confirm:

1. **Scope:** Should deformation be purely **visual/cosmetic**, or do you also want it to affect **physics** (mass, aerodynamics, bounding boxes)? Physics integration is much more invasive.
2. **Collision trigger:** Do you want deformation only from **vehicle-to-vehicle contact**, or also from **terrain crashes / hard landings** (which the game already detects via `GroundContact`)?
3. **Persistence:** Should dents persist across save/load, or be session-only?
4. **Recovery:** Should dents slowly "pop out" (elastic recovery), or remain permanent until repaired?
5. **Performance budget:** How many simultaneously deformed parts do you expect? (e.g., 10? 100? 1000?) This affects whether a CPU-side dictionary lookup per-part per-frame is acceptable.
