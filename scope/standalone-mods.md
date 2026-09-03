<!-- These mods live in the repo but are NOT bundled in the unscience supermod; cataloged as secondary reference. -->
# Standalone Mods — Game Integration Scope

These mods live in the repo but are **NOT** bundled in the unscience supermod; cataloged as secondary reference for KSA game-update breakage.

Covered: `marque`, `byo-music`, `steely-eyed-missile-kitten`, `mesh-deform`, `stampy`.

**Verified game versions**

- NEW decomp `2026.9.7.5402`: `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD decomp `2026.8.22.5348`: `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`
- NEW Content: `~/repos/meow-sci/ksa-game-assemblies/current/Content`

`Decomp path (NEW)` is relative to the NEW decomp root (namespace-foldered, e.g. `KSA/Vehicle.cs`); line numbers are 5402 and "OLD" means 5348.
`Mod code` paths are relative to the repo root `~/repos/meow-sci/unscience`.
All five mods **compile clean against NEW (5402)**, so every *typed* member below is implicitly present in NEW; the focus is (a) string-based reflection / asset-id / GLSL-anchor strings that the compiler cannot check, and (b) behavioral/enum/asset deltas.

**Headline risk summary**

- `marque` — NO breaking deltas.
- `byo-music` — NO breaking deltas (asset id `SabotageMusic` is a placeholder, never stock in either version).
- `steely-eyed-missile-kitten` — NO breaking deltas. Full Vehicle telemetry surface + `Situation` enum are signature-identical 4680↔4750. Only latent risk is its string-compare against `Situation` names, which is currently safe.
- **`mesh-deform` — BREAKING (now guarded, Phase 2).** The GLSL struct anchor it rewrites (`MeshIndirect.vert`) was refactored in 4750 *and* the color pipeline stopped consulting `ShaderReference.Shader`; runtime shader activation can no longer take effect. The mod now self-detects this and disables activation with a UI notice.
- `stampy` — template/skeleton, NO real game integration beyond `HotkeyGuard`. NO deltas.

---

## marque

**Purpose** — Adds a **Marque** submenu to the game's View menu bar for toggling orbit-line visibility on vehicles and celestial bodies (bulk All/None/Planetoids, a sorted Vehicles submenu, a recursive SOI celestial tree, and a filterable "Everything" list).

**Standalone entry (class+file)** — `MeowSci.Marque.Mod` (`marque/Mod.cs`, `[StarMapMod]`). Menu logic lives in `MeowSci.MarqueLib.MarqueLib` (`marque.lib/MarqueLib.cs`). Not an `ISubmod`; not in unscience.

**UI/hotkeys** — Injected View-menu submenu (always visible via the Harmony prefix). The standalone `Mod.cs` also has a vestigial F11 debug window ("press me" button) unrelated to function.

**Persistence** — None (toggles live game state directly).

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Harmony prefix | `marque/Patcher.cs:39-44` | `GaugeCanvas.OnDrawMenuBar()` — `public static void` | `KSA/GaugeCanvas.cs:1396` | Yes | None (OLD `:1396`; body `1396-1440` byte-identical; still static/void/no-args) | Prefix runs inside the View menu; renders `BeginMenu("Marque")`. Static target → empty-arg prefix valid. `GaugeCanvas.cs`'s 5402 diff is only `CanvasesRender`/`Render` taking `IViewport`. |
| 2 | Direct API | `marque.lib/MarqueLib.cs:49,52,56,74,77,84,105-117,148-169` | `IOrbiter.ShowOrbit` — `ref bool ShowOrbit { get; }` (set) | `KSA/IOrbiter.cs:20` | Yes | None | Toggling via `o.ShowOrbit = true/!show`. `ref bool` property; assignment compiles + works. |
| 3 | Direct API | `marque.lib/MarqueLib.cs:45` (via `CelestialProvider`) | `Universe.CurrentSystem.All.UnsafeAsList().OfType<IOrbiter>()` | `KSA/Universe.cs:94`, `KSA/CelestialSystem.cs:64` | Yes | None | `CurrentSystem` is `CelestialSystem? {get;private set;}`. |
| 4 | Direct API | `marque.lib/MarqueLib.cs:93` | `CelestialSystem.GetWorldSun() : StellarBody?` | `KSA/CelestialSystem.cs:565`; call site `KSA/Universe.cs:177` | Yes | None | Root of the celestial tree; `sun.Id`, `sun.Children`. |
| 5 | Direct API (typecheck) | `marque.lib/MarqueLib.cs:56,114,157-159` | `is not Asteroid and not Comet` | `KSA/Asteroid.cs:3`, `KSA/Comet.cs:3` | Yes | None (5402 only retyped `Asteroid.OnDrawUi(IGameViewport)`) | Both `: MinorBody`. "Planetoids" filter. |
| 6 | Direct API | `marque.lib/MarqueLib.cs:101,126-135` | `IParentBody.Children`, `Celestial`, `Astronomical.Id` | `KSA/Celestial.cs:23` (`: Astronomical,…,IParentBody`), `KSA/Astronomical.cs:12,104` | Yes | None | Recursive SOI tree walk. |
| 7 | Direct API | `marque.lib/MarqueLib.cs:63,71,84` (via `VehicleProvider`) | `Vehicle.Id`; `Vehicle.ShowOrbit` (as `IOrbiter`) | `KSA/Vehicle.cs:374` (`ShowOrbit`) | Yes | None | Vehicles submenu. |
| 8 | Harmony prefix (HotkeyGuard) | `marque/Patcher.cs:17`, `marque/HotkeyGuard.cs:20-22` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None (file byte-identical) | NOTE: marque ships a **local copy** of `HotkeyGuard` (`namespace MeowSci.Marque`) instead of the shared `MeowSci.KsaAbstractions.HotkeyGuard`. Functionally identical; deviates from the "use the shared guard" rule. |

**Game assets referenced** — None.

**Update-risk findings (4750→5018)** — No compile or signature deltas: `GaugeCanvas.OnDrawMenuBar()`
(the Harmony target), `IOrbiter.ShowOrbit`, and every celestial/vehicle type are signature-identical,
as is `GameSettings.OnKeyAll` for the local `HotkeyGuard` copy. ⚠ **But the menu marque injects into
moved**: rev 4940 added a **Hud dropdown to the file bar** and relocated the gauge enable/disable
toggles there from the View dropdown, and revs 4919/4959/5003 pulled the sequence UI, burn UI and all
pop-ups into the gauge-canvas system. `OnDrawMenuBar` still runs, but *where* marque's entries land
relative to the reorganised menus **needs a live pass**. Minor hygiene note, still open: the
duplicated local `HotkeyGuard` should be consolidated onto `ksa-abstractions.lib`.

#### Carried over from the 4680→4750 review — No breaking deltas detected. All patch targets, the `IOrbiter.ShowOrbit` property, and every celestial/vehicle type are signature-identical between versions.

---

## byo-music

**Purpose** — "Bring Your Own Music" — load a `MusicPlayList` asset by id from `ModLibrary` and play it via the KSA/FMOD audio API. Currently a one-button demo wired to the id `"SabotageMusic"`.

**Standalone entry (class+file)** — `MeowSci.ByoMusic.Mod` (`byo-music/Mod.cs`, `[StarMapMod]`). Playback helper: `MeowSci.ByoMusicLib.MusicPlayer` (`byo-music.lib/MusicPlayer.cs`). Not an `ISubmod`; not in unscience.

**UI/hotkeys** — F11 toggles a window with a single "Listen all ya'll" button (`Mod.cs:90-105`) that fetches and plays the `SabotageMusic` playlist.

**Persistence** — None.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Direct API | `byo-music.lib/MusicPlayer.cs:8` | `ModLibrary.Get<MusicPlayList>(string)` | `KSA/ModLibrary.cs`, `KSA/MusicPlayList.cs:6` (`: SoundReference`) | Yes | None | Generic asset fetch; returns null on miss. |
| 2 | Direct API | `byo-music.lib/MusicPlayer.cs:10` | `MusicPlayList.PlayMusic(out ChannelWrapper? iChannel, ulong delaySamples = 0)` | `KSA/MusicPlayList.cs:21` | Yes | None | Mod calls `PlayMusic(out _)`; out-param + optional arg match. Routes through `GameAudio.System` (FMOD `Brutal.FmodApi`). |
| 3 | Asset (sound) | `byo-music/Mod.cs:99` | `MusicPlayList` asset id `"SabotageMusic"` | `Content/Core/Sounds.xml` (stock `<MusicPlaylist>` blocks) | **No** (not stock) | None — never stock in 4680 or 4750 | Stock playlist ids are location-based: `EarthSOIMusic`, `LunaSOIMusic`, … (`Sounds.xml:522+`). `Get<>("SabotageMusic")` returns null → guarded no-op (`Mod.cs:101`). Dead unless the user ships their own `SabotageMusic` asset. Pre-existing, not a regression. |
| 4 | Harmony prefix (HotkeyGuard) | `byo-music/Patcher.cs:19` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None | Uses shared `MeowSci.KsaAbstractions.HotkeyGuard`. |

**Game assets referenced** — `MusicPlayList "SabotageMusic"` (placeholder; not present in stock Content). Stock alternatives that *do* resolve: `EarthSOIMusic`, `LunaSOIMusic`, and three more in `Content/Core/Sounds.xml`.

**Update-risk findings (4680→4750)** — No breaking deltas detected. `MusicPlayList.PlayMusic` signature and `ModLibrary.Get<T>` are unchanged. The only non-functional condition (missing `SabotageMusic` asset) predates 4680 and is handled by a null check.

---

## steely-eyed-missile-kitten

**Purpose** — Passive telemetry monitor + flight-event detector + YAML mission tracker, persisting to SQLite. Samples **all** vehicles at a configurable rate (default 2 Hz), detects 8 event types, and evaluates mission condition trees. This is the richest game-read surface of the five.

**Standalone entry (class+file)** — `MeowSci.SteelyEyedMissileKitten.Mod` (`steely-eyed-missile-kitten/Mod.cs`, `[StarMapMod]`). All game reads are funneled through `MeowSci.SteelyEyedMissileKittenLib.Telemetry.VehicleTelemetry` (`steely-eyed-missile-kitten.lib/Telemetry/VehicleTelemetry.cs`) — single chokepoint by design. Not an `ISubmod`; not in unscience.

**UI/hotkeys** — F11 toggles a 3-tab window (Telemetry / Events / Missions) — `Mod.cs:110-166`.

**Persistence** — SQLite via `Microsoft.Data.Sqlite` at `Documents/My Games/Kitten Space Agency/.steely-eyed-missile-kitten/events.db` (`Mod.cs:58-66`). Stores flight events + mission progress. Mission YAML loaded from a bundled `missions/` dir and a user dir (`Mod.cs:71-75`). These are local-file, not game-API, integration.

> Note: the UI (`UI/MonitorUI.cs`, `UI/MissionUI.cs`), `EventDetector`, `MissionEvaluator`, and `MissionManager` read **only** from `TelemetrySnapshot` POCOs — no direct game calls outside `VehicleTelemetry` + the abstraction providers. Verified by grep.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Direct API | `…/VehicleTelemetry.cs:40` | `Vehicle.Id` (string, via `IObjectId`) | `KSA/Vehicle.cs` | Yes | None | Identity key. |
| 2 | Direct API | `…/VehicleTelemetry.cs:44` | `Vehicle.Parent` — `IParentBody Parent => Orbit.Parent` | `KSA/Vehicle.cs:332` | Yes | None (OLD `:299`) | |
| 3 | Direct API | `…/VehicleTelemetry.cs:46` | `IParentBody.Id` (via `IObjectId`) | `KSA/IParentBody.cs`, `KSA/Astronomical.cs` | Yes | None | SOI-change detection key. |
| 4 | Direct API | `…/VehicleTelemetry.cs:47` | `IParentBody.MeanRadius` (double) | `KSA/Vehicle.cs:482` (+ `IRadius`) | Yes | None | Used for Ap/Pe altitude (radius subtraction). |
| 5 | Direct API | `…/VehicleTelemetry.cs:50` | `IParentBody.GetAtmosphereReference()` → `AtmosphereReference?` | `KSA/Vehicle.cs:2221` (call site), `KSA/PhysicalAtmosphereReference.cs` | Yes | None | Null on airless bodies. |
| 6 | Direct API | `…/VehicleTelemetry.cs:52` | `AtmosphereReference.Physical.Height.InMeters()` | `KSA/PhysicalAtmosphereReference.cs:23`, `KSA/DistanceReference.cs:148` | Yes | None | `Physical` is `PhysicalAtmosphereReference`; `Height` is `DistanceReference`. |
| 7 | Direct API | `…/VehicleTelemetry.cs:55` | `Vehicle.GetBarometricAltitude()` : double | `KSA/Vehicle.cs:2175` | Yes | None (OLD `:2085`) | |
| 8 | Direct API | `…/VehicleTelemetry.cs:56` | `Vehicle.GetRadarAltitude()` : double | `KSA/Vehicle.cs:2180` | Yes | None (OLD `:2090`) | |
| 9 | Direct API | `…/VehicleTelemetry.cs:59` | `Vehicle.OrbitalSpeed` — `double => GetVelocityCci().Length()` | `KSA/Vehicle.cs:532` | Yes | None (OLD `:497`) | |
| 10 | Direct API | `…/VehicleTelemetry.cs:60` | `Vehicle.GetSurfaceSpeed()` : double | `KSA/Vehicle.cs:2169` | Yes | None (OLD `:2079`) | |
| 11 | Direct API | `…/VehicleTelemetry.cs:61` | `Vehicle.GetInertialSpeed()` : double | `KSA/Vehicle.cs:2164` | Yes | None (OLD `:2074`) | |
| 12 | Direct API | `…/VehicleTelemetry.cs:64` | `Vehicle.Orbit` — `Orbit Orbit => Patch.Orbit` | `KSA/Vehicle.cs:330` | Yes | None (OLD `:297`) | |
| 13 | Direct API | `…/VehicleTelemetry.cs:64-73` | `Orbit.Apoapsis / Periapsis / Eccentricity / Inclination / Period / SemiMajorAxis` (double) | `KSA/Orbit.cs` (members exercised in `KSA/IOrbiter.cs:97-106`) | Yes | None | All guarded with `double.IsFinite`. |
| 14 | Direct API | `…/VehicleTelemetry.cs:76` | `Vehicle.TotalMass` (float) | `KSA/Vehicle.cs:512` | Yes | None (OLD `:479`) | Mass props — unaffected by 4681 electrical refactor. |
| 15 | Direct API | `…/VehicleTelemetry.cs:77` | `Vehicle.InertMass` (float) | `KSA/Vehicle.cs:514` | Yes | None (OLD `:481`) | |
| 16 | Direct API | `…/VehicleTelemetry.cs:78` | `Vehicle.PropellantMass` (float) | `KSA/Vehicle.cs:516` | Yes | None (OLD `:483`) | |
| 17 | Direct API | `…/VehicleTelemetry.cs:81` | `Vehicle.AccelerationBody` — `double3 => KinematicMeasurements.AccelerationBody` | `KSA/Vehicle.cs:518` | Yes | None (OLD `:485`) | G-force = `.Length()/9.80665` — same constant the game uses (`Vehicle.cs:4277`). |
| 18 | Direct API | `…/VehicleTelemetry.cs:85` | `Vehicle.Situation` — `Situation Situation => _props.Situation` | `KSA/Vehicle.cs:494` | Yes | None (OLD `:461`) | |
| 19 | Direct API (ext) | `…/VehicleTelemetry.cs:86` | `SituationEx.HasAnyContact(this Situation) : bool` | `KSA/SituationEx.cs:18` | Yes | None | Extension; used for liftoff/landing logic. |
| 20 | **Enum-name string-compare** | `…/VehicleTelemetry.cs:87-89,149`; `…/Events/EventDetector.cs:62,83,103`; `…/UI/MonitorUI.cs:117-118` | `Situation` names via `.ToString()`: `"Landed","Floating","Sailing","Rolling","Freefall","Maneuvering"` | `KSA/Situation.cs:3` — `enum Situation : byte` (8 states) | Yes | None — enum **byte-identical** in both versions | **Latent risk surface.** Snapshot stores `Situation.ToString()`; detector/UI string-compare. A future enum **rename** would break silently (compile-clean). Currently safe: `Maneuvering=0,Freefall=1,Rolling=2,Landed=3,Sailing=4,Floating=5,Dragging=6,Bottomed=7` unchanged 4680↔4750. Mod ignores `Dragging`/`Bottomed` (by design). |
| 21 | Direct API | `…/VehicleTelemetry.cs:101` | `PhysicalAtmosphereReference.GetAtmosphericPressureAtAltitude(double) : double` | `KSA/PhysicalAtmosphereReference.cs:80` | Yes | None | In try/catch. |
| 22 | Direct API | `…/VehicleTelemetry.cs:102` | `PhysicalAtmosphereReference.GetAtmosphericDensityAtAltitude(double) : double` | `KSA/PhysicalAtmosphereReference.cs:85` | Yes | None | |
| 23 | Direct API | `…/VehicleTelemetry.cs:111` | `Vehicle.GetPositionEcl() : double3` | `KSA/Vehicle.cs:613` | Yes | None | Distance math between snapshots. |
| 24 | Direct API | `…/Monitoring/MonitoringLoop.cs:45`, `Mod.cs:158` (via `SimTimeProvider`) | `Universe.GetElapsedSimTime() : SimTime`; `SimTime.Seconds()` | `KSA/Universe.cs`, `KSA/SimTime.cs` | Yes | None | |
| 25 | Direct API | `…/Monitoring/MonitoringLoop.cs:46` (via `VehicleProvider`) | `Universe.CurrentSystem.All.UnsafeAsList().OfType<Vehicle>()` | `KSA/Universe.cs:92` | Yes | None | All-vehicle enumeration. |
| 26 | Harmony prefix (HotkeyGuard) | `steely-eyed-missile-kitten/Patcher.cs:19` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None | Shared `KsaAbstractions.HotkeyGuard`. |

**Game assets referenced** — None (SQLite + YAML are local files, not game assets).

**Update-risk findings (4680→4750)** — No breaking deltas detected.

- Every Vehicle telemetry member (Ap/Pe/ecc/incl/period/sma, baro/radar alt, orbital/surface/inertial speed, total/inert/propellant mass, `AccelerationBody`, `Situation`, `GetPositionEcl`) is **signature-identical**; only source line numbers shifted.
- `Situation` enum is unchanged (8 states, `: byte`) — relevant to changelog 4704 "aerostats (Situation)": the `Floating`/`Sailing` states the splashdown logic relies on exist in both, and no new state names were introduced, so the string-compare detector is intact.
- Changelog 4681 (electrical/power refactor) does not intersect this mod — it reads mass/propellant, never electrical/energy.
- Changelog 4684 (ground-impact threshold speed→kinetic energy) is internal to the game's damage model; this mod's landing/splashdown detection keys off `Situation` + `HasAnyContact()`, not impact thresholds — unaffected.
- Only standing caution is structural (item 20): the `Situation` string-compare would not be caught by the compiler if the enum is ever renamed. Consider switching to enum-value comparison to harden.

---

## mesh-deform

**Purpose** — Per-part GPU vertex deformation (radial dents/bulges). Captures the rendered `Part` via a Harmony prefix, injects a 2-float payload into `PerInstanceData` padding, and **rewrites the `MeshIndirect.vert` GLSL at runtime** to displace vertices. Visual-only, session-only.

**Standalone entry (class+file)** — `MeowSci.MeshDeform.Mod` (`mesh-deform/Mod.cs`, `[StarMapMod]`), which hosts `MeowSci.MeshDeformLib.MeshDeformSubmod : ISubmod` (`mesh-deform.lib/MeshDeformSubmod.cs`). The `ISubmod` exists but mesh-deform is **not** wired into the unscience supermod.

**UI/hotkeys** — F11 window; `Active` checkbox triggers shader activation; vehicle/part pickers, magnitude/radius sliders, Apply/Clear (`MeshDeformSubmod.cs`).

**Persistence** — None (in-memory `Dictionary<Part,…>` keyed by reference; cleared on unload).

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Harmony prefix (AccessTools) | `mesh-deform.lib/MeshDeformPatches.cs:34-43` | `PartModelModule.UpdateRenderData(in double4x4, bool, **IViewport**, int)` | `KSA/PartModelModule.cs:87` | Yes | **Retyped** (OLD `:87` took `Viewport`); still a single overload, prefix binds only `__instance` | Captures `Part` into a `ThreadLocal`. 5402 body also switched the light-off bit to `Parent.FullPart.IsLightSwitchedOff()` (`:106`) — irrelevant to the prefix. |
| 2 | Direct API (in prefix) | `mesh-deform.lib/MeshDeformPatches.cs:101` | `PartModelModule.Parent` (Part, from `ModuleBase.Parent`) | `KSA/ModuleBase.cs:31` | Yes | None | `__instance.Parent`. |
| 3 | Harmony prefix (AccessTools) | `mesh-deform.lib/MeshDeformPatches.cs:51-60` | `PartModel.AddInstance(PerInstanceData, **IViewport**, int)` | `KSA/PartModel.cs:408` | Yes | **Retyped + new gate** (OLD `:407` took `Viewport`): body now starts `if (!viewport.HasAny(ViewportOptionFlags.RenderPartModels)) return;` (`:410-413`) and gates raytrace registration on `viewport.HasAll(UseRaytracing)` (`:415`) | Single overload → no ambiguity. Prefix takes `ref` first arg only, so the retype is invisible to it; the prefix still runs before the new early-return (harmless — it is a no-op unless `ShadersActive`). |
| 4 | Struct reinterpret (`Unsafe.As`) | `mesh-deform.lib/MeshDeformManager.cs:127-134`, `MeshDeformPatches.cs:118-122` | `PartModel.PerInstanceData` = `{float4x4 ModelMatrix; int StateBitFlag; uint EmissiveColor; private int packing1; float Wetness}` | `KSA/PartModel.cs:332-342` | Yes | None — layout identical (OLD `:331-341`) | Reuses `packing1`@72 / `Wetness`@76 as `DeformMagnitude/DeformRadius`. **See finding re: GLSL collision.** |
| 5 | Direct API | `mesh-deform.lib/MeshDeformShaders.cs:76,110` | `PartModelRenderer.ColorData.Rebuild()` — `public static void` | `KSA/PartModelRenderer.cs:18` (`ColorData`), `:275` (`Rebuild`) | Yes | None (5402 diff = `IViewport` retypes + `viewport.IsMain()`) | Rebuilds pipelines after shader swap. Colour pipeline still compiles `MeshIndirectVert` per variant via `CompileVariantWithCustomOptions` (`:109-125`), ignoring `.Shader`. |
| 6 | Direct API + asset id | `mesh-deform.lib/MeshDeformShaders.cs:73,154,221` | `ModLibrary.Get<ShaderReference>("MeshIndirectVert")` | `KSA/ShaderReference.cs:21`; `KSA/ModLibrary.cs:1042`; asset reg `Content/Core/DefaultAssets.xml:55` | Yes | None | Asset id `MeshIndirectVert` → file `Shaders/Mesh/MeshIndirect.vert`. Id present in NEW. |
| 7 | Reflection (string) | `mesh-deform.lib/MeshDeformShaders.cs:98-106` | `ShaderReference.DoLoad()` — `internal override void` via `GetMethod("DoLoad", NonPublic\|Public\|Instance)` | `KSA/ShaderReference.cs:168` | Yes | None (file byte-identical) | Restores original shader on deactivate. |
| 8 | Reflection (string) | `mesh-deform.lib/MeshDeformShaders.cs:320-329` | `ShaderReference.Shader { get; private set; }` setter / fallback field `<Shader>k__BackingField` | `KSA/ShaderReference.cs:34` | Yes | None | Private setter present → primary path used. |
| 9 | Reflection (string) | `mesh-deform.lib/MeshDeformShaders.cs:421` | `ShaderReference.ModPath` (string, on `FileReference` base) | `KSA/ShaderReference.cs:74` (uses `base.ModPath`); `KSA/FileReference.cs:24` | Yes | None | Resolves on-disk shader path. |
| 10 | Direct API | `mesh-deform.lib/MeshDeformShaders.cs:426` | `ShaderReference.LocalPath` | `KSA/ShaderReference.cs:50` (usage) | Yes | None | Fallback path source. |
| 11 | Reflection (string, cross-asm) | `mesh-deform.lib/MeshDeformShaders.cs:458-471` | `RenderCore.ShaderModuleUtils.FromFile(Device, string, out VkShaderStageFlags, CompileOptions)` | `KSA/ShaderReference.cs:109,129` (call sites) | Yes | None | Located by type name `RenderCore.ShaderModuleUtils` + method `FromFile` with `(Device, string, out…)`. |
| 12 | Direct API | `mesh-deform.lib/MeshDeformShaders.cs:71` | `Program.GetRenderer().Device` | `KSA/Program.cs:558` | Yes | None (OLD `:535`) | Vulkan device for compile. |
| 13 | Direct API | `mesh-deform.lib/MeshDeformSubmod.cs` | `Vehicle.Parts.Parts`, `Part.DisplayName`, `Part.Id` | `KSA/Vehicle.cs:604`, `KSA/PartTree.cs:95`, `KSA/Part.cs:698,700` | Yes | None (5402 initialises `DisplayName` from `Template.DisplayName`, `Part.cs:1391` — cosmetic) | Part enumeration + labels. |
| 14 | **Asset / GLSL anchor strings** | `mesh-deform.lib/MeshDeformShaders.cs:356-357` (struct), `:362` (main), probe `:166` | `MeshIndirect.vert` literals: `"    uint EmissiveColor;\n};"` **and** `"    vec4 worldPosVec4 = worldMatrix * vec4(inPos, 1.0);"` | `Content/Core/Shaders/Mesh/MeshIndirect.vert` (byte-identical 5348↔5402; struct `:11-27`, main anchor `:63`) | **Partial** (main anchor present; **struct anchor REMOVED**) | **BROKEN** (unchanged) | See finding below. |
| 15 | Harmony prefix (HotkeyGuard) | `mesh-deform/Patcher.cs:18` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None | Shared guard. |

**Game assets referenced** — `ShaderReference "MeshIndirectVert"` → `Content/Core/Shaders/Mesh/MeshIndirect.vert` (vertex shader rewritten at runtime). No sounds/parts.

**Update-risk findings (4750→5018)** — still dead (unchanged cause), and the byte hazard deepened.

- 🔴 **`PerInstanceData.packing2` — the slot `DeformRadius` maps onto — is now the game's
  `public float Wetness`.** 5018 added an `ENABLE_WETNESS` shader variant (`MeshIndirect.vert`
  `outWetness`@loc8), compiled when `GameSettings.Current.Graphics.VesselWater` is on. mesh-deform's
  `AddInstanceDeformPatch.Prefix` returns early unless `MeshDeformShaders.ShadersActive`, which
  remains false on ≥4693, so **nothing is written today** — but un-gating the feature would now
  corrupt vessel wetness as well as the emissive slot.
- ✅ The self-disable probe is still correct: `MeshIndirect.vert` in 5018 still carries the
  `#ifdef ENABLE_*` feature gates (and gained `ENABLE_WETNESS`/`ENABLE_FROST`), so the anchors the
  mod needs remain absent and it disables cleanly. **Not a new regression.**
- ✅ Both Harmony targets (`PartModel.AddInstance`, `PartModelModule.UpdateRenderData`) are
  signature-identical in 5018.

#### Carried over from the 4680→4750 review — **BREAKING DELTA in the GLSL shader rewrite (item 14).**

- The C# side is entirely intact: both Harmony targets (`PartModel.AddInstance`, `PartModelModule.UpdateRenderData`), `PartModelModule.Parent`, the `PerInstanceData` struct layout, `ColorData.Rebuild`, and all `ShaderReference` reflection (`DoLoad`, `Shader` private setter, `ModPath`) + `ShaderModuleUtils.FromFile` are signature-identical in both versions. The mod loads and patches without error.
- **But `MeshIndirect.vert` was refactored in 4750.** OLD (4680) ended the struct exactly with the mod's anchor:
  ```glsl
  struct InstanceData { mat4 WorldMatrix; int Highlighted; uint EmissiveColor; };
  ```
  NEW (4750) wraps the field in a preprocessor guard and adds two new fields:
  ```glsl
  struct InstanceData {
      mat4 WorldMatrix; int Highlighted;
      #ifdef ENABLE_EMISSIVE  uint  EmissiveColor; #endif
      #ifdef ENABLE_TEMPERATURE float Temperature; #endif
      #ifdef ENABLE_THIN_FILM   float TfiThickness; #endif
  };
  ```
  The struct-injection anchor `"    uint EmissiveColor;\n};"` **no longer exists**, so `ModifyVertexShader` (`MeshDeformShaders.cs:270`) silently does NOT inject `DeformMagnitude`/`DeformRadius` into the struct.
- **Failure is masked, then fatal at GPU compile.** The main-displacement anchor (`vec4 worldPosVec4 = worldMatrix * vec4(inPos, 1.0);`) is still present (NEW `MeshIndirect.vert:53`), so step 2 succeeds and `ValidateStructModified` (`:113`) *passes* — because the injected `main()` body references the field names, fooling the `source.Contains("DeformMagnitude"/"DeformRadius")` check. The shader is then handed to `ShaderModuleUtils.FromFile`, where `main()` references `instanceData.DeformMagnitude/.DeformRadius` that don't exist in the struct → **GLSL compile error → `Activate()` returns false**. Net effect: clicking **Active** fails on 4750 and deformation never renders.
- **Secondary collision risk even if the anchor is fixed.** The new `ENABLE_TEMPERATURE`/`ENABLE_THIN_FILM` variants add `float Temperature` / `float TfiThickness` at the same trailing struct slots the mod wants to repurpose from `packing1/packing2`. So the "reuse 8 padding bytes" scheme is now contended by stock engine-emissive/thin-film features; a fix must account for the active shader `#define` variant, not just re-point the anchor.
- **Root cause is also architectural (verified in `PartModelRenderer.cs` + `ShaderReference.cs`, NEW).**
  `PartModelRenderer.ColorData` now compiles `MeshIndirectVert` via
  `ShaderReference.CompileVariantWithCustomOptions()` (NEW `ShaderReference.cs:119`) — reading GLSL fresh
  from disk per `ENABLE_*` variant and destroying the module immediately — and **ignores
  `ShaderReference.Shader`** for the color pipeline. So even with a fixed anchor, the mod's `.Shader` swap
  + `ColorData.Rebuild()` cannot affect rendering; reviving deformation requires Harmony-patching the
  shared part-shader compilation (blast radius = every part) and GPU validation.
- **GUARDED (Phase 2).** `MeshDeformShaders.IsSupported` (`mesh-deform.lib/MeshDeformShaders.cs`) probes
  the on-disk shader and disables activation with a clear UI notice (`MeshDeformSubmod.RenderBody`);
  `Activate()` short-circuits; and `AddInstanceDeformPatch.Prefix` early-outs unless `ShadersActive`, so
  it never writes the padding slots the new shader repurposes for `Temperature`/`TfiThickness`.
- **Fix direction (if revived):** Harmony-patch `CompileVariantWithCustomOptions` for `MeshIndirectVert`
  to compile a modified source, inject after the `#ifdef` block (anchor on `};` of `InstanceData` or on
  `float TfiThickness;\n    #endif\n};`), and confirm the chosen padding does not alias
  `Temperature`/`TfiThickness` in any compiled variant. Tighten `ValidateStructModified` to assert the
  *struct* (not just `main`) contains the new fields.

---

## stampy

**Purpose** — Template/placeholder mod (clone of `fixme-mod-name`). No implemented feature; an F11 window with a "press me" button that logs to console.

**Standalone entry (class+file)** — `mod.Mod` (`stampy/Mod.cs`, `[StarMapMod]`; namespace is the default `mod`). No `.lib`. Not in unscience.

**UI/hotkeys** — F11 toggles a stub window (`Mod.cs:72-104`).

**Persistence** — None.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|-----------|
| 1 | Harmony prefix (HotkeyGuard) | `stampy/Patcher.cs:19` | `GameSettings.OnKeyAll(GlfwKeyEvent)` — `public static bool` | `KSA/GameSettings.cs:3301` | Yes | None (file byte-identical) | Shared `KsaAbstractions.HotkeyGuard`; the only real game touchpoint. |
| 2 | Lifecycle | `stampy/Mod.cs:19-70` | StarMap attributes: `[StarMapMod]`, `StarMapImmediateLoad`, `StarMapAllModsLoaded`, `StarMapBeforeGui`, `StarMapAfterGui`, `StarMapUnload` | `StarMap.API` | Yes | None | Standard mod lifecycle. |

**Game assets referenced** — None.

**Update-risk findings (4680→4750)** — No breaking deltas detected. The only game integration is `HotkeyGuard` (`GameSettings.OnKeyAll`, unchanged) and StarMap lifecycle. Nothing else to break.

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **marque clean.** `GaugeCanvas.OnDrawMenuBar()` (its Harmony prefix target) is unchanged, and so are
  `IOrbiter.ShowOrbit`, `Universe.CurrentSystem`, `CelestialSystem.GetWorldSun()` and the
  `Astronomical.Children` walk in `marque.lib/MarqueLib.cs`. Rev 5332 changed `Program.DrawMenuBar` only
  by wrapping the Save/Load `MenuItem` in `if (!IsEditorOpen)` — the menu marque injects into is intact.
  Rev 5265 (*"all ImGuiHelper functions require a draw list"*) is game-internal; `IOrbiter.cs`'s own
  overlay draws moved to `ImGuiHelper.GetOverlayDrawList(inViewport)`, which marque does not call.
- ❌ **mesh-deform — still broken, and unchanged from 5261.** `Content/Core/Shaders/Mesh/MeshIndirect.vert`
  is **byte-identical** between the two builds, and its struct anchor still does not match:
  `MeshDeformShaders.cs:166` tests for `"    uint EmissiveColor;\n};"`, but the file has
  `uint EmissiveColor;` at `:16` followed by `#endif` at `:17`. The mod self-disables. Its
  world-position anchor (`MeshDeformShaders.cs:362`, `"    vec4 worldPosVec4 = worldMatrix * vec4(inPos,
  1.0);"`) **does** still match, at `:63`. Its reflection targets (`ShaderReference.{Shader, DoLoad,
  ModPath}`, `ShaderModuleUtils.FromFile`) and its two patch targets
  (`PartModelModule.UpdateRenderData`, `PartModel.AddInstance`) all resolve. **Not a new regression.**
- ✅ **byo-music clean.** `ModLibrary.Get<MusicPlayList>` is unchanged; its `"SabotageMusic"` id is still
  non-stock and still null-guarded.
- ✅ **steely-eyed-missile-kitten clean** — see [`telemetry.md`](telemetry.md). The `Situation` enum names
  it compares against are unchanged; its readings shift with the rev-5317/5318/5340 flight-computer and
  part-characteristics fixes.
- ✅ **jplrepo's IL transpiler still matches.** It injects `SaveMenuCursorPos()` before the **first**
  `ImGui.SetCursorPosY` call in `Program.DrawMenuBar`. Rev 5332 rewrote that method, but the
  version-string positioning block is untouched: `DrawProgramMenusHook()` → `SetCursorPosY` →
  `SetCursorPosX` → `EndMenuBar`, with still exactly **one** `SetCursorPosY`, at `Program.cs:509→512`
  (was `:506→509`). `Program.DrawProgramMenusHook()` — its prefix target — is unchanged.
  This is the most fragile binding in the suite (IL shape, not a symbol) and should be re-checked every
  time `DrawMenuBar` moves.
- ℹ️ **stampy** — no game touchpoints changed this span.

---

## Area summary — Update-risk findings (5348 → 5402)

Revisions 5349–5400 are **unlogged** (the only changelog entry in this span is rev 5401, "Fixed crash
for incorrect data stride for thumbnail rendering"); the decomp/Content diff is the only evidence. All
five mods compile clean against 5402, and none of them references the retired `Viewport` type
(`rg '\bViewport\b'` over their sources: zero hits), so the `Viewport` → `IViewport`/`IGameViewport`
replacement is not a compile break here. `GameSettings.cs` (every HotkeyGuard target) is byte-identical.

- ✅ **marque clean.** `GaugeCanvas.OnDrawMenuBar()` is still at `:1396` and its body (`1396-1440`) is
  byte-identical; the file's only diff is `CanvasesRender`/`Render` taking `IViewport`.
  `Program.DrawMenuBar` changed only its first parameter (`IGameViewport`, `Program.cs:3344`).
  `IOrbiter.ShowOrbit` (`:20`), `Vehicle.ShowOrbit` (`:374`), `CelestialSystem.GetWorldSun()` (`:565`),
  `IParentBody.Children` (byte-identical file), `Asteroid`/`Comet : MinorBody` are unchanged — the
  `IOrbiter.cs`/`CelestialSystem.cs` diffs are cursor/hit-test-viewport rework in the game's own
  overlay code (`PartPicker`, `CursorTarget.IsHitTestViewport`, debris colouring), none of which marque
  calls. The rev-5018 "where do the entries land" **live pass is still open**.
- ✅ **byo-music clean.** `MusicPlayList.cs` byte-identical; `ModLibrary.Get<T>` moved to `:1042`;
  `GameAudio.cs` diff is a window-size clamp and `IGameViewport` retype. `Content/Core/Sounds.xml` is
  identical and still has no `SabotageMusic` (null-guarded, pre-existing).
- ❌ **mesh-deform — still broken, unchanged cause.** `MeshIndirect.vert` and `MeshIndirect.frag` are
  **byte-identical** to 5348 (md5 `b3ff05fd…` / `c8ad58b4…`), so the struct anchor is still absent and
  the mod self-disables. What did change: both Harmony targets were retyped to `IViewport` —
  `PartModel.AddInstance(PerInstanceData, IViewport, int)` (`PartModel.cs:408`) and
  `PartModelModule.UpdateRenderData(…, IViewport, int)` (`PartModelModule.cs:87`) — and `AddInstance`
  gained an early-return `if (!viewport.HasAny(ViewportOptionFlags.RenderPartModels)) return;`
  (`:410-413`). Both remain single overloads, so the by-name `AccessTools.Method` lookups still bind,
  and the prefixes never touch the viewport argument. `PerInstanceData` (`:332-342`) layout is
  unchanged (`packing1`@72 private, `Wetness`@76). `ShaderReference.cs` is byte-identical;
  `PartModelRenderer.ColorData` still compiles `MeshIndirectVert` per `ENABLE_*` variant
  (`PartModelRenderer.cs:109-125`). **Not a new regression; no code change.**
- ✅ **steely-eyed-missile-kitten** — `Situation.cs` byte-identical (string-compare surface intact);
  full telemetry surface in [`telemetry.md`](telemetry.md).
- ℹ️ **stampy** — nothing to break beyond `GameSettings.OnKeyAll` (`:3301`, unchanged).
- ℹ️ Content diffs this span (`Common/RayIntersections.glsl` cylinder-intersection fix,
  `Mesh/ModelPbr.frag` + `Mesh/ModelNormal.frag` two-sided normal flip for parachute canopies, new
  `Mesh/StaticObjectNormalIndirect.frag` registered as `StaticObjectPrePassIndirectFrag` in
  `DefaultAssets.xml:62`, `ParachuteAssets.xml` added to `mod.toml`) touch nothing these mods read;
  `MeshIndirectVert` is still `DefaultAssets.xml:55`.
- **Verified clean** (line numbers refreshed above): marque rows 1–8; byo-music 1–4; mesh-deform 1–13,
  15 (14 remains BROKEN, pre-existing); stampy 1–2; steely row 26.
- **Needs a live pass**: marque menu placement (carried from 5018); nothing new.
