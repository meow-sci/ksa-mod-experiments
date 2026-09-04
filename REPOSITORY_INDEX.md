
# KSA Mod Experiments - Repository Index

This document serves as a comprehensive index of all mods and libraries in this KSA mod experiment project. It's designed to help AI agents and developers quickly discover existing functionality and understand the purpose of each mod.

> **Game integration scope:** for how each feature plugs into KSA (Harmony patches, reflection, game types, shaders, assets) and how to check what a game update breaks, start at [`scope/FULL_SCOPE.md`](scope/FULL_SCOPE.md) and its master index [`scope/game-integration-surface.md`](scope/game-integration-surface.md). Keeping `scope/` current is mandatory — see [`AGENTS.md`](AGENTS.md).

## Core Libraries

### [ksa-abstractions.lib](ksa-abstractions.lib)
Shared library with common abstractions used across multiple mods. Provides utility classes and base functionality.
- `VehicleProvider` — get all vehicles or the controlled vehicle from `Universe.CurrentSystem`
- `CelestialProvider` — get all celestial bodies (`Celestial`) or all orbiters (`IOrbiter`) from `Universe.CurrentSystem`
- `SimTimeProvider` — wrapper for `Universe.GetElapsedSimTime()`
- `ReflectionHelpers` — utility for safe field/property access via reflection
- `PartHelpers` — recursive part tree helpers
- `IGameStateScheduler` / `GameStateQueue` / `GameThread` — thread-safe game-state scheduler; enqueue mutations from HTTP/background threads, drain on game thread in `OnBeforeUi`
- `ISubmod` — generic submod interface used by unscience supermod: `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- `HotkeyGuard` — mandatory Harmony prefix on `GameSettings.OnKeyAll` that swallows game hotkeys while an ImGui text input has focus
- `HiddenUiFrameHook` — Harmony prefix on `Program.OnDrawUiConsole` that replays a host's registered `BeforeGui`/`AfterGui` per-frame work while the game HUD is hidden (F2), because StarMap's `[StarMapBeforeGui]`/`[StarMapAfterGui]` targets are skipped by the game in that state; used by unscience
- `EasingType` enum + `EasingHelper.ApplyEasing()` — shared easing utility (Linear/EaseIn/EaseOut/EaseInOut with power params); used by zippo.lib, garrys-torch.lib, camera-controller-override.lib
- `XkcdColorHelper` — cached reflection-based lookup of all ~950 `KSAColor.Xkcd` named colors; provides `GetAll()`, `FindByName()`, `GetNames()`; used by zippo.lib and doh.lib
- `Directions` — named `double3` unit axes (`Up`/`Down`/`Left`/`Right`/`Forward`/`Backward`) in KSA's Y-up, -Z-forward convention; replaces `KSA.Double3Ex.{Up,Forward,…}`, removed by the game in build `2026.8.3.5117`. No current consumer (its only user, space-tape, was removed in `2026.8.22.5348`)

---

## Vehicle Manipulation Mods

### [eternal-flame](eternal-flame) / [eternal-flame.lib](eternal-flame.lib)
Infinite fuel and electricity hack. Monitors selected vehicles and periodically refills fuel tanks and battery charge at a configurable interval.
- Filterable vehicle combo box for selection
- Add/remove vehicles to a monitored list
- Per-vehicle **Fuel** and **Elec** toggles
- Configurable refill interval (0–5000ms drag slider)
- Refill loop runs from a Harmony vehicle solver hook so electrical state updates feed into simulation
- F11 window toggle

### [garrys-torch](garrys-torch) / [garrys-torch.lib](garrys-torch.lib)
Vehicle welding system. Attaches one vehicle to another with support for position offsets, rotation, and uniform scaling. Welds persist per-frame.
- Vehicle-to-vehicle welding anchored to a **specific part** on the target vehicle (CoM-drift-proof; tracks robotics-moved parts)
- Position and rotation offsets expressed relative to the target part's local frame
- Per-weld rotation offset (pitch/yaw/roll)
- Uniform vehicle scaling with KittenEva avatar support
- Rotation lock toggle and auto-unweld on parent mismatch
- Weld updates run from `Mod.OnAfterUi` (and unscience's `OnAfterUi`); `GarrysTorchSubmod.UpdateWelds(dt)` calls `KSA.JobSystems.VehicleSolvers.Wait()` before any `Vehicle.Teleport` to drain worker threads — required to avoid `Collection was modified` (from `_vehicleStates.Remove` racing worker iteration) and `SnapToLeader body/origin time mismatch` errors
- Multiple simultaneous welds with topological sort for correct ordering
- User-defined presets persisted to TOML (`~/.unscience/garrys-torch-presets.toml`)
- Save weld settings as named presets, load presets into create form
- ImGui control panel with filterable combos (vehicle → part → preset) and bordered weld sections
- **Animation system**: Smooth interpolation of weld position/rotation/scale with configurable easing (Linear, EaseIn, EaseOut, EaseInOut) and per-power control. Queued animations per weld.
- **Public API**: `GarrysTorchSubmod.Instance` singleton, `CreateWeld`, `ModifyWeld`, `RemoveWeld`, `AnimateWeld`, `FindWeld`, preset pass-throughs — exposed for use by `unladen-swallow.lib` RPC endpoints
- **Safe update API**: `GarrysTorchSubmod.UpdateBeforeVehicleSolvers(dt)` performs animation and weld teleports; ordinary `ISubmod.Update(dt)` is intentionally non-mutating for weld physics

### [kiwis-marbles](kiwis-marbles) / [kiwis-marbles.lib](kiwis-marbles.lib)
Celestial body welding mod. Repositions planets and moons by welding them to follow other celestial bodies or vehicles at user-defined offsets. Bypasses physics for the source body, rewriting its orbit once per sim step from a Harmony prefix on `Universe.ExecuteNextVehicleSolvers` (after the orbit/vehicle solver results are applied, before the next step is queued).
- Weld any planet or moon to any orbiter (celestial or vehicle)
- CCI-frame offset input with unit scale selector (m / km / Mm / Gm)
- Live offset editing per active weld
- Cross-parent welding: `Celestial.SetOrbit()` + explicit `IParentBody.Children` re-parenting
- Multiple welds with topological sort for correct weld chain ordering
- ImGui control panel (F9 toggle)
- **kiwis-marbles.lib**: `CelestialWeldEntry` (Source/Target/Offset/OriginalOrbit), `CelestialWeldEngine` (per-step repositioning, re-parenting, subtree refresh, Kahn's topological sort), `KiwisMarblesPatches` (solver-step Harmony hook shared by the standalone mod and unscience)

### [zippo](zippo) / [zippo.lib](zippo.lib)
Light control and animation system. Selects vehicles and light parts, then controls their intensity and color using the full XKCD color palette. Supports queued single-step animations that interpolate both color and intensity with configurable easing.
- Vehicle and light part selection
- Light intensity control (0-1 slider)
- Light color: 950+ XKCD named colors via filterable combobox + custom color picker
- On/off toggle for lights
- **Animation system**: Queue-based single-step animations (max 25/part) interpolating color+intensity with Linear/EaseIn/EaseOut/EaseInOut easing + power control; manual controls locked during animation
- Recursive part tree search for light components
- Real-time light property updates
- **Public API** (`ZippoSubmod.Instance`): `GetLightPartInfos()`, `SetLightState()`, `QueueAnimation()`, `ClearAnimationQueue()` — used by unladen-swallow RPC
- **RPC endpoints** (via unladen-swallow): `GET /zippo/lights`, `POST /zippo/lights/state`, `POST /zippo/animate`, `DELETE /zippo/animate`

### [i-feel-seen](i-feel-seen) / [i-feel-seen.lib](i-feel-seen.lib)
Vehicle render distance override. Allows tracking and toggling render visibility for specific vehicles independent of camera distance.
- Vehicle-selective render override
- Vehicle tracking system
- Per-vehicle visibility toggle
- Vehicle position and orientation patching
- Multi-vehicle management

---

## Camera & View Control Mods

### [hot-pursuit](hot-pursuit) / [hot-pursuit.lib](hot-pursuit.lib)
Part-mounted secondary cameras. Arm placement, click a rendered vehicle part, and Hot Pursuit leases
one of KSA's stock secondary viewports for a live feed welded to that exact surface.
- Mesh-precise placement via `Cursor.GetEgoRay(Program.MainViewport)` + `Part.RayCastEgo`; anchors the
  returned hit sub-part by stable vehicle id and `Part.InstanceId`
- Cameras follow vehicle, robotics, sub-part, floating-origin, and scaled-part motion every frame
- Per-camera part-local XYZ translation, mount-relative pitch/yaw/roll, 15-120 degree FOV, resolution,
  visibility, close/reopen, and removal controls
- Uses KSA's public `ViewportRegistry.TryClaimSecondaryViewport` ownership API and stock viewport
  rendering/window; no custom Vulkan path
- Shares the four preallocated secondary slots with stock Add Camera and docking cameras; KSA's
  eight-slot registry is sealed after startup, so four is the absolute extra-camera ceiling
- Missing/despawned targets become safely dormant; closed viewport windows release their lease
- `hot-pursuit.lib`: `HotPursuitSubmod`, camera/owner state, ray picker, part-relative pose math, and
  `HotPursuitPatches` (selective `FixedController.OnFrame` prefix)
- Session-only state; standalone F11 window and unscience `ISubmod` integration

### [camera-controller-override](camera-controller-override) / [camera-controller-override.lib](camera-controller-override.lib)
Advanced camera animation system. Provides 8 configurable animation types (zoom, spiral, orbit, shake) with easing functions and keyframe sequencing for orbit and fly camera modes.
- Zoom in/out, zoom to offset, spiral zoom in/out, standard orbit, loopy orbit, shake animations
- Keyframe sequence player — chain animations with configurable duration and easing
- Linear, Ease In, Ease Out, Ease In-Out easing with power control
- OrbitController and FlyController patching via `CameraControllerOverridePatches` (Apply/Remove)
- **camera-controller-override.lib**: `CameraControllerOverrideSubmod` (ISubmod — all 30+ config fields, full animation UI in RenderContent), `CameraControllerOverridePatches` (shared Apply/Remove Harmony patches for sequence playback), `KeyframeSequencePlayer`, `KeyframeSequencePanel`, 8 animation implementations, `AnimationHelpers`

### [glass](glass) / [glass.lib](glass.lib)
Camera FOV control. Provides 8 lens presets (from super telephoto at 15° to fisheye at 120°) and manual FOV adjustment.
- 8 camera lens presets (telephoto, wide-angle, fisheye, etc.)
- Manual FOV slider control
- Real-time FOV adjustment
- Camera.FieldOfView and Camera.UpdateProjection patching
- Game default preset (50°)
- **glass.lib**: `FovController` — programmatic camera FOV control; `SetFov()`, `DisableOverride()`, `ApplyFov()`, `GetCurrentFovDegrees()`. Used by `unladen-swallow.lib` to expose FOV control over HTTP.

---

## Information Display & Monitoring Mods

### [average-twr](average-twr) / [average-twr.lib](average-twr.lib)
TWR (Thrust-to-Weight Ratio) calculator and display. Shows real-time TWR and maximum acceleration data for the controlled vehicle.
- Real-time TWR calculation
- Maximum acceleration computation
- Sample collection at 100 Hz
- ImGui display window (F11 toggle)

### [geeforce](geeforce) / [geeforce.lib](geeforce.lib)
G-force recorder and display. Monitors acceleration forces (g-forces) acting on the controlled vehicle at 40 Hz sample rate with ring-buffer history, peak/jerk detection, and interactive scrub slider.
- 40 Hz sampling rate (25ms intervals) with ring-buffer history (30s–1h)
- Per-axis (X/Y/Z body frame) acceleration, jerk, kill-gee breach detection
- Interactive scrub slider for reviewing history; live/paused modes
- Configurable history window and kill-gee/jerk thresholds
- **geeforce.lib**: `GeeForceSubmod` (ISubmod — owns sampling loop + delegates to GForceUI.RenderContent), `GForceRecorder` (ring-buffer + stats), `GForceUI` (static graph/UI — `RenderContent()` for embedded use, `Render()` for standalone window)

### [kitchen-sink](kitchen-sink) / [kitchen-sink.lib](kitchen-sink.lib)
Random collection of one-off hacks and fixes for KSA. F11 window toggle.
- **Fix Invisible Subparts**: button that calls `ReinitializeDerivedValues` on `Program.Editor.EditingSpace.Parts` to restore visibility of invisible subparts in the vehicle editor (workaround for a KSA bug)
- **Force IVA Rendering**: toggle that directly mutates `Template.Internal` on all `PartModel` instances to force interior parts to render outside IVA camera mode; includes a Harmony constructor patch to catch newly created parts and a `PartModel.AddInstance` editor override so IVA SubParts remain visible in the vehicle editor
- **kitchen-sink.lib**: `KitchenSinkSubmod` (ISubmod — renders fix panels), `IvaForceRender` (static API — template mutation + tracking for IVA force rendering)

### [steely-eyed-missile-kitten](steely-eyed-missile-kitten) / [steely-eyed-missile-kitten.lib](steely-eyed-missile-kitten.lib)
Mission monitoring, event detection, and achievement tracking mod. Passively samples telemetry for all vehicles at a configurable rate, detects interesting flight events, evaluates YAML-defined mission conditions, and persists everything to a local SQLite database.
- Passive telemetry monitoring for all vehicles (configurable rate, default 2 Hz)
- Monitored metrics: altitude (baro/radar), speed (orbital/surface/inertial), orbital params (Ap/Pe/eccentricity/inclination), mass, g-forces, situation
- Flight event detection: SoiChanged, Liftoff, Landed, SplashDown, AtmosphereEntered, AtmosphereExited, StableOrbitAchieved, OrbitEscaped
- YAML mission definitions with flexible condition trees: threshold, event, location, and composite (all_of, any_of, sequence)
- SQLite persistence of all flight events and mission progress to `Documents/My Games/Kitten Space Agency/.steely-eyed-missile-kitten/events.db`
- Three-tab F11 ImGui window: Live Telemetry table, Event Feed (color-coded, filtered), Missions (activate/track/abandon)
- JSON Schema for mission YAML IDE validation (`missions/mission-schema.json`)
- **steely-eyed-missile-kitten.lib**: `VehicleTelemetry` (all KSA API reads co-located), `TelemetrySnapshot`, `MonitoringLoop`, `EventDetector`, `EventBus`, `EventDatabase`, `EventWriter`, `MissionLoader`/`MissionManager`/`MissionEvaluator`, `MonitorUI`/`EventFeedUI`/`MissionUI`

---

### [red-alert](red-alert) / [red-alert.lib](red-alert.lib)
Vehicle action plan builder. Composes reusable lists of light and solar-panel actions across one or more vehicles. Each plan has a single **Engage** button that fires every action in order.
- Discovers per-part capabilities by scanning each vehicle's part tree (`LightModule` template components, `LightSwitch`, `SolarPanel`, `KeyframeAnimationModule`)
- Action picker only shows actions the selected part supports (e.g. only solar parts get Deploy/Retract)
- Action types: light off / on / toggle, light color, light actuate (animation 0..1), solar deploy / retract / toggle, solar actuate
- Standalone F11 ImGui window plus `ISubmod` integration with unscience supermod
- ImGui UI follows the standard submod layout: padded content area, Create form + collapsible plan sections with bordered child windows, filterable Vehicle/Part combos
- **red-alert.lib**: `RedAlertSubmod` (ISubmod UI), `ActionPlan` / `PlannedAction` / `ActionablePart` / `ActionType` / `PartCapability` (data model), `ActionScanner` (capability discovery), `ActionExecutor` (live execution via `KeyframeAnimationModule.TimeGoal`), `LightActions` (light color + on/off via reflection)

---

## Animation & Visual Effects Mods

### [blinky](blinky) / [blinky.lib](blinky.lib)
Dynamic LCD pixel grid builder. Builds NxM engine pixel grids at runtime by dynamically creating and attaching engine parts to existing vehicles. Supports **multiple named grids per vehicle** via compound `(vehicleId, gridName)` key.
- Runtime part creation via manual `TreeParent`/`TreeChildren` wiring — no pre-built vehicle needed
- **Multiple grids per vehicle** — each grid has a unique name, independently configured and controlled
- Grid names: alphanumeric + hyphens only (`[a-zA-Z0-9-]`); part ID format: `pixel_{gridName}_{row}_{col}_{a|b}`
- Layout modes: Flat (plane) or Cylinder (sides only, radius auto-calculated from width × spacing)
- Configurable grid size, spacing, position offset, engine scale, and engine template
- Batch creation with single `PartTree.CreateFromNewPartTree()` rebuild (N→1 recomputes)
- **BlinkyGridManager** — static singleton managing grids by `(vehicleId, gridName)` compound key, shared with RPC endpoints
- **Global scan** — discovers blinky grids across all loaded vehicles (Debug menu)
- **Propellant feed** — each pixel engine is wired to a fuel-bearing part through its own **declared feed connector** (`RocketCore.FeedConnectors`); KSA rejects any other route, so this is what makes the pixels actually light
- **Repair Feed** — re-wires a grid's propellant feed in place (for grids found by scanning, or built before the feed-wiring fix) and rebuilds the resource managers
- **Ignition/throttle warning** — flags when the vehicle is shut down, in Auto burn mode, or at zero throttle, any of which keeps the grid dark
- **Diagnose** — logs per-controller ignition state, feed connector wiring, and `ResourceManager.ConsumptionOrder` tank counts
- **Static display** — paints a set of pixels with optional intelligent diff (reset mode)
- **Off** — turns off all pixels and stops any running scroll on a specific grid
- Pattern presets: All On, Checkerboard, Alt Rows, Alt Cols
- Render engine meshes toggle for performance boost
- Build/Destroy individual grids at any time; vehicle combo selector with filter
- Per-grid collapsible UI sections with info table, pattern buttons, and destroy
- Menu bar with Debug menu for global grid scanning
- **blinky.lib**: `BlinkyGridManager` (compound-key scroll/static/off/pattern APIs, `ScanAllVehicles`), `ScrollAnimation`, `PixelGrid` (single-grid + `ScanAllFromVehicle` auto-discovery), `PixelPatterns`, `LcdGridConfig`, `LcdGridBuilder` (`BuildGrid`, `DestroyGrid`, `ScanExistingGrid`, `RepairFuelFeeds`), `BlinkyPixelGrid`. Used by `unladen-swallow.lib` for RPC endpoints.

### [its-so-shiny](its-so-shiny) / [its-so-shiny.lib](its-so-shiny.lib)
Light-part pixel grid builder. Builds Blinky-style NxM grids using KSA's built-in `LightPart` instead of engine parts, avoiding engine ignition, thrust cancellation, and fuel/resource graph complexity.
- Runtime `LightPart` creation via manual `TreeParent`/`TreeChildren` wiring and a single part-tree rebuild
- One light part per pixel, named `shiny_{gridName}_{row}_{col}`
- Flat and cylindrical layouts with configurable grid size, spacing, offset, light scale, color, and intensity
- Connects created light parts to battery-bearing parts when available so stock `PowerConsumer` light switches can receive power
- Reuses freshly created parts for grid registration and deduplicates template-backed appearance writes to reduce large-grid build overhead
- Pattern controls: off, all on, alternating rows, alternating columns, checkerboard
- Global scan discovers existing `shiny_*` grids across loaded vehicles
- Standalone F11 ImGui window plus direct unscience submod integration
- **its-so-shiny.lib**: `ItsSoShinySubmod` (ISubmod UI), `ShinyGridManager` (registration, patterns, static display, scroll APIs), `ShinyGridBuilder` (runtime creation/destruction), `ShinyPixelGrid`, `ShinyPixelCell`, `ShinyGridConfig`, `ShinyScrollAnimation`, `ShinyPixelPatterns`. Used by `unladen-swallow.lib` for RPC endpoints.

### [kitten-animations](kitten-animations) / [kitten-animations.lib](kitten-animations.lib)
Kitten avatar animation controller. Plays every animation the game has loaded for the controlled kitten, triggers facial expressions, and exposes the blend weights and locomotion tuning that decide how hard each animation lands.
- Full ground/EVA locomotion set: idle, walk, run, jump, jump land, tumble/flail, ladder, moon walk, moon run, swim, swim idle, seated idle + seated idle actions
- Full MMU set: idle default, idle actions, six directional loops, arm retract
- Live blend samplers (walk/moonwalk, run/moonrun, swim pair, MMU directional) and overlay poses (blink, ear/helmet mask)
- Harmony prefix on `AnimatedRenderable.UpdateAnimation` so a forced clip survives the game's per-frame clip selection; blend time, playback-rate multiplier, freeze and restart
- 5 facial expressions (angry, awe, happy, sad, scared) with variant selection, strength, ease-in/hold/ease-out or latch — driven through a `CatExpressionAnim` the mod owns, because the game rewrites its own expression weight every frame from vehicle acceleration
- Animation strength knobs: ear motion weight, eye look angle, eye pitch offset, personality mood-face weight, reactive-face cap
- Animation-facing slice of `KittenLocomotionTuning.Current` (blend time, playback-rate clamps, nominal clip speeds, moonwalk/swim ramps, jump-land timing) with a scoped reset
- Live readout: locomotion mode, control mode, ground speed, gravity, jump-chain stage, game playback rate, blend weights
- **kitten-animations.lib**: `KittenAnimationsSubmod` (ISubmod — binds the kitten and renders all sections), `KittenAnimationCatalog` (discovers every loaded clip; ground set lives in private `KittenRenderable` fields), `KittenAnimProcessors` (typed handles on the game's four anim processors), `KittenExpressionController` (mod-owned `CatExpressionAnim` + envelope), `KittenAnimationDriver` (override state applied from the pose prefix), `KittenAnimationPatches` (Harmony), `KittenAvatarAccessor` (kitten/renderable/avatar access), `Ui/` (Playback, AnimationLibrary, Expression, Strength, Tuning sections)

### [byo-music](byo-music) / [byo-music.lib](byo-music.lib)
Bring Your Own Music - Custom music player. Plays audio playlists from defined assets (e.g., SabotageMusic playlist).
- Playlist loading and playback
- Asset-driven music integration
- ImGui music control panel
- Multi-sound support

### [thug-life](thug-life) / [thug-life.lib](thug-life.lib)
Apply the "thug life" pixel-art sunglasses meme as a 2D textured quad anchored to any vehicle's part or subpart in 3D space.
- Programmatic 15x4 R8G8B8A8UNorm texture (no PNG asset shipped) built from an ASCII pattern in `ThugLifeTexturePattern.cs`
- Quad drawn in the offscreen MSAA pass via a Harmony postfix on `SuperMeshRenderSystem.RenderMainPass` using KSA's stock `UnlitMeshVert`/`UnlitMeshFrag` shaders
- GPU pipeline/texture/buffers are created **lazily on the first entry** — `Program.OffscreenTarget` does not exist yet at `[StarMapAllModsLoaded]` time
- Per-frame MVP uses `Program.GetRenderCamera()` so the quad is correct in every rendered viewport, crew portraits included
- Per-entry vehicle / part / subpart pickers with filtered combos
- Per-entry position offset, rotation (pitch/yaw/roll), and width/height — all in the anchor part's local frame
- Multiple simultaneous sunglasses; visible toggle and remove per entry
- **animate thug**: one-click button shown when the selected target is an EVA kitten (`KittenEva`) — anchors a pre-tuned pose (rot `-90,0,90`, `0.975` x `0.2` m) and slides it from `0.251,0,-2` onto the face at `0.251,0,-0.761` over 1.2 s (ease-out); no part selection needed
- F12 toggle for the standalone window; **also bundled into the unscience supermod**
- **thug-life.lib**: `ThugLifeSubmod` (ISubmod UI), `ThugLifeRenderManager` (static `Active`/`Instance` for the render postfix, owns entry list + lazily-created GPU resources), `ThugLifeQuadRenderer` (pipeline + descriptor + VB/IB + per-frame ego-space MVP draw), `ThugLifeTextureFactory` (`SimpleVkTexture` + sampler upload), `ThugLifeRenderPatches` (shared `Apply`/`Remove` Harmony postfix used by both standalone Patcher and unscience Patcher), `ThugLifeEntry`, `ThugLifeSlide` (ease-out position animation), `KittenGlassesPreset` (the animate-thug pose + `IsKitten`), `ThugLifeTexturePattern`

---

### [pyro](pyro) / [pyro.lib](pyro.lib)
Standalone volumetric engine plumes — the game's exhaust effect with no engine part. Each plume is welded to a vehicle → part → sub-part anchor with position/rotation offsets and rendered through KSA's own `VolumetricExhaustRenderer`.
- Harmony **postfix** on `Vehicle.AddVolumetricExhaustInstances` submits pyro's plumes into the game's own per-frame exhaust batch (same camera, delta time, transient LUT)
- Per-plume `VolumetricExhaustInstance` — real startup/shutdown transients; **Enabled** checkbox + On/Off button, All On/All Off
- Per-plume template pick, throttle, position/rotation offsets (part-local; fires along the part's -X axis like stock nozzles)
- Per-plume **nozzle physics** (exit/throat radius, chamber pressure & temperature, gamma, gas constant) → `PlumeData` via an isentropic nozzle model mirroring `RocketNozzle.UpdatePlumeData`, so plumes under/over-expand with altitude
- Per-plume look overrides (absorption density ×, refraction) written into the private per-instance shader struct
- **Preset system** (same pattern as garrys-torch): save any plume's full settings (template, offsets, throttle, nozzle physics, look) as a named preset via modal with duplicate-name validation; filterable preset combo + delete-with-confirmation in the create form; persisted as TOML at `My Games/Kitten Space Agency/.unscience/pyro-presets.toml`
- **Template Editor**: the game's hidden exhaust debug editor controls (absorption, emission colours/brightness, Mach diamonds, noise, length weights, quality) for the shared templates — affects all users of the template
- Auto-removes plumes whose vehicle or anchor part disappears
- Reflection: `VolumetricExhaustTemplate.References` (template list; stock-id fallback) and `VolumetricExhaustInstance._shaderData`
- **Public API**: `PyroSubmod.Instance`, `CreatePlume`, `SetTemplate`, `FindPlume`, `RemovePlume`, `SetAllEnabled`, preset methods (`GetPresetNames`/`GetPreset`/`PresetExists`/`SavePreset`/`DeletePreset`/`ApplyPreset`, `PlumePreset`), `PlumeTemplates`, `PlumePhysics`

## UI & Customization Mods

### [skittles](skittles) / [skittles.lib](skittles.lib)
Global ImGui theme manager. Provides a theme picker and a full style editor that affect every window and control across the entire application, using `ImGui.GetStyle()` — no Harmony patching required.
- Theme picker with filterable combobox (F11 toggle)
- Built-in themes: Game Default, Dark, Light, Classic, Inanimate Carbon Rod
- Full theme editor wrapping `ImGui.ShowStyleEditor()` — 60 color slots + all style vars
- Save/load custom themes as TOML files to/from disk
- Persistent theme selection across game sessions; restores game default on unload
- **skittles.lib**: `ThemeDefinition` (60-color + style POCO), `ThemeSerializer` (Tomlyn TOML I/O), `ThemeManager` (load/save/apply/list), `BuiltInThemes` (Inanimate Carbon Rod preset)

### [con-man](con-man) / [con-man.lib](con-man.lib)
Game UI layout manager for gauge canvases. Saves and restores HUD gauge visibility, position, and scale to named layouts stored as TOML files.
- Save current gauge layout to named TOML file
- Load/apply saved layouts with filtered combobox selector
- Set a startup default layout (auto-applied on game launch)
- Delete layouts with confirmation
- Live gauge summary table showing all GaugeCanvas state (name, id, enabled, offset, scale)
- Persistence in Documents/My Games/Kitten Space Agency/.con-man/layouts/
- F11 window toggle (standalone mode)
- **con-man.lib**: `ConManSubmod` (ISubmod — layout selector/save/delete/startup default UI), `LayoutManager` (save/load/delete/apply/list layouts, startup default), `GaugeStateAccessor` (reflection-based access to GaugeCanvas private fields), `LayoutSerializer` (Tomlyn TOML I/O for layouts and config)

---

## HTTP RPC Mods

### [unladen-swallow](unladen-swallow) / [unladen-swallow.lib](unladen-swallow.lib)
HTTP RPC server mod. Embeds a GenHTTP server (`0.0.0.0:7887`) that exposes KSA mod functionality over a REST API. ImGui window (F11 toggle) with enable/disable checkbox. Exposes camera FOV control via `glass.lib`, blinky pixel grid control via `blinky.lib`, its-so-shiny light grid control via `its-so-shiny.lib`, and camera animation sequencing via `camera-controller-override.lib`.
- F11 toggle ImGui window
- Enable/disable HTTP server via checkbox
- Live server status indicator (Running/Stopped)
- `GET /health` — server liveness check
- `GET /fov` — returns current FOV state (current, override, isActive)
- `POST /fov` — sets camera FOV override (`{ "fov": 30.0 }`) or disables it (`{ "fov": 0 }`)
- Expanded blinky API (14 endpoints) covering grid lifecycle, animation control, render settings, and engine control:
- `GET /blinky/grids` — list registered grids (optional `vehicleId` filter)
- `POST /blinky/grids` — build and register a new grid on a vehicle
- `DELETE /blinky/grids` — destroy/unregister a grid (`vehicleId` and `gridName` query params)
- `POST /blinky/grids/scan` — scan a specific vehicle for an existing named grid
- `POST /blinky/grids/scan-all` — discover and register grids across all vehicles
- `POST /blinky/grids/repair` — re-wire a registered grid's propellant feed so its engines can light
- `POST /blinky/animate` — start scrolling animation from client-supplied pixels
- `DELETE /blinky/animate` — stop an active scroll without clearing current pixels
- `POST /blinky/animate/builtin` — start built-in scrolling animation payload
- `POST /blinky/static` — display a static pixel pattern
- `POST /blinky/pattern` — apply built-in patterns (`allOn`, `allOff`, `checkerboard`, `altRows`, `altCols`)
- `POST /blinky/off` — turn all pixels off and stop scroll
- `GET /blinky/render` / `POST /blinky/render` — get/set pixel part mesh rendering toggle
- `POST /blinky/engines/deactivate` — deactivate non-LCD engines on a vehicle
- Expanded its-so-shiny API (9 endpoints) covering light grid lifecycle, animation control, and appearance:
- `GET /shiny/grids` — list registered light grids (optional `vehicleId` filter)
- `POST /shiny/grids` — build and register a new light grid on a vehicle (with color/intensity)
- `DELETE /shiny/grids` — destroy/unregister a light grid (`vehicleId` and `gridName` query params)
- `POST /shiny/grids/scan` — scan a specific vehicle for an existing named light grid
- `POST /shiny/grids/scan-all` — discover and register light grids across all vehicles (with default color/intensity)
- `POST /shiny/animate` — start scrolling animation from client-supplied pixels
- `DELETE /shiny/animate` — stop an active scroll without clearing current pixels
- `POST /shiny/static` — display a static pixel pattern
- `POST /shiny/pattern` — apply built-in patterns (`allOn`, `allOff`, `checkerboard`, `altRows`, `altCols`)
- `POST /shiny/off` — turn all pixels off and stop scroll
- `GET /shiny/appearance` — get current color/intensity for a light grid (`vehicleId` and `gridName` query params)
- `POST /shiny/appearance` — set color and intensity for a light grid
- `POST /camera/animate` — runs a camera animation sequence (zoom, orbit, spiral, shake, pan, rotate, groups, return-to-start)
- `GET /camera/status` — returns current playback state (Playing/Stopped/Paused, keyframe index, elapsed time)
- `DELETE /camera/stop` — stops any running camera animation
- `GET /torch/welds` — list all active welds
- `POST /torch/welds` — create a weld (`{ "sourceVehicleId": "...", "targetVehicleId": "...", "data": {...} }` or supply `presetName`)
- `DELETE /torch/welds` — unweld/remove a weld (`{ "sourceVehicleId": "..." }`)
- `POST /torch/welds/modify` — immediately modify a weld's position/rotation/scale/lockRotation (partial update — only provided fields updated)
- `POST /torch/welds/animate` — smoothly interpolate a weld to target state over a duration with easing
- `GET /torch/presets` — list all named weld presets
- `POST /torch/presets` — save or update a named preset (`{ "name": "...", "data": {...} }`)
- `DELETE /torch/presets` — delete a named preset (`{ "name": "..." }`)
- **unladen-swallow.lib**: `SwallowServer` (GenHTTP host), `FovEndpoint`, `BlinkyListEndpoint`, `BlinkyGridsEndpoint`, `BlinkyGridScanEndpoint`, `BlinkyGridScanAllEndpoint`, `BlinkyGridRepairEndpoint`, `BlinkyAnimateEndpoint`, `BlinkyBuiltInScrollEndpoint`, `BlinkyStaticEndpoint`, `BlinkyPatternEndpoint`, `BlinkyOffEndpoint`, `BlinkyRenderEndpoint`, `BlinkyEngineDeactivateEndpoint`, `ShinyListEndpoint`, `ShinyGridsEndpoint`, `ShinyGridScanEndpoint`, `ShinyGridScanAllEndpoint`, `ShinyAnimateEndpoint`, `ShinyStaticEndpoint`, `ShinyPatternEndpoint`, `ShinyOffEndpoint`, `ShinyAppearanceEndpoint`, `CameraAnimateEndpoint`, `CameraStatusEndpoint`, `CameraStopEndpoint`, `TorchWeldsEndpoint`, `TorchWeldModifyEndpoint`, `TorchWeldAnimateEndpoint`, `TorchPresetsEndpoint` (all with game-thread scheduling), shared API types. References `glass.lib`, `blinky.lib`, `its-so-shiny.lib`, `camera-controller-override.lib`, `garrys-torch.lib`, and `ksa-abstractions.lib`.

---

## Kitten Spawning & Customization Mods

### [doh](doh) / [doh.lib](doh.lib)
Programmatic kitten spawning with per-kitten GPU material customization. Spawns KittenEva entities at arbitrary positions with unique tint colors via runtime MaterialData creation in GpuMaterialSystem.
- Vehicle-relative positioning with configurable body-frame offset (XYZ)
- Batch spawning (1–20 kittens) with chain offsets
- Character selection from ModLibrary or random assignment
- Per-kitten material tinting via custom AlbedoColor on cloned GPU materials
- Unique or shared material sets for batch spawns
- Live recoloring of spawned kittens via GPU buffer writes
- Individual despawn or despawn-all management
- Spawned kitten registry with full tracking
- F8 ImGui window with vehicle/character combos (filterable), color picker, kitten list table
- **doh.lib**: `MaterialSystemAccessor` (reflection bridge to GpuMaterialSystem/GpuTextureSystem), `MaterialFactory` (runtime per-kitten material creation), `KittenMaterialSet` (per-kitten GPU handles + live UpdateTint), `KittenSpawner` (spawn/despawn/recolor engine replicating EVADoor.CreateKittenEva), `SpawnRequest`/`SpawnResult` (DTOs), `SpawnedKittenRegistry` (state tracking), `DohSubmod` (ISubmod for unscience integration). All methods game-thread-only; RPC-ready via GameThread.Scheduler.
- **Unscience integration**: DOH is available as a submod in the unscience supermod via `DohSubmod`.

---

## Visual Customization Mods

### [humble-arteest](humble-arteest) / [humble-arteest.lib](humble-arteest.lib)
Part painting and visual customization mod. Three features: vehicle part painting via runtime shader patching, kitten character tinting via GPU material buffer writes, and per-engine emissive glow control.
- **Vehicle Paint**: Recolors individual part instances at runtime. The color is quantized to 7:7:7 sRGB and packed into the **free high bits (11..31) of `PerInstanceData.StateBitFlag`** — bits KSA does not use — so no game field, struct layout, or vertex shader is touched. A Harmony prefix on `RenderCore.ShaderModuleUtils.FromFile` compiles an in-memory patched copy of `MeshIndirect.frag` / `MeshIndirectRaytraced.frag` (nothing on disk is modified) that unpacks those bits and blends them into the albedo. Installed through the game's own deferred `Program.RendererRebuildNeeded` rebuild. Targeting: per part instance, per part type, or global; blend modes Multiply / Tint / Replace; works in flight and in the vehicle editor.
- **Kitten Color**: Tints character models (fur, glass, eyes) by writing AlbedoColor to the `GpuMaterialSystem.BigBuffer` via Vulkan staged uploads. Only affects `ModelPbr.frag` path — vehicle parts are unaffected.
- **Engine Emissive**: Per-engine Temperature/TFI override via Harmony prefix on `PartModelDynamic.AddInstance()`. No shader modifications needed — uses the game's existing emissive color LUT.
- F11 window toggle (standalone mode)
- Unscience supermod integration via `ISubmod`: `VehiclePaintSubmod`, `KittenColorSubmod`, `EngineEmissiveSubmod` (grouped by `HumbleArteestSubmod`)
- Harmony patches: `VehiclePaintPatches` (5 seams — `ShaderModuleUtils.FromFile`, both `*Module.UpdateRenderData`, both `*.AddInstance`), `EngineEmissivePatches` (PartModelDynamic.AddInstance)
- **humble-arteest.lib**: `VehiclePaint` (paint registry + bit encoding), `VehiclePaintShaders` (GLSL injection + install/rebuild), `VehiclePaintPatches`, `PaintTargets` (flight + editor part enumeration), `VehiclePaintSubmod` (+ `VehiclePaintSubmodTables`), `KittenColor` (GPU buffer writes), `KittenColorSubmod`, `EngineEmissive` (temperature state), `EngineEmissivePatches`, `EngineEmissiveSubmod`

### [graffiti](graffiti) / [graffiti.lib](graffiti.lib)
Click-to-place **projected PNG decals** on vehicle hulls and terrain. Pick a PNG from a decal library, press Place at Click..., click anywhere in the 3D world — the decal conforms to whatever surface is under the cursor and stays welded to it (part-local on vehicles, geodetic lat/lon on terrain). A port of the gatOS sticker system with a point-and-click UX.
- **Decal library** at `My Games/Kitten Space Agency/.unscience/decals/`: built-in ImGui **file browser** (quick links, drives, filter) copies imports in (auto-uniquified); hand-dropped PNGs picked up by Rescan (which also hot-swaps changed files)
- **One-shot click placement**: filterable decal dropdown → Place at Click... → cursor-following hint → click places via a `Cursor.GetEgoRay(viewport)` raycast (mesh-precise `Part.RayCastEgo` on vehicles first, bounding-sphere pick for KittenEva kittens, accurate CPU terrain march + bisection behind); Esc cancels; a miss keeps placement armed
- **Unlimited decals** with a multi-select listbox (Ctrl/Cmd toggles, Shift range-selects), Delete Selected + Clear All; dormant (despawned-anchor) decals shown, never pruned
- Placement settings: width/height, depth (0 = auto: half the larger side, floored at 0.3 m hull / 2 m terrain — a too-shallow box crops wide decals on curved hulls; terrain boxes also auto-deepen 1%-of-distance to survive terrain LOD at zoom), roll, range, alpha, brightness, configurable max draw distance (default 50 km), debug-checker toggle
- **Render**: Harmony postfix on `RenderTarget.ResolveAttachments` (the GridPass post-resolve window, main viewport only, flight scene only) draws a unit cube per decal; the fragment shader reconstructs scene position from reverse-Z depth and projects the PNG onto it — decals wrap hull curvature and tessellated terrain. Textures via KSA's bindless table (`SimpleVkTexture`, 2048 cap, deferred destroy)
- **No string reflection** — all-public API surface; game updates break loudly at compile time
- **Public API**: `GraffitiSubmod.Instance`, `PlaceAtCursor`, `Arm`/`Disarm`, `RemoveDecals`, `ClearDecals`, `RefreshLibrary`, `Decals`, `DecalLibrary`
- **graffiti.lib**: `GraffitiSubmod` (+ `.Ui`, `.Placement` partials), `DecalEntry`, `DecalPicker` (cursor raycast), `DecalAnchors` (per-frame decal-space composition), `DecalRenderer` + `DecalShaders` (projected-decal pass), `DecalTextures` (PNG decode/upload/bindless slots + retire queue), `DecalLibrary`, `FileBrowser`, `GraffitiPatches`, `GraffitiUi`

### [rocky-mcrock-face](rocky-mcrock-face) / [rocky-mcrock-face.lib](rocky-mcrock-face.lib)
Swap the **meshes and textures of KSA's planetary ring system** (Saturn's instanced rock field + 2D band) at runtime. Pick any built-in mesh — including every part/subpart mesh (~800 in a filterable dropdown) — per ring LOD, change the rock PBR material textures (diffuse/normal/AoRoughMetal), the ring band texture (which also drives the planet's ring shadow), and the rock field's size/density/draw-distance/thickness.
- **Data-level swap, no Harmony patches**: mutates the public `PlanetaryRingsReference` XML-backed tree (`RingLodReference.MeshFileReference.Mesh`, material texture references), then forces the game's own `Program.RebuildRenderer()` settings path so `PlanetaryRingsRenderData` rebuilds from the mutated references with correct GPU sync
- **Mesh conversion**: part/subpart meshes are atlas-interleaved (no `DeviceMesh`), so they are cloned into private `Simple` `MeshReference`s sharing the retained CPU-side `HostPrimitives` and uploaded as a per-attribute-stream `SimpleVkMesh` (the ring pipeline's format) on first use; clones cached for the mod's lifetime
- **Asset catalog** via reflection over `ModLibrary.AllMeshes`/`AllFiles` (the parts-now `GameRegistry` pattern); textures filtered to bound bindless handles, normal maps to `TexturePowerReference`
- Overrides are session-only (deliberately not persisted — a game restart is back to the stock ring); Restore Defaults reverts within a session
- F11 window toggle (standalone mode); unscience supermod integration via `ISubmod`
- **rocky-mcrock-face.lib**: `RockyMcRockFaceSubmod` (+ `.Ui` partial), `RingSwapController` (snapshot/apply/restore/rebuild), `RingAssetCatalog`, `RingMeshFactory` (interleaved→Simple clone cache), `RingSelection`, `RockyUi` (public form/grid/combo helpers, reused by bloomin-onion)

### [bloomin-onion](bloomin-onion) / [bloomin-onion.lib](bloomin-onion.lib)
Define **brand-new planetary rings at runtime** and apply them to **any celestial body** (a moon, Earth, a ringless gas giant). Every parameter KSA's ring XML exposes is editable in an ImGui panel: geometry (frame, inclination, ascending node, inner/outer radius with a *Fit to Body* helper, detail scale), the 2D ring band — either **painted** (base color + colored stripes/gaps with softness, ringlet noise, live preview strip) or any game texture plus a control strip — volumetric dust (thickness/render-distance/raymarch step ranges, fade-to-meshes), and the instanced rock field (size, thickness, draw distance, density with a per-chunk instance estimate, 1–5 LODs with per-LOD mesh from the whole game mesh catalog, PBR material).
- **Data-level, no Harmony patches**: builds a complete `PlanetaryRingsReference` tree (volume, step, ring objects, LODs, material, value wrappers with the game's angle normalization), assigns it to `Celestial.BodyTemplate.RingsReference` (original snapshotted for Remove), refreshes the transparencies renderer's body list (public `PopulatePlanets()` + private `_anyRings`), disposes the rings renderer and runs the game's own `Program.RebuildRenderer()`
- **Painted textures at runtime**: `RingBandPainter` rasterizes stripes/noise into a 2048×1 RGBA8 band + control strip (R = rocks allowed, G = dust thickness); `PaintedTextureReference` is a `TextureReference` subclass fed from a `GenericTexture` → `TextureAsset` and bound through the game's own `Bind` (bindless handle, CPU copy retained for the per-frame control-strip sampling); cached by content hash, freed once unreferenced after a rebuild
- Reuses rocky-mcrock-face's `RingAssetCatalog` / `RingMeshFactory` for meshes and textures; stock Saturn assets fill any empty slot (resolved from the system's existing ring, else by id)
- **Presets** persist to `.unscience/bloomin-onion-rings.toml` (Tomlyn); *Copy <body>'s Ring* imports an existing definition (e.g. Saturn's) as a starting point; body assignments are session-only by design
- Ring shadow on the planet follows automatically (per-frame read); the far-away distant-sphere shadow is synced best-effort by reflection
- Vessels/kittens are **not** supported directly (the ring renderer is Celestial-bound at every level); weld a small ringed moon to a vessel with kiwis-marbles instead
- F11 window toggle (standalone mode); unscience supermod integration via `ISubmod`
- **bloomin-onion.lib**: `BloominOnionSubmod` (+ `.Ui`, `.UiSections` partials), `RingDefinition` (+ `RingStripe`, `RingLodDefinition`), `RingDefinitionController` (apply/remove/snapshots/prune), `RingReferenceBuilder` (definition → game tree, validation), `RingRendererRebuilder` (rebuild + distant-sphere sync), `RingBandPainter`, `PaintedTextureReference`, `RingTextureFactory`, `StockRingAssets`, `RingPresetStore`, `RingDefinitionSerializer`

---

## Unified Supermod

### [unscience](unscience)
Unified supermod that consolidates the standalone feature mods into a single ImGui window with collapsible headers and a gear icon (⚙) context menu for per-submod visibility toggles. All submod logic lives directly in the respective `.lib` projects — unscience instantiates these lib submods and orchestrates them via the `ISubmod` interface from `ksa-abstractions.lib`. A single Harmony instance consolidates their patches. Standalone mods continue to work independently.
- F11 window toggle with unified panel for all core submods
- Submods: Average TWR, Blinky, Bloomin' Onion, Camera Controller Override, Con-Man, Doh, Don't Stifle Me, Eternal Flame, Garry's Torch, G-Force Monitor, Glass, Graffiti, Hot Pursuit, Humble Arteest (Vehicle Paint, Kitten Color, Engine Emissive), I Feel Seen, Its So Shiny, Kitchen Sink, Kitten Animations, Kiwi's Marbles, Parts Now, Pyro, Red Alert, Rocky McRock Face, Skittles, Thug Life, Unladen Swallow, Zippo (27 total)
- Uses `ISubmod` interface (from `ksa-abstractions.lib`): `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- Each submod class lives in its `.lib` project (e.g. `AverageTwrSubmod` in `average-twr.lib`, `BlinkySubmod` in `blinky.lib`)
- `unscience/Submods/` directory removed — no thin UI wrapper layer; submod classes own their own ImGui rendering
- `Update(dt)` runs every frame for all submods (even hidden) for frame-critical logic
- Consolidated Harmony patches include blinky render-skip, camera-controller-override sequence playback, glass main-camera FOV override, hot-pursuit fixed-camera pose, humble-arteest vehicle paint + engine emissive, i-feel-seen render distance, skittles hotkey blocking, pyro exhaust submission, and graffiti decal pass
- References all feature `.lib` projects, including `hot-pursuit.lib`, plus `ksa-abstractions.lib`

---

## Template/Placeholder Mods

### [fixme-mod-name](fixme-mod-name) / [fixme-mod-name.lib](fixme-mod-name.lib)
Placeholder/template mod with basic mod structure. Requires proper naming and implementation.
- Basic mod skeleton
- F11 window toggle
- Ready for feature development

---

## Part Editor Mods

### [parts-now](parts-now) / [parts-now.lib](parts-now.lib)
Runtime Part / SubPart loader. Paste Part XML into a brand new mod folder, or load / reload / unload an existing mod folder, without restarting the game. New parts appear in the vehicle editor's part browser immediately with thumbnails and game data attached.
- Paste XML workflow — mod-id form with live validation, three tabbed XML documents (Assets / Part / GameData, 256 KiB each) with clipboard paste, Validate then Install & Load
- Writes a real KSA mod folder (`mod.toml` + XML, UTF-8 no BOM, LF, atomic tmp+move) under the game's discovered mods path and adds an enabled manifest entry so the parts also load at next launch
- Mod folder workflow — scans the mods directory, classifies each folder (Content / StarMap / Both / Empty, LoadedAtBoot / LoadedByPartsNow / NotLoaded) and offers Load / Reload / Unload only where it is safe
- Reload = purge + load, so an edited GLB, KTX2 or XML really is re-read (`SerializedCollection.Register` silently drops duplicate ids, which would otherwise skip the file read)
- Fail-closed reload/unload safety gate — refuses while a live vehicle flies one of the parts, while the vehicle editor holds one, while a job is in flight, or for anything KSA loaded at boot
- Mesh headroom reservation — inflates `DeviceMeshInterleaved.Shared`'s size counters from `[StarMapAllModsLoaded]` (before `ModLibrary.Bind` allocates the single shared interleaved buffer) and rewinds the bump cursor on the first frame; configurable in `parts-now.toml`, effective next launch, with leak accounting for bytes an unload can never reclaim
- Fifteen validation rules (V1–V15) run before anything is written or registered, including the crash guard that rejects a `<PbrMaterial>` missing any of Diffuse / Normal / AoRoughMetal
- Incremental game-data attach, model warming, and per-part thumbnail rendering (2 per frame) against KSA's offscreen thumbnail viewport
- Single reflection layer with a self-test that disables loading with a readable message instead of crashing when KSA internals move
- Standalone window on F10 (configurable); also an unscience submod
- **parts-now.lib**: `PartsNowSubmod` (ISubmod entry point), `GameRegistry` (the only reflection into `ModLibrary` internals), `MeshBudget` (shared-buffer headroom + leak accounting), `BundleParser` / `BundleValidator` (V1–V15), `RuntimeModLoader` (load state machine), `RuntimeModUnloader` (safety gate + purge/rollback), `PartThumbnailGenerator`, `ModIdValidator` / `ModFolderWriter` / `ModFolderScanner`, `StatusPanel` / `PastePanel` / `ModFolderPanel` / `ResultsPanel`

---

### [dont-stifle-me](dont-stifle-me) / [dont-stifle-me.lib](dont-stifle-me.lib)
Vehicle editor un-limiter. KSA build `2026.8.22.5348` clamped top-level part scaling to 0.5x–2x and made the scale gizmo uniform across all axes; this mod restores the old freedom behind a single toggle.
- Two toggles: **Enabled** (clamp removal + per-axis scaling, default on) and **Snap scaling** (0.25 m diameter increments, default on = game behavior); flip live
- Postfix on `VehicleEditor.ScaleBoundsFor` widens bounds to `(1e-6, +inf)`; prefix on `UpdateSelectedScale` applies the drag to the dragged axis only; prefix on `QuantizeScale` bypasses snapping when off
- Reuses the game's private `QuantizeScale` / `ForEachPartWithSymmetry` delegates so snapping (when on) and symmetry match stock
- Standalone: "Don't Stifle Me" top-level menu via `Program.DrawProgramMenusHook` postfix; bundled in unscience as a submod section
- Known limit: connectors/mass follow the largest axis (game's `ScaleFactors`), so non-uniform parts may have off-surface connectors

## Orbit & Navigation Mods

### [marque](marque) / [marque.lib](marque.lib)
Orbit line visibility manager. Adds a **Marque** submenu to the game's View menu bar for toggling orbit lines on vehicles and celestial bodies.
- **Vehicles** submenu: All/None bulk toggle + alphabetically sorted individual vehicle toggles with checkmarks
- **Celestials** submenu: All/None bulk toggle + hierarchical celestial tree organized by SOI
  - Planets with moons open as submenus with their own All/None controls
  - Recursive depth — moons with sub-moons get nested submenus
- Menus stay open after clicking (no auto-close) for fast multi-toggle workflows
- Harmony prefix on `GaugeCanvas.OnDrawMenuBar` to inject into the View menu
- **marque.lib**: `MarqueLib.DrawMarqueMenus()` — full menu rendering logic, vehicle listing, celestial hierarchy traversal, orbit visibility toggling via `IOrbiter.ShowOrbit`


---

## Organization Notes

- **Top-level mods**: Folders without `.lib` suffix or standalone folders are runnable mods
- **.lib folders**: Contain headless/library functionality that can be used by the corresponding mod
- **ksa-abstractions.lib**: Shared utilities used across multiple mods
