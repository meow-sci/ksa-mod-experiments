# Telemetry / Monitoring Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the telemetry mods
(`average-twr`, `geeforce`). Every game-facing member these mods touch is enumerated
and verified against decompiled sources.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/Vehicle.cs`). **Mod code** paths are relative to the
repo root `C:\Users\Alex\repos\meow-sci\unscience`.

**How these mods are hosted (both)**

- Telemetry + game reads live in the `*.lib` project (`average-twr.lib`, `geeforce.lib`).
- Each `.lib` exposes an `ISubmod` (`MeowSci.KsaAbstractions.ISubmod`) consumed two ways:
  1. Standalone StarMap mod (`average-twr/Mod.cs`, `geeforce/Mod.cs`) — own ImGui window, F11 toggle.
  2. Embedded in the **unscience** supermod (`unscience/Mod.cs:64` `new AverageTwrSubmod()`,
     `unscience/Mod.cs:72` `new GeeForceSubmod()`) as collapsible sections.
- All game access is funneled through `ksa-abstractions.lib` helpers
  (`VehicleProvider`, `SimTimeProvider`, `HotkeyGuard`), so those helpers are part of
  each mod's effective integration surface and are catalogued per mod below.

**Summary of 4680 -> 4750 risk: NO breaking deltas.** Every typed member, enum, and
patched method these mods use is byte-for-byte identical in signature between OLD and
NEW; only source line numbers shifted as the game classes grew. Details per mod.

---

## average-twr

**Purpose** — Real-time Thrust-to-Weight Ratio and max-linear-acceleration monitor.
Samples the controlled vehicle at 100 Hz (`SampleInterval = 0.01`s,
`average-twr.lib/AverageTwrSubmod.cs:16`) and reports running mean / std-dev /
harmonic mean / "Brachi" mean for both TWR and max acceleration. Pure read-only
telemetry; the only Harmony patch is the shared `HotkeyGuard`.

**Unscience integration** — `AverageTwrSubmod : ISubmod` (`average-twr.lib/AverageTwrSubmod.cs:8`).
`Update(dt)` accumulates dt and, each 10 ms, calls
`VehicleProvider.GetControlledVehicle()` then `TwrDataReader.ReadTwr` +
`TwrDataReader.ComputeMaxAcceleration`, feeding a `TwrSampleAccumulator`
(`average-twr.lib/AverageTwrSubmod.cs:23-39`). Statistics are pure math
(`TwrStatistics.cs`, `TwrSampleAccumulator.cs`) with zero game coupling.
Instantiated by the unscience supermod (`unscience/Mod.cs:64`) and by the standalone
host (`average-twr/Mod.cs:27`).

**UI/hotkeys** — Standalone window "Average TWR / Accel", 420x260, toggled by **F11**
(`average-twr/Mod.cs:51,77`). Content (`AverageTwrSubmod.RenderContent`): status +
samples table, collapsible "TWR" and "Max Acceleration" stat tables, Start/Pause and
Reset buttons (`average-twr.lib/AverageTwrSubmod.cs:41-126`). All ImGui via
`Brutal.ImGuiApi`. Embedded mode renders the same content without window framing.

**Persistence** — None. No disk I/O. Accumulator and `_isCollecting` are in-memory and
reset on `Reset()` / mod reload. No StarMap save hooks.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs:11` (called `average-twr.lib/AverageTwrSubmod.cs:31`) | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle` | `KSA/Program.cs:254` | Yes | Same (OLD `Program.cs:253`) | Returns null when no vehicle controlled; mod null-checks. |
| 2 | Direct typed API | `average-twr.lib/TwrDataReader.cs:6` | `Vehicle.NavBallData` — `public ref readonly NavBallData NavBallData` | `KSA/Vehicle.cs:528` | Yes | Same (OLD `Vehicle.cs:493`) | `ref readonly` struct accessor. |
| 3 | Direct typed API | `average-twr.lib/TwrDataReader.cs:6` | `NavBallData.ThrustWeightRatio` — `public double ThrustWeightRatio` (field) | `KSA/NavBallData.cs:21` | ✅ | **Semantic drift (rev 5114)** | Public struct field; value 0 until flight computer populates it. **Meaning changed on 5117**: the game now computes it as `ComputeActiveThrust(AtmosphericPressure) * throttle / weight` (`KSA/Vehicle.cs:2454-2457`) instead of `TotalEngineVacuumThrust * throttle / weight`. Same field, same type, **ambient-corrected and propellant-aware** value. No code change needed; the mod's readings shift. |
| 4 | Direct typed API | `average-twr.lib/TwrDataReader.cs:26` | `Vehicle.FlightComputer` — `public FlightComputer FlightComputer { get; private set; }` | `KSA/Vehicle.cs:415` | ✅ | Same | — |
| 5 | Direct typed API | `average-twr.lib/TwrDataReader.cs:26` | `Vehicle.ComputeActiveThrust(float ambientPressure) → float` | `KSA/Vehicle.cs:6069` | ✅ | **NEW binding (replaces `VehicleConfig.TotalEngineVacuumThrust`)** | Sums `EngineController.ComputeActivePerformance(state, com, ambientPressure).ThrustMax.Length()` over active engines; skips engines with no propellant. This is the same call the game's navball TWR uses. |
| 6 | Direct typed API | `average-twr.lib/TwrDataReader.cs:26` | `FlightComputer.AmbientPressure` — `public float AmbientPressure` (field) | `KSA/FlightComputer.cs:57` | ✅ | **NEW binding (rev 5114)** | Populated from `states.Environment.AtmosphericPressure` each FC update (`KSA/FlightComputer.cs:296`). 0 in vacuum, which makes `ComputeActiveThrust` return `VacuumData` directly. |
| 7 | Direct typed API | `average-twr.lib/TwrDataReader.cs:27` | `Vehicle.TotalMass` — `public float TotalMass => _props.TotalMassPropsAsmb.Props.Mass` | `KSA/Vehicle.cs:512` | ✅ | Same | kg. Mod divides thrust/mass for max accel. |
| 8 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs:11-12` | `Vehicle.Parent` — `public IParentBody Parent => Orbit.Parent` | `KSA/Vehicle.cs:332` | Yes | Same (OLD `Vehicle.cs:299`) | Only used in `ComputeSurfaceGravity`, which is **not called** on the sampling path. Still must compile. |
| 9 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs:11` | `IParentBody.MeanRadius` — `double MeanRadius { get; }` (via `IRadius`) | `KSA/IRadius.cs:5` | Yes | Same (present both, IRadius) | Dead path (see #8). |
| 10 | Direct typed API (dead path) | `average-twr.lib/TwrDataReader.cs:12` | `IParentBody.Mass` — `double Mass { get; }` | `KSA/IParentBody.cs:11` | Yes | Same (OLD `IParentBody.cs:11`) | Dead path (see #8). |
| 11 | Harmony + Reflection | `average-twr/Patcher.cs:19` -> `ksa-abstractions.lib/HotkeyGuard.cs:21,23` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool OnKeyAll(GlfwKeyEvent keyEvent)`; resolved via `AccessTools.Method(typeof(GameSettings), nameof(GameSettings.OnKeyAll))`, prefix `ref bool __result` | `KSA/GameSettings.cs:2379` | Yes | Same (OLD `GameSettings.cs:2347`) | Reflection by `nameof` — breaks only if method renamed/removed or return type changes from bool. |
| 12 | Direct typed API | `ksa-abstractions.lib/HotkeyGuard.cs:38` | `Program.ConsoleWindow.IsOpen` — `public static ConsoleWindow ConsoleWindow` + `.IsOpen` | `KSA/Program.cs:246` | Yes | Same (OLD `Program.cs:245`) | Guard so console typing isn't suppressed. |
| 13 | Render/GPU | `average-twr.lib/AverageTwrSubmod.cs:41-157`, `average-twr/Mod.cs:75-81` | `Brutal.ImGuiApi.ImGui` tables/buttons/text + `SubmodUI` child window | `Brutal.ImGuiApi/*` | Yes | Same | Binding library, not KSA telemetry; compiled clean vs NEW. Standard widgets only. |
| 14 | Lifecycle | `average-twr/Mod.cs:19-73` | StarMap attributes: `StarMapMod`, `StarMapImmediateLoad`, `StarMapAllModsLoaded`, `StarMapBeforeGui`, `StarMapAfterGui`, `StarMapUnload` (StarMap.API 0.3.6) | (StarMap.API package) | Yes | Same | Sampling in `OnBeforeUi`, render in `OnAfterUi`. |

**Game assets referenced** — None. No textures, meshes, audio, or part/config assets are loaded.

**Update-risk findings (5018 -> 5117)**

- 🔴 **BREAK, FIXED — `VehicleConfigInfo.TotalEngineVacuumThrust` removed (rev 5114).** Build error
  CS1061 at `average-twr.lib/TwrDataReader.cs:17`. Rev 5114 deleted the whole vacuum-referenced
  aggregate family from `FlightComputer.VehicleConfigInfo` — `TotalEngineVacuumThrust`,
  `TotalEngineVacuumMassFlowRate`, `TotalEngineExhaustVelocity`, `TotalEngineIsp` — along with the
  loop in `UpdateVehicleConfig` that filled them. Changelog: *"Made the flight computer aware when
  engines run out of propellant and stop taking credit for the thrust they produce in burn planning"*
  / *"…dV and TWR ratings reflect the engines that are actually capable of producing thrust. TWR also
  takes atmospheric pressure into account."*
  **Fix applied:** `ComputeMaxAcceleration` now calls
  `vehicle.ComputeActiveThrust(vehicle.FlightComputer.AmbientPressure)` (rows 5–6), which is exactly
  what the game's own navball TWR uses (`KSA/Vehicle.cs:2454`).
- ⚠️ **Semantic drift, no code change — `NavBallData.ThrustWeightRatio` changed meaning (rev 5114).**
  Same field, same `double`, but the game now derives it from ambient-corrected, propellant-aware
  thrust instead of vacuum thrust. `ReadTwr` therefore reports different numbers on 5117 with no
  edit. Choosing `ComputeActiveThrust` for row 5 keeps `ReadTwr` and `ComputeMaxAcceleration`
  measuring the *same* quantity, as they did before — the alternative (reconstructing vacuum thrust
  from `EngineController.VacuumData`) would have silently desynced the two numbers in the UI.
- `NavBallData.DeltaVInVacuum` was renamed to `NavBallData.DeltaV` (same rev, same reason). **Not
  referenced by any mod in this repo** — no action.
- All other typed members + the patched `OnKeyAll` are signature-identical 5018→5117 (line shifts only).
- **README drift (not a break, but misleading for triage):** `average-twr/README.md` claims
  `ReadTwr` returns `float` and `ComputeMaxAcceleration` returns `vehicle.TotalThrust / vehicle.TotalMass`.
  Actual code returns `double` from `NavBallData.ThrustWeightRatio` and computes
  `Vehicle.ComputeActiveThrust(FlightComputer.AmbientPressure) / TotalMass`. **`Vehicle.TotalThrust` does not exist**
  in either decomp version (the only `TotalThrust` is `RocketControllerData...Performance.TotalThrust`
  used for a part tooltip at `KSA/Vehicle.cs:3739`). Recommend correcting the README.
- **Opportunity (rev 4696), superseded:** rev 4696 added a static thrust-from-template helper on
  `RocketControllerData`. Moot as of 5117 — the mod now goes through `Vehicle.ComputeActiveThrust`,
  which wraps `EngineController.ComputeActivePerformance` and is the game's own path.
- `ComputeSurfaceGravity` (`TwrDataReader.cs:8-13`, reads `Parent.Mass`/`Parent.MeanRadius`, G=6.6743e-11)
  is dead code on the sampling path but still compiles against the live API — keep it consistent if `IParentBody` changes.

---

## geeforce

**Purpose** — Real-time g-force recorder: samples the controlled vehicle's body-frame
acceleration at 40 Hz (`SampleIntervalSec = 0.025`s, `geeforce.lib/GeeForceSubmod.cs:13`),
stores up to 1 h in a ring buffer, and graphs magnitude + per-axis (X/Y/Z) + jerk with
peak detection and kill-gee / jerk breach counting.

**Unscience integration** — `GeeForceSubmod : ISubmod` (`geeforce.lib/GeeForceSubmod.cs:8`).
`Update(dt)` drives a fixed-step accumulator; each 25 ms it calls
`VehicleProvider.GetControlledVehicle()` and
`SimTimeProvider.GetElapsedTime().Seconds()`, then `GForceRecorder.RecordSample(vehicle, simTime)`
(`geeforce.lib/GeeForceSubmod.cs:25-38`). The recorder reads `vehicle.AccelerationBody`
(`geeforce.lib/GForceRecorder.cs:96`) and converts to Gs with a hard-coded
`StandardGravity = 9.80665` (`GForceRecorder.cs:19`) — note it does **not** divide by the
local body's surface gravity. All buffer math, jerk, peak, and breach logic is pure C#.
Rendering is delegated to `GForceUI` (custom ImGui draw-list plotting). Instantiated by the
unscience supermod (`unscience/Mod.cs:72`) and the standalone host (`geeforce/Mod.cs:27`).

**UI/hotkeys** — Standalone window "G-Force Monitor", 560x680, toggled by **F11**
(`geeforce/Mod.cs:51,77`). Content (`GForceUI.RenderContent`): stats table, custom
line/grid graph via `ImDrawListPtr`, kill-gees `DragFloat`, scrub `SliderFloat`, window-size
buttons (30s..1h), Record/Pause, Clear, Axes/Jerk checkboxes (`geeforce.lib/GForceUI.cs:42-64,439-488`).

**Persistence** — None. Ring buffer is in-memory and lost on reload/`Clear()`. UI prefs
(`_killGeesThreshold`, `_selectedWindowIdx`, `_showAxes`, `_showJerk`, `_isLive`, scroll
offset) are **static fields** in `GForceUI` (`geeforce.lib/GForceUI.cs:9-41`) — process-global,
not serialized, reset to defaults on reload. `IsRecording` defaults `false`
(`GForceRecorder.cs:34`), so nothing is captured until the user presses Record.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API | `ksa-abstractions.lib/VehicleProvider.cs:11` (called `geeforce.lib/GeeForceSubmod.cs:31`) | `Program.ControlledVehicle` — `public static Vehicle? ControlledVehicle` | `KSA/Program.cs:254` | Yes | Same (OLD `Program.cs:253`) | Null-checked before sampling. |
| 2 | Direct typed API | `ksa-abstractions.lib/SimTimeProvider.cs:9` (called `geeforce.lib/GeeForceSubmod.cs:34`) | `Universe.GetElapsedSimTime()` — `public static SimTime GetElapsedSimTime()` | `KSA/Universe.cs:1991` | Yes | Same (OLD `Universe.cs:1440`) | Returns `SimTime`. |
| 3 | Direct typed API | `geeforce.lib/GeeForceSubmod.cs:34` | `SimTime.Seconds()` — `public double Seconds()` (instance) | `KSA/SimTime.cs:67` | Yes | Same (OLD `SimTime.cs:67`) | `SimTime` is `readonly struct`; timestamps the sample. |
| 4 | Direct typed API | `geeforce.lib/GForceRecorder.cs:96` | `Vehicle.AccelerationBody` — `public double3 AccelerationBody => KinematicMeasurements.AccelerationBody` | `KSA/Vehicle.cs:518` | Yes | Same (OLD `Vehicle.cs:485`) | Backed by `KinematicMeasurements.AccelerationBody` (`double3` field, `KSA/KinematicMeasurements.cs:9`). Body-frame proper accel; mod reads `.X/.Y/.Z/.Length()`. |
| 5 | Harmony + Reflection | `geeforce/Patcher.cs:19` -> `ksa-abstractions.lib/HotkeyGuard.cs:21,23` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool OnKeyAll(GlfwKeyEvent keyEvent)`; resolved via `nameof`, prefix `ref bool __result` | `KSA/GameSettings.cs:2379` | Yes | Same (OLD `GameSettings.cs:2347`) | Reflection by `nameof`; breaks only if renamed/removed or non-bool return. |
| 6 | Direct typed API | `ksa-abstractions.lib/HotkeyGuard.cs:38` | `Program.ConsoleWindow.IsOpen` — `public static ConsoleWindow ConsoleWindow` + `.IsOpen` | `KSA/Program.cs:246` | Yes | Same (OLD `Program.cs:245`) | Console-typing guard. |
| 7 | Render/GPU | `geeforce.lib/GForceUI.cs:180-392` | `Brutal.ImGuiApi` draw list: `GetWindowDrawList`, `ImDrawListPtr.AddRectFilled/AddLine/AddText/AddCircleFilled`, `PushClipRect`, `GetColorU32`, `ImColor8`, `float2/float4` | `Brutal.ImGuiApi/*` | Yes | Same | Binding library, not KSA telemetry; compiled clean vs NEW. Heaviest render surface of the two mods (custom plotting). |
| 8 | Lifecycle | `geeforce/Mod.cs:19-73` | StarMap attributes (same set as average-twr) | (StarMap.API package) | Yes | Same | Sampling in `OnBeforeUi`, render in `OnAfterUi`. |

**Game assets referenced** — None. No textures, meshes, audio, or part/config assets are loaded.

**Update-risk findings (4680 -> 4750)**

- No breaking deltas. `Vehicle.AccelerationBody`, `Universe.GetElapsedSimTime`, `SimTime.Seconds`,
  `Program.ControlledVehicle`, and the patched `OnKeyAll` are all signature-identical OLD->NEW (line shifts only).
- **`Situation` enum — referenced in README only, NOT read by code.** `geeforce/README.md` mentions
  reading `vehicle.Velocity.GetBodyFrameAcceleration()` and a `Situation` enum, but the actual sampler
  uses `vehicle.AccelerationBody` and never touches `Situation`. For completeness, `KSA/Situation.cs`
  is **identical** in both versions: `enum Situation : byte` with `Maneuvering=0, Freefall=1, Rolling=2,
  Landed=3, Sailing=4, Floating=5, Dragging=6, Bottomed=7`. It is **not** a `[Flags]` bitfield in either
  4680 or 4750 (any bitfield change predates 4680). Rev 4704's aerostat "Landed while floating" change
  is a semantic/value reuse within this stable enum and has **no effect** on geeforce.
- **README drift (not a break):** the documented acceleration source (`Velocity.GetBodyFrameAcceleration()`),
  the g-force formula (divide by computed surface gravity), and the `GForceSample` shape (float fields named
  `X/Y/Z/Magnitude`) do not match the implementation (`double` fields `Longitudinal/Lateral/Normal/Magnitude`,
  fixed 9.80665 divisor, `AccelerationBody`). Recommend correcting the README to reflect `AccelerationBody`.
- Electrical power/energy refactor (rev 4681) and ground-impact kinetic-energy change (rev 4684) are
  irrelevant — geeforce reads neither power/energy nor impact data.

---

## Cross-cutting notes (both mods)

- **Shared chokepoint:** `Program.ControlledVehicle` and `GameSettings.OnKeyAll` are the highest-value
  members to watch — a rename/removal of either breaks every telemetry mod (and most of the suite) at once.
  `OnKeyAll` is reflection-resolved (`nameof`), so a rename surfaces as a runtime patch failure, not a
  compile error; a signature/return-type change to non-`bool` would break the prefix contract.
- **Shared helper `VehicleProvider` also exposes** `GetAllVehicles()`/`FindVehicle()`
  (`ksa-abstractions.lib/VehicleProvider.cs:14-29`) touching `Universe.CurrentSystem`
  (`KSA/Universe.cs:92`, OLD `91`), `CelestialSystem.All.UnsafeAsList()`, and `Vehicle.Id` (`IObjectId`).
  Neither average-twr nor geeforce calls these, but they compile into the shared assembly — listed here so a
  future break in those members is attributed correctly.
- Both mods are **read-only telemetry**: the only Harmony patch is the shared `HotkeyGuard`; no game state is mutated.
