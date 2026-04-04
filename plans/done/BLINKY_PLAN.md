# Blinky Mod — Implementation Plan

> **Goal**: Build a mod that dynamically creates an LCD pixel grid of engine parts at runtime and attaches it to an existing vehicle, then controls those engines on/off to display animations — like blinken, but without requiring a pre-built vehicle with named pixel parts.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Analysis Summary](#analysis-summary)
3. [Implementation Tasks](#implementation-tasks)
   - [Phase 0: Discovery — Runtime Part Creation API](#phase-0-discovery--runtime-part-creation-api)
   - [Phase 1: Project Scaffolding](#phase-1-project-scaffolding)
   - [Phase 2: Runtime Part Discovery & Template Resolution](#phase-2-runtime-part-discovery--template-resolution)
   - [Phase 3: Dynamic LCD Grid Builder](#phase-3-dynamic-lcd-grid-builder)
   - [Phase 4: Resource Graph Optimization (Harmony Patch)](#phase-4-resource-graph-optimization-harmony-patch)
   - [Phase 5: Engine Control Integration](#phase-5-engine-control-integration)
   - [Phase 6: ImGui Configuration UI](#phase-6-imgui-configuration-ui)
   - [Phase 7: Animation System Integration](#phase-7-animation-system-integration)
   - [Phase 8: Testing & Polish](#phase-8-testing--polish)
4. [Risk Analysis](#risk-analysis)

---

## Architecture Overview

```
blinky/                        ← Mod entry point (UI + lifecycle)
├── Mod.cs                    ← ImGui UI, vehicle selection, lifecycle hooks
├── Patcher.cs                ← Harmony patches (resource graph suppression)
├── mod.toml                  ← Mod manifest
└── blinky.csproj

blinky.lib/                    ← Reusable core logic (headless)
├── LcdGridBuilder.cs         ← Dynamic part creation & grid assembly
├── LcdGridConfig.cs          ← Grid configuration data (width, height, spacing, offset)
├── PartCreator.cs            ← Low-level Part instantiation via reflection/game API
├── BlinkyPixelGrid.cs        ← Enhanced PixelGrid that owns dynamically-created parts
├── ResourceGraphPatcher.cs   ← Harmony-based RecomputeAllDerivedData suppression
└── blinky.lib.csproj

Dependencies:
├── ksa-abstractions.lib      ← PartHelpers, ReflectionHelpers, VehicleProvider
└── blinken.lib               ← LcdAnimation, PixelPatterns (reuse animation system)
```

---

## Analysis Summary

### How Blinken Works (Existing Reference)

Blinken operates on **pre-existing** vehicles where engine parts are named with the convention `pixel_{row}_{col}_{a|b}`:

1. **Part Discovery** — `PixelGrid.ScanFromVehicle()` iterates all parts, parses `pixel_` IDs, groups `a`/`b` pairs into a grid dictionary keyed by `(row, col)`.

2. **Engine Caching** — For each grid cell, caches `EngineController[]` via `part.SubtreeModules.Get<EngineController>()` for O(1) per-frame access.

3. **Engine Control** — Calls `controller.SetIsActive(null, bool)` to toggle engines. After bulk changes, calls `vehicle.Parts.RecomputeAllDerivedData()` to update vehicle stats.

4. **Min Throttle** — Sets `controller.MinimumThrottle = 0.0001f` so engines fire at any throttle level.

5. **Animation** — `LcdAnimation` uses a sparse `HashSet<(int x, int y)>` for source pixel data. A sliding window of width `GridCols` scans across the source image at `ScrollSpeed` pixels/second, only updating engine states when the integer column advances.

**Key Code References**:
- Part scanning: `blinken.lib/PixelGrid.cs` — `ScanFromVehicle()` method
- Engine toggle: `blinken/Mod.cs` — `SetEngineActive()`, `SetEngineActiveCached()`
- Animation: `blinken.lib/LcdAnimation.cs` — `Init()` and `Update(double dt)`
- Pixel data: `blinken.lib/LcdAnimationPixels.cs` — static `(int x, int y)[]` array
- Patterns: `blinken.lib/PixelPatterns.cs` — `AllOn`, `Checkerboard`, etc.

### KSA Part System Architecture

**Part Hierarchy**: `Vehicle` → `Vehicle.Parts.Parts` (top-level `Part[]`) → each `Part` has `SubParts` (recursive tree).

**Part Properties** (verified at runtime):
- `part.Id` — string identifier
- `part.DisplayName` — human name
- `part.Template` — `PartTemplate` reference with visual/simulation data
- `part.Scale` — writable `double3`
- `part.IsSubPart` — bool
- `part.PartParent` — parent Part (nullable)
- `part.SubParts` — child part collection
- `part.SubtreeModules.Get<T>()` — typed module access

**Part Definition System**: Parts are data-driven via XML (`*Assets.xml` + `*GameData.xml`). The game loads these into `ModLibrary` at startup. Parts are retrieved via `ModLibrary.Get<T>(assetId)`.

**Asset Loading Pipeline**:
```
Game startup → ModLibrary.LoadAll() → XmlLoader.Load<AssetBundle>()
  → PartTemplate.OnDataLoad() → ModLibrary.Register(part)
  → MeshFileReference.OnDataLoad() → GltfLoader reads .glb
  → TextureReference.OnDataLoad() → queues GPU upload
```

### Runtime Part Instantiation — CRITICAL UNKNOWN

**No existing mod in this codebase creates Part objects at runtime.** The decompiled sources (`decomp/ksa/`) have **not been generated yet** — they need to be created by running `cd decomp && bun run index.ts`.

The key unknowns that must be resolved in Phase 0:

| Unknown | What We Need | Where to Find It |
|---------|-------------|-------------------|
| Part constructor | `new Part(...)` signature or factory method | Decompiled `Part` class |
| Adding parts to vehicle | `vehicle.Parts.Add(...)` or equivalent | Decompiled `VehicleParts` / `PartCollection` class |
| PartTemplate retrieval | `ModLibrary.Get<PartTemplate>(enginePartId)` | Decompiled `ModLibrary` class |
| Part local positioning | Setting position within vehicle tree | Decompiled `Part` class — transform fields |
| Module initialization | How EngineController gets attached to new parts | Decompiled `Part` creation flow |

### Resource Graph Recomputation

`vehicle.Parts.RecomputeAllDerivedData()` — called after engine state changes. For a dynamically-built grid of N×M engines, this could be called N×M times during construction.

**Optimization Strategy**: Harmony prefix patch to suppress `RecomputeAllDerivedData()` during batch operations, then call it once at the end.

```csharp
// Suppression pattern:
[HarmonyPatch(typeof(VehicleParts), "RecomputeAllDerivedData")]  // exact type TBD
[HarmonyPrefix]
static bool Prefix()
{
    return !ResourceGraphPatcher.IsSuppressed;  // return false = skip original
}
```

**Risk Assessment**: Medium risk. The resource graph computation likely updates:
- Total mass / center of mass
- Resource flow networks (fuel routing)
- Thrust calculations
- Stage composition

Suppressing it during part addition should be safe because no simulation tick occurs between additions (we're in the GUI thread). The single call at the end rebuilds everything correctly.

---

## Implementation Tasks

### Phase 0: Discovery — Runtime Part Creation API

> **CRITICAL PREREQUISITE**: This phase MUST complete before any other work. The entire mod depends on understanding how to create Part objects at runtime.

#### Task 0.1: Generate Decompiled Sources

Run the decompilation tool to generate browsable KSA source files:

```bash
cd decomp
bun run index.ts
```

This creates `decomp/ksa/*.cs` files with decompiled class definitions.

#### Task 0.2: Discover Part Constructor & Factory

Search decompiled sources for:

```bash
# In decomp/ksa/ directory:
grep -rn "class Part " *.cs          # Find Part class definition
grep -rn "new Part(" *.cs            # Find Part instantiation sites
grep -rn "CreatePart\|InstantiatePart\|PartFactory" *.cs
grep -rn "class VehicleParts\|class PartCollection" *.cs
```

**What to discover**:
1. `Part` constructor signature — what parameters does it take?
2. Does Part need a `PartTemplate`? How is it bound?
3. How does `vehicle.Parts` work? What is the backing collection?
4. Is there an `Add`/`Insert` method on the parts collection?
5. How are `SubtreeModules` populated when creating a Part?

#### Task 0.3: Discover Part Local Transform

Search for how parts store their position within the vehicle:

```bash
grep -rn "Position\|LocalPosition\|Transform\|Offset" Part.cs
grep -rn "class PartTransform\|PartPlacement" *.cs
```

**What to discover**:
1. How is a Part's position set relative to its parent?
2. Is there a `Transform` object with local position/rotation?
3. How does the editor set part positions when placing them?

#### Task 0.4: Discover ModLibrary Part Retrieval

```bash
grep -rn "class ModLibrary" *.cs
grep -rn "ModLibrary.Get\|ModLibrary.Register" *.cs
grep -rn "PartTemplate" *.cs | head -50
```

**What to discover**:
1. How to retrieve a PartTemplate by ID: `ModLibrary.Get<PartTemplate>(id)`?
2. What IDs do engine parts have? (e.g., `"CorePropulsionA_Prefab_EngineSmallA"`)
3. Can templates be enumerated to find available engine parts?

#### Task 0.5: Prototype Runtime Part Creation

Build a minimal test in the blinky mod (or use the existing `fixme-mod-name` template):

```csharp
// Debug button in ImGui:
if (ImGui.Button("Dbg: Create Test Part"))
{
    var vehicle = VehicleProvider.GetControlledVehicle();
    if (vehicle == null) return;
    
    // 1. Get an engine PartTemplate from ModLibrary
    var template = ModLibrary.Get<PartTemplate>("CorePropulsionA_Prefab_EngineSmallA");
    Console.WriteLine($"blinky: template = {template?.GetType().FullName ?? "null"}");
    
    // 2. Try to create a Part from the template
    //    (exact API TBD from decompiled sources)
    // var newPart = new Part(template, ...);
    
    // 3. Set position relative to vehicle
    // newPart.Position = new double3(0, 5, 0);
    
    // 4. Add to vehicle
    // vehicle.Parts.Add(newPart);
    
    // 5. Recompute
    // vehicle.Parts.RecomputeAllDerivedData();
}
```

Use the **runtime reflection dump strategy** from `debug.md` to discover APIs that don't match decompiled sources:

```csharp
// Dump Part class structure at runtime:
private static void DumpPartStructure(Part part)
{
    var type = part.GetType();
    while (type != null && type != typeof(object))
    {
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            object? val = null;
            try { val = f.GetValue(part); } catch { val = "<error>"; }
            Console.WriteLine($"blinky: [{type.Name}] {f.Name} ({f.FieldType.Name}) = {val}");
        }
        type = type.BaseType;
    }
}
```

#### Task 0.6: Document Findings

Record all discovered APIs and update the plan with concrete implementation details. This is the gate for proceeding to Phase 1+.

**Expected deliverables from Phase 0**:
- Part constructor signature
- Method to add Part to vehicle.Parts
- Part local positioning API
- ModLibrary PartTemplate retrieval pattern
- Any required initialization steps for new Parts
- Understanding of when/how EngineController is automatically created vs manually attached

---

### Phase 1: Project Scaffolding

#### Task 1.1: Create blinky.lib Project

Create `blinky.lib/blinky.lib.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.BlinkyLib</AssemblyName>
    <RootNamespace>MeowSci.BlinkyLib</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
    <ProjectReference Include="..\blinken.lib\blinken.lib.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
    <PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
  </ItemGroup>

  <!-- Same KSA DLL references as blinken.lib.csproj -->
  <ItemGroup>
    <Reference Include="Brutal.Core.Common" Condition="Exists('$(KSAFolder)Brutal.Core.Common.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Common.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.ImGui" Condition="Exists('$(KSAFolder)Brutal.ImGui.dll')">
      <HintPath>$(KSAFolder)Brutal.ImGui.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.ImGui.Abstractions" Condition="Exists('$(KSAFolder)Brutal.ImGui.Abstractions.dll')">
      <HintPath>$(KSAFolder)Brutal.ImGui.Abstractions.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Brutal.Core.Strings" Condition="Exists('$(KSAFolder)Brutal.Core.Strings.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Strings.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
      <HintPath>$(KSAFolder)KSA.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

#### Task 1.2: Create blinky Mod Project

Use `fixme-mod-name/` as template. Create `blinky/blinky.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.Blinky</AssemblyName>
    <DistDir>$(SelectedDistModDir)blinky\</DistDir>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\blinky.lib\blinky.lib.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
    <PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
  </ItemGroup>

  <!-- Same KSA DLL references + copy targets as fixme-mod-name.csproj -->
</Project>
```

Create `blinky/mod.toml`:

```toml
name = "blinky"
description = "Dynamic LCD pixel grid builder"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.Blinky"
```

#### Task 1.3: Create Skeleton Files

**`blinky/Mod.cs`** — standard lifecycle, F11 toggle, ImGui window (copy pattern from `fixme-mod-name/Mod.cs`).

**`blinky/Patcher.cs`** — standard Harmony setup (copy pattern from `fixme-mod-name/Patcher.cs`), with harmony id `"blinky"`.

#### Task 1.4: Update slnx and Index

Add to `ksa-mod-experiments.slnx`:
```xml
<Project Path="blinky/blinky.csproj" />
<Project Path="blinky.lib/blinky.lib.csproj" />
```

Update `REPOSITORY_INDEX.md` with blinky entry.

#### Task 1.5: Verify Build

```bash
dotnet build
```

Must compile clean before proceeding.

---

### Phase 2: Runtime Part Discovery & Template Resolution

#### Task 2.1: Create PartCreator.cs

`blinky.lib/PartCreator.cs` — encapsulates runtime Part creation logic discovered in Phase 0.

```csharp
using System;
using System.Reflection;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Creates Part instances at runtime from PartTemplates retrieved via ModLibrary.
/// API details populated from Phase 0 discovery.
/// </summary>
public static class PartCreator
{
    /// <summary>
    /// Retrieves a PartTemplate by its asset ID from ModLibrary.
    /// </summary>
    /// <param name="partTemplateId">
    /// The asset ID of the part template (e.g., "CorePropulsionA_Prefab_EngineSmallA").
    /// This ID matches the Part Id from the *Assets.xml definitions.
    /// </param>
    public static object? GetPartTemplate(string partTemplateId)
    {
        // Implementation TBD from Phase 0 discovery.
        // Likely: ModLibrary.Get<PartTemplate>(partTemplateId)
        // May need reflection if PartTemplate is not in the public API.
        throw new NotImplementedException("Phase 0 discovery required");
    }

    /// <summary>
    /// Creates a new Part instance from a template and positions it.
    /// </summary>
    /// <param name="template">PartTemplate retrieved via GetPartTemplate()</param>
    /// <param name="partId">Unique string ID to assign (e.g., "blinky_pixel_0_0_a")</param>
    /// <param name="localPosition">Position offset in parent's local frame (metres)</param>
    /// <returns>Newly created Part, or null on failure</returns>
    public static Part? CreatePart(object template, string partId, double3 localPosition)
    {
        // Implementation TBD from Phase 0 discovery.
        // Steps (expected):
        //   1. Instantiate Part from template
        //   2. Set part.Id = partId
        //   3. Set local position/transform
        //   4. Return part (not yet added to vehicle)
        throw new NotImplementedException("Phase 0 discovery required");
    }

    /// <summary>
    /// Adds a Part to a vehicle's part tree.
    /// </summary>
    /// <param name="vehicle">Target vehicle</param>
    /// <param name="part">Part to add (created via CreatePart)</param>
    /// <param name="parentPart">Optional parent part; if null, adds as root part</param>
    public static void AddPartToVehicle(Vehicle vehicle, Part part, Part? parentPart = null)
    {
        // Implementation TBD from Phase 0 discovery.
        // Likely: vehicle.Parts.Parts.Add(part) or similar
        // May need reflection if the collection is read-only
        throw new NotImplementedException("Phase 0 discovery required");
    }
}
```

#### Task 2.2: Vehicle & Part Selector

The mod needs a way for the user to:
1. Select which vehicle to attach the LCD grid to
2. Select which engine part template to use for the pixels
3. (Optional) Select which existing part to use as the attachment parent

This is handled in the ImGui UI (Phase 6) but the data structures belong in the lib:

```csharp
// blinky.lib/LcdGridConfig.cs
namespace MeowSci.BlinkyLib;

public class LcdGridConfig
{
    /// <summary>Width of the LCD grid in pixels (number of engine columns).</summary>
    public int Width { get; set; } = 16;

    /// <summary>Height of the LCD grid in pixels (number of engine rows).</summary>
    public int Height { get; set; } = 9;

    /// <summary>Spacing between engine parts in metres (X and Y).</summary>
    public float SpacingMetres { get; set; } = 0.5f;

    /// <summary>Offset from the source vehicle origin in body frame (metres).</summary>
    public Brutal.Numerics.double3 Offset { get; set; }

    /// <summary>
    /// The PartTemplate asset ID to use for each pixel engine.
    /// e.g., "CorePropulsionA_Prefab_EngineSmallA"
    /// </summary>
    public string EnginePartTemplateId { get; set; } = "";

    /// <summary>Total number of engine parts that will be created = Width × Height × 2 (a/b pairs).</summary>
    public int TotalParts => Width * Height * 2;
}
```

---

### Phase 3: Dynamic LCD Grid Builder

#### Task 3.1: LcdGridBuilder.cs

`blinky.lib/LcdGridBuilder.cs` — orchestrates the creation of the full engine pixel grid.

```csharp
using System;
using System.Collections.Generic;
using KSA;
using MeowSci.BlinkenLib;  // Reuse PixelGrid, LcdAnimation

namespace MeowSci.BlinkyLib;

/// <summary>
/// Builds an LCD pixel grid of engine parts on a vehicle at runtime.
/// Each pixel position gets two engine parts (a/b pair), following blinken conventions.
/// </summary>
public class LcdGridBuilder
{
    /// <summary>
    /// Builds the entire LCD grid on the specified vehicle.
    /// </summary>
    /// <param name="vehicle">Vehicle to attach engines to</param>
    /// <param name="config">Grid configuration (dimensions, spacing, offset, template)</param>
    /// <returns>PixelGrid populated with the new engine parts, or null on failure</returns>
    public static PixelGrid? BuildGrid(Vehicle vehicle, LcdGridConfig config)
    {
        Console.WriteLine($"blinky: building {config.Width}x{config.Height} grid " +
                          $"({config.TotalParts} parts) with spacing {config.SpacingMetres}m");

        // 1. Get engine part template
        var template = PartCreator.GetPartTemplate(config.EnginePartTemplateId);
        if (template == null)
        {
            Console.WriteLine($"blinky: ERROR — template '{config.EnginePartTemplateId}' not found");
            return null;
        }

        // 2. Suppress resource graph recomputation during bulk add
        ResourceGraphPatcher.Suppress();

        try
        {
            // 3. Create engine parts in grid pattern
            for (int row = 0; row < config.Height; row++)
            {
                for (int col = 0; col < config.Width; col++)
                {
                    // Calculate position: grid origin + row/col offset + config offset
                    // X axis = columns (horizontal), Y axis = rows (vertical)
                    // Z axis = depth (all engines at same depth)
                    var basePos = new Brutal.Numerics.double3(
                        config.Offset.x + col * config.SpacingMetres,
                        config.Offset.y + row * config.SpacingMetres,
                        config.Offset.z
                    );

                    // Create engine pair (a and b), slightly offset in Z
                    string idA = $"pixel_{row}_{col}_a";
                    string idB = $"pixel_{row}_{col}_b";

                    var partA = PartCreator.CreatePart(template, idA, basePos);
                    var partB = PartCreator.CreatePart(template, idB, basePos);

                    if (partA != null) PartCreator.AddPartToVehicle(vehicle, partA);
                    if (partB != null) PartCreator.AddPartToVehicle(vehicle, partB);
                }
            }
        }
        finally
        {
            // 4. Unsuppress and trigger a single recomputation
            ResourceGraphPatcher.Unsuppress();
        }

        // 5. Single recompute after all parts added
        vehicle.Parts.RecomputeAllDerivedData();

        // 6. Scan the vehicle to build the PixelGrid (reuses blinken's scanner)
        var grid = PixelGrid.ScanFromVehicle(vehicle);
        Console.WriteLine($"blinky: grid build complete — {grid.Count} pixel pairs found");
        return grid;
    }

    /// <summary>
    /// Removes all blinky-created engine parts from a vehicle.
    /// </summary>
    public static void DestroyGrid(Vehicle vehicle)
    {
        // Implementation depends on Phase 0 discovery of Part removal API
        // Likely involves:
        //   1. Find all parts with "pixel_" prefix
        //   2. Remove each from vehicle.Parts
        //   3. RecomputeAllDerivedData()
        Console.WriteLine("blinky: grid destruction not yet implemented");
    }
}
```

**Key Design Decisions**:
- Part IDs follow blinken's `pixel_{row}_{col}_{a|b}` convention so that `PixelGrid.ScanFromVehicle()` from blinken.lib can be reused directly.
- Grid positions are computed from config offset + row/col × spacing.
- The `a`/`b` pair per pixel matches blinken's design (two engines per LCD pixel for a brighter display).

---

### Phase 4: Resource Graph Optimization (Harmony Patch)

#### Task 4.1: ResourceGraphPatcher.cs

`blinky.lib/ResourceGraphPatcher.cs` — provides suppression of `RecomputeAllDerivedData()` during batch operations.

```csharp
using System;
using HarmonyLib;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Harmony patch to suppress RecomputeAllDerivedData() calls during batch part addition.
/// 
/// SAFETY ANALYSIS:
/// - RecomputeAllDerivedData() rebuilds: mass, CoM, resource flow graphs, thrust calculations
/// - During part addition on the GUI thread, no simulation tick runs between additions
/// - Therefore, suppressing during batch addition and calling once at end is safe
/// - The final single call rebuilds everything from the current complete state
/// 
/// RISK: If the game internally calls RecomputeAllDerivedData() as part of Part insertion
///       (not just externally by mod code), those calls will also be suppressed.
///       This should be fine as long as we unsuppress + recompute before any sim tick.
/// </summary>
public static class ResourceGraphPatcher
{
    private static int _suppressionDepth = 0;

    /// <summary>True when recomputation is currently suppressed.</summary>
    public static bool IsSuppressed => _suppressionDepth > 0;

    /// <summary>Begin suppressing RecomputeAllDerivedData() calls. Supports nested calls.</summary>
    public static void Suppress()
    {
        _suppressionDepth++;
        Console.WriteLine($"blinky: resource graph suppression ON (depth={_suppressionDepth})");
    }

    /// <summary>Stop suppressing. When depth reaches 0, calls are allowed again.</summary>
    public static void Unsuppress()
    {
        if (_suppressionDepth > 0)
            _suppressionDepth--;
        Console.WriteLine($"blinky: resource graph suppression OFF (depth={_suppressionDepth})");
    }

    /// <summary>Reset suppression depth to 0 (emergency recovery).</summary>
    public static void Reset()
    {
        _suppressionDepth = 0;
    }
}
```

#### Task 4.2: Harmony Prefix Patch

In `blinky/Patcher.cs`, add the Harmony patch. The exact target type must be determined in Phase 0 — `vehicle.Parts` returns an object whose type needs to be discovered (likely `VehicleParts` or similar):

```csharp
// In Patcher.cs — add this patch class:

// NOTE: The target type for RecomputeAllDerivedData must be discovered in Phase 0.
// vehicle.Parts is likely of type KSA.VehicleParts or similar.
// Use runtime reflection: vehicle.Parts.GetType().FullName to confirm.

// [HarmonyPatch(typeof(KSA.VehicleParts), "RecomputeAllDerivedData")]  // TYPE TBD
// internal static class RecomputePatch
// {
//     static bool Prefix()
//     {
//         if (ResourceGraphPatcher.IsSuppressed)
//         {
//             Console.WriteLine("blinky: suppressed RecomputeAllDerivedData() call");
//             return false;  // Skip original
//         }
//         return true;  // Let it run
//     }
// }
```

**Discovery Step**: In Phase 0, run this debug code to find the exact type:
```csharp
var vehicle = VehicleProvider.GetControlledVehicle();
if (vehicle != null)
{
    Console.WriteLine($"blinky: vehicle.Parts type = {vehicle.Parts.GetType().FullName}");
    // Also check if RecomputeAllDerivedData exists:
    var method = vehicle.Parts.GetType().GetMethod("RecomputeAllDerivedData");
    Console.WriteLine($"blinky: RecomputeAllDerivedData method = {method?.DeclaringType?.FullName}.{method?.Name}");
}
```

#### Task 4.3: Performance Measurement

Add timing instrumentation to measure the actual impact:

```csharp
// In LcdGridBuilder.BuildGrid():
var sw = System.Diagnostics.Stopwatch.StartNew();
// ... create all parts ...
sw.Stop();
Console.WriteLine($"blinky: part creation took {sw.ElapsedMilliseconds}ms");

sw.Restart();
vehicle.Parts.RecomputeAllDerivedData();
sw.Stop();
Console.WriteLine($"blinky: single RecomputeAllDerivedData took {sw.ElapsedMilliseconds}ms");
```

This data helps validate whether the suppression strategy is worthwhile and whether the grid size needs to be capped.

---

### Phase 5: Engine Control Integration

#### Task 5.1: BlinkyPixelGrid.cs

`blinky.lib/BlinkyPixelGrid.cs` — extends the concept of `PixelGrid` with ownership semantics for dynamically created parts.

```csharp
using System;
using System.Collections.Generic;
using KSA;
using MeowSci.BlinkenLib;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Wraps a PixelGrid with lifecycle management for dynamically created parts.
/// Tracks whether the grid was built by blinky (owned) or scanned from existing parts (borrowed).
/// </summary>
public class BlinkyPixelGrid
{
    public PixelGrid Grid { get; }
    public LcdGridConfig Config { get; }
    public bool IsOwned { get; }  // true if blinky created the parts; false if scanned

    public BlinkyPixelGrid(PixelGrid grid, LcdGridConfig config, bool isOwned)
    {
        Grid = grid;
        Config = config;
        IsOwned = isOwned;
    }

    /// <summary>
    /// Deactivates all engines in the grid and sets them to minimum throttle.
    /// </summary>
    public void DeactivateAll(Vehicle vehicle)
    {
        foreach (var (_, engines) in Grid.Engines)
            for (int i = 0; i < engines.Length; i++)
                engines[i].SetIsActive(null, false);
    }

    /// <summary>
    /// Prepares all engines for LCD operation (sets MinimumThrottle, then recomputes).
    /// </summary>
    public void PrepareForAnimation(Vehicle vehicle)
    {
        var engineControllers = vehicle.Parts.Modules.Get<EngineController>();
        foreach (var controller in engineControllers)
            controller.MinimumThrottle = 0.0001f;
        vehicle.Parts.RecomputeAllDerivedData();
    }
}
```

---

### Phase 6: ImGui Configuration UI

#### Task 6.1: Main Window Layout

`blinky/Mod.cs` — the ImGui window should have these sections:

**Section 1: Vehicle Selection**
```csharp
// Show current controlled vehicle
var vehicle = VehicleProvider.GetControlledVehicle();
ImGui.Text($"Vehicle: {vehicle?.Id ?? "none"}");
```

**Section 2: Grid Configuration**
```csharp
// Grid dimensions
ImGui.SliderInt("Width (columns)", ref _configWidth, 1, 64);
ImGui.SliderInt("Height (rows)", ref _configHeight, 1, 64);
ImGui.Text($"Total parts: {_configWidth * _configHeight * 2}");

// Spacing
ImGui.SliderFloat("Spacing (m)", ref _configSpacing, 0.1f, 5.0f);

// Offset from vehicle origin
ImGui.DragFloat3("Offset (m)", ref _configOffset, 0.1f);

// Engine template selection
ImGui.InputText("Engine Part ID", ref _enginePartId, 256);
// TODO: Could add a dropdown of known engine template IDs
```

**Section 3: Build Controls**
```csharp
if (ImGui.Button("Build Grid"))
{
    var config = new LcdGridConfig
    {
        Width = _configWidth,
        Height = _configHeight,
        SpacingMetres = _configSpacing,
        Offset = new double3(_configOffset.x, _configOffset.y, _configOffset.z),
        EnginePartTemplateId = _enginePartId,
    };
    _blinkyGrid = new BlinkyPixelGrid(
        LcdGridBuilder.BuildGrid(vehicle, config),
        config,
        isOwned: true
    );
}

if (_blinkyGrid != null && ImGui.Button("Destroy Grid"))
{
    LcdGridBuilder.DestroyGrid(vehicle);
    _blinkyGrid = null;
}
```

**Section 4: Pattern Controls** (reuse blinken patterns)
```csharp
if (_blinkyGrid?.Grid != null)
{
    // Same pattern buttons as blinken
    if (ImGui.Button("All On"))
        ApplyPattern(PixelPatterns.AllOn);
    ImGui.SameLine();
    if (ImGui.Button("Checkerboard"))
        ApplyPattern(PixelPatterns.Checkerboard);
    // ... etc.
}
```

**Section 5: Animation Controls** (reuse blinken LcdAnimation)
```csharp
if (ImGui.Button(_animActive ? "Stop" : "Start"))
{
    _animActive = !_animActive;
    if (_animActive)
        _lcdAnimation.Init(_blinkyGrid.Grid);
}
ImGui.SliderFloat("Speed", ref speed, 0.5f, 20f);
```

**Section 6: Debug**
```csharp
if (ImGui.Button("Dbg: Dump Part Structure"))
{
    // Reflection dump for development
}
```

---

### Phase 7: Animation System Integration

#### Task 7.1: Reuse Blinken Animation System

The `LcdAnimation` class from `blinken.lib` can be reused directly since blinky's pixel grid follows the same `PixelGrid` structure (same `pixel_{row}_{col}_{a|b}` naming).

```csharp
// In Mod.cs:
private readonly LcdAnimation _lcdAnimation = new();  // from MeowSci.BlinkenLib

// In OnAfterUi():
if (_animActive && _blinkyGrid?.Grid != null)
    _lcdAnimation.Update(dt);
```

#### Task 7.2: Custom Pixel Data

Blinky should support loading different pixel data sources, not just the hardcoded `LcdAnimationPixels.Pixels`. Options:

1. **Reuse blinken's static data** — simplest, works immediately
2. **Runtime pixel data injection** — allow setting custom `(int x, int y)[]` data

For option 2, create a modified animation class or extend `LcdAnimation` with a configurable pixel source:

```csharp
// blinky.lib/BlinkyAnimation.cs
using MeowSci.BlinkenLib;

namespace MeowSci.BlinkyLib;

/// <summary>
/// Extended LCD animation that accepts custom pixel data at runtime.
/// </summary>
public class BlinkyAnimation : LcdAnimation
{
    // If LcdAnimation doesn't support custom pixel data injection,
    // consider either:
    //   a) Modifying LcdAnimation to accept pixel data as Init() parameter
    //   b) Duplicating the animation logic with custom data support
    //
    // Preferred: Modify LcdAnimation.Init() to accept optional pixel array parameter.
    // This benefits both blinken and blinky.
}
```

**Recommended approach**: Modify `blinken.lib/LcdAnimation.cs` to accept an optional `(int x, int y)[]` parameter in `Init()`:

```csharp
// Modified Init signature in LcdAnimation.cs:
public void Init(PixelGrid grid, (int x, int y)[]? customPixels = null)
{
    _grid = grid;
    GridRows = grid.Rows;
    GridCols = grid.Cols;

    var pixels = customPixels ?? LcdAnimationPixels.Pixels;
    // ... rest of init
}
```

---

### Phase 8: Testing & Polish

#### Task 8.1: Incremental Testing

Test each phase independently:

1. **Phase 0 Gate**: Can you create a single Part at runtime and see it on the vehicle?
2. **Phase 3 Gate**: Can you build a 2×2 grid and see 4 engine positions?
3. **Phase 4 Gate**: Does the suppression patch work? Measure with/without.
4. **Phase 5 Gate**: Can you toggle engines on the dynamically-created grid?
5. **Phase 7 Gate**: Does the scrolling animation work on the dynamic grid?

#### Task 8.2: Scale Testing

Test with progressively larger grids to find performance limits:
- 4×4 = 32 parts
- 8×8 = 128 parts
- 16×9 = 288 parts
- 32×18 = 1152 parts
- 64×36 = 4608 parts (stress test)

Record timings for:
- Grid creation time
- `RecomputeAllDerivedData()` time  
- Per-frame animation update time
- Frame rate impact

#### Task 8.3: Error Handling

- Handle missing vehicle gracefully
- Handle invalid template IDs
- Handle grid build failures (partial cleanup)
- Protect animation from null grid
- Log errors with `Console.WriteLine`

#### Task 8.4: Update Documentation

- Create `blinky/README.md` describing the mod
- Update `REPOSITORY_INDEX.md` with blinky and blinky.lib entries

---

## Risk Analysis

### High Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Part constructor is not accessible** | Cannot create parts at all | Use reflection to call private constructors. If no constructor exists, investigate editor code path as alternative. |
| **Parts collection is immutable** | Cannot add parts to vehicle | Use reflection to access backing collection. Or use editor API if it exposes an "add part" method. |
| **EngineController not auto-created** | Dynamic parts don't have engines | Manually create and attach EngineController via reflection, copying from template. |

### Medium Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Resource graph suppression breaks sim** | Vehicle state corruption | Test thoroughly. Add emergency reset. Only suppress during same-frame batch operations. |
| **Large grids cause frame drops** | Poor UX | Cap grid size. Profile per-frame cost. Consider engine pooling or LOD. |
| **Part positioning doesn't work as expected** | Grid layout wrong | Debug with small grids. Adjust coordinate system based on observed behavior. |

### Low Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Decompiled sources don't match runtime** | API discovery harder | Use runtime reflection dump strategy (documented in debug.md). |
| **Part template IDs are wrong** | Can't find templates | Enumerate ModLibrary at runtime to find valid IDs. |
| **MinThrottle / Active state issues** | Engines don't light | Same pattern as blinken — verified working. |

---

## Dependencies & Prerequisites

```
Phase 0 (BLOCKING) ─┬─→ Phase 1 (scaffolding)
                     │     └─→ Phase 2 (part discovery)
                     │           └─→ Phase 3 (grid builder)
                     │                 └─→ Phase 4 (resource opt)
                     │                       └─→ Phase 5 (engine ctrl)
                     │                             └─→ Phase 6 (UI)
                     │                                   └─→ Phase 7 (animation)
                     │                                         └─→ Phase 8 (testing)
                     │
                     └─→ Generate decomp/ksa/ sources first!
```

Phase 0 is the **critical path** — everything else depends on discovering the runtime Part creation API. If Part creation proves impossible through the expected approach, alternative strategies include:
1. Using the vehicle editor's internal API to place parts programmatically
2. Creating a custom XML asset bundle at runtime and triggering a reload
3. Spawning pre-built vehicles with the pixel grid (hybrid approach with blinken)