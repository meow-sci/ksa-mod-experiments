# Telemetry / Monitoring Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **average-twr, geeforce**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Recorder samples, record/pause/reset controls and plots live in Live State. Authoring starts/resumes a recorder or applies detached monitoring settings. GeeForce breach checks run during Update, independently of whether any UI is shown. Workspace restoration does not reset sample buffers, clocks or breach counters.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the telemetry mods
(`average-twr`, `geeforce`). Every game-facing member these mods touch is enumerated
and verified against decompiled sources.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard remains in `unscience/Patcher.cs`; feature Harmony groups are registered by their owning libraries through `ConfigureRuntime`. See [architecture](00-architecture-and-abstractions.md).

## average-twr

**Purpose** — Real-time Thrust-to-Weight Ratio and max-linear-acceleration monitor.
Samples the controlled vehicle at 100 Hz (`SampleInterval = 0.01`s,
`average-twr.lib/AverageTwrSubmod.cs`) and reports running mean / std-dev /
harmonic mean / "Brachi" mean for both TWR and max acceleration. Pure read-only
telemetry; the only Harmony patch is the shared `HotkeyGuard`.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Recorder start/resume action and authoring disclosure state. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs` (called `average-twr.lib/AverageTwrSubmod.cs`) | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle` | `KSA/Program.cs` | Yes | Same (OLD `Program.cs`) | Returns null when no vehicle controlled; mod null-checks. |
| 2 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `Vehicle.NavBallData` — `public ref readonly NavBallData NavBallData` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | `ref readonly` struct accessor. |
| 3 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `NavBallData.ThrustWeightRatio` — `public double ThrustWeightRatio` (field) | `KSA/NavBallData.cs` | ✅ | **Semantic drift (rev 5114)** | Public struct field; value 0 until flight computer populates it. **Meaning changed on 5117**: the game now computes it as `ComputeActiveThrust(AtmosphericPressure) * throttle / weight` (`KSA/Vehicle.cs`) instead of `TotalEngineVacuumThrust * throttle / weight`. Same field, same type, **ambient-corrected and propellant-aware** value. No code change needed; the mod's readings shift. |
| 4 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `Vehicle.FlightComputer` — `public FlightComputer FlightComputer { get; private set; }` | `KSA/Vehicle.cs` | ✅ | Same (OLD `Vehicle.cs`) | — |
| 5 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `Vehicle.ComputeActiveThrust(float ambientPressure) → float` | `KSA/Vehicle.cs` | ✅ | Same (OLD `Vehicle.cs`; body identical). NEW binding @5117 (replaces `VehicleConfig.TotalEngineVacuumThrust`) | Sums `EngineController.ComputeActivePerformance(state, com, ambientPressure).ThrustMax.Length()` over active engines; skips engines with no propellant. This is the same call the game's navball TWR uses. |
| 6 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `FlightComputer.AmbientPressure` — `public float AmbientPressure` (field) | `KSA/FlightComputer.cs` | ✅ | Same (OLD `FlightComputer.cs`). NEW binding @5117 (rev 5114) | Populated from `states.Environment.AtmosphericPressure` each FC update (`KSA/FlightComputer.cs`). 0 in vacuum, which makes `ComputeActiveThrust` return `VacuumData` directly. |
| 7 | Direct typed API | `average-twr.lib/TwrDataReader.cs` | `Vehicle.TotalMass` — `public float TotalMass => _props.TotalMassPropsAsmb.Props.Mass` | `KSA/Vehicle.cs` | ✅ | Same (OLD `Vehicle.cs`) | kg. Mod divides thrust/mass for max accel. |
| 8 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs` | `Vehicle.Parent` — `public IParentBody Parent => Orbit.Parent` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Only used in `ComputeSurfaceGravity`, which is **not called** on the sampling path. Still must compile. |
| 9 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs` | `IParentBody.MeanRadius` — `double MeanRadius { get; }` (via `IRadius`) | `KSA/IRadius.cs` | Yes | Same (present both, IRadius) | Dead path (see #8). |
| 10 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs` | `IParentBody.Mass` — `double Mass { get; }` | `KSA/IParentBody.cs` | Yes | Same (OLD `IParentBody.cs`) | Dead path (see #8). |
| 11 | Harmony + Reflection | `unscience/Patcher.cs` -> `ksa-abstractions.lib/HotkeyGuard.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool OnKeyAll(GlfwKeyEvent keyEvent)`; resolved via `AccessTools.Method(typeof(GameSettings), nameof(GameSettings.OnKeyAll))`, prefix `ref bool __result` | `KSA/GameSettings.cs` | Yes | Same (file byte-identical 5348→5402; single overload) | Reflection by `nameof` — breaks only if method renamed/removed or return type changes from bool. |
| 12 | Direct typed API | `ksa-abstractions.lib/HotkeyGuard.cs` | `Program.ConsoleWindow.IsOpen` — `public static ConsoleWindow ConsoleWindow` + `.IsOpen` | `KSA/Program.cs` | Yes | Same (OLD `Program.cs`) | Guard so console typing isn't suppressed. |
| 13 | Render/GPU | `average-twr.lib/AverageTwrSubmod.cs`, `unscience/Mod.cs` | `Brutal.ImGuiApi.ImGui` tables/buttons/text + `SubmodUI` child window | `Brutal.ImGuiApi/*` | Yes | Same | Binding library, not KSA telemetry; compiled clean vs NEW. Standard widgets only. |
| 14 | Lifecycle | `unscience/Mod.cs` | StarMap attributes: `StarMapMod`, `StarMapImmediateLoad`, `StarMapAllModsLoaded`, `StarMapBeforeGui`, `StarMapAfterGui`, `StarMapUnload` (StarMap.API 0.3.6) | (StarMap.API package) | Yes | Same | Sampling in `OnBeforeUi`, render in `OnAfterUi`. |

**Game assets referenced** — None. No textures, meshes, audio, or part/config assets are loaded.

## geeforce

**Purpose** — Real-time g-force recorder: samples the controlled vehicle's body-frame
acceleration at 40 Hz (`SampleIntervalSec = 0.025`s, `geeforce.lib/GeeForceSubmod.cs`),
stores up to 1 h in a ring buffer, and graphs magnitude + per-axis (X/Y/Z) + jerk with
peak detection and kill-gee / jerk breach counting.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Threshold, axes/jerk display and time-window settings. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs` (called `geeforce.lib/GeeForceSubmod.cs`) | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle` | `KSA/Program.cs` | Yes | Same (OLD `Program.cs`) | Null-checked before sampling. |
| 2 | Direct typed API | `ksa-abstractions.lib/SimTimeProvider.cs` (called `geeforce.lib/GeeForceSubmod.cs`) | `Universe.GetElapsedTime()` — `public static UniverseTime GetElapsedTime()` (renamed from `GetElapsedSimTime()`/`SimTime` @5261, rev 5211) | `KSA/Universe.cs` | Yes | Same (OLD `Universe.cs`) | Returns `UniverseTime` (Int128 nanoseconds). |
| 3 | Direct typed API | `geeforce.lib/GeeForceSubmod.cs` | `UniverseTime.Seconds()` — `public double Seconds()` (instance; was `SimTime.Seconds()` before 5261) | `KSA/UniverseTime.cs` | Yes | Same (file byte-identical 5348→5402) | `UniverseTime` is a `readonly struct`; timestamps the sample. |
| 4 | Direct typed API | `geeforce.lib/GForceRecorder.cs` | `Vehicle.AccelerationBody` — `public double3 AccelerationBody => KinematicMeasurements.AccelerationBody` | `KSA/Vehicle.cs` | Yes | Same (OLD `Vehicle.cs`) | Backed by `KinematicMeasurements.AccelerationBody` (`double3` field, `KSA/KinematicMeasurements.cs`, file byte-identical). Body-frame proper accel; mod reads `.X/.Y/.Z/.Length()`. Since 5402 the integrated value includes parachute drag/torque (see 5348→5402 summary). |
| 5 | Harmony + Reflection | `unscience/Patcher.cs` -> `ksa-abstractions.lib/HotkeyGuard.cs` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool OnKeyAll(GlfwKeyEvent keyEvent)`; resolved via `nameof`, prefix `ref bool __result` | `KSA/GameSettings.cs` | Yes | Same (file byte-identical) | Reflection by `nameof`; breaks only if renamed/removed or non-bool return. |
| 6 | Direct typed API | `ksa-abstractions.lib/HotkeyGuard.cs` | `Program.ConsoleWindow.IsOpen` — `public static ConsoleWindow ConsoleWindow` + `.IsOpen` | `KSA/Program.cs` | Yes | Same (OLD `Program.cs`) | Console-typing guard. |
| 7 | Render/GPU | `geeforce.lib/GForceUI.cs` | `Brutal.ImGuiApi` draw list: `GetWindowDrawList`, `ImDrawListPtr.AddRectFilled/AddLine/AddText/AddCircleFilled`, `PushClipRect`, `GetColorU32`, `ImColor8`, `float2/float4` | `Brutal.ImGuiApi/*` | Yes | Same | Binding library, not KSA telemetry; compiled clean vs NEW. Heaviest render surface of the two mods (custom plotting). |
| 8 | Lifecycle | `unscience/Mod.cs` | StarMap attributes (same set as average-twr) | (StarMap.API package) | Yes | Same | Sampling in `OnBeforeUi`, render in `OnAfterUi`. |

**Game assets referenced** — None. No textures, meshes, audio, or part/config assets are loaded.

## Cross-cutting notes (both mods)

- **Shared chokepoint:** `Program.ControlledVehicle` and `GameSettings.OnKeyAll` are the highest-value
  members to watch — a rename/removal of either breaks every telemetry mod (and most of the suite) at once.
  `OnKeyAll` is reflection-resolved (`nameof`), so a rename surfaces as a runtime patch failure, not a
  compile error; a signature/return-type change to non-`bool` would break the prefix contract.
- **Shared helper `VehicleProvider` also exposes** `GetAllVehicles()`/`FindVehicle()`
  (`ksa-abstractions.lib/VehicleProvider.cs`) touching `Universe.CurrentSystem`
  (`KSA/Universe.cs`), `CelestialSystem.All.UnsafeAsList()` (`KSA/CelestialSystem.cs`), and `Vehicle.Id` (`IObjectId`).
  Neither average-twr nor geeforce calls these, but they compile into the shared assembly — listed here so a
  future break in those members is attributed correctly.
- Both mods are **read-only telemetry**: the only Harmony patch is the shared `HotkeyGuard`; no game state is mutated.

---

## Historical evidence

See [dated integration and upgrade reference](history/telemetry.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Current runtime release behavior

Paused recording performs no vehicle sampling. Live State reports Recording or Paused accurately. Release pauses and clears measurements. Release stops collection and clears the recorder without changing the draft.

Feature hook targets retain their existing signatures; patch ownership now follows explicit demand through the shared runtime coordinator. Native acceptance remains outstanding.
