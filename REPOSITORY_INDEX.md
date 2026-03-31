
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
- `ISubmod` — generic submod interface used by grant supermod: `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`

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

### [garys-torch](garys-torch) / [garys-torch.lib](garys-torch.lib)
Vehicle welding system. Attaches one vehicle to another with support for position offsets, rotation, and uniform scaling. Welds persist per-frame.
- Vehicle-to-vehicle welding with position offset (XYZ in body frame)
- Per-weld rotation offset (pitch/yaw/roll)
- Uniform vehicle scaling with KittenEva avatar support
- Rotation lock toggle and auto-unweld on parent mismatch
- Multiple simultaneous welds
- User-defined presets persisted to TOML (`~/.iryr/garrys-torch-presets.toml`)
- Save weld settings as named presets, load presets into create form
- ImGui control panel with filterable combos and bordered weld sections

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
Light control system. Selects vehicles and light parts, then controls their intensity and color using XKCD color palette.
- Vehicle and light part selection
- Light intensity control (0-1 slider)
- Light color preset selection (Marine, HotPink, RadioactiveGreen, BabyPurple)
- On/off toggle for lights
- Recursive part tree search for light components
- Real-time light property updates

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

---

## Animation & Visual Effects Mods

### [blinken](blinken) / [blinken.lib](blinken.lib)
LCD display animation system for pre-built pixel engine grids. Scans vehicles for parts named `pixel_{row}_{col}_{a|b}` and animates them as an LCD scrolling display.
- Pixel grid (LCD) rendering via engine on/off control
- Scrolling text/image animation with configurable speed
- Engine controller caching for per-frame O(1) access
- Pattern presets: All On, Checkerboard, Alt Rows/Cols
- `PixelGrid.ScanFromVehicle()` — scans vehicle for pixel engine pairs
- `LcdAnimation` — manages scroll state and updates engine active states

### [blinky](blinky) / [blinky.lib](blinky.lib)
Dynamic LCD pixel grid builder. Builds NxM engine pixel grids at runtime by dynamically creating and attaching engine parts to existing vehicles. Supports **multiple named grids per vehicle** via compound `(vehicleId, gridName)` key. Self-contained — does NOT depend on blinken.lib.
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

### [kitten-animations](kitten-animations) / [kitten-animations.lib](kitten-animations.lib)
Kitten avatar animation controller. Manages MMU body animations, facial expressions, and walking animations for the kitten avatar character with smooth ease-in transitions.
- 7 MMU body movement animations (idle, move in 6 directions)
- 5 facial expressions (angry, awe, happy, sad, scared) with configurable duration (1–5s)
- 2 walking animations (running, walking)
- Smooth 250ms quadratic ease-in for expression weight blending
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
HTTP RPC server mod. Embeds a GenHTTP server (`0.0.0.0:7887`) that exposes KSA mod functionality over a REST API. ImGui window (F11 toggle) with enable/disable checkbox. Exposes camera FOV control via `glass.lib` and blinky pixel grid control via `blinky.lib`.
- F11 toggle ImGui window
- Enable/disable HTTP server via checkbox
- Live server status indicator (Running/Stopped)
- `GET /health` — server liveness check
- `GET /fov` — returns current FOV state (current, override, isActive)
- `POST /fov` — sets camera FOV override (`{ "fov": 30.0 }`) or disables it (`{ "fov": 0 }`)
- `POST /blinky/animate` — starts a scrolling animation on a vehicle's named grid (`{ "vehicleId": "...", "gridName": "...", "pixels": [...], "speed": 1.0 }`)
- `POST /blinky/static` — displays a static pixel pattern on a vehicle's named grid (`{ "vehicleId": "...", "gridName": "...", "pixels": [...], "reset": true }`)
- `POST /blinky/off` — turns off all pixels and stops scroll on a vehicle's named grid (`{ "vehicleId": "...", "gridName": "..." }`)
- `GET /blinky/grids` — lists all registered grids (optional `vehicleId` query filter)
- **unladen-swallow.lib**: `SwallowServer` (GenHTTP host), `FovEndpoint`, `BlinkyAnimateEndpoint`, `BlinkyStaticEndpoint`, `BlinkyOffEndpoint` (all with game-thread scheduling), shared API types. References `glass.lib`, `blinky.lib`, and `ksa-abstractions.lib`.

---

## Rendering & Thumbnails

### [inanimate-carbon-rod](inanimate-carbon-rod) / [inanimate-carbon-rod.lib](inanimate-carbon-rod.lib)
On-demand subpart thumbnail generator. The game skips thumbnail generation for subparts (`IsSubPart == true`) during startup — this mod generates them at runtime via the same Vulkan rendering pipeline, triggered by a button click in the UI.
- F10 window toggle (standalone mode)
- One-click "Generate Subpart Thumbnails" button
- Mirrors game's `ThumbnailCreator` Vulkan rendering loop exactly
- Saves/restores camera and viewport state after generation
- Scrollable thumbnail grid (64x64 images) with subpart ID tooltips
- Progress bar and status display during generation
- Static cache (`SubpartThumbnailCache`) for cross-mod access to generated thumbnails
- **inanimate-carbon-rod.lib**: `SubpartThumbnailGenerator` (on-demand Vulkan rendering), `SubpartThumbnailCache` (static thumbnail storage), `LdrPostPassCommand` (HDR→LDR blit for VRAM optimization), `InanimeCarbonicRodSubmod` (ISubmod — full UI)
- No Harmony patches required — uses only public game APIs (plus reflection for `ModLibrary.AllParts`)

---

## Unified Supermod

### [grant](grant)
Unified supermod that consolidates 14 standalone mods into a single ImGui window with collapsible headers and a gear icon (⚙) context menu for per-submod visibility toggles. All submod logic lives directly in the respective `.lib` projects — grant instantiates these lib submods and orchestrates them via the `ISubmod` interface from `ksa-abstractions.lib`. A single Harmony instance consolidates patches from blinky, camera-controller-override, glass, i-feel-seen, and skittles. Standalone mods continue to work independently.
- F11 window toggle with unified panel for all 15 submods
- Submods: Average TWR, Blinky, Camera Controller Override, Con-Man, Eternal Flame, Gary's Torch, G-Force Monitor, Glass, I Feel Seen, Inanimate Carbon Rod, Kitten Animations, Kiwi's Marbles, Skittles, Unladen Swallow, Zippo
- Uses `ISubmod` interface (from `ksa-abstractions.lib`): `Name`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- Each submod class lives in its `.lib` project (e.g. `AverageTwrSubmod` in `average-twr.lib`, `BlinkySubmod` in `blinky.lib`)
- `grant/Submods/` directory removed — no thin UI wrapper layer; submod classes own their own ImGui rendering
- `Update(dt)` runs every frame for all submods (even hidden) for frame-critical logic
- Consolidated Harmony patches: blinky render-skip, camera-controller-override sequence playback, glass FOV override, i-feel-seen render distance, skittles hotkey blocking
- References all `.lib` projects: average-twr.lib, blinky.lib, camera-controller-override.lib, con-man.lib, eternal-flame.lib, garys-torch.lib, geeforce.lib, glass.lib, i-feel-seen.lib, inanimate-carbon-rod.lib, kitten-animations.lib, kiwis-marbles.lib, skittles.lib, unladen-swallow.lib, zippo.lib, ksa-abstractions.lib

---

## Template/Placeholder Mods

### [fixme-mod-name](fixme-mod-name) / [fixme-mod-name.lib](fixme-mod-name.lib)
Placeholder/template mod with basic mod structure. Requires proper naming and implementation.
- Basic mod skeleton
- F11 window toggle
- Ready for feature development

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