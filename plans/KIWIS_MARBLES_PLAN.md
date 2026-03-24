# Kiwi's Marbles — Implementation Plan

A mod for repositioning celestial bodies (planets, moons) in-game by "welding" them to follow other celestial bodies or vehicles at user-defined offsets. Design mirrors `garys-torch` (vehicle welding) adapted for the celestial body API.

---

## Architecture Summary

```
ksa-abstractions.lib/
  └── CelestialProvider.cs         (NEW — list/get celestial bodies)

kiwis-marbles.lib/
  ├── CelestialWeldEntry.cs        (NEW — weld data structure)
  └── CelestialWeldEngine.cs       (NEW — per-frame repositioning + DAG sort)

kiwis-marbles/
  ├── Mod.cs                       (MODIFY — full ImGui UI + per-frame update loop)
  ├── Patcher.cs                   (EXISTS — no changes needed)
  ├── kiwis-marbles.csproj         (MODIFY — add ProjectReferences)
  └── mod.toml                     (EXISTS — no changes needed)
```

### Key Differences from garys-torch

| Aspect | garys-torch (Vehicles) | kiwis-marbles (Celestials) |
|--------|----------------------|--------------------------|
| Source type | `Vehicle` | `Celestial` (the class, not the struct) |
| Target type | `Vehicle` | `IOrbiter` (can be `Celestial` or `Vehicle`) |
| Reposition method | `Vehicle.Teleport(orbit, body2Cce, bodyRates)` | `Celestial.SetOrbit(orbit)` + `Celestial.UpdatePerFrameData()` |
| Offset type | `float3` (meters, body-frame-relative) | `double3` (meters, CCI-frame) |
| Rotation control | Yes (lock rotation, Euler offset) | No (celestials rotate on their own axis via angular velocity) |
| Scale control | Yes (part scaling) | No (celestial rendering is separate) |
| Body frame | Target's body orientation used to transform offset | No body frame — offset is raw CCI direction |
| Cross-parent weld | Rejected (parent mismatch = unweld) | Supported — `SetOrbit()` auto-updates parent via `SetParent()` |

### Coordinate System Notes

- **CCI (Celestial-Centered Inertial)**: Inertial frame centered on the parent body. Celestial positions from `GetPositionCci()` are relative to their parent body in this frame.
- When source and target share the same parent: offset directly in CCI relative to that parent.
- When parents differ: source's parent changes to target's parent via `SetOrbit()` → `SetParent()`.
- Celestial `SetParent()` auto-manages the `Children` list (removes from old parent, adds to new).

### Decompiled Source References

Key decompiled files for reference during implementation:

- `decomp/ksa/KSA/Celestial.cs` — Base class for planets/moons. Has `SetOrbit()`, `GetPositionCci()`, `GetVelocityCci()`, `UpdatePerFrameData()`, `GetCci2Cce()`.
- `decomp/ksa/KSA/PlanetaryBody.cs` — Concrete planet/moon class (extends `StaticCelestial` extends `Celestial`).
- `decomp/ksa/KSA/StellarBody.cs` — Star class. NO orbit, always at origin. Cannot be a weld source.
- `decomp/ksa/KSA/Orbit.cs` — `Orbit.CreateFromStateCci(parent, time, posCci, velCci, color)` creates orbit from state vectors.
- `decomp/ksa/KSA/CelestialSystem.cs` — `All.GetList()` returns all Astronomical objects; `Get<T>(id)` for typed lookup.
- `decomp/ksa/KSA/Vehicle.cs` — `Vehicle.Teleport()` for reference; vehicles implement `IOrbiter`.
- `decomp/ksa/KSA/LookupCollection.cs` — `GetList()`, `Get(string id)`, `TryGet()`.

---

## Task 1: Add CelestialProvider to ksa-abstractions.lib

**File**: `ksa-abstractions.lib/CelestialProvider.cs` (NEW)

**Purpose**: Provide a clean abstraction for accessing celestial bodies, mirroring `VehicleProvider`. This is a generic pattern useful for multiple mods, so it belongs in ksa-abstractions.lib.

**Implementation**:

```csharp
using System.Collections.Generic;
using System.Linq;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Static helpers to get celestial bodies from the current system.</summary>
public static class CelestialProvider
{
    /// <summary>Returns all Celestial objects (planets, moons) in the current system.</summary>
    public static List<Celestial> GetAllCelestials()
    {
        var all = Universe.CurrentSystem?.All?.GetList();
        if (all == null) return new List<Celestial>();
        return all.OfType<Celestial>().ToList();
    }

    /// <summary>Returns all IOrbiter objects (celestials + vehicles) in the current system.</summary>
    public static List<IOrbiter> GetAllOrbiters()
    {
        var result = new List<IOrbiter>();
        var celestials = Universe.CurrentSystem?.All?.GetList();
        if (celestials != null)
        {
            foreach (var item in celestials)
                if (item is IOrbiter orbiter)
                    result.Add(orbiter);
        }
        return result;
    }

    /// <summary>Returns a specific celestial body by ID, or null if not found.</summary>
    public static Celestial? GetCelestial(string id)
        => Universe.CurrentSystem?.Get<Celestial>(id);
}
```

**Notes**:
- `Universe.CurrentSystem.All.GetList()` returns `List<Astronomical>` which includes both `Celestial` (planets/moons) and `Vehicle` types
- We filter with `OfType<Celestial>()` to get only celestial bodies
- `GetAllOrbiters()` returns everything that implements `IOrbiter` — useful for the target selector which can be any orbiting body
- `StellarBody` (stars) are NOT `Celestial` — they are `Astronomical` directly. They can't be moved so this is correct.

**Reference**: Follow the exact pattern from `ksa-abstractions.lib/VehicleProvider.cs`:
```csharp
public static class VehicleProvider
{
    public static Vehicle? GetControlledVehicle() => Program.ControlledVehicle;
    public static List<Vehicle> GetAllVehicles() =>
        Universe.CurrentSystem?.Vehicles?.GetList() ?? new List<Vehicle>();
}
```

---

## Task 2: Create CelestialWeldEntry in kiwis-marbles.lib

**File**: `kiwis-marbles.lib/CelestialWeldEntry.cs` (NEW)

**Purpose**: Data class representing an active weld between a celestial body (source) and an orbiter (target).

**Implementation**:

```csharp
using Brutal.Numerics;
using KSA;

namespace MeowSci.KiwisMarblesLib;

/// <summary>Represents a single active weld that locks a celestial body's position relative to an orbiter.</summary>
public class CelestialWeldEntry
{
    /// <summary>The celestial body being repositioned.</summary>
    public Celestial Source = null!;

    /// <summary>The orbiter the source follows (can be Celestial or Vehicle).</summary>
    public IOrbiter Target = null!;

    /// <summary>
    /// Offset from the target's CCI position, in meters.
    /// Applied directly in the CCI frame (not rotated by any body frame).
    /// Use large values — planetary distances are typically millions to billions of meters.
    /// </summary>
    public double3 Offset;
}
```

**Design decisions**:
- `Source` is `Celestial` (not `IOrbiter`) because only celestial bodies should be moved by this mod. Vehicles are handled by garys-torch.
- `Target` is `IOrbiter` so it can be either a `Celestial` or a `Vehicle`. Both implement `IOrbiter` which provides `GetPositionCci()`, `GetVelocityCci()`, `Orbit.Parent`, etc.
- `Offset` is `double3` (not `float3`) because celestial distances require double precision. A `float3` maxes out at ~1e38 which is technically enough, but intermediate calculations with doubles avoid precision loss.
- No rotation or scale fields — celestial bodies have their own angular velocity (not controllable like vehicle body rates) and their rendering is via `DistantSphereRenderer` (not part-based scaling).

**Reference**: Modeled after `garys-torch.lib/WeldEntry.cs`:
```csharp
public class WeldEntry
{
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    public float3 Position;
    public float3 Rotation;
    public float Scale = 1f;
    public bool LockRotation = true;
}
```

---

## Task 3: Create CelestialWeldEngine in kiwis-marbles.lib

**File**: `kiwis-marbles.lib/CelestialWeldEngine.cs` (NEW)

**Purpose**: Stateless engine that performs the per-frame celestial body repositioning and DAG ordering. This is the core logic of the mod.

**Implementation**:

```csharp
using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarblesLib;

/// <summary>Stateless engine for celestial body weld computation.</summary>
public static class CelestialWeldEngine
{
    /// <summary>
    /// Repositions the source celestial body to maintain its weld relative to the target.
    /// Returns false if the weld should be removed (e.g. source or target no longer valid).
    /// </summary>
    public static bool UpdateWeld(CelestialWeldEntry entry)
    {
        // Validate source and target still exist
        if (entry.Source == null || entry.Target == null)
        {
            Console.WriteLine("kiwis-marbles: Source or target is null, removing weld");
            return false;
        }

        // Get the target's parent body — this is what the source will orbit
        IParentBody targetParent = entry.Target.Parent;
        if (targetParent == null)
        {
            Console.WriteLine("kiwis-marbles: Target has no parent body, removing weld");
            return false;
        }

        // Get target's current state in CCI frame (relative to target's parent)
        double3 tgtPosCci = entry.Target.GetPositionCci();
        double3 tgtVelCci = entry.Target.GetVelocityCci();

        // Compute source's new position: target position + user-defined offset
        double3 newSrcPosCci = tgtPosCci + entry.Offset;

        // Source gets same velocity as target (rigid constraint — follows target)
        double3 newSrcVelCci = tgtVelCci;

        // Determine orbit line color — preserve existing if possible
        byte4 orbitColor = entry.Source.OrbitColor;

        // Create new orbit from the computed state
        Orbit newOrbit = Orbit.CreateFromStateCci(
            targetParent,
            SimTimeProvider.GetElapsedTime(),
            newSrcPosCci,
            newSrcVelCci,
            orbitColor
        );

        // Apply the new orbit — this also changes parent if needed
        entry.Source.SetOrbit(newOrbit);

        // Refresh cached frame transforms (position, velocity, rotation matrices)
        entry.Source.UpdatePerFrameData();

        return true;
    }

    /// <summary>
    /// Returns welds sorted so that a target celestial is always processed before
    /// any source that depends on it. Uses Kahn's topological sort.
    /// If a cycle is detected, the original order is returned unchanged.
    /// </summary>
    public static List<CelestialWeldEntry> TopologicalSort(List<CelestialWeldEntry> welds)
    {
        var inDegree = new Dictionary<CelestialWeldEntry, int>();
        var adj = new Dictionary<CelestialWeldEntry, List<CelestialWeldEntry>>();

        foreach (var w in welds)
        {
            inDegree[w] = 0;
            adj[w] = new List<CelestialWeldEntry>();
        }

        // Build dependency graph:
        // If weld X's source is weld Y's target, then X depends on Y
        // (Y must be processed first so its target is in the right position)
        foreach (var x in welds)
        {
            foreach (var y in welds)
            {
                if (x != y && (IOrbiter)x.Source == y.Target)
                {
                    adj[x].Add(y);
                    inDegree[y]++;
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<CelestialWeldEntry>();
        foreach (var w in welds)
            if (inDegree[w] == 0)
                queue.Enqueue(w);

        var sorted = new List<CelestialWeldEntry>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);
            foreach (var neighbor in adj[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count == welds.Count)
            return sorted;

        Console.WriteLine("kiwis-marbles: TopologicalSort: cycle detected, leaving order as-is.");
        return new List<CelestialWeldEntry>(welds);
    }
}
```

**Key implementation details**:

1. **Repositioning mechanism**: Unlike vehicles which use `Teleport()`, celestial bodies use:
   ```csharp
   entry.Source.SetOrbit(newOrbit);         // Replaces orbit + auto-updates parent
   entry.Source.UpdatePerFrameData();       // Refreshes cached position/velocity/transforms
   ```
   From `decomp/ksa/KSA/Celestial.cs`:
   ```csharp
   public void SetOrbit(Orbit newOrbiter)
   {
       Orbit = newOrbiter;
       SetParent(newOrbiter.Parent);  // Auto-manages Children lists
   }
   ```

2. **Cross-parent welding**: When source and target have different parents, `SetOrbit()` handles the parent change automatically:
   ```csharp
   // SetParent internals (from Celestial.cs):
   public IParentBody Parent
   {
       set
       {
           if (_parent != value)
           {
               if (_parent != null) _parent.Children.Remove(this);
               value.Children.Add(this);
               _parent = value;
           }
       }
   }
   ```
   Example: Welding Moon (parent: Earth) to Mars (parent: Sun) → Moon's parent becomes Sun.

3. **Child propagation**: Children of the moved celestial body automatically follow because their orbits are defined relative to their parent. Moving Earth automatically moves Moon (since Moon orbits Earth). No explicit child handling needed.

4. **CCI frame offset**: The offset is applied directly in the CCI frame without body-frame rotation:
   ```csharp
   double3 newSrcPosCci = tgtPosCci + entry.Offset;
   ```
   This is simpler than garys-torch's body-frame-relative offset because celestial bodies don't have a meaningful body orientation for positioning purposes.

5. **Topological sort**: Identical algorithm to garys-torch's `WeldEngine.TopologicalSort()`. The cast `(IOrbiter)x.Source` is needed because `Source` is `Celestial` and `Target` is `IOrbiter` — we need to compare them as the same type.

6. **Orbit line color**: Preserved via `entry.Source.OrbitColor`. This should be available from the `IOrbiter` interface or from the existing orbit. Check at implementation time — if `OrbitColor` isn't directly accessible, get it from `entry.Source.Orbit.OrbitLineColor`.

**Verification needed at implementation time**:
- Confirm `Celestial` exposes `OrbitColor` or check if it's via `Orbit.OrbitLineColor`
- Confirm `IOrbiter` cast works for `Celestial` (the decompiled source shows `Celestial : Astronomical, IOrbiter`)
- Test that `UpdatePerFrameData()` is sufficient to refresh all rendering after `SetOrbit()`

---

## Task 4: Update Project Files (Dependencies)

### 4a: Update kiwis-marbles.lib.csproj

**File**: `kiwis-marbles.lib/kiwis-marbles.lib.csproj` (MODIFY)

**Current content** (placeholder):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.KiwisMarblesLib</AssemblyName>
    <RootNamespace>MeowSci.KiwisMarblesLib</RootNamespace>
    <Description>Example lib</Description>
    <PackageId>MeowSci.KiwisMarblesLib</PackageId>
  </PropertyGroup>
  <ItemGroup>
  </ItemGroup>
</Project>
```

**Replace with** (modeled after `garys-torch.lib/garys-torch.lib.csproj`):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.KiwisMarblesLib</AssemblyName>
    <RootNamespace>MeowSci.KiwisMarblesLib</RootNamespace>
    <Description>Core logic for celestial body welding</Description>
    <PackageId>MeowSci.KiwisMarblesLib</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Brutal.Core.Numerics" Condition="Exists('$(KSAFolder)Brutal.Core.Numerics.dll')">
      <HintPath>$(KSAFolder)Brutal.Core.Numerics.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
      <HintPath>$(KSAFolder)KSA.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

**Dependencies needed**:
- `ksa-abstractions.lib` — for `SimTimeProvider`
- `StarMap.API` — for KSA types access
- `Brutal.Core.Numerics` — for `double3`, `doubleQuat`, `byte4`, `float3`
- `KSA` — for `Celestial`, `IOrbiter`, `Orbit`, `Universe`, etc.

### 4b: Update kiwis-marbles.csproj

**File**: `kiwis-marbles/kiwis-marbles.csproj` (MODIFY)

**Add ProjectReferences** (after existing `<ItemGroup>` with PackageReferences):
```xml
  <ItemGroup>
    <ProjectReference Include="..\kiwis-marbles.lib\kiwis-marbles.lib.csproj" />
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>
```

**Reference**: garys-torch.csproj has these ProjectReferences:
```xml
  <ItemGroup>
    <ProjectReference Include="..\garys-torch.lib\garys-torch.lib.csproj" />
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>
```

---

## Task 5: Implement Mod.cs — UI and Per-Frame Update Loop

**File**: `kiwis-marbles/Mod.cs` (MODIFY — replace template content)

**Purpose**: ImGui window for creating/managing celestial welds, plus per-frame update loop that applies all active welds.

### 5a: Class Fields

```csharp
using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.KiwisMarblesLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarbles;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    // Active welds list (topologically sorted)
    private readonly List<CelestialWeldEntry> _welds = new List<CelestialWeldEntry>();

    // Pending weld creation state
    private int _pendingSourceIndex = 0;
    private int _pendingTargetIndex = 0;
    private float3 _pendingOffset = new float3(0f, 0f, 0f);
    private float _pendingOffsetScale = 1000f;  // Multiplier for offset values (UI convenience)
    private string? _weldError = null;

    // Cached lists (refreshed each frame for the UI)
    private List<Celestial> _celestials = new List<Celestial>();
    private List<IOrbiter> _orbiters = new List<IOrbiter>();
```

**Notes on UI offset handling**:
- The user enters offset values as `float3` in the ImGui controls (DragFloat3)
- A separate "scale factor" (`_pendingOffsetScale`) multiplies the entered values to produce the actual `double3` offset
- Scale factor options: 1 (meters), 1000 (km), 1_000_000 (Mm), 1_000_000_000 (Gm)
- This lets users work with small readable numbers while achieving planetary-scale offsets
- Example: entering (384.4, 0, 0) with scale=1000 → offset of (384400, 0, 0) meters ≈ Moon-Earth distance

### 5b: Lifecycle Methods

```csharp
    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _isInitialized = true;
            Console.WriteLine("kiwis-marbles: Initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            // Toggle window with F9 (different key from garys-torch's F11)
            if (ImGui.IsKeyPressed(ImGuiKey.F9))
                _windowVisible = !_windowVisible;

            // Update all active welds — remove any that fail
            var toRemove = new List<CelestialWeldEntry>();
            foreach (var weld in _welds)
                if (!CelestialWeldEngine.UpdateWeld(weld))
                    toRemove.Add(weld);
            foreach (var weld in toRemove)
                RemoveWeld(weld);

            // Render UI
            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error during unload: {ex.Message}");
        }
    }
```

**Key choice**: Use `F9` instead of `F11` to avoid conflict with garys-torch. Check existing mods for key conflicts — if F9 is taken, use another key. Could also be made configurable in a future iteration.

### 5c: Weld Management Methods

```csharp
    private void InitiateWeld(Celestial source, IOrbiter target, double3 offset)
    {
        // Prevent duplicate source welds
        foreach (var weld in _welds)
        {
            if (weld.Source == source)
            {
                _weldError = $"{source.Id} is already welded as a source.";
                return;
            }
        }

        // Prevent welding a body to itself
        if ((IOrbiter)source == target)
        {
            _weldError = "Cannot weld a body to itself.";
            return;
        }

        _weldError = null;

        _welds.Add(new CelestialWeldEntry
        {
            Source = source,
            Target = target,
            Offset = offset,
        });

        SortWelds();
        Console.WriteLine($"kiwis-marbles: Welded {source.Id} to {target.Id}");
    }

    private void RemoveWeld(CelestialWeldEntry entry)
    {
        Console.WriteLine($"kiwis-marbles: Unwelded {entry.Source.Id} from {entry.Target.Id}");
        _welds.Remove(entry);
    }

    private void SortWelds()
    {
        var sorted = CelestialWeldEngine.TopologicalSort(_welds);
        _welds.Clear();
        foreach (var w in sorted)
            _welds.Add(w);
    }
```

**Reference**: Directly mirrors `garys-torch/Mod.cs` `InitiateWeld()` / `RemoveWeld()` / `SortWelds()`.

### 5d: RenderWindow — ImGui UI

```csharp
    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(500, 600), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Kiwi's Marbles###kiwis-marbles", ref _windowVisible))
        {
            // Refresh celestial/orbiter lists each frame
            _celestials = CelestialProvider.GetAllCelestials();
            _orbiters = CelestialProvider.GetAllOrbiters();

            // ============ CREATE WELD SECTION ============
            ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld");
            ImGui.Separator();
            ImGui.Indent();

            if (_celestials.Count == 0)
            {
                ImGui.Text("No celestial bodies available.");
            }
            else
            {
                // Source selector — celestial bodies only
                var sourceIds = new string[_celestials.Count];
                for (int i = 0; i < _celestials.Count; i++)
                    sourceIds[i] = _celestials[i].Id;

                _pendingSourceIndex = Math.Clamp(_pendingSourceIndex, 0, _celestials.Count - 1);

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
                ImGui.Combo("##src", ref _pendingSourceIndex, sourceIds, sourceIds.Length);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Source (celestial)");

                // Target selector — all orbiters (celestials + vehicles)
                var targetIds = new string[_orbiters.Count];
                for (int i = 0; i < _orbiters.Count; i++)
                    targetIds[i] = _orbiters[i].Id;

                _pendingTargetIndex = Math.Clamp(_pendingTargetIndex, 0, _orbiters.Count - 1);

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
                ImGui.Combo("##tgt", ref _pendingTargetIndex, targetIds, targetIds.Length);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Target (any orbiter)");

                // Offset controls
                if (ImGui.CollapsingHeader("Offset##offsetsection", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    // Offset scale selector (meters, km, Mm, Gm)
                    ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Offset Unit Scale");
                    string[] scaleLabels = { "m (×1)", "km (×1,000)", "Mm (×1,000,000)", "Gm (×1,000,000,000)" };
                    float[] scaleValues = { 1f, 1000f, 1_000_000f, 1_000_000_000f };
                    int scaleIndex = Array.IndexOf(scaleValues, _pendingOffsetScale);
                    if (scaleIndex < 0) scaleIndex = 1; // default km
                    if (ImGui.Combo("##offsetscale", ref scaleIndex, scaleLabels, scaleLabels.Length))
                        _pendingOffsetScale = scaleValues[scaleIndex];

                    ImGui.Separator();

                    // Offset XYZ (entered in chosen unit, multiplied by scale)
                    ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Offset (x / y / z)");
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.DragFloat3("##pendingoffset", ref _pendingOffset, 0.1f, 0f, 0f);

                    // Show computed offset in meters
                    double3 computedOffset = new double3(
                        _pendingOffset.X * _pendingOffsetScale,
                        _pendingOffset.Y * _pendingOffsetScale,
                        _pendingOffset.Z * _pendingOffsetScale
                    );
                    ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1f),
                        $"= ({computedOffset.X:G6}, {computedOffset.Y:G6}, {computedOffset.Z:G6}) m");

                    ImGui.Unindent();
                }

                ImGui.Separator();

                // Validation
                bool sourceIsTarget = _celestials.Count > 0 && _orbiters.Count > 0
                    && (IOrbiter)_celestials[_pendingSourceIndex] == _orbiters[_pendingTargetIndex];

                if (sourceIsTarget)
                {
                    ImGui.TextColored(new float4(1, 0.4f, 0.4f, 1), "Source and target must differ.");
                }
                else
                {
                    if (_weldError != null)
                        ImGui.TextColored(new float4(1, 0.4f, 0.4f, 1), _weldError);

                    if (ImGui.Button("Create Weld##addweld"))
                    {
                        double3 offset = new double3(
                            _pendingOffset.X * _pendingOffsetScale,
                            _pendingOffset.Y * _pendingOffsetScale,
                            _pendingOffset.Z * _pendingOffsetScale
                        );
                        InitiateWeld(
                            _celestials[_pendingSourceIndex],
                            _orbiters[_pendingTargetIndex],
                            offset
                        );
                    }
                }
            }

            ImGui.Unindent();

            // ============ ACTIVE WELDS SECTION ============
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Active Welds");
            ImGui.Separator();

            CelestialWeldEntry? toRemove = null;
            for (int i = 0; i < _welds.Count; i++)
            {
                ImGui.Spacing();
                var weld = _welds[i];
                string header = $"Weld {i + 1}: {weld.Source.Id} -> {weld.Target.Id}";

                if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();
                    ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");

                    // Show current parent info
                    ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1f),
                        $"Source parent: {weld.Source.Parent?.Id ?? "none"}");
                    ImGui.TextColored(new float4(0.6f, 0.6f, 0.6f, 1f),
                        $"Target parent: {weld.Target.Parent?.Id ?? "none"}");

                    ImGui.Separator();

                    // Live-editable offset (use a local float3 for the drag control)
                    ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Offset (x / y / z, raw meters)");
                    // For live editing, use float3 proxy since ImGui works with floats
                    float3 offsetProxy = new float3(
                        (float)weld.Offset.X,
                        (float)weld.Offset.Y,
                        (float)weld.Offset.Z
                    );
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.DragFloat3($"##offset{i}", ref offsetProxy, 1000f, 0f, 0f))
                    {
                        weld.Offset = new double3(offsetProxy.X, offsetProxy.Y, offsetProxy.Z);
                    }

                    ImGui.Separator();

                    // Unweld button
                    if (ImGui.Button($"Unweld##{i}"))
                        toRemove = weld;

                    ImGui.Unindent();
                }
            }

            if (toRemove != null)
                RemoveWeld(toRemove);
        }
        ImGui.End();
    }
```

**UI Features**:
1. **Source selector**: Combo box showing only `Celestial` objects (planets/moons, not stars)
2. **Target selector**: Combo box showing all `IOrbiter` objects (celestials + vehicles)
3. **Offset controls**: DragFloat3 with a unit scale selector (m/km/Mm/Gm) for intuitive input
4. **Computed offset display**: Shows the actual offset in meters for verification
5. **Validation**: Prevents welding a body to itself
6. **Active welds**: Collapsing sections with live-editable offset, parent info display, and unweld button

**ImGui conventions** follow garys-torch exactly:
- `ImGui.Begin("Title###id", ref _windowVisible)` — unique ID with `###`
- `ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver)` — default size
- `ImGui.TextColored()` with `KSAColor.Xkcd.*` for themed text
- `ImGui.DragFloat3("##uniqueId", ...)` with `##` for hidden labels
- `ImGui.Combo("##id", ref index, labels, count)` for dropdowns
- `ImGui.CollapsingHeader()` for sections
- `ImGui.Indent()` / `ImGui.Unindent()` for nesting

**Note on float/double mismatch**: ImGui controls work with `float` types. For the active weld editor, we use a `float3` proxy and convert to/from `double3`. This loses precision for very large values but is acceptable for interactive editing. The initial offset is set with full double precision via the creation controls.

---

## Task 6: Remove Placeholder Code from kiwis-marbles.lib

**File**: `kiwis-marbles.lib/KiwisMarblesLib.cs` (DELETE or REPLACE)

The current file contains placeholder code:
```csharp
public sealed class KiwisMarblesLib
{
    public static void Thing()
    {
        Console.WriteLine("Hello from the example library!");
    }
}
```

**Action**: Delete this file entirely. The real lib code is in `CelestialWeldEntry.cs` and `CelestialWeldEngine.cs`.

---

## Task 7: Update Documentation

### 7a: Update kiwis-marbles/README.md

**File**: `kiwis-marbles/README.md` (REPLACE entire content)

Replace the template README with actual mod documentation covering:
- Mod overview: what it does (weld celestial bodies to follow other orbiters)
- Features list
- Architecture (Mod.cs UI + kiwis-marbles.lib engine)
- Usage instructions (F9 toggle, create weld workflow, offset unit scale)
- Data structures documentation
- Key game APIs used

Follow the style of `garys-torch/README.md` for consistency.

### 7b: Update REPOSITORY_INDEX.md

**File**: `REPOSITORY_INDEX.md` (MODIFY)

Add an entry for kiwis-marbles in the appropriate section:
```markdown
### kiwis-marbles
Celestial body welding mod. Allows repositioning planets and moons by "welding" them to follow other celestial bodies or vehicles at user-defined offsets. Uses per-frame orbit replacement via `Celestial.SetOrbit()` with DAG-ordered processing for weld chains.
- **kiwis-marbles.lib**: Core welding engine (`CelestialWeldEntry`, `CelestialWeldEngine`) with topological sort for dependency ordering.
```

---

## Task 8: Build Verification

After all implementation is complete:

```bash
dotnet build
```

**Must compile cleanly.** Common issues to watch for:
- Missing `using` statements for KSA types
- `IOrbiter` interface members (`.Id` might need to be accessed via cast to `IObjectId`)
- `Celestial` might not directly expose `.Id` — check if it's via `Astronomical.Id` base class
- `OrbitColor` property access — might be `Orbit.OrbitLineColor` instead
- The `(IOrbiter)source` cast in validation — verify `Celestial` implements `IOrbiter`

---

## Implementation Order

Execute tasks in this order for clean incremental compilation:

1. **Task 1** — CelestialProvider (ksa-abstractions.lib) — no new dependencies
2. **Task 4a** — Update kiwis-marbles.lib.csproj — add KSA references
3. **Task 6** — Delete placeholder KiwisMarblesLib.cs
4. **Task 2** — CelestialWeldEntry.cs — simple data class
5. **Task 3** — CelestialWeldEngine.cs — depends on Task 2 + Task 1
6. **Task 4b** — Update kiwis-marbles.csproj — add ProjectReferences
7. **Task 5** — Mod.cs full implementation — depends on all above
8. **Task 8** — `dotnet build` verification
9. **Task 7** — Documentation updates

---

## Open Questions / Future Enhancements

These are NOT part of this plan but noted for future consideration:

1. **Persistence**: Welds are lost on mod reload. Could save/load weld configurations to a JSON file.
2. **Orbital element editor**: Instead of CCI offset, let users set orbital elements (SMA, eccentricity, inclination) directly.
3. **Spherical offset mode**: Distance + azimuth + elevation instead of XYZ — more intuitive for orbital positioning.
4. **Weld presets**: Pre-defined configurations for common scenarios (e.g., "binary planet", "ringworld formation").
5. **Visual indicators**: Draw lines or markers between welded bodies in the 3D view.
6. **Safety checks**: Warn when welding might produce unreasonable physics (e.g., body inside another body's surface).
7. **Undo/restore**: Store original orbit before welding so it can be restored on unweld.