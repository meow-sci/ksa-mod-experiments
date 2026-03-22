
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

---

## Vehicle Manipulation Mods

### [garys-torch](garys-torch) / [garys-torch.lib](garys-torch.lib)
Vehicle welding system. Attaches one vehicle to another with support for position offsets, rotation, and uniform scaling. Welds persist per-frame.
- Vehicle-to-vehicle welding with position offset (XYZ in body frame)
- Per-weld rotation offset (pitch/yaw/roll)
- Uniform vehicle scaling with KittenEva avatar support
- Rotation lock toggle and auto-unweld on parent mismatch
- Multiple simultaneous welds
- ImGui control panel with preset system

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
LCD display animation system. Provides pixel grid rendering and text scrolling animations for display panels.
- Pixel grid (LCD) rendering
- Text scrolling animations
- Animation controller
- Engine controller integration

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

## UI & Customization Mods

### [skittles](skittles) / [skittles.lib](skittles.lib)
Global ImGui theme manager. Provides a theme picker and a full style editor that affect every window and control across the entire application, using `ImGui.GetStyle()` — no Harmony patching required.
- Theme picker with filterable combobox (F11 toggle)
- Built-in themes: Game Default, Dark, Light, Classic, Inanimate Carbon Rod
- Full theme editor wrapping `ImGui.ShowStyleEditor()` — 60 color slots + all style vars
- Save/load custom themes as TOML files to/from disk
- Persistent theme selection across game sessions; restores game default on unload
- **skittles.lib**: `ThemeDefinition` (60-color + style POCO), `ThemeSerializer` (Tomlyn TOML I/O), `ThemeManager` (load/save/apply/list), `BuiltInThemes` (Inanimate Carbon Rod preset)

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