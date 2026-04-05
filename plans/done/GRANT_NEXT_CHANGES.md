# MAJOR

using "mod-a"/"mod-a.lib" as an example mod name for illustrative purposes which represents all mods and their libs

- the IGrantSubmod interface should've been put into ksa-abstractions.lib as a generic interface pattern (not tied to "grant" at all), and the submods should've been defined in each mod-a.lib csproj and reused from both mod-a's ImGui code and grant supermod ImGui code so that we're not largely duplicating the ImGui code per mod ui behavior
- the the harmony patch behavior should be defined in mod-a.lib and reused and called from mod-a and grant supermod

the goal here is that each mod-a and mod-a.lib pair largely contain all the logic for that mod, and mod-a + mod-a.lib can still be used standalone where mod-a provides a ImGui window that includes the mod-a.lib ui code

but grant supermod contains *all* of the submod functionality together in a single ImGui window collected under collapsible headers that can be toggled on/off for visibility

# plan

## overview

There are 10 grant submods (files in `grant/Submods/`) that each implement `IGrantSubmod` and contain ImGui UI code. This same ImGui code is **largely duplicated** in each standalone mod's `Mod.cs`. The goal is to define that ImGui UI code **once** in each `mod.lib` project, then reuse it from both the standalone mod and the grant supermod.

Similarly, 4 mods have Harmony patches that are defined in both the standalone mod's `Patcher.cs` AND duplicated in `grant/Patcher.cs`. Those should be defined once in each `mod.lib` and called from both places.

### current duplication map

| Mod | grant/Submods/*Submod.cs (lines) | standalone Mod.cs ImGui (lines) | Harmony in standalone Patcher.cs | Harmony in grant/Patcher.cs |
|-----|----------------------------------|--------------------------------|----------------------------------|---------------------------|
| average-twr | AverageTwrSubmod.cs (~90) | average-twr/Mod.cs (~90) | No real patches | No |
| blinky | BlinkySubmod.cs (~480) | blinky/Mod.cs (~480) | render-skip (3 patches) | render-skip (3 patches) |
| eternal-flame | EternalFlameSubmod.cs (~120) | eternal-flame/Mod.cs (~120) | No real patches | No |
| garrys-torch | GarrysTorchSubmod.cs (~230) | garrys-torch/Mod.cs (~230) | No real patches | No |
| glass | GlassSubmod.cs (~90) | glass/Mod.cs (~90) | FOV (2 patches) | FOV (2 patches) |
| i-feel-seen | IFeelSeenSubmod.cs (~70) | i-feel-seen/Mod.cs (~70) | render distance (2 patches) | render distance (2 patches) |
| kiwis-marbles | KiwisMarblesSubmod.cs (~350) | kiwis-marbles/Mod.cs (~350) | No real patches | No |
| skittles | SkittlesSubmod.cs (~320) | skittles/Mod.cs (~320) | hotkey blocking (1 patch) | hotkey blocking (1 patch) |
| unladen-swallow | UnladenSwallowSubmod.cs (~60) | unladen-swallow/Mod.cs (~60) | No real patches | No |
| zippo | ZippoSubmod.cs (~280) | zippo/Mod.cs (~280) | No real patches | No |

### target architecture

```
ksa-abstractions.lib/
  ISubmod.cs              <-- generic submod interface (renamed from IGrantSubmod, NOT grant-specific)

each-mod.lib/
  [existing lib code]     <-- business logic (already exists)
  EachModSubmod.cs        <-- NEW: implements ISubmod, contains ALL ImGui + Update logic
  EachModPatches.cs       <-- NEW (only for blinky, glass, i-feel-seen, skittles): static patch registration

each-mod/
  Mod.cs                  <-- SIMPLIFIED: creates submod instance, wraps in ImGui.Begin/End window
  Patcher.cs              <-- SIMPLIFIED: calls into mod.lib patch registration

grant/
  Mod.cs                  <-- UNCHANGED conceptually: creates all submod instances, orchestrates
  Patcher.cs              <-- SIMPLIFIED: calls into each mod.lib patch registration
  Submods/                <-- DELETED entirely (all 10 files)
  IGrantSubmod.cs         <-- DELETED (replaced by ksa-abstractions.lib/ISubmod.cs)
```

---

## phase 1: create `ISubmod` interface in `ksa-abstractions.lib`

### task 1.1: add `ISubmod.cs` to `ksa-abstractions.lib`

**file:** `ksa-abstractions.lib/ISubmod.cs`
**namespace:** `MeowSci.KsaAbstractions`
**visibility:** `public` (so all projects can reference it)

```csharp
namespace MeowSci.KsaAbstractions;

/// <summary>
/// Generic submod interface for a UI panel that can be embedded in any host window.
/// Implementations render ImGui content (without Begin/End) and manage their own state.
/// </summary>
public interface ISubmod
{
    /// <summary>Display name shown in headers and menus.</summary>
    string Name { get; }

    /// <summary>Called once during initialization to set up state and lib instances.</summary>
    void Initialize();

    /// <summary>Called every frame for pre-UI computation (sampling, ticking, etc.).</summary>
    void Update(double dt);

    /// <summary>
    /// Renders ImGui content. Caller is responsible for Begin/End window framing.
    /// Do NOT call ImGui.Begin/End for the main content area. Additional popup/child windows are fine.
    /// </summary>
    void RenderContent();

    /// <summary>Called during unload to clean up resources.</summary>
    void Dispose();
}
```

**why public:** every `.lib` project implements it; every mod project and grant references it.

**note:** this interface is intentionally identical to the current `IGrantSubmod` but:
- lives in `ksa-abstractions.lib` (available everywhere)
- is `public` not `internal`
- is named generically (`ISubmod` not `IGrantSubmod`)
- namespace is `MeowSci.KsaAbstractions`

**csproj change:** none needed — `ksa-abstractions.lib.csproj` already has ImGui references.

---

## phase 2: create submod classes in each `.lib` project

For each of the 10 mods, move the submod implementation from `grant/Submods/*Submod.cs` into the corresponding `.lib` project. The class becomes `public` so both the standalone mod and grant can instantiate it.

### general pattern for each mod.lib submod

Each new file follows this pattern (using average-twr as an example):

**file:** `average-twr.lib/AverageTwrSubmod.cs`
**namespace:** `MeowSci.AverageTwrLib` (matches existing lib namespace)
**class:** `public sealed class AverageTwrSubmod : ISubmod`

The implementation is a **direct copy** of the code from `grant/Submods/AverageTwrSubmod.cs` with these changes:
1. namespace changes from `MeowSci.Grant.Submods` → `MeowSci.AverageTwrLib`
2. `using MeowSci.Grant;` removed; `using MeowSci.KsaAbstractions;` added (for `ISubmod`)
3. `internal sealed class` → `public sealed class`
4. implements `ISubmod` instead of `IGrantSubmod`

### task 2.1: `average-twr.lib/AverageTwrSubmod.cs`

- **source:** copy content from `grant/Submods/AverageTwrSubmod.cs`
- **namespace:** `MeowSci.AverageTwrLib`
- **class:** `public sealed class AverageTwrSubmod : ISubmod`
- **csproj change to `average-twr.lib.csproj`:** already has `ksa-abstractions.lib` dependency — no change needed
- **special notes:** uses `TwrSampleAccumulator`, `TwrDataReader`, `TwrStatistics`, `VehicleProvider` — all already in this lib or ksa-abstractions

### task 2.2: `blinky.lib/BlinkySubmod.cs`

- **source:** copy content from `grant/Submods/BlinkySubmod.cs` (~480 lines)
- **namespace:** `MeowSci.BlinkyLib`
- **class:** `public sealed class BlinkySubmod : ISubmod`
- **csproj change to `blinky.lib.csproj`:** already has `ksa-abstractions.lib` dependency — no change needed
- **special notes:** 
  - this is the largest submod (~480 lines) with complex grid builder UI
  - references `BlinkyGridManager`, `LcdGridBuilder`, `LcdGridConfig`, `PixelGrid`, `ScrollAnimation`, `BuiltInScrollPixels` — all in `blinky.lib`
  - references `VehicleProvider`, `PartHelpers` from `ksa-abstractions.lib`
  - also contains a reference to `grant/Patcher.RenderPixelParts` static bool. This needs to move to `blinky.lib` as a static field e.g. `BlinkyPatchState.RenderPixelParts` in a new file `blinky.lib/BlinkyPatchState.cs`

### task 2.3: `eternal-flame.lib/EternalFlameSubmod.cs`

- **source:** copy content from `grant/Submods/EternalFlameSubmod.cs`
- **namespace:** `MeowSci.EternalFlameLib`
- **class:** `public sealed class EternalFlameSubmod : ISubmod`
- **csproj change to `eternal-flame.lib.csproj`:** add `<ProjectReference>` to `ksa-abstractions.lib` (currently missing). Also needs ImGui + KSA dll references added matching other lib csprojs
- **special notes:** references `FuelManager` from this lib, `Universe` from KSA

### task 2.4: `garrys-torch.lib/GarrysTorchSubmod.cs`

- **source:** copy content from `grant/Submods/GarrysTorchSubmod.cs` (~230 lines)
- **namespace:** `MeowSci.GarrysTorchLib`
- **class:** `public sealed class GarrysTorchSubmod : ISubmod`
- **csproj change to `garrys-torch.lib.csproj`:** already has `ksa-abstractions.lib` — no change needed
- **special notes:** references `WeldEngine`, `WeldEntry`, `WeldPreset`, `VehicleProvider` — all already available

### task 2.5: `glass.lib/GlassSubmod.cs`

- **source:** copy content from `grant/Submods/GlassSubmod.cs`
- **namespace:** `MeowSci.GlassLib`
- **class:** `public sealed class GlassSubmod : ISubmod`
- **csproj change to `glass.lib.csproj`:** add `<ProjectReference>` to `ksa-abstractions.lib` (currently missing). Also needs ImGui + Numerics dll references added
- **special notes:** references `FovController` (already in glass.lib), `Program.GetCamera()`, `Camera` from KSA

### task 2.6: `i-feel-seen.lib/IFeelSeenSubmod.cs`

- **source:** copy content from `grant/Submods/IFeelSeenSubmod.cs`
- **namespace:** `MeowSci.IFeelSeenLib`
- **class:** `public sealed class IFeelSeenSubmod : ISubmod`
- **public property:** `public VehicleTracker Tracker => _tracker;` (needed by Patcher wiring in both standalone & grant)
- **csproj change to `i-feel-seen.lib.csproj`:** already has `ksa-abstractions.lib` — no change needed
- **special notes:** `VehicleTracker`, `TrackedVehicle` already in this lib

### task 2.7: `kiwis-marbles.lib/KiwisMarblesSubmod.cs`

- **source:** copy content from `grant/Submods/KiwisMarblesSubmod.cs` (~350 lines)
- **namespace:** `MeowSci.KiwisMarblesLib`
- **class:** `public sealed class KiwisMarblesSubmod : ISubmod`
- **csproj change to `kiwis-marbles.lib.csproj`:** already has `ksa-abstractions.lib` — needs ImGui dll references added (for ImGui calls in submod)
- **special notes:** references `CelestialWeldEngine`, `CelestialWeldEntry`, `CelestialProvider` — all available

### task 2.8: `skittles.lib/SkittlesSubmod.cs`

- **source:** copy content from `grant/Submods/SkittlesSubmod.cs` (~320 lines)
- **namespace:** `MeowSci.SkittlesLib`
- **class:** `public sealed class SkittlesSubmod : ISubmod`
- **public property:** `public bool HasFocusedTextInput` (needed by Patcher wiring in grant)
- **csproj change to `skittles.lib.csproj`:** add `<ProjectReference>` to `ksa-abstractions.lib`. already has ImGui references.
- **special notes:** 
  - has a separate theme editor window (calls ImGui.Begin/End for the editor popup — this is allowed per the interface contract)
  - references `ThemeManager`, `ThemeDefinition`, `ThemeSerializer`, `ModConfig`, `BuiltInThemes` — all in skittles.lib

### task 2.9: `unladen-swallow.lib/UnladenSwallowSubmod.cs`

- **source:** copy content from `grant/Submods/UnladenSwallowSubmod.cs`
- **namespace:** `MeowSci.UnladenSwallowLib`
- **class:** `public sealed class UnladenSwallowSubmod : ISubmod`
- **csproj change to `unladen-swallow.lib.csproj`:** add `<ProjectReference>` to `ksa-abstractions.lib` (already has it). Needs ImGui dll references if not already present (it already has KSA ref, needs ImGui)
- **special notes:** references `SwallowServer`, `GameThread` — all available

### task 2.10: `zippo.lib/ZippoSubmod.cs`

- **source:** copy content from `grant/Submods/ZippoSubmod.cs` (~280 lines)
- **namespace:** `MeowSci.ZippoLib`
- **class:** `public sealed class ZippoSubmod : ISubmod`
- **csproj change to `zippo.lib.csproj`:** already has `ksa-abstractions.lib` and ImGui — no change needed
- **special notes:** references `LightController`, `VehicleProvider` — all available

---

## phase 3: move Harmony patch logic to `.lib` projects (4 mods only)

Only 4 mods have real Harmony patches that are duplicated between the standalone mod and grant:
- **blinky** — 3 render-skip patches
- **glass** — 2 FOV override patches
- **i-feel-seen** — 2 vehicle render distance patches
- **skittles** — 1 hotkey blocking patch

### design for lib-based patch helpers

The `.lib` projects **cannot** use `[HarmonyPatch]` attributes and auto-patching because the Harmony attribute patching is tied to the assembly that defines the patches. Instead, create **static helper methods** in each lib that do **manual patching** via the Harmony API.

Each lib gets a `*Patches.cs` file with a pattern like:

```csharp
namespace MeowSci.SomeLib;

public static class SomePatches
{
    public static void Apply(HarmonyLib.Harmony harmony) { /* manual patch registration */ }
    public static void Remove(HarmonyLib.Harmony harmony) { /* unpatch */ }
}
```

The standalone mod's `Patcher.cs` and grant's `Patcher.cs` both call `SomePatches.Apply(harmony)`.

### task 3.1: `blinky.lib/BlinkyPatchState.cs` + `blinky.lib/BlinkyPatches.cs`

**BlinkyPatchState.cs:**
```csharp
namespace MeowSci.BlinkyLib;

/// <summary>Shared state for blinky Harmony patches (render-skip toggle).</summary>
public static class BlinkyPatchState
{
    /// <summary>When false, pixel-engine meshes are hidden for better performance.</summary>
    public static bool RenderPixelParts = false;
}
```

**BlinkyPatches.cs:**
Static class with `Apply(Harmony harmony)` and `Remove(Harmony harmony)` methods that manually register the 3 render-skip prefix patches for `PartModelModule.UpdateRenderData`, `PartModelDynamicModule.UpdateRenderData`, `PartModelGlassModule.UpdateRenderData`.

Each prefix checks `BlinkyPatchState.RenderPixelParts` and skips if the part Id starts with `"pixel_"`.

**csproj:** `blinky.lib.csproj` already has `Lib.Harmony` — no change needed.

### task 3.2: `glass.lib/GlassPatches.cs`

Static class with `Apply(Harmony harmony)` and `Remove(Harmony harmony)` that manually register:
- prefix on `Camera.ChangeFieldOfView` — blocks when `FovController.IsOverrideActive`
- prefix on `Camera.UpdateProjection` — sets `_fovRadians` field from `FovController.OverrideFovDegrees`

**csproj change to `glass.lib.csproj`:** add `Lib.Harmony` package reference.

### task 3.3: `i-feel-seen.lib/IFeelSeenPatches.cs`

Static class with `Apply(Harmony harmony, VehicleTracker tracker)` and `Remove(Harmony harmony)` that manually register:
- prefix on `Vehicle.GetWorldMatrix` — overrides world matrix for tracked vehicles
- prefix on `Vehicle.UpdateRenderData` — forces render data update for tracked vehicles

The `tracker` parameter is stored in a static field and checked by the prefix methods.

**csproj change to `i-feel-seen.lib.csproj`:** add `Lib.Harmony` package reference.

### task 3.4: `skittles.lib/SkittlesPatches.cs`

Static class with `Apply(Harmony harmony, Func<bool> hasFocusedTextInput)` and `Remove(Harmony harmony)` that manually register:
- prefix on `GameSettings.OnKeyAll` — blocks hotkeys when skittles text input is focused

**csproj change to `skittles.lib.csproj`:** add `Lib.Harmony` package reference. Also need `StarMap.API` and KSA dll reference for `GameSettings`.

---

## phase 4: simplify standalone mod projects

Each standalone mod's `Mod.cs` is simplified to:
1. Instantiate the submod class from its `.lib`
2. Call `Initialize()` / `Update()` / `RenderContent()` / `Dispose()` at the right lifecycle points
3. Wrap `RenderContent()` in `ImGui.Begin/End` for the standalone window

Each standalone mod's `Patcher.cs` is simplified to call the lib's patch helpers (for the 4 mods that have patches) or kept as-is if it has no real patches.

### general pattern for simplified standalone Mod.cs

```csharp
using MeowSci.SomeLib;
// ... other usings ...

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    private bool _isInitialized, _isDisposed, _windowVisible;
    private SomeSubmod _submod = null!;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        _submod = new SomeSubmod();
        Patcher.Patch(/* pass submod/tracker if needed */);
        _submod.Initialize();
        _isInitialized = true;
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        _submod.Update(dt);
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        if (ImGui.IsKeyPressed(ImGuiKey.F11)) _windowVisible = !_windowVisible;
        if (_windowVisible) RenderWindow();
    }

    [StarMapUnload]
    public void Unload()
    {
        _submod.Dispose();
        Patcher.Unload();
        _isDisposed = true;
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(W, H), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Window Title", ref _windowVisible))
            _submod.RenderContent();
        ImGui.End();
    }
}
```

### task 4.1: simplify `average-twr/Mod.cs`

- replace all ImGui rendering + TWR logic with `AverageTwrSubmod` delegation
- delete `_accumulator`, `_timeSinceLastSample`, `_isCollecting`, `SampleInterval`, `RenderWindow()` body
- keep window size `(420, 260)`, title `"Average TWR / Accel"`, hotkey `F11`

### task 4.2: simplify `blinky/Mod.cs`

- replace all ImGui rendering + grid builder UI with `BlinkySubmod` delegation
- `Patcher.cs` simplified to call `BlinkyPatches.Apply(harmony)` / `Remove(harmony)`
- update references from `Patcher.RenderPixelParts` → `BlinkyPatchState.RenderPixelParts`

### task 4.3: simplify `eternal-flame/Mod.cs`

- replace all ImGui rendering + fuel manager UI with `EternalFlameSubmod` delegation
- add `<ProjectReference>` to `ksa-abstractions.lib` in csproj (for ISubmod)

### task 4.4: simplify `garrys-torch/Mod.cs`

- replace all ImGui rendering + welding UI with `GarrysTorchSubmod` delegation

### task 4.5: simplify `glass/Mod.cs`

- replace all ImGui rendering + FOV UI with `GlassSubmod` delegation
- `Patcher.cs` simplified to call `GlassPatches.Apply(harmony)` / `Remove(harmony)`

### task 4.6: simplify `i-feel-seen/Mod.cs`

- replace all ImGui rendering + tracker UI with `IFeelSeenSubmod` delegation
- `Patcher.cs` simplified to call `IFeelSeenPatches.Apply(harmony, submod.Tracker)` / `Remove(harmony)`

### task 4.7: simplify `kiwis-marbles/Mod.cs`

- replace all ImGui rendering + celestial welding UI with `KiwisMarblesSubmod` delegation

### task 4.8: simplify `skittles/Mod.cs`

- replace all ImGui rendering + theme manager UI with `SkittlesSubmod` delegation
- `Patcher.cs` simplified to call `SkittlesPatches.Apply(harmony, () => submod.HasFocusedTextInput)` / `Remove(harmony)`

### task 4.9: simplify `unladen-swallow/Mod.cs`

- replace all ImGui rendering + server UI with `UnladenSwallowSubmod` delegation

### task 4.10: simplify `zippo/Mod.cs`

- replace all ImGui rendering + light control UI with `ZippoSubmod` delegation

---

## phase 5: update grant supermod

### task 5.1: update `grant/Mod.cs`

- change `using MeowSci.Grant.Submods;` → usings for each lib namespace
- change `IGrantSubmod` → `ISubmod` (from `MeowSci.KsaAbstractions`)
- change `new AverageTwrSubmod()` → `new MeowSci.AverageTwrLib.AverageTwrSubmod()` (or use namespace aliases)
- add `using MeowSci.AverageTwrLib;` etc. for each lib (using fully qualified names or aliases to avoid ambiguity between types with same name from different libs)
- rest of orchestration logic stays identical (visibility toggles, collapse/expand, etc.)
- wire up patcher dependencies using submod instances same as before:
  - `Patcher.IFeelSeenTracker = iFeelSeen.Tracker;`
  - `Patcher.SkittlesHasFocusedTextInput = () => skittles.HasFocusedTextInput;`

### task 5.2: update `grant/Patcher.cs`

- remove all `[HarmonyPatch]` inner classes (Blinky, Glass, IFeelSeen, Skittles patch classes)
- keep the `Patch()` / `Unload()` methods but simplify them to call each lib's patches:

```csharp
public static void Patch()
{
    _harmony = new Harmony("MeowSci.Grant");
    BlinkyPatches.Apply(_harmony);
    GlassPatches.Apply(_harmony);
    IFeelSeenPatches.Apply(_harmony, IFeelSeenTracker!);
    SkittlesPatches.Apply(_harmony, SkittlesHasFocusedTextInput!);
}
public static void Unload()
{
    if (_harmony != null)
    {
        BlinkyPatches.Remove(_harmony);
        GlassPatches.Remove(_harmony);
        IFeelSeenPatches.Remove(_harmony);
        SkittlesPatches.Remove(_harmony);
    }
    _harmony = null;
}
```

- keep `IFeelSeenTracker` and `SkittlesHasFocusedTextInput` static properties for wiring
- remove `RenderPixelParts` static (moved to `BlinkyPatchState`)
- remove `_fovRadiansField` (moved to `GlassPatches`)

### task 5.3: delete `grant/IGrantSubmod.cs`

This file is fully replaced by `ksa-abstractions.lib/ISubmod.cs`.

### task 5.4: delete `grant/Submods/` directory (all 10 files)

All submod implementations now live in their respective `.lib` projects:
- delete `grant/Submods/AverageTwrSubmod.cs`
- delete `grant/Submods/BlinkySubmod.cs`
- delete `grant/Submods/EternalFlameSubmod.cs`
- delete `grant/Submods/GarrysTorchSubmod.cs`
- delete `grant/Submods/GlassSubmod.cs`
- delete `grant/Submods/IFeelSeenSubmod.cs`
- delete `grant/Submods/KiwisMarblesSubmod.cs`
- delete `grant/Submods/SkittlesSubmod.cs`
- delete `grant/Submods/UnladenSwallowSubmod.cs`
- delete `grant/Submods/ZippoSubmod.cs`

### task 5.5: update `grant/grant.csproj`

No new project references needed — grant already references all `.lib` projects and `ksa-abstractions.lib`.

---

## phase 6: csproj dependency additions (summary)

Some `.lib` projects need new dependencies to support the submod classes (ImGui, Harmony, ksa-abstractions). Here's the complete list:

| lib project | needs ksa-abstractions.lib ref | needs Lib.Harmony pkg | needs ImGui dlls | needs KSA dll | other |
|-------------|-------------------------------|----------------------|-----------------|--------------|-------|
| average-twr.lib | already has | no | already has | already has | — |
| blinky.lib | already has | already has | already has | already has | add `BlinkyPatchState.cs` |
| eternal-flame.lib | **ADD** | no | **ADD** (Brutal.ImGui, Brutal.ImGui.Abstractions, Brutal.Core.Numerics) | already has | — |
| garrys-torch.lib | already has | no | already has | already has | — |
| glass.lib | **ADD** | **ADD** | **ADD** (Brutal.ImGui, Brutal.ImGui.Abstractions, Brutal.Core.Numerics, Brutal.Core.Common) | already has | add `StarMap.API` pkg |
| i-feel-seen.lib | already has | **ADD** | already has | already has (via ksa-abstractions transitive) | — |
| kiwis-marbles.lib | already has | no | **ADD** (Brutal.ImGui, Brutal.ImGui.Abstractions, Brutal.Core.Numerics) | already has | — |
| skittles.lib | **ADD** | **ADD** | already has | already has | add `StarMap.API` pkg (for GameSettings) |
| unladen-swallow.lib | already has | no | **ADD** (Brutal.ImGui, Brutal.ImGui.Abstractions, Brutal.Core.Numerics) | already has | — |
| zippo.lib | already has | no | already has | already has | — |

---

## phase 7: build verification and cleanup

### task 7.1: `dotnet build` the entire solution

Run `dotnet build` from the repo root and fix any compilation errors. Common expected issues:
- namespace mismatches
- missing usings
- visibility changes (internal → public)
- static field references that moved (e.g., `Patcher.RenderPixelParts` → `BlinkyPatchState.RenderPixelParts`)

### task 7.2: update `REPOSITORY_INDEX.md`

Update the grant section to reflect the new architecture — submods now live in `.lib` projects, grant references them directly.

### task 7.3: update `grant/README.md`

Update architecture section to describe the new pattern.

### task 7.4: update each mod's `README.md` if needed

Note the new submod class in each `.lib` project description.

---

## implementation ordering and dependencies

```
phase 1 (ISubmod interface) — no dependencies, do first
  ↓
phase 2 (submod classes in .lib) — depends on phase 1
  ↓
phase 3 (Harmony helpers in .lib) — depends on phase 2 (some submods reference patch state)
  ↓
phase 4 (simplify standalone mods) — depends on phases 2 + 3
  ↓
phase 5 (update grant) — depends on phases 2 + 3
  ↓
phase 6 (csproj fixups) — can be done incrementally during phases 2-5
  ↓
phase 7 (build + docs) — final verification
```

phases 4 and 5 are **independent** of each other and can be done in parallel.

---

## key design decisions

1. **`ISubmod` not `IGrantSubmod`** — the interface is generic and reusable, not tied to grant
2. **submod classes are `public sealed`** — both standalone mod and grant need to instantiate them
3. **Harmony patches use manual registration** — `.lib` projects provide `Apply(Harmony)` / `Remove(Harmony)` static methods because attribute-based auto-patching is assembly-scoped
4. **Harmony state is static in lib** — e.g., `BlinkyPatchState.RenderPixelParts`, `IFeelSeenPatches._tracker` — so both standalone and grant can reference the same state
5. **standalone mods remain fully functional** — each mod still creates its own `ImGui.Begin/End` window, but delegates all content rendering to its `.lib` submod
6. **grant remains the orchestrator** — creates all submod instances, manages visibility, renders collapsible headers, consolidates patches
7. **no behavior changes** — the refactor is purely structural; all UI and logic behavior must be preserved exactly

---

## files created (new)

| file | project |
|------|---------|
| `ksa-abstractions.lib/ISubmod.cs` | ksa-abstractions.lib |
| `average-twr.lib/AverageTwrSubmod.cs` | average-twr.lib |
| `blinky.lib/BlinkySubmod.cs` | blinky.lib |
| `blinky.lib/BlinkyPatchState.cs` | blinky.lib |
| `blinky.lib/BlinkyPatches.cs` | blinky.lib |
| `eternal-flame.lib/EternalFlameSubmod.cs` | eternal-flame.lib |
| `garrys-torch.lib/GarrysTorchSubmod.cs` | garrys-torch.lib |
| `glass.lib/GlassSubmod.cs` | glass.lib |
| `glass.lib/GlassPatches.cs` | glass.lib |
| `i-feel-seen.lib/IFeelSeenSubmod.cs` | i-feel-seen.lib |
| `i-feel-seen.lib/IFeelSeenPatches.cs` | i-feel-seen.lib |
| `kiwis-marbles.lib/KiwisMarblesSubmod.cs` | kiwis-marbles.lib |
| `skittles.lib/SkittlesSubmod.cs` | skittles.lib |
| `skittles.lib/SkittlesPatches.cs` | skittles.lib |
| `unladen-swallow.lib/UnladenSwallowSubmod.cs` | unladen-swallow.lib |
| `zippo.lib/ZippoSubmod.cs` | zippo.lib |

## files modified

| file | change |
|------|--------|
| `average-twr/Mod.cs` | simplify to delegate to submod |
| `blinky/Mod.cs` | simplify to delegate to submod |
| `blinky/Patcher.cs` | simplify to call `BlinkyPatches` |
| `eternal-flame/Mod.cs` | simplify to delegate to submod |
| `eternal-flame/eternal-flame.csproj` | add ksa-abstractions.lib ref |
| `garrys-torch/Mod.cs` | simplify to delegate to submod |
| `glass/Mod.cs` | simplify to delegate to submod |
| `glass/Patcher.cs` | simplify to call `GlassPatches` |
| `glass/glass.csproj` | add ksa-abstractions.lib ref |
| `glass.lib/glass.lib.csproj` | add Harmony, ImGui, ksa-abstractions refs |
| `i-feel-seen/Mod.cs` | simplify to delegate to submod |
| `i-feel-seen/Patcher.cs` | simplify to call `IFeelSeenPatches` |
| `i-feel-seen.lib/i-feel-seen.lib.csproj` | add Harmony ref |
| `kiwis-marbles/Mod.cs` | simplify to delegate to submod |
| `kiwis-marbles.lib/kiwis-marbles.lib.csproj` | add ImGui refs |
| `skittles/Mod.cs` | simplify to delegate to submod |
| `skittles/Patcher.cs` | simplify to call `SkittlesPatches` |
| `skittles.lib/skittles.lib.csproj` | add Harmony, ksa-abstractions, StarMap.API refs |
| `unladen-swallow/Mod.cs` | simplify to delegate to submod |
| `unladen-swallow.lib/unladen-swallow.lib.csproj` | add ImGui refs |
| `zippo/Mod.cs` | simplify to delegate to submod |
| `grant/Mod.cs` | update usings, use `ISubmod`, reference lib submods |
| `grant/Patcher.cs` | remove inline patches, call lib patches |
| `eternal-flame.lib/eternal-flame.lib.csproj` | add ImGui + ksa-abstractions refs |
| `REPOSITORY_INDEX.md` | update grant architecture description |
| `grant/README.md` | update architecture description |

## files deleted

| file | reason |
|------|--------|
| `grant/IGrantSubmod.cs` | replaced by `ksa-abstractions.lib/ISubmod.cs` |
| `grant/Submods/AverageTwrSubmod.cs` | moved to `average-twr.lib` |
| `grant/Submods/BlinkySubmod.cs` | moved to `blinky.lib` |
| `grant/Submods/EternalFlameSubmod.cs` | moved to `eternal-flame.lib` |
| `grant/Submods/GarrysTorchSubmod.cs` | moved to `garrys-torch.lib` |
| `grant/Submods/GlassSubmod.cs` | moved to `glass.lib` |
| `grant/Submods/IFeelSeenSubmod.cs` | moved to `i-feel-seen.lib` |
| `grant/Submods/KiwisMarblesSubmod.cs` | moved to `kiwis-marbles.lib` |
| `grant/Submods/SkittlesSubmod.cs` | moved to `skittles.lib` |
| `grant/Submods/UnladenSwallowSubmod.cs` | moved to `unladen-swallow.lib` |
| `grant/Submods/ZippoSubmod.cs` | moved to `zippo.lib` |
