
# KSA Mod Experiments - Repository Index

This document serves as a comprehensive index of all mods and libraries in this KSA mod experiment project. It's designed to help AI agents and developers quickly discover existing functionality and understand the purpose of each mod.

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
- `EasingType` enum + `EasingHelper.ApplyEasing()` — shared easing utility (Linear/EaseIn/EaseOut/EaseInOut with power params); used by zippo.lib, garrys-torch.lib, camera-controller-override.lib
- `XkcdColorHelper` — cached reflection-based lookup of all ~950 `KSAColor.Xkcd` named colors; provides `GetAll()`, `FindByName()`, `GetNames()`; used by zippo.lib and doh.lib

---

## Vehicle Manipulation Mods

### [eternal-flame](eternal-flame) / [eternal-flame.lib](eternal-flame.lib)
Infinite fuel hack. Monitors selected vehicles and periodically refills their consumables at a configurable interval.
- Filterable vehicle combo box for selection
- Add/remove vehicles to a monitored list
- Per-vehicle active/inactive toggle
- Configurable refill interval (0–1000ms drag slider)
- Background refill loop runs independently of UI visibility
- F11 window toggle

### [garrys-torch](garrys-torch) / [garrys-torch.lib](garrys-torch.lib)
Vehicle welding system. Attaches one vehicle to another with support for position offsets, rotation, and uniform scaling. Welds persist per-frame.
- Vehicle-to-vehicle welding anchored to a **specific part** on the target vehicle (CoM-drift-proof; tracks robotics-moved parts)
- Position and rotation offsets expressed relative to the target part's local frame
- Per-weld rotation offset (pitch/yaw/roll)
- Uniform vehicle scaling with KittenEva avatar support
- Rotation lock toggle and auto-unweld on parent mismatch
- Weld updates run from a Harmony prefix on `Universe.ExecuteNextVehicleSolvers`, before KSA queues vehicle solver jobs; this avoids refactored physics-loop kinematic/analytic state races
- Multiple simultaneous welds with topological sort for correct ordering
- User-defined presets persisted to TOML (`~/.unscience/garrys-torch-presets.toml`)
- Save weld settings as named presets, load presets into create form
- ImGui control panel with filterable combos (vehicle → part → preset) and bordered weld sections
- **Animation system**: Smooth interpolation of weld position/rotation/scale with configurable easing (Linear, EaseIn, EaseOut, EaseInOut) and per-power control. Queued animations per weld.
- **Public API**: `GarrysTorchSubmod.Instance` singleton, `CreateWeld`, `ModifyWeld`, `RemoveWeld`, `AnimateWeld`, `FindWeld`, preset pass-throughs — exposed for use by `unladen-swallow.lib` RPC endpoints
- **Safe update API**: `GarrysTorchSubmod.UpdateBeforeVehicleSolvers(dt)` performs animation and weld teleports; ordinary `ISubmod.Update(dt)` is intentionally non-mutating for weld physics

### [kiwis-marbles](kiwis-marbles) / [kiwis-marbles.lib](kiwis-marbles.lib)
Celestial body welding mod. Repositions planets and moons by welding them to follow other celestial bodies or vehicles at user-defined offsets. Bypasses physics for the source body, updating it every game tick.
- Weld any planet or moon to any orbiter (celestial or vehicle)
- CCI-frame offset input with unit scale selector (m / km / Mm / Gm)
- Live offset editing per active weld
- Cross-parent welding via `Celestial.SetOrbit()` auto-reparenting
- Multiple welds with topological sort for correct weld chain ordering
- ImGui control panel (F9 toggle)
- **kiwis-marbles.lib**: `CelestialWeldEntry` (Source/Target/Offset) and `CelestialWeldEngine` (per-frame repositioning + Kahn's topological sort)

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
- **Static display** — paints a set of pixels with optional intelligent diff (reset mode)
- **Off** — turns off all pixels and stops any running scroll on a specific grid
- Pattern presets: All On, Checkerboard, Alt Rows, Alt Cols
- Render engine meshes toggle for performance boost
- Build/Destroy individual grids at any time; vehicle combo selector with filter
- Per-grid collapsible UI sections with info table, pattern buttons, and destroy
- Menu bar with Debug menu for global grid scanning
- **blinky.lib**: `BlinkyGridManager` (compound-key scroll/static/off/pattern APIs, `ScanAllVehicles`), `ScrollAnimation`, `PixelGrid` (single-grid + `ScanAllFromVehicle` auto-discovery), `PixelPatterns`, `LcdGridConfig`, `LcdGridBuilder`, `BlinkyPixelGrid`. Used by `unladen-swallow.lib` for RPC endpoints.

### [its-so-shiny](its-so-shiny) / [its-so-shiny.lib](its-so-shiny.lib)
Light-part pixel grid builder. Builds Blinky-style NxM grids using KSA's built-in `LightPart` instead of engine parts, avoiding engine ignition, thrust cancellation, and fuel/resource graph complexity.
- Runtime `LightPart` creation via manual `TreeParent`/`TreeChildren` wiring and a single part-tree rebuild
- One light part per pixel, named `shiny_{gridName}_{row}_{col}`
- Flat and cylindrical layouts with configurable grid size, spacing, offset, light scale, color, and intensity
- Connects created light parts to battery-bearing parts when available so stock `PowerConsumer` light switches can receive power
- Pattern controls: off, all on, alternating rows, alternating columns, checkerboard
- Global scan discovers existing `shiny_*` grids across loaded vehicles
- Standalone F11 ImGui window plus direct unscience submod integration
- **its-so-shiny.lib**: `ItsSoShinySubmod` (ISubmod UI), `ShinyGridManager` (registration, patterns, static display, scroll APIs), `ShinyGridBuilder` (runtime creation/destruction), `ShinyPixelGrid`, `ShinyPixelCell`, `ShinyGridConfig`, `ShinyScrollAnimation`, `ShinyPixelPatterns`.

### [kitten-animations](kitten-animations) / [kitten-animations.lib](kitten-animations.lib)
Kitten avatar animation controller. Manages MMU body animations, facial expressions, and walking animations for the kitten avatar character with smooth ease-in transitions.
- 7 MMU body movement animations (idle, move in 6 directions)
- 5 facial expressions (angry, awe, happy, sad, scared) with configurable duration (1–5s)
- 2 walking animations (running, walking)
- Smooth 250ms quadratic ease-in for expression weight blending
- Clears KSA's cached `CatExpressionAnim` pose when switching expression assets so each expression triggers independently on current game builds
- **kitten-animations.lib**: `KittenAnimationsSubmod` (ISubmod — owns KittenAnimationController and all animation UI in RenderContent), `KittenAnimationController` (expression state + timers + playback), `KittenAvatarAccessor` (reflection-based avatar access)

### [byo-music](byo-music) / [byo-music.lib](byo-music.lib)
Bring Your Own Music - Custom music player. Plays audio playlists from defined assets (e.g., SabotageMusic playlist).
- Playlist loading and playback
- Asset-driven music integration
- ImGui music control panel
- Multi-sound support

---

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
HTTP RPC server mod. Embeds a GenHTTP server (`0.0.0.0:7887`) that exposes KSA mod functionality over a REST API. ImGui window (F11 toggle) with enable/disable checkbox. Exposes camera FOV control via `glass.lib`, blinky pixel grid control via `blinky.lib`, and camera animation sequencing via `camera-controller-override.lib`.
- F11 toggle ImGui window
- Enable/disable HTTP server via checkbox
- Live server status indicator (Running/Stopped)
- `GET /health` — server liveness check
- `GET /fov` — returns current FOV state (current, override, isActive)
- `POST /fov` — sets camera FOV override (`{ "fov": 30.0 }`) or disables it (`{ "fov": 0 }`)
- Expanded blinky API (13 endpoints) covering grid lifecycle, animation control, render settings, and engine control:
- `GET /blinky/grids` — list registered grids (optional `vehicleId` filter)
- `POST /blinky/grids` — build and register a new grid on a vehicle
- `DELETE /blinky/grids` — destroy/unregister a grid (`vehicleId` and `gridName` query params)
- `POST /blinky/grids/scan` — scan a specific vehicle for an existing named grid
- `POST /blinky/grids/scan-all` — discover and register grids across all vehicles
- `POST /blinky/animate` — start scrolling animation from client-supplied pixels
- `DELETE /blinky/animate` — stop an active scroll without clearing current pixels
- `POST /blinky/animate/builtin` — start built-in scrolling animation payload
- `POST /blinky/static` — display a static pixel pattern
- `POST /blinky/pattern` — apply built-in patterns (`allOn`, `allOff`, `checkerboard`, `altRows`, `altCols`)
- `POST /blinky/off` — turn all pixels off and stop scroll
- `GET /blinky/render` / `POST /blinky/render` — get/set pixel part mesh rendering toggle
- `POST /blinky/engines/deactivate` — deactivate non-LCD engines on a vehicle
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
- **unladen-swallow.lib**: `SwallowServer` (GenHTTP host), `FovEndpoint`, `BlinkyListEndpoint`, `BlinkyGridsEndpoint`, `BlinkyGridScanEndpoint`, `BlinkyGridScanAllEndpoint`, `BlinkyAnimateEndpoint`, `BlinkyBuiltInScrollEndpoint`, `BlinkyStaticEndpoint`, `BlinkyPatternEndpoint`, `BlinkyOffEndpoint`, `BlinkyRenderEndpoint`, `BlinkyEngineDeactivateEndpoint`, `CameraAnimateEndpoint`, `CameraStatusEndpoint`, `CameraStopEndpoint`, `TorchWeldsEndpoint`, `TorchWeldModifyEndpoint`, `TorchWeldAnimateEndpoint`, `TorchPresetsEndpoint` (all with game-thread scheduling), shared API types. References `glass.lib`, `blinky.lib`, `camera-controller-override.lib`, `garrys-torch.lib`, and `ksa-abstractions.lib`.

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
- **Vehicle Paint**: Per-part RGB tinting by hijacking PerInstanceData padding bytes (3 unused ints at bytes 68–79) and compiling modified GLSL shaders at runtime via `ShaderModuleUtils.FromFile()` + `ShaderReference` swap + `PartModelRenderer.ColorData.Rebuild()`
- **Kitten Color**: Tints character models (fur, glass, eyes) by writing AlbedoColor to the `GpuMaterialSystem.BigBuffer` via Vulkan staged uploads. Only affects `ModelPbr.frag` path — vehicle parts are unaffected.
- **Engine Emissive**: Per-engine Temperature/TFI override via Harmony prefix on `PartModelDynamic.AddInstance()`. No shader modifications needed — uses the game's existing emissive color LUT.
- F11 window toggle (standalone mode)
- Unscience supermod integration via `ISubmod`: `VehiclePaintSubmod`, `KittenColorSubmod`, `EngineEmissiveSubmod`
- Harmony patches: `VehiclePaintPatches` (PartModel.AddInstance), `EngineEmissivePatches` (PartModelDynamic.AddInstance)
- Experiments directory with Phase 0 feasibility validation tests
- **humble-arteest.lib**: `VehiclePaint` (shader swap + paint state), `VehiclePaintPatches`, `VehiclePaintSubmod`, `KittenColor` (GPU buffer writes), `KittenColorSubmod`, `EngineEmissive` (temperature state), `EngineEmissivePatches`, `EngineEmissiveSubmod`

---

## Unified Supermod

### [unscience](unscience)
Unified supermod that consolidates 14 standalone mods into a single ImGui window with collapsible headers and a gear icon (⚙) context menu for per-submod visibility toggles. All submod logic lives directly in the respective `.lib` projects — unscience instantiates these lib submods and orchestrates them via the `ISubmod` interface from `ksa-abstractions.lib`. A single Harmony instance consolidates patches from blinky, camera-controller-override, glass, i-feel-seen, and skittles. Standalone mods continue to work independently.
- F11 window toggle with unified panel for all core submods
- Submods: Average TWR, Blinky, Camera Controller Override, Con-Man, Doh, Eternal Flame, Garry's Torch, G-Force Monitor, Glass, Humble Arteest (Vehicle Paint, Kitten Color, Engine Emissive), I Feel Seen, Kitten Animations, Kiwi's Marbles, Skittles, Space Tape, Unladen Swallow, Zippo
- Uses `ISubmod` interface (from `ksa-abstractions.lib`): `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- Each submod class lives in its `.lib` project (e.g. `AverageTwrSubmod` in `average-twr.lib`, `BlinkySubmod` in `blinky.lib`)
- `unscience/Submods/` directory removed — no thin UI wrapper layer; submod classes own their own ImGui rendering
- `Update(dt)` runs every frame for all submods (even hidden) for frame-critical logic
- Consolidated Harmony patches: blinky render-skip, camera-controller-override sequence playback, glass FOV override, humble-arteest vehicle paint + engine emissive, i-feel-seen render distance, skittles hotkey blocking
- References all `.lib` projects: average-twr.lib, blinky.lib, camera-controller-override.lib, con-man.lib, eternal-flame.lib, garrys-torch.lib, geeforce.lib, glass.lib, humble-arteest.lib, i-feel-seen.lib, kitten-animations.lib, kiwis-marbles.lib, skittles.lib, space-tape.lib, unladen-swallow.lib, zippo.lib, ksa-abstractions.lib

---

## Template/Placeholder Mods

### [fixme-mod-name](fixme-mod-name) / [fixme-mod-name.lib](fixme-mod-name.lib)
Placeholder/template mod with basic mod structure. Requires proper naming and implementation.
- Basic mod skeleton
- F11 window toggle
- Ready for feature development

---

## Part Editor Mods

### [space-tape](space-tape) / [space-tape.lib](space-tape.lib)
In-game Part editor. Compose new Parts from existing SubParts by placing them in 3D space with transform controls. Saves Part definitions as KSA mod XML files.
- Owns SubPart thumbnail generation and cache
- Thumbnail rendering quietly restores KSA camera follow/control state without emitting `Following ...` timed alerts
- Unscience panel minimal flow: `Load SubParts` + `Open/Close Part Editor`
- Load SubParts modal with generation controls (Images per SubPart, image size, Generate/Re-generate, generation progress)
- Dedicated SubParts floating window tied to Part Editor lifecycle
- SubParts window view controls: grid/list mode toggle, thumbnail size, animation delay, filter, and large viewer toggle
- Load/import workflow uses compact 2x2 filterable combo table (category/part + import source)
- Save flow moved to toolbar `Save` button with a modal popup
- SubPart catalog browser with animated thumbnail previews
- 3D editing scene with gizmos for translate/rotate/scale and origin axis marker
- Hover highlight and click-to-select SubParts in the 3D viewport with native highlight/selection shaders
- Quick-flip rotation hotkeys (D = +45° Y-axis, F = +45° X-axis)
- Plane-locked drag — P key cycles pan modes (Normal / YZ / XZ / XY), click-and-drag to move SubParts constrained to a plane
- Camera snap views (Front, Back, Left, Right, Top, Bottom) for standard orthographic vantage points
- Grid plane overlay — translucent origin-centered reference grids with independent X/Y/Z plane toggles plus configurable size, spacing, regular color, and axis-line color/alpha
- Grid rendering uses KSA's orbit line renderer to preserve line alpha without modifying core shader files
- Import existing game parts (SubParts, Connectors, Tanks, Batteries, Generators, etc.)
- Import hardening: logs and skips invalid imported SubPart records; editor gizmo rendering guards invalid mesh data while Space Tape is active
- Fuel tank definition (Cylindrical/Spherical) with full material/density/mass config
- Connector system — define attachment points with position, rotation, and flag types (Internal/ToSurface/FromSurface)
- Coupling support — Decoupler, Docking Port, and EVA Door with connector references
- Multiple Batteries, Generators, and Power Consumers per part
- 3D connector gizmo visualization (color-coded by flag type, highlights selected)
- ImGui property panel with transform editing, GameData sections (Tank, Power, Connectors, Coupling)
- Saves Part XML + GameData XML to space-tape-parts mod directory with Tomlyn mod.toml management
- Hot-reload spike for registering parts at runtime without restart
- **space-tape.lib**: `SpaceTapeSubmod` (ISubmod entry point), `CameraSnapController` (camera snap and OrbitLinePass grid renderer), `PartEditorInteraction` (hover/select/drag/quick-flip/plane-drag), `PartCatalog`, `PartImporter`, `GameDataEditorUi`, `ConnectorGizmo`

### [flexo](flexo) / [flexo.lib](flexo.lib)
Robotics mod. Introduces articulated Parts (hinges, rotors) to KSA's static Part system. Design robotic parts in a dedicated editor, then control them at runtime.
- Hinge creator: select fixed and moving Parts from a vehicle, define rotation axis, degree range, resting position, and motor speed
- TOML-based persistence — flexo definitions saved to `~/.flexo/flexo_part_*.toml`
- Vehicle scanning — detect flexo parts on the active vehicle by matching Part template IDs and connectivity
- Runtime hinge control — open/close/reset buttons, manual angle slider, animated rotation via `Part.Asmb2ParentAsmb`
- 3D editor scene with camera snaps, lighting, hover/select interaction (reuses space-tape patterns)
- Live preview — rotate Parts in the editor to verify hinge axis and range before saving
- Unscience integration as ISubmod with runtime panel and floating editor window
- **flexo.lib**: `FlexoSubmod` (ISubmod entry point), `FlexoDataManager` (TOML persistence), `HingeController` (per-instance rotation math), `FlexoEditorScene`, `FlexoEditorInteraction`, `FlexoEditorUi`

---

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