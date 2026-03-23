
# KSA Mod Experiments - Repository Index

This document serves as a comprehensive index of all mods and libraries in this KSA mod experiment project. It's designed to help AI agents and developers quickly discover existing functionality and understand the purpose of each mod.

## Core Libraries

### [ksa-abstractions.lib](ksa-abstractions.lib)
Shared library with common abstractions used across multiple mods. Provides utility classes and base functionality.
- Reusable abstractions for various mods
- Common vehicle operations
- Utility classes for mod development

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
- ImGui control panel with preset system

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
Advanced camera animation system. Provides configurable zoom, pan, and spiral animations with easing functions for orbit and fly camera modes.
- Zoom in/out animations with configurable duration and easing
- Zoom to offset position (orbital tracking)
- Spiral zoom animation
- OrbitController and FlyController patching
- Easing function support

### [glass](glass) / [glass.lib](glass.lib)
Camera FOV control. Provides 8 lens presets (from super telephoto at 15° to fisheye at 120°) and manual FOV adjustment.
- 8 camera lens presets (telephoto, wide-angle, fisheye, etc.)
- Manual FOV slider control
- Real-time FOV adjustment
- Camera.FieldOfView and Camera.UpdateProjection patching
- Game default preset (50°)

---

## Information Display & Monitoring Mods

### [average-twr](average-twr) / [average-twr.lib](average-twr.lib)
TWR (Thrust-to-Weight Ratio) calculator and display. Shows real-time TWR and maximum acceleration data for the controlled vehicle.
- Real-time TWR calculation
- Maximum acceleration computation
- Sample collection at 100 Hz
- ImGui display window (F11 toggle)

### [geeforce](geeforce) / [geeforce.lib](geeforce.lib)
G-force recorder and display. Monitors acceleration forces (g-forces) acting on the controlled vehicle at 40 Hz sample rate.
- G-force acceleration measurement
- 40 Hz sampling rate (25ms intervals)
- GForceRecorder with configurable capacity
- ImGui display window
- Real-time acceleration data collection

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
Dynamic LCD pixel grid builder. Builds an NxM engine pixel grid at runtime by dynamically creating and attaching engine parts to an existing vehicle.
- Runtime part creation via manual `TreeParent`/`TreeChildren` wiring — no pre-built vehicle needed
- Configurable grid size (1–64 cols × 1–32 rows)
- Configurable spacing (0.1–5.0 m between pixels) and XYZ offset from vehicle root
- Engine template quick-select (EngineA1–A6)
- Batch creation with single `PartTree.CreateFromNewPartTree()` rebuild (N→1 recomputes)
- Same pattern and animation controls as blinken (reuses blinken.lib)
- Build/Destroy grid at any time; destruction splits pixel parts back out of the vehicle
- Debug panel: runtime dump of vehicle parts type, root part, engine templates list

### [kitten-animations](kitten-animations) / [kitten-animations.lib](kitten-animations.lib)
Kitten avatar animation controller. Manages animations for the kitten avatar character with frame-by-frame updates.
- Character animation control
- Kitten avatar integration
- Animation updates per frame
- Avatar accessor for state queries

### [byo-music](byo-music) / [byo-music.lib](byo-music.lib)
Bring Your Own Music - Custom music player. Plays audio playlists from defined assets (e.g., SabotageMusic playlist).
- Playlist loading and playback
- Asset-driven music integration
- ImGui music control panel
- Multi-sound support

---

## Template/Placeholder Mods

### [fixme-mod-name](fixme-mod-name) / [fixme-mod-name.lib](fixme-mod-name.lib)
Placeholder/template mod with basic mod structure. Requires proper naming and implementation.
- Basic mod skeleton
- F11 window toggle
- Ready for feature development


---

## Organization Notes

- **Top-level mods**: Folders without `.lib` suffix or standalone folders are runnable mods
- **.lib folders**: Contain headless/library functionality that can be used by the corresponding mod
- **ksa-abstractions.lib**: Shared utilities used across multiple mods