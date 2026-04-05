# Grant Refactor Plan

## Overview

This document defines a sequenced set of refactoring tasks to restructure the `ksa-mod-experiments` repository. The goals are:

1. **Create `ksa-abstractions.lib`** — a shared KSA abstraction layer insulating mod code from direct KSA game API references
2. **Extract mod logic into companion `.lib` projects** — each mod gets a `[modname].lib` library containing reusable, stateless-preferred logic
3. **Standardize naming** — all projects use `MeowSci.*` assembly names and namespaces
4. **Standardize build/dist** — all mod csproj files use the zippo-style `CopyCustomContent` pattern for `MeowSci.*.dll` auto-copy
5. **Update `grant` supermod** — references all `.lib` projects for compile-time linkage

---

## Repository Context

### Projects In Scope for Refactoring (Mod Projects)

| Directory | Current AssemblyName | New Mod Assembly | New Lib Assembly | Has Harmony Patches | Extra Files |
|-----------|---------------------|-----------------|-----------------|--------------------|----|
| `average-twr` | `average-twr` | `MeowSci.AverageTwr` | `MeowSci.AverageTwrLib` | No (boilerplate only) | None |
| `blinken` | `blinken` | `MeowSci.Blinken` | `MeowSci.BlinkenLib` | No (boilerplate only) | `LcdAnimationPixels.cs`, `scripts/`, `svgs/` |
| `byo-music` | `byo-music` | `MeowSci.ByoMusic` | `MeowSci.ByoMusicLib` | No (boilerplate only) | `Assets.xml`, `Music/` dir |
| `camera-controller-override` | `camera-controller-override` | `MeowSci.CameraControllerOverride` | `MeowSci.CameraControllerOverrideLib` | **Yes** (2 prefix patches: `OrbitController.OnFrame`, `FlyController.OnFrame`) | `Animation/*.cs`, `UI/*.cs` |
| `garrys-torch` | `garrys-torch` | `MeowSci.GarrysTorch` | `MeowSci.GarrysTorchLib` | No (boilerplate only) | None |
| `geeforce` | `geeforce` | `MeowSci.GeeForce` | `MeowSci.GeeForceLib` | No (boilerplate only) | `GForceRecorder.cs`, `GForceUI.cs` |
| `i-feel-seen` | `i-feel-seen` | `MeowSci.IFeelSeen` | `MeowSci.IFeelSeenLib` | **Yes** (2 prefix patches: `Vehicle.GetWorldMatrix`, `Vehicle.UpdateRenderData`) | None |
| `kitten-animations` | `kitten-animations` | `MeowSci.KittenAnimations` | `MeowSci.KittenAnimationsLib` | No (boilerplate only) | None |
| `zippo` | `zippo` | `MeowSci.Zippo` | `MeowSci.ZippoLib` | No (boilerplate only) | None |

### Projects NOT In Scope (Do Not Modify)

- `decomp/` — decompiled game sources for reference
- `docs/` — documentation
- `fixme-mod-name/` — debugging project
- `logs/` — log files
- `plans/` — planning documents
- `stampy/` — debugging project

### Reference Projects (Existing Patterns to Follow)

- `example-lib-project/` — exemplar library project (OutputType=Library, AssemblyName=`MeowSci.ExampleLib`, RootNamespace=`MeowSci.ExampleLib`)
- `ksa-abstractions.lib/` — empty shell already created (AssemblyName=`MeowSci.KsaAbstractions`, RootNamespace=`MeowSci.KsaAbstractions`)

### Key Build Conventions

- `Directory.Build.props` defines shared properties: `net10.0`, `LangVersion 13.0`, `KSAFolder`, `KSAUserDir`, `SelectedDistModDir`
- All mod projects are OutputType=Library (loaded by StarMap)
- Mod dist: each mod copies its output to `$(DistDir)` via `CopyCustomContent` MSBuild target
- Library assembly auto-copy pattern (from zippo): `<MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />` then `<Copy>` to `$(DistDir)`

### Common KSA API Usage Across Mods (Abstraction Candidates)

These KSA game APIs are used by **3 or more mods** and are prime candidates for `ksa-abstractions.lib`:

| KSA API | Mods Using It | Abstraction |
|---------|---------------|-------------|
| `Program.ControlledVehicle` | average-twr, blinken, geeforce, garrys-torch, kitten-animations, i-feel-seen | `VehicleProvider.GetControlledVehicle()` |
| `Universe.CurrentSystem?.Vehicles?.GetList()` | zippo, i-feel-seen, garrys-torch | `VehicleProvider.GetAllVehicles()` |
| `vehicle.Parts.Parts` (part tree traversal) | zippo, blinken, garrys-torch, i-feel-seen | `PartHelpers.TraverseParts(vehicle)` |
| Reflection-based field access (`GetField/SetField` via BindingFlags) | zippo, kitten-animations, garrys-torch | `ReflectionHelpers.GetFieldValue<T>()` / `SetFieldValue()` |
| `Universe.GetElapsedSimTime()` | geeforce, garrys-torch | `SimTimeProvider.GetElapsedTime()` |

---

## Task Sequence

### TASK 0: Update `ksa-abstractions.lib` with Common Abstractions

**Goal:** Populate the `ksa-abstractions.lib` project with KSA abstraction wrappers that insulate mod code from direct KSA API calls for commonly-used operations.

**Context:** The `ksa-abstractions.lib` project already exists as an empty shell at `ksa-abstractions.lib/ksa-abstractions.lib.csproj` with `AssemblyName=MeowSci.KsaAbstractions` and `RootNamespace=MeowSci.KsaAbstractions`. It currently contains only a placeholder file `AbstractionsExampleToBeDeleted.cs`. The library project references are defined in `example-lib-project/example-lib-project.csproj` as a pattern — library projects use `OutputType=Library` and do NOT include game DLL references since they only define abstractions.

**IMPORTANT:** The abstractions library needs to reference KSA game DLLs (KSA.dll, Brutal.*.dll) because the abstraction methods wrap real KSA types. Add the same `<Reference>` items used by mod projects (with `Private=false` and `Condition` checks). This is different from pure-logic library projects.

**Steps:**

1. **Delete** `AbstractionsExampleToBeDeleted.cs`

2. **Update `ksa-abstractions.lib.csproj`:**
   - Add `<Reference>` items for KSA game DLLs (same set used by all mod projects): `Brutal.Core.Common`, `Brutal.Core.Numerics`, `Brutal.ImGui`, `Brutal.ImGui.Abstractions`, `Brutal.Core.Strings`, `KSA` — all with `Private=false` and `Condition="Exists(...)"` pattern
   - Add NuGet reference: `<PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />`

3. **Create `VehicleProvider.cs`** in namespace `MeowSci.KsaAbstractions`:
   ```csharp
   // Static helper to get vehicles — wraps Program.ControlledVehicle and Universe.CurrentSystem.Vehicles
   public static class VehicleProvider
   {
     public static Vehicle? GetControlledVehicle() => Program.ControlledVehicle;
     public static List<Vehicle> GetAllVehicles() =>
       Universe.CurrentSystem?.Vehicles?.GetList() ?? new List<Vehicle>();
   }
   ```

4. **Create `PartHelpers.cs`** in namespace `MeowSci.KsaAbstractions`:
   ```csharp
   // Static helpers for part tree traversal — wraps vehicle.Parts.Parts recursive walking
   public static class PartHelpers
   {
     public static List<Part> GetAllParts(Vehicle vehicle) { /* recursive traversal */ }
     public static List<Part> GetPartsWhere(Vehicle vehicle, Func<Part, bool> predicate) { /* filtered traversal */ }
   }
   ```

5. **Create `ReflectionHelpers.cs`** in namespace `MeowSci.KsaAbstractions`:
   ```csharp
   // Reflection utilities for accessing private/internal KSA fields
   // Used by zippo (LightModule fields), kitten-animations (KittenEva._renderable), garrys-torch (KittenEva scale)
   public static class ReflectionHelpers
   {
     private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
     public static object? GetFieldValue(object? obj, string fieldName) => ...
     public static void SetFieldValue(object? obj, string fieldName, object? value) => ...
     public static T? GetFieldValue<T>(object? obj, string fieldName) where T : class => ...
   }
   ```

6. **Verify** the project compiles: `dotnet build ksa-abstractions.lib/ksa-abstractions.lib.csproj`
7. **Verify** the full solution compiles: `dotnet build`

**What NOT to abstract:** Mod-specific KSA API usage (e.g., `EngineController.SetIsActive` in blinken, `MusicPlayList` in byo-music, `OrbitController/FlyController` in camera-controller-override). These are too specific to individual mods.

---

### TASK 1: Refactor `average-twr`

**Goal:** Split the `average-twr` mod into `average-twr` (mod shell) + `average-twr.lib` (reusable logic library). Rename namespaces/assemblies to `MeowSci.*` convention. Wire up `ksa-abstractions.lib`.

**Context:**
- **Current code:** All logic in `Mod.cs` (~200 lines). `Patcher.cs` is boilerplate only (no active patches). `mod.toml` for StarMap registration.
- **Current namespace:** `mod`
- **Current AssemblyName:** `average-twr`
- **Functionality:** Samples TWR and max-acceleration from the controlled vehicle at 10ms intervals, accumulates running sums, computes statistics (mean, stddev, harmonic mean, brachi mean). Displays in an ImGui window toggled by F11.
- **KSA APIs used:**
  - `Program.ControlledVehicle` → use `VehicleProvider.GetControlledVehicle()` from abstractions
  - `vehicle.NavBallData.ThrustWeightRatio`
  - `vehicle.FlightComputer.VehicleConfig.TotalEngineVacuumThrust`
  - `vehicle.Parent.Mass`, `vehicle.Parent.MeanRadius`, `vehicle.TotalMass`
- **State maintained:** `_sampleCount`, `_twrSum/SumSq/SumInv/SumInvSqrt`, `_accelSum/SumSq/SumInv/SumInvSqrt`, `_timeSinceLastSample`, `_isCollecting` plus lifecycle flags

**Steps:**

1. **Create `average-twr.lib/` directory** and `average-twr.lib.csproj`:
   - Use `example-lib-project.csproj` as template
   - `AssemblyName=MeowSci.AverageTwrLib`, `RootNamespace=MeowSci.AverageTwrLib`
   - Add `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references (same set as mod project) since the lib needs KSA types for vehicle data access

2. **Create lib source files** in namespace `MeowSci.AverageTwrLib`:
   - **`TwrStatistics.cs`** — stateless pure math functions:
     ```csharp
     public static class TwrStatistics
     {
       public static double ComputeMean(double sum, int count) => ...
       public static double ComputeStdDev(double sum, double sumSq, int count) => ...
       public static double ComputeHarmonicMean(double sumInverse, int count) => ...
       public static double ComputeBrachiMean(double sumInverseSqrt, int count) => ...
     }
     ```
   - **`TwrSampleAccumulator.cs`** — stateful sample collection object:
     ```csharp
     public class TwrSampleAccumulator
     {
       // Fields: SampleCount, TwrSum, TwrSumSq, TwrSumInv, TwrSumInvSqrt, AccelSum, etc.
       public void AddSample(double twr, double accel) { ... }
       public void Reset() { ... }
       // Properties for computed stats using TwrStatistics
     }
     ```
   - **`TwrDataReader.cs`** — reads TWR/accel data from a vehicle:
     ```csharp
     public static class TwrDataReader
     {
       public static double ReadTwr(Vehicle vehicle) => vehicle.NavBallData.ThrustWeightRatio;
       public static double ComputeSurfaceGravity(Vehicle vehicle) => ...
       public static double ComputeMaxAcceleration(Vehicle vehicle) => ...
     }
     ```

3. **Refactor `average-twr/` mod project:**
   - Update `average-twr.csproj`:
     - Set `AssemblyName=MeowSci.AverageTwr`, `RootNamespace=MeowSci.AverageTwr`
     - Add `<ProjectReference>` to `average-twr.lib` and `ksa-abstractions.lib`
     - Update `CopyCustomContent` target to include `MeowSci.*.dll/pdb` auto-copy (zippo pattern)
     - Update `DistDir` to use `$(SelectedDistModDir)average-twr\` (use the MSBuild property, not hardcoded path) — check current value, some use hardcoded, some use the property
   - Update `Mod.cs`:
     - Change namespace to `MeowSci.AverageTwr`
     - Replace inline statistics logic with calls to `TwrSampleAccumulator` and `TwrDataReader`
     - Replace `Program.ControlledVehicle` with `VehicleProvider.GetControlledVehicle()`
     - Keep ImGui rendering in the mod (UI stays in mod, not lib)
   - Update `Patcher.cs`:
     - Change namespace to `MeowSci.AverageTwr`
     - Keep Harmony ID as `"average-twr"`
   - `mod.toml` — no changes needed (mod name stays as-is for game registration)

4. **Add `average-twr.lib` to solution:** Update `ksa-mod-experiments.slnx`

5. **Verify:** `dotnet build` — must compile and have same functional behavior

---

### TASK 2: Refactor `blinken`

**Goal:** Split `blinken` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~450 lines), `Patcher.cs` (boilerplate), `LcdAnimationPixels.cs` (static pixel data array).
- **Current namespace:** `mod`
- **Current AssemblyName:** `blinken`
- **Functionality:** Discovers engine pairs named `pixel_{row}_{col}_a/b` on the controlled vehicle, controls them as a pixel grid. Supports patterns (all on, checkerboard, alternating rows/cols) and LCD scroll animation using bitmap pixel data.
- **KSA APIs used:**
  - `Program.ControlledVehicle` → abstract to `VehicleProvider`
  - `vehicle.Parts.Parts` → abstract to `PartHelpers`
  - `Part.Id` (string parsing for pixel grid pattern)
  - `part.SubParts`, `part.SubtreeModules`
  - `EngineController.IsActive`, `EngineController.SetIsActive(null, bool)`, `EngineController.MinimumThrottle`
  - `vehicle.Parts.RecomputeAllDerivedData()`
- **State:** pixel grid dict, engine controller cache, LCD animation state (offset, speed, pixel set), vehicle dirty-check
- **Additional data files:** `dean_dots_65h.cs_array`, `dean_dots_65h.json`, `dean_dots.cs_array`, `dean_dots.json` (pixel data), `scripts/` dir, `svgs/` dir — these are auxiliary and should stay in the blinken mod project, not the lib

**Steps:**

1. **Create `blinken.lib/` directory** and `blinken.lib.csproj`:
   - `AssemblyName=MeowSci.BlinkenLib`, `RootNamespace=MeowSci.BlinkenLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references for KSA types used (EngineController, Part, Vehicle)

2. **Create lib source files** in namespace `MeowSci.BlinkenLib`:
   - **`PixelGrid.cs`** — pixel grid data structure and scanning:
     ```csharp
     public class PixelGrid
     {
       // Dictionary<(int row, int col), (Part a, Part b)> grid
       // Dictionary<(int row, int col), EngineController[]> cachedControllers
       public int Rows { get; }
       public int Cols { get; }
       public static PixelGrid ScanFromVehicle(Vehicle vehicle) { ... }
       // Regex parsing of pixel_{row}_{col}_a/b from Part.Id
     }
     ```
   - **`PixelPatterns.cs`** — stateless pattern selectors:
     ```csharp
     public static class PixelPatterns
     {
       public static bool AllOn((int row, int col) pos) => true;
       public static bool Checkerboard((int row, int col) pos) => (pos.row + pos.col) % 2 == 0;
       public static bool AlternatingRows((int row, int col) pos) => pos.row % 2 == 0;
       public static bool AlternatingCols((int row, int col) pos) => pos.col % 2 == 0;
       public static void ApplyPattern(PixelGrid grid, Func<(int, int), bool> selector) { ... }
     }
     ```
   - **`LcdAnimation.cs`** — LCD scroll animation logic:
     ```csharp
     public class LcdAnimation
     {
       // Scroll state, speed, pixel set, dimensions
       public void Update(double deltaTime, PixelGrid grid) { ... }
       public void Start(HashSet<(int, int)> pixelSet, int imageWidth, int imageHeight) { ... }
       public void Stop() { ... }
     }
     ```
   - **Move `LcdAnimationPixels.cs`** to the lib project (rename namespace to `MeowSci.BlinkenLib`)

3. **Refactor `blinken/` mod project:**
   - Update `blinken.csproj`:
     - `AssemblyName=MeowSci.Blinken`, `RootNamespace=MeowSci.Blinken`
     - `<ProjectReference>` to `blinken.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy to `CopyCustomContent`
   - Update `Mod.cs`:
     - Namespace → `MeowSci.Blinken`
     - Replace inline pixel grid scanning/management with `PixelGrid` from lib
     - Replace inline pattern logic with `PixelPatterns` from lib
     - Replace inline LCD animation logic with `LcdAnimation` from lib
     - Replace `Program.ControlledVehicle` with `VehicleProvider.GetControlledVehicle()`
   - Update `Patcher.cs`:
     - Namespace → `MeowSci.Blinken`

4. **Add `blinken.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 3: Refactor `byo-music`

**Goal:** Split `byo-music` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~80 lines — minimal/stub). `Patcher.cs` (boilerplate). `Assets.xml` and `Music/` directory.
- **Current namespace:** `mod`
- **Current AssemblyName:** `byo-music`
- **Functionality:** Registers music assets via `Assets.xml`, provides a button to play a music playlist via `ModLibrary.Get<MusicPlayList>("SabotageMusic").PlayMusic(...)`.
- **KSA APIs used:**
  - `ModLibrary.Get<MusicPlayList>(string)` — asset lookup
  - `MusicPlayList.PlayMusic(out ChannelWrapper?)` — playback
- **State:** Only lifecycle flags (`_isInitialized`, `_isDisposed`, `_windowVisible`)
- **Special build considerations:** `Assets.xml` and `Music/` directory must be copied to output and dist. The csproj has special `<None Update>` items and a more complex `CopyCustomContent` target that creates `$(DistDir)Music` and copies music files.

**Steps:**

1. **Create `byo-music.lib/` directory** and `byo-music.lib.csproj`:
   - `AssemblyName=MeowSci.ByoMusicLib`, `RootNamespace=MeowSci.ByoMusicLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references for KSA types (`ModLibrary`, `MusicPlayList`)

2. **Create lib source files** in namespace `MeowSci.ByoMusicLib`:
   - **`MusicPlayer.cs`** — music lookup and playback:
     ```csharp
     public static class MusicPlayer
     {
       public static MusicPlayList? GetPlaylist(string assetId) => ModLibrary.Get<MusicPlayList>(assetId);
       public static void Play(MusicPlayList playlist) => playlist.PlayMusic(out _);
     }
     ```

3. **Refactor `byo-music/` mod project:**
   - Update `byo-music.csproj`:
     - `AssemblyName=MeowSci.ByoMusic`, `RootNamespace=MeowSci.ByoMusic`
     - `<ProjectReference>` to `byo-music.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy to `CopyCustomContent`
     - Keep existing `Assets.xml` and `Music/` copy logic intact
   - Update `Mod.cs`: namespace → `MeowSci.ByoMusic`, use `MusicPlayer` from lib
   - Update `Patcher.cs`: namespace → `MeowSci.ByoMusic`

4. **Add `byo-music.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 4: Refactor `camera-controller-override`

**Goal:** Split `camera-controller-override` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (lifecycle + UI with ~100+ configuration fields for 8 animation types), `Patcher.cs` (**has active Harmony patches** — 2 prefix patches on `OrbitController.OnFrame` and `FlyController.OnFrame`), `Animation/AnimationHelpers.cs`, `Animation/IKeyframeAnimation.cs`, `Animation/Keyframe.cs`, `Animation/KeyframeSequencePlayer.cs`, `Animation/Animations/` (8 animation classes), `UI/KeyframeSequencePanel.cs`
- **Current namespace:** `mod` (sub-namespaces: `mod.Animation`, `mod.Animation.Animations`, `mod.UI`)
- **Current AssemblyName:** `camera-controller-override`
- **Functionality:** Camera animation system with 8 animation types, keyframe sequences, easing system, live UI editor. Harmony patches intercept camera controller `OnFrame` to inject animation playback.
- **KSA APIs used:**
  - `Controller`, `OrbitController`, `FlyController` — patched types
  - `Camera`, `Camera.Following`, `Camera.GetPositionEgo()`
  - `Transform3D` — camera transform manipulation
  - `Universe`, `Orbit`
  - Extensive `Brutal.Numerics` (double3, doubleQuat, quaternion operations)
- **CRITICAL:** Harmony patches are in `Patcher.cs` and reference `KeyframeSequencePlayer` — the patches and the player are tightly coupled. The patches must stay in the mod project (they need Harmony which is a mod-level dependency), but the animation logic (player, helpers, animations, keyframes) can go to the lib.

**Steps:**

1. **Create `camera-controller-override.lib/` directory** and `camera-controller-override.lib.csproj`:
   - `AssemblyName=MeowSci.CameraControllerOverrideLib`, `RootNamespace=MeowSci.CameraControllerOverrideLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references (needs `KSA`, `Brutal.Core.Numerics` for Transform3D, Camera, Controller types)

2. **Move animation infrastructure to lib** (namespace `MeowSci.CameraControllerOverrideLib`):
   - Move `Animation/AnimationHelpers.cs` → update namespace to `MeowSci.CameraControllerOverrideLib.Animation`
   - Move `Animation/IKeyframeAnimation.cs` → same namespace
   - Move `Animation/Keyframe.cs` → same namespace
   - Move `Animation/KeyframeSequencePlayer.cs` → same namespace
   - Move all `Animation/Animations/*.cs` (8 files) → namespace `MeowSci.CameraControllerOverrideLib.Animation.Animations`
   - Move `UI/KeyframeSequencePanel.cs` → namespace `MeowSci.CameraControllerOverrideLib.UI`

3. **Refactor `camera-controller-override/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.CameraControllerOverride`, `RootNamespace=MeowSci.CameraControllerOverride`
     - `<ProjectReference>` to `camera-controller-override.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy
   - Update `Mod.cs`:
     - Namespace → `MeowSci.CameraControllerOverride`
     - Add `using` for lib namespaces
     - All animation references now come from lib
   - Update `Patcher.cs`:
     - Namespace → `MeowSci.CameraControllerOverride`
     - **Keep patches here** — they require Harmony and intercept game methods
     - `_sequencePlayer` can be typed from the lib: `KeyframeSequencePlayer` from `MeowSci.CameraControllerOverrideLib.Animation`
     - The `HandleOnFramePrefix` logic stays in patcher but delegates to lib's player
   - Remove `Animation/` and `UI/` directories from mod project (files moved to lib)

4. **Add `camera-controller-override.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 5: Refactor `garrys-torch`

**Goal:** Split `garrys-torch` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~large, monolithic — all logic in one file). `Patcher.cs` (boilerplate only).
- **Current namespace:** `mod`
- **Current AssemblyName:** `garrys-torch`
- **Functionality:** Vehicle "welding" — rigidly couples one vehicle to another with configurable position offset (xyz), Euler rotation (pitch/yaw/roll), uniform scale. Supports multiple simultaneous welds with topological sorting for dependency ordering. Includes preset configurations.
- **KSA APIs used:**
  - `Program.ControlledVehicle` → abstract to `VehicleProvider`
  - `Universe.CurrentSystem?.Vehicles?.GetList()` → abstract to `VehicleProvider`
  - `Universe.GetElapsedSimTime()`
  - `Vehicle.GetPositionCci()`, `GetVelocityCci()`, `GetBody2Cci()`, `BodyRates`, `Teleport()`
  - `Orbit.CreateFromStateCci()`
  - `Part.Scale`, `part.SubParts`
  - Reflection for `KittenEva` (private `CharacterAvatar.Core.Scale`) → use `ReflectionHelpers` from abstractions
  - `Brutal.Numerics` quaternion/vector math
- **State:** `_welds: List<WeldEntry>`, pending weld UI state, preset selections

**Steps:**

1. **Create `garrys-torch.lib/` directory** and `garrys-torch.lib.csproj`:
   - `AssemblyName=MeowSci.GarrysTorchLib`, `RootNamespace=MeowSci.GarrysTorchLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references (needs Vehicle, Part, Orbit, KittenEva types)

2. **Create lib source files** in namespace `MeowSci.GarrysTorchLib`:
   - **`WeldEntry.cs`** — data class:
     ```csharp
     public class WeldEntry
     {
       public Vehicle Source;
       public Vehicle Target;
       public float3 Position;
       public float3 Rotation; // Euler degrees
       public float Scale;
       public bool LockRotation;
     }
     ```
   - **`WeldPreset.cs`** — preset data:
     ```csharp
     public record WeldPreset(string Name, float3 Position, float3 Rotation, float Scale, bool LockRotation);
     ```
   - **`WeldEngine.cs`** — core weld computation (stateless):
     ```csharp
     public static class WeldEngine
     {
       public static void UpdateWeld(WeldEntry weld) { ... }
       // Contains the quaternion math, position calculation, teleport call
       public static doubleQuat EulerDegreesToQuat(float pitch, float yaw, float roll) { ... }
       public static void ApplyVehicleScale(Vehicle vehicle, float scale) { ... }
       public static void SetPartScaleRecursive(Part part, float scale) { ... }
       public static List<WeldEntry> TopologicalSort(List<WeldEntry> welds) { ... }
     }
     ```

3. **Refactor `garrys-torch/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.GarrysTorch`, `RootNamespace=MeowSci.GarrysTorch`
     - `<ProjectReference>` to `garrys-torch.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy
   - Update `Mod.cs`:
     - Namespace → `MeowSci.GarrysTorch`
     - Extract weld logic to use `WeldEngine` from lib
     - Replace `Program.ControlledVehicle` with `VehicleProvider.GetControlledVehicle()`
     - Replace `Universe.CurrentSystem?.Vehicles?.GetList()` with `VehicleProvider.GetAllVehicles()`
     - Replace inline reflection with `ReflectionHelpers` from abstractions where applicable
     - Keep ImGui rendering in mod
   - Update `Patcher.cs`: namespace → `MeowSci.GarrysTorch`

4. **Add `garrys-torch.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 6: Refactor `geeforce`

**Goal:** Split `geeforce` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (lifecycle + sampling), `GForceRecorder.cs` (circular buffer data recording + statistics), `GForceUI.cs` (static UI rendering class with graph drawing). `Patcher.cs` (boilerplate).
- **Current namespace:** `mod`
- **Current AssemblyName:** `geeforce`
- **Functionality:** Real-time G-force monitoring with 40Hz sampling, circular buffer recording (30s–1h), time-series graph visualization, axis breakdown, jerk tracking, threshold breach detection.
- **KSA APIs used:**
  - `Program.ControlledVehicle` → abstract to `VehicleProvider`
  - `Vehicle.AccelerationBody` — acceleration vector
  - `Universe.GetElapsedSimTime()` — simulation time
- **State:** `GForceRecorder` (circular buffer, statistics), `GForceUI` (static UI state — selected history, scrub position, display toggles)
- **Already well-separated:** `GForceRecorder.cs` and `GForceUI.cs` are already cleanly separated from `Mod.cs`. This mod is the best-structured for extraction.

**Steps:**

1. **Create `geeforce.lib/` directory** and `geeforce.lib.csproj`:
   - `AssemblyName=MeowSci.GeeForceLib`, `RootNamespace=MeowSci.GeeForceLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references (needs Vehicle for AccelerationBody, Universe for sim time)

2. **Move/refactor to lib** (namespace `MeowSci.GeeForceLib`):
   - **Move `GForceRecorder.cs`** → namespace `MeowSci.GeeForceLib`, rename class as needed
   - **Move `GForceUI.cs`** → namespace `MeowSci.GeeForceLib`, this is the ImGui graph renderer
   - The `GForceSample` struct (if defined inside `GForceRecorder`) should be a top-level public type in the lib

3. **Refactor `geeforce/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.GeeForce`, `RootNamespace=MeowSci.GeeForce`
     - `<ProjectReference>` to `geeforce.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy
   - Update `Mod.cs`:
     - Namespace → `MeowSci.GeeForce`
     - Replace `Program.ControlledVehicle` with `VehicleProvider.GetControlledVehicle()`
     - Reference `GForceRecorder` and `GForceUI` from lib namespace
   - Update `Patcher.cs`: namespace → `MeowSci.GeeForce`
   - Remove `GForceRecorder.cs` and `GForceUI.cs` from mod project (moved to lib)

4. **Add `geeforce.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 7: Refactor `i-feel-seen`

**Goal:** Split `i-feel-seen` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~moderate size). `Patcher.cs` (**has active Harmony patches** — 2 prefix patches on `Vehicle.GetWorldMatrix` and `Vehicle.UpdateRenderData`).
- **Current namespace:** `mod`
- **Current AssemblyName:** `i-feel-seen`
- **Functionality:** Vehicle render distance override — allows tracking specific vehicles so they always render regardless of camera distance (bypasses LOD culling).
- **KSA APIs used:**
  - `Universe.CurrentSystem?.Vehicles?.GetList()` → abstract to `VehicleProvider`
  - `Vehicle.GetWorldMatrix`, `Vehicle.UpdateRenderData` — patched methods
  - `Vehicle.GetMatrixAsmb2Ego`, `Vehicle.Body2Cce`, `Vehicle.IsEditedVehicle`, `Vehicle.Parts`
  - `Viewport`, `Camera.GetPositionEgo()`
  - `Brutal.Numerics` matrix/quaternion types
- **State:** `_tracked: List<TrackedVehicle>` (Vehicle + SeeMe bool), `_pendingVehicleIndex`
- **CRITICAL:** Harmony patches reference `TrackedVehicle` list — patches must stay in mod, but tracking logic can go to lib.

**Steps:**

1. **Create `i-feel-seen.lib/` directory** and `i-feel-seen.lib.csproj`:
   - `AssemblyName=MeowSci.IFeelSeenLib`, `RootNamespace=MeowSci.IFeelSeenLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references

2. **Create lib source files** in namespace `MeowSci.IFeelSeenLib`:
   - **`VehicleTracker.cs`** — tracking state and logic:
     ```csharp
     public class TrackedVehicle
     {
       public Vehicle Vehicle { get; set; }
       public bool SeeMe { get; set; }
     }
     
     public class VehicleTracker
     {
       public List<TrackedVehicle> Tracked { get; }
       public void Add(Vehicle v) { ... }
       public void Remove(Vehicle v) { ... }
       public void SetSeeMe(Vehicle v, bool value) { ... }
       public bool IsTracked(Vehicle v) { ... }
     }
     ```
   - **`RenderOverride.cs`** — world matrix calculation logic (extracted from patch):
     ```csharp
     public static class RenderOverride
     {
       public static float4x4 CalculateWorldMatrix(Vehicle vehicle, Camera camera) { ... }
       // The math that the Harmony prefix does, extracted as a callable function
     }
     ```

3. **Refactor `i-feel-seen/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.IFeelSeen`, `RootNamespace=MeowSci.IFeelSeen`
     - `<ProjectReference>` to `i-feel-seen.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy
   - Update `Mod.cs`:
     - Namespace → `MeowSci.IFeelSeen`
     - Use `VehicleTracker` and `VehicleProvider` from libs
   - Update `Patcher.cs`:
     - Namespace → `MeowSci.IFeelSeen`
     - **Keep patches here** — they intercept `Vehicle.GetWorldMatrix` and `Vehicle.UpdateRenderData`
     - Delegate calculation to `RenderOverride.CalculateWorldMatrix()` from lib
     - Reference `VehicleTracker` from lib for tracked vehicle list

4. **Add `i-feel-seen.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 8: Refactor `kitten-animations`

**Goal:** Split `kitten-animations` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~moderate size). `Patcher.cs` (boilerplate only).
- **Current namespace:** `mod`
- **Current AssemblyName:** `kitten-animations`
- **Functionality:** Triggers kitten avatar animations (MMU movement, expressions with configurable duration and quadratic ease-in, walking). Uses reflection to access private `KittenEva._renderable` → `KittenRenderable._characterAvatar` → `CharacterAvatar`.
- **KSA APIs used:**
  - `Program.ControlledVehicle` → abstract to `VehicleProvider`
  - `KittenEva` — type check and reflection source
  - `CharacterAvatar`, `CharacterModel`, `AnimationAssetRef`, `CatExpressionAnim`, `IAnimation`
  - Reflection to access private fields → use `ReflectionHelpers` from abstractions
- **State:** `_currentExpression` enum, `_currentExpressionAnim`, `_expressionTimer`, `_expressionDuration`, `_expressionEaseInTimer`, `_random`

**Steps:**

1. **Create `kitten-animations.lib/` directory** and `kitten-animations.lib.csproj`:
   - `AssemblyName=MeowSci.KittenAnimationsLib`, `RootNamespace=MeowSci.KittenAnimationsLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references (needs KittenEva, CharacterAvatar, animation types)

2. **Create lib source files** in namespace `MeowSci.KittenAnimationsLib`:
   - **`KittenAvatarAccessor.cs`** — reflection-based avatar extraction:
     ```csharp
     public static class KittenAvatarAccessor
     {
       public static KittenEva? GetKittenFromVehicle(Vehicle vehicle) { ... }
       public static CharacterAvatar? GetAvatar(KittenEva kitten) { ... }
       // Uses ReflectionHelpers from ksa-abstractions.lib
     }
     ```
   - **`ExpressionController.cs`** — expression management:
     ```csharp
     public class ExpressionController
     {
       // Expression state: current expression, timer, ease-in
       public void StartExpression(CharacterAvatar avatar, ExpressionType type, float duration) { ... }
       public void Update(double deltaTime, CharacterAvatar avatar) { ... }
       public static float CalculateEaseInWeight(float elapsed, float easeInDuration) { ... }
     }
     ```
   - **`AnimationTrigger.cs`** — stateless animation triggering:
     ```csharp
     public static class AnimationTrigger
     {
       public static void PlayMmuAnimation(CharacterAvatar avatar, string animName) { ... }
       public static void PlayWalkAnimation(CharacterAvatar avatar, string animName) { ... }
     }
     ```

3. **Refactor `kitten-animations/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.KittenAnimations`, `RootNamespace=MeowSci.KittenAnimations`
     - `<ProjectReference>` to `kitten-animations.lib` and `ksa-abstractions.lib`
     - Add `MeowSci.*.dll/pdb` auto-copy
   - Update `Mod.cs`:
     - Namespace → `MeowSci.KittenAnimations`
     - Replace inline avatar access with `KittenAvatarAccessor` from lib
     - Replace inline expression logic with `ExpressionController` from lib
     - Replace `Program.ControlledVehicle` with `VehicleProvider.GetControlledVehicle()`
   - Update `Patcher.cs`: namespace → `MeowSci.KittenAnimations`

4. **Add `kitten-animations.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 9: Refactor `zippo`

**Goal:** Split `zippo` into mod shell + library. Rename to `MeowSci.*`.

**Context:**
- **Current code:** `Mod.cs` (~400 lines, all logic). `Patcher.cs` (boilerplate).
- **Current namespace:** `mod`
- **Current AssemblyName:** `zippo`
- **Functionality:** Light control tool — discovers light-equipped parts on vehicles, allows toggling lights on/off, adjusting emissive intensity (0–1), and setting color presets. Uses heavy reflection to access `LightModule+TemplateData` internals (Intensity, Color fields).
- **KSA APIs used:**
  - `Universe.CurrentSystem?.Vehicles?.GetList()` → abstract to `VehicleProvider`
  - `vehicle.Parts.Parts` → abstract to `PartHelpers`
  - `Part.Template` (PartTemplate), `Part.LightSwitch`, `Part.FullPart.LightSwitch`
  - `LightSwitch.LightIsActive`
  - Reflection: `PartTemplate.Components` list → `LightModule+TemplateData` → `Intensity.Value`, `Color.R/G/B`
  - `Part.DisplayName`, `Part.Id`
- **State:** vehicle list, light parts list, combo indices, intensity/color state
- **Already references** `ksa-abstractions.lib` and `example-lib-project` in csproj
- **Already has** the `MeowSci.*.dll/pdb` auto-copy pattern in `CopyCustomContent`

**Steps:**

1. **Create `zippo.lib/` directory** and `zippo.lib.csproj`:
   - `AssemblyName=MeowSci.ZippoLib`, `RootNamespace=MeowSci.ZippoLib`
   - `<ProjectReference>` to `ksa-abstractions.lib`
   - Add game DLL references

2. **Create lib source files** in namespace `MeowSci.ZippoLib`:
   - **`LightDiscovery.cs`** — find light-equipped parts:
     ```csharp
     public static class LightDiscovery
     {
       public static bool HasLights(PartTemplate template) { ... }
       public static List<Part> FindLightParts(Vehicle vehicle) { ... }
       // Uses ReflectionHelpers from ksa-abstractions.lib for Component introspection
     }
     ```
   - **`LightController.cs`** — intensity and color control:
     ```csharp
     public static class LightController
     {
       public static float ReadIntensity(PartTemplate template) { ... }
       public static void WriteIntensity(Part part, float intensity) { ... }
       public static void WriteColor(Part part, float3 color) { ... }
       public static void ToggleLight(Part part, bool enabled) { ... }
     }
     ```
   - **`ColorPresets.cs`** — color presets data:
     ```csharp
     public static class ColorPresets
     {
       public static readonly (string Name, float3 Color)[] Presets = { ... };
       public static float3 GetPresetColor(int index) { ... }
     }
     ```

3. **Refactor `zippo/` mod project:**
   - Update csproj:
     - `AssemblyName=MeowSci.Zippo`, `RootNamespace=MeowSci.Zippo`
     - `<ProjectReference>` to `zippo.lib` and `ksa-abstractions.lib`
     - Remove `<ProjectReference>` to `example-lib-project` (no longer needed as example reference)
     - `MeowSci.*.dll/pdb` auto-copy already present
   - Update `Mod.cs`:
     - Namespace → `MeowSci.Zippo`
     - Replace inline light discovery with `LightDiscovery` from lib
     - Replace inline light control with `LightController` from lib
     - Replace inline color presets with `ColorPresets` from lib
     - Replace vehicle listing with `VehicleProvider.GetAllVehicles()`
   - Update `Patcher.cs`: namespace → `MeowSci.Zippo`

4. **Add `zippo.lib` to solution**

5. **Verify:** `dotnet build`

---

### TASK 10: Update `grant` Supermod with All Lib References

**Goal:** Update the `grant` mod project to reference all `.lib` projects, making it an all-in-one supermod with compile-time access to all mod features.

**Context:**
- `grant` is already a working mod project with skeleton `Mod.cs` (has a basic ImGui window with F11 toggle) and `Patcher.cs` (boilerplate).
- Current namespace: `mod`, AssemblyName: `grant`
- The goal is ONLY to update the csproj for compile-time linkage. Do NOT implement any new mod code in `grant`'s `Mod.cs` — just ensure it compiles with access to all lib types.

**Steps:**

1. **Update `grant/grant.csproj`:**
   - Set `AssemblyName=MeowSci.Grant`, `RootNamespace=MeowSci.Grant`
   - Add `<ProjectReference>` entries for ALL lib projects:
     ```xml
     <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
     <ProjectReference Include="..\average-twr.lib\average-twr.lib.csproj" />
     <ProjectReference Include="..\blinken.lib\blinken.lib.csproj" />
     <ProjectReference Include="..\byo-music.lib\byo-music.lib.csproj" />
     <ProjectReference Include="..\camera-controller-override.lib\camera-controller-override.lib.csproj" />
     <ProjectReference Include="..\garrys-torch.lib\garrys-torch.lib.csproj" />
     <ProjectReference Include="..\geeforce.lib\geeforce.lib.csproj" />
     <ProjectReference Include="..\i-feel-seen.lib\i-feel-seen.lib.csproj" />
     <ProjectReference Include="..\kitten-animations.lib\kitten-animations.lib.csproj" />
     <ProjectReference Include="..\zippo.lib\zippo.lib.csproj" />
     <ProjectReference Include="..\example-lib-project\example-lib-project.csproj" />
     ```
   - Add `MeowSci.*.dll/pdb` auto-copy to `CopyCustomContent` (zippo pattern)
   - Update `DistDir` to use `$(SelectedDistModDir)grant\` if not already

2. **Update `grant/Mod.cs`:**
   - Change namespace from `mod` to `MeowSci.Grant`
   - No other code changes — keep existing skeleton

3. **Update `grant/Patcher.cs`:**
   - Change namespace from `mod` to `MeowSci.Grant`

4. **Verify:** `dotnet build`

---

### TASK 11: Solution Cleanup and Final Verification

**Goal:** Ensure the solution file, all projects, and build output are consistent and correct.

**Steps:**

1. **Update `ksa-mod-experiments.slnx`:**
   - Ensure ALL new `.lib` projects are added:
     - `average-twr.lib/average-twr.lib.csproj`
     - `blinken.lib/blinken.lib.csproj`
     - `byo-music.lib/byo-music.lib.csproj`
     - `camera-controller-override.lib/camera-controller-override.lib.csproj`
     - `garrys-torch.lib/garrys-torch.lib.csproj`
     - `geeforce.lib/geeforce.lib.csproj`
     - `i-feel-seen.lib/i-feel-seen.lib.csproj`
     - `kitten-animations.lib/kitten-animations.lib.csproj`
     - `zippo.lib/zippo.lib.csproj`
   - Verify existing projects are still listed

2. **Verify all `DistDir` consistency:**
   - All mod projects should use `$(SelectedDistModDir)modname\` pattern (from `Directory.Build.props`) rather than hardcoded paths where possible
   - Current state: some use `$(SelectedDistModDir)` (average-twr, blinken), others use hardcoded `C:\Program Files\...` paths (byo-music, camera-controller-override, garrys-torch, geeforce, i-feel-seen, grant, zippo) — standardize all to use `$(SelectedDistModDir)`

3. **Verify all `CopyCustomContent` targets:**
   - Every mod project must include the `MeowSci.*.dll;MeowSci.*.pdb` auto-copy block
   - Verify `$(AssemblyName)` in FilesToCopy reflects the new `MeowSci.*` name

4. **Full solution build:** `dotnet build`
   - Must compile with zero errors
   - Verify output directories contain expected assemblies

5. **Spot-check assembly names in output:**
   - Each mod's `bin/Debug/` should contain `MeowSci.ModName.dll` plus `MeowSci.*.dll` for its lib dependencies
   - Each mod's `$(DistDir)` should contain the same

---

## Task Dependency Graph

```
TASK 0 (ksa-abstractions.lib)
  │
  ├──> TASK 1 (average-twr)
  ├──> TASK 2 (blinken)
  ├──> TASK 3 (byo-music)
  ├──> TASK 4 (camera-controller-override)
  ├──> TASK 5 (garrys-torch)
  ├──> TASK 6 (geeforce)
  ├──> TASK 7 (i-feel-seen)
  ├──> TASK 8 (kitten-animations)
  ├──> TASK 9 (zippo)
  │
  └──> TASK 10 (grant) ──depends on──> TASKS 1-9
                │
                └──> TASK 11 (cleanup/verification) ──depends on──> ALL
```

Tasks 1–9 are independent of each other and can be done in any order (all depend only on Task 0).
Task 10 depends on all lib projects existing (Tasks 1–9).
Task 11 is final verification after everything is complete.

---

## Reference: Naming Convention Summary

| Project Directory | Assembly Name (Mod) | Assembly Name (Lib) | Root Namespace (Mod) | Root Namespace (Lib) |
|---|---|---|---|---|
| `ksa-abstractions.lib` | — | `MeowSci.KsaAbstractions` | — | `MeowSci.KsaAbstractions` |
| `example-lib-project` | — | `MeowSci.ExampleLib` | — | `MeowSci.ExampleLib` |
| `average-twr` + `average-twr.lib` | `MeowSci.AverageTwr` | `MeowSci.AverageTwrLib` | `MeowSci.AverageTwr` | `MeowSci.AverageTwrLib` |
| `blinken` + `blinken.lib` | `MeowSci.Blinken` | `MeowSci.BlinkenLib` | `MeowSci.Blinken` | `MeowSci.BlinkenLib` |
| `byo-music` + `byo-music.lib` | `MeowSci.ByoMusic` | `MeowSci.ByoMusicLib` | `MeowSci.ByoMusic` | `MeowSci.ByoMusicLib` |
| `camera-controller-override` + `.lib` | `MeowSci.CameraControllerOverride` | `MeowSci.CameraControllerOverrideLib` | `MeowSci.CameraControllerOverride` | `MeowSci.CameraControllerOverrideLib` |
| `garrys-torch` + `garrys-torch.lib` | `MeowSci.GarrysTorch` | `MeowSci.GarrysTorchLib` | `MeowSci.GarrysTorch` | `MeowSci.GarrysTorchLib` |
| `geeforce` + `geeforce.lib` | `MeowSci.GeeForce` | `MeowSci.GeeForceLib` | `MeowSci.GeeForce` | `MeowSci.GeeForceLib` |
| `i-feel-seen` + `i-feel-seen.lib` | `MeowSci.IFeelSeen` | `MeowSci.IFeelSeenLib` | `MeowSci.IFeelSeen` | `MeowSci.IFeelSeenLib` |
| `kitten-animations` + `.lib` | `MeowSci.KittenAnimations` | `MeowSci.KittenAnimationsLib` | `MeowSci.KittenAnimations` | `MeowSci.KittenAnimationsLib` |
| `zippo` + `zippo.lib` | `MeowSci.Zippo` | `MeowSci.ZippoLib` | `MeowSci.Zippo` | `MeowSci.ZippoLib` |
| `grant` | `MeowSci.Grant` | — | `MeowSci.Grant` | — |

## Reference: CopyCustomContent Template (Zippo Pattern)

Every mod csproj should have this `CopyCustomContent` target:

```xml
<Target Name="CopyCustomContent" AfterTargets="AfterBuild">
  <MakeDir Directories="$(DistDir)" />
  <ItemGroup>
    <FilesToCopy Include="$(OutputPath)mod.toml" />
    <FilesToCopy Include="$(OutputPath)$(AssemblyName).dll" />
    <FilesToCopy Include="$(OutputPath)$(AssemblyName).pdb" />
    <FilesToCopy Include="$(OutputPath)$(AssemblyName).deps.json" />
  </ItemGroup>
  <Copy SourceFiles="@(FilesToCopy)" DestinationFolder="$(DistDir)" />
  <Message Text="Copied mod files to $(DistDir)" Importance="high" />

  <!-- Auto-copy all MeowSci.* library assemblies -->
  <ItemGroup>
    <MeowSciAssemblies Include="$(TargetDir)MeowSci.*.dll;$(TargetDir)MeowSci.*.pdb" />
  </ItemGroup>
  <Copy SourceFiles="@(MeowSciAssemblies)"
        DestinationFolder="$(DistDir)"
        Condition="'@(MeowSciAssemblies)' != ''" />
</Target>
```

For mods with extra assets (e.g., `byo-music` with `Assets.xml` and `Music/`), add the asset-specific copy steps in addition to the above.

## Reference: Library Project csproj Template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.XxxLib</AssemblyName>
    <RootNamespace>MeowSci.XxxLib</RootNamespace>
    <Description>Xxx library</Description>
    <PackageId>MeowSci.XxxLib</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <!-- Game DLL references (needed if lib uses KSA types directly) -->
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
