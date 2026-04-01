# Humble Arteest — KSA Part Painting & Visual Effects Mod

Humble Arteest provides three visual customization features for KSA:

1. **Vehicle Paint** — Tints vehicle parts with per-part RGB colors via runtime shader patching
2. **Kitten Color** — Tints kitten character models by modifying GPU material buffers
3. **Engine Emissive** — Controls per-engine glow/heat effects via Temperature field overrides

All features are accessible as `ISubmod` implementations for use in the grant supermod or standalone.

---

## Architecture Overview

### Why Three Separate Approaches?

KSA uses **two completely different rendering pipelines** for different object types, plus a third data path for dynamic parts:

| Object Type | Shader Path | Material Source | Per-Instance Color? |
|---|---|---|---|
| Vehicle parts (static) | `MeshIndirect.frag` | PerDrawData texture indices | ❌ Not natively — we add it |
| Vehicle parts (dynamic) | `DynamicMeshIndirect.frag` | PerDrawData texture indices | Temperature/TFI only |
| Kitten characters | `ModelPbr.frag` | GpuMaterialSystem buffer | ✅ Via AlbedoColor |

Because of this split, each feature uses a different technical approach matched to its rendering path.

---

## Feature 1: Vehicle Paint (Runtime Shader Patching)

### How It Works

Vehicle parts are rendered via `MeshIndirect.vert/frag` using Vulkan indirect draw calls. Each part instance sends an 80-byte `PerInstanceData` struct to the GPU:

```
PerInstanceData (80 bytes):
  float4x4 ModelMatrix      64 bytes — world transform
  int      StateBitFlag       4 bytes — highlight/grab/select bits
  int      packing1           4 bytes — UNUSED (we hijack this)
  int      packing2           4 bytes — UNUSED (we hijack this)
  int      packing3           4 bytes — UNUSED (we hijack this)
```

The three padding integers (bytes 68–79) are unused by the game. We repurpose them to carry RGB paint color data from C# to the GPU shader.

### Step-by-Step Data Flow

1. **Harmony Prefix on `PartModel.AddInstance()`** (`VehiclePaintPatches.cs`)
   - Intercepts every per-instance data submission before it reaches the GPU
   - Uses `Unsafe.As<>` to reinterpret the struct padding fields as floats
   - Writes R, G, B color values into `packing1`, `packing2`, `packing3`
   - The paint color comes from `VehiclePaint.TryGetEffectiveColor()` which checks per-PartModel overrides first, then falls back to the global "paint all" color

2. **Runtime Shader Compilation** (`VehiclePaint.ActivateShaders()`)
   - Reads the game's original GLSL shader source from disk via `ShaderReference.LocalPath`
   - Modifies the source **in memory** (never touches the game files):
     - **Vertex shader** (`MeshIndirect.vert`): Adds `float PaintR/G/B` fields to the `InstanceData` struct and new `out` variables at locations 6/7/8
     - **Fragment shader** (`MeshIndirect.frag`): Adds matching `in` variables and applies `sampledColor *= paintTint` when the paint vector is non-zero
   - Writes to a temporary file in the same directory (for `#include` path resolution)
   - Compiles via `ShaderModuleUtils.FromFile()` (reflection) → new `VkShaderModule`
   - Swaps the compiled module into the existing `ShaderReference` via backing field
   - Deletes the temp file
   - Calls `PartModelRenderer.ColorData.Rebuild()` to recreate Vulkan pipelines with the new shaders

3. **Paint Application**
   - The modified fragment shader reads paint RGB as interpolated floats from the vertex shader
   - When `dot(paintTint, paintTint) > 0.001` (i.e., non-zero color), it multiplies the albedo texture color
   - This is a **multiplicative tint**: white = no change, red = red tint, dark = darkens

4. **Shader Restoration** (`VehiclePaint.DeactivateShaders()`)
   - Calls `ShaderReference.DoLoad()` via reflection, which recompiles from the original game files
   - Rebuilds pipelines, restoring vanilla rendering

### Key Shader Modifications (Vertex)

```glsl
// Original struct:
struct InstanceData {
    mat4 WorldMatrix;
    int Highlighted;
};

// Modified struct — paint color in padding slots:
struct InstanceData {
    mat4 WorldMatrix;
    int Highlighted;
    float PaintR;    // was packing1
    float PaintG;    // was packing2
    float PaintB;    // was packing3
};

// New output variables:
layout(location = 6) out float outPaintR;
layout(location = 7) out float outPaintG;
layout(location = 8) out float outPaintB;
```

### Key Shader Modifications (Fragment)

```glsl
// New input variables:
layout(location = 6) in float inPaintR;
layout(location = 7) in float inPaintG;
layout(location = 8) in float inPaintB;

// After albedo texture sampling:
vec3 paintTint = vec3(inPaintR, inPaintG, inPaintB);
if (dot(paintTint, paintTint) > 0.001) {
    sampledColor *= paintTint;
}
```

### Critical Game Infrastructure Used

- **`ModLibrary.Get<ShaderReference>(id)`** — retrieves named shader references by ID (`MeshIndirectVert`, `MeshIndirectFrag`)
- **`ShaderReference.LocalPath`** — file path to the GLSL source on disk
- **`ShaderReference.Shader`** — the compiled `VkShaderModule` (swapped via reflection)
- **`ShaderReference.DoLoad()`** — recompiles shader from its source file (used for restore)
- **`ShaderModuleUtils.FromFile(device, path, out stageFlags, out errorMsg)`** — compiles GLSL to SPIR-V to VkShaderModule
- **`PartModelRenderer.ColorData.Rebuild()`** — static method that destroys and recreates the Vulkan rendering pipelines, picking up the swapped shader modules
- **`Program.GetRenderer().Device`** — Vulkan device handle for shader compilation
- **`PartModel.AddInstance(ref PerInstanceData, ...)`** — the Harmony patch target

### Struct Alignment (Critical for Future Maintenance)

The C# struct and GLSL struct **must** have identical memory layouts:

| Offset | C# Field | GLSL Field | Size |
|--------|----------|------------|------|
| 0 | `ModelMatrix` | `WorldMatrix` | 64 |
| 64 | `StateBitFlag` | `Highlighted` | 4 |
| 68 | `packing1` → PaintR | `PaintR` | 4 |
| 72 | `packing2` → PaintG | `PaintG` | 4 |
| 76 | `packing3` → PaintB | `PaintB` | 4 |

If KSA changes the `PerInstanceData` struct layout, adds fields, removes padding, or changes the shader's `InstanceData` struct, the paint system will break. Look for:
- Changes to `PartModel.PerInstanceData` (C# struct)
- Changes to `MeshIndirect.vert` InstanceData struct
- Changes to `MeshIndirect.frag` input variable locations
- New `PartModelRenderer` pipeline setup code

### What Could Break and How to Fix It

| Breakage | Symptom | Fix |
|----------|---------|-----|
| Padding fields removed from PerInstanceData | Crash or wrong colors | Find new unused bytes or switch to a different data injection approach |
| Shader InstanceData struct changed | Colors applied to wrong data | Update the string replacements in `ModifyVertexShader()` / `ModifyFragmentShader()` to match new struct |
| `ShaderReference` API changed | Shader swap fails | Update reflection targets — check field names, method signatures |
| `PartModelRenderer.ColorData.Rebuild()` removed | Pipelines not rebuilt after swap | Find the new pipeline rebuild mechanism |
| `ShaderModuleUtils.FromFile()` signature changed | Compilation fails | Update the `FindFromFileMethod()` reflection to match new parameters |
| New shader output locations conflict | Vulkan validation errors | Change paint output locations (currently 6/7/8) to unused slots |
| Game adds its own per-instance color system | Our modifications conflict | Consider using the game's native system instead |

### Files

- `VehiclePaint.cs` — paint state management + shader compilation/swap/restore
- `VehiclePaintPatches.cs` — Harmony prefix on `PartModel.AddInstance`
- `VehiclePaintSubmod.cs` — ISubmod UI panel

---

## Feature 2: Kitten Color (GPU Material Buffer Writes)

### How It Works

Kitten character models (fur, glass, eyes) are rendered via `ModelPbr.frag`, which reads materials from `GpuMaterialSystem`. Unlike vehicle parts, these materials include an `AlbedoColor` field (float4) that is **multiplied** with the albedo texture in the shader.

The default `AlbedoColor` is `(1, 1, 1, 1)` (white = no tint). By writing a different color to the GPU material buffer, we tint all characters using that material.

### Step-by-Step Data Flow

1. **Initialization** (`KittenColor.Initialize()`)
   - Uses reflection to access `Program.Instance` → `MaterialSystem` (type `GpuMaterialSystem`)
   - Reads the `AssetMap` (ConcurrentDictionary) which maps material names to handles
   - Caches `BigBuffer` (the GPU buffer containing all MaterialData) and `DeviceCtx` (Vulkan context)

2. **Color Application** (`KittenColor.WriteAlbedoColor()`)
   - Calculates the byte offset: `handle * sizeof(MaterialData) + offsetof(AlbedoColor)`
   - Creates a Vulkan staging pool and command buffer
   - Uses `VkUtils.StageAndUploadToBuffer()` to write the new float4 color at the correct offset
   - This is a direct GPU buffer write — the change takes effect immediately

3. **Reset** — writes `(1, 1, 1, 1)` to all materials, restoring the original white multiplier

### Why This Doesn't Work for Vehicle Parts

Vehicle parts use `MeshIndirect.frag` which reads texture indices from `PerDrawData` and samples textures directly — it **never reads** from `GpuMaterialSystem`. The `AlbedoColor` field only exists in the `MaterialSet.glsl` include used by `ModelPbr.frag`.

The material list from `GpuMaterialSystem.AssetMap` is populated by:
- `CharacterRenderResources` — kitten fur, glass, eye materials
- `GltfPbrSystem` — GLTF model materials

Vehicle parts never register here.

### Alpha Channel Behavior

The `ModelPbr.frag` shader has a discard threshold: `if (alpha < 0.1) discard`. Setting `AlbedoColor.W` (alpha) below 0.1 makes material fragments invisible. Values 0.1–1.0 modulate opacity.

### Critical Game Infrastructure Used

- **`KSA.Program.Instance`** — game singleton (accessed via reflection: `typeof(Part).Assembly.GetType("KSA.Program")`)
- **`GpuMaterialSystem`** (field on Program) — manages the GPU material buffer
- **`GpuMaterialSystem.AssetMap`** — `ConcurrentDictionary<string, GpuObjectHandle>` mapping names to handles
- **`GpuMaterialSystem.BigBuffer`** — `BufferEx` containing all `MaterialData` structs packed sequentially
- **`MaterialData.AlbedoColor`** — `float4` field at a specific offset within the struct
- **`VkUtils.StageAndUploadToBuffer()`** — uploads data to a Vulkan buffer via staging

### MaterialData Struct Layout

The `AlbedoColor` offset is determined via `Marshal.OffsetOf<MaterialData>("AlbedoColor")`. If KSA reorganizes `MaterialData`, this offset may change. The struct is defined in `KSA.MaterialData`.

### What Could Break and How to Fix It

| Breakage | Symptom | Fix |
|----------|---------|-----|
| `MaterialData.AlbedoColor` field renamed/removed | Offset calculation fails | Find the new field name or offset in the decompiled struct |
| `GpuMaterialSystem` internals restructured | Initialization fails | Update reflection paths — walk the hierarchy to find AssetMap/BigBuffer |
| `ModelPbr.frag` stops reading AlbedoColor | Colors have no effect | Check if a new uniform or buffer binding replaced it |
| `Program.Instance` accessor changed | Can't reach MaterialSystem | Update the reflection path to the game singleton |
| New material types registered | Unexpected tinting | Filter the AssetMap by material name patterns |

### Files

- `KittenColor.cs` — reflection-based GPU buffer access + color writes
- `KittenColorSubmod.cs` — ISubmod UI panel

---

## Feature 3: Engine Emissive (Temperature Field Override)

### How It Works

Dynamic vehicle parts (engines, heat shields) use `PartModelDynamic.PerInstanceData` which includes a `Temperature` float field. The game's `DynamicMeshIndirect.frag` shader already reads this field and uses it as an index into an emissive color lookup table — this is how running engines glow.

We simply override the Temperature value before it reaches the GPU, giving explicit control over engine glow intensity.

### Step-by-Step Data Flow

1. **Harmony Prefix on `PartModelDynamic.AddInstance()`** (`EngineEmissivePatches.cs`)
   - Checks `EngineEmissive.TryGetEffective()` for per-engine or global overrides
   - Uses `Unsafe.As<>` to reinterpret the struct and write `Temperature` and `TfiThickness`
   - No shader modifications needed — Temperature is already wired end-to-end

2. **Per-Engine Targeting**
   - `EngineEmissive.ScanDynamicParts(vehicle)` traverses the part tree
   - Finds parts with `PartModelDynamicModule` and extracts their `PartModelDynamic` reference
   - Each engine gets its own slider in the UI

### PerInstanceData Layout (Dynamic — 80 bytes)

```
float4x4 ModelMatrix      64 bytes
int      StateBitFlag       4 bytes
float    Temperature        4 bytes  ← we override this
float    TfiThickness       4 bytes  ← we override this
int      packing1           4 bytes
```

### What Temperature Does in the Shader

The `DynamicMeshIndirect.frag` shader uses Temperature to:
1. Sample a color from a temperature lookup table (LUT) texture
2. Add the LUT color as emissive lighting
3. Higher Temperature values → more intense glow (cool blue → orange → bright white)

`TfiThickness` controls thin-film interference effects (rainbow sheen).

### What Could Break and How to Fix It

| Breakage | Symptom | Fix |
|----------|---------|-----|
| `PartModelDynamic.PerInstanceData` layout changes | Wrong field overridden | Update the mirror struct `WritablePerInstanceData` to match |
| `PartModelDynamic.AddInstance` signature changes | Harmony patch fails | Update the patch target method and parameter types |
| Temperature field renamed | Compiler error in mirror struct | Rename in `WritablePerInstanceData` |
| `PartModelDynamicModule` removed/renamed | Part scanning fails | Find the new module type for dynamic parts |

### Files

- `EngineEmissive.cs` — per-engine state management + part scanning
- `EngineEmissivePatches.cs` — Harmony prefix on `PartModelDynamic.AddInstance`
- `EngineEmissiveSubmod.cs` — ISubmod UI panel

---

## Project Structure

```
humble-arteest/                    — Standalone mod (F11 toggle)
├── Mod.cs                         — StarMapMod lifecycle, creates submods
├── Patcher.cs                     — Harmony setup: VehiclePaint + EngineEmissive patches
├── humble-arteest.csproj
└── mod.toml

humble-arteest.lib/                — Core library (referenced by grant supermod)
├── VehiclePaint.cs                — Paint state + shader compilation/swap
├── VehiclePaintPatches.cs         — Harmony prefix on PartModel.AddInstance
├── VehiclePaintSubmod.cs          — ISubmod UI for vehicle painting
├── KittenColor.cs                 — GPU material buffer AlbedoColor writes
├── KittenColorSubmod.cs           — ISubmod UI for kitten coloring
├── EngineEmissive.cs              — Per-engine temperature state management
├── EngineEmissivePatches.cs       — Harmony prefix on PartModelDynamic.AddInstance
├── EngineEmissiveSubmod.cs        — ISubmod UI for engine glow control
├── Experiments/                   — Phase 0 validation experiments (retained for reference)
│   ├── GamePaths.cs               — KSA install path resolution
│   ├── ShaderLoadTest.cs          — Experiment 0.1: shader file loading test
│   ├── PaddingTest.cs             — Experiment 0.2: PerInstanceData padding passthrough
│   ├── MaterialColorTest.cs       — Experiment 0.3: AlbedoColor material test
│   ├── TemperatureTest.cs         — Experiment 0.4: Temperature field override test
│   └── ShaderHotReloadTest.cs     — Experiment 0.5: Runtime shader hot-reload test
└── humble-arteest.lib.csproj
```

---

## Grant Supermod Integration

All three submods (`VehiclePaintSubmod`, `KittenColorSubmod`, `EngineEmissiveSubmod`) implement `ISubmod` from `ksa-abstractions.lib`. Grant registers them alongside other submods and provides Harmony patches via its consolidated patcher.

Grant's `Patcher.cs` calls:
- `VehiclePaintPatches.Apply(harmony)` / `.Remove(harmony)`
- `EngineEmissivePatches.Apply(harmony)` / `.Remove(harmony)`

Grant's `Patcher.Unload()` also calls:
- `VehiclePaint.Cleanup()` — deactivates shaders and clears paint state
- `EngineEmissive.Cleanup()` — clears all engine overrides

Kitten Color has no Harmony patches — it works purely via GPU buffer writes.

---

## Key Decompiled Source References

For future maintenance when KSA updates break this mod, these are the key decompiled sources to examine:

| File | What to Check |
|------|---------------|
| `decomp/ksa/KSA/PartModel.cs` | `PerInstanceData` struct layout (lines ~340-351), `AddInstance()` |
| `decomp/ksa/KSA/PartModelDynamic.cs` | Dynamic `PerInstanceData` struct (lines ~350-361) |
| `decomp/ksa/KSA/PartModelModule.cs` | `UpdateRenderData()` — how PerInstanceData is populated |
| `decomp/ksa/KSA/PartModelDynamicModule.cs` | Dynamic variant with Temperature/TFI |
| `decomp/ksa/KSA/PartModelRenderer.cs` | Pipeline setup, `ColorData.Rebuild()` |
| `decomp/ksa/KSA/MaterialData.cs` | `MaterialData` struct with `AlbedoColor` |
| `decomp/ksa/KSA/GpuMaterialSystem.cs` | Material GPU buffer, `AssetMap`, `BigBuffer` |
| `decomp/ksa/Content/Core/Shaders/Mesh/MeshIndirect.vert` | Vertex shader InstanceData struct |
| `decomp/ksa/Content/Core/Shaders/Mesh/MeshIndirect.frag` | Fragment shader — albedo sampling + highlight mixing |
| `decomp/ksa/Content/Core/Shaders/Mesh/DynamicMeshIndirect.frag` | Temperature LUT emissive application |
| `decomp/ksa/Content/Core/Shaders/Common/MaterialSet.glsl` | AlbedoColor multiplication pattern |
| `decomp/ksa/KSA.AssetReloader/ShaderReloader.cs` | Built-in shader hot-reload system |
| `decomp/ksa/KSA/ShaderReference.cs` | Shader asset with `DoLoad()`, `Shader`, `LocalPath` |

---

## Rendering Pipeline Summary

```
Part Instance (in Vehicle)
    │
    ▼
PartModelModule.UpdateRenderData()           ← creates PerInstanceData
    │
    ▼
PartModel.AddInstance(ref instanceData)      ← HARMONY PATCH: inject paint RGB into padding
    │  Stages to per-viewport instance list
    ▼
PartModel.WriteInstancesToGpu()
    │  Writes to GPU storage buffers:
    │    - PerInstanceDataVectors (Set 3, Binding 0) → Vertex Shader
    │    - PerDrawDataVectors (Set 2, Binding 0)     → Fragment Shader
    ▼
vkCmdDrawIndexedIndirect()
    │
    ▼
Vertex Shader (MeshIndirect.vert)            ← MODIFIED: reads PaintR/G/B from struct
    │  Passes paint values to fragment via locations 6/7/8
    ▼
Fragment Shader (MeshIndirect.frag)          ← MODIFIED: multiplies albedo by paint tint
    │  Samples textures → applies paint tint → applies highlights
    ▼
Output Color
```

---

## Experiment History

The `Experiments/` directory contains the Phase 0 validation tests that proved feasibility:

| Experiment | Result | What It Proved |
|-----------|--------|---------------|
| 0.1 Shader File Loading | ✅ PASSED | KSA loads GLSL from disk at runtime |
| 0.2 Padding Passthrough | ✅ PASSED | C# padding bytes reach GPU shader unchanged |
| 0.3 Material AlbedoColor | ✅ MIXED | Works for kittens (ModelPbr), not vehicle parts (MeshIndirect) |
| 0.4 Temperature Override | ✅ PASSED | Per-instance data flows correctly, no shader changes needed |
| 0.5 Shader Hot-Reload | ✅ PASSED | Runtime shader compilation + pipeline rebuild works from a mod |

These experiments are retained for reference and can be re-run for regression testing after KSA updates.