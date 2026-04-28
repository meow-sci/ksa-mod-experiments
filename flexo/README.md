# Flexo — Robotics Mod

Introduces articulated Parts (hinges, rotors) to KSA's static Part system. Design robotic parts in a dedicated editor, then control them at runtime through the unscience panel.

## Features

- **Hinge Creator**: Select fixed and moving Parts from a vehicle, define rotation axis, degree range, resting position, and motor speed
- **Runtime Control**: Open/close/reset buttons, manual angle slider, speed control for each hinge on the active vehicle
- **Vehicle Scanning**: Automatically detects flexo parts on the controlled vehicle by matching Part template IDs and connectivity
- **TOML Persistence**: Definitions saved to `~/.flexo/flexo_part_*.toml`
- **3D Editor Scene**: Isolated editing space with camera snaps, lighting, origin gizmo, and live preview
- **Live Preview**: Rotate Parts in the editor to verify hinge axis and range before saving

## Usage

### Standalone
Press F11 to toggle the Flexo window.

### Unscience Integration
The Flexo panel appears in Unscience's Toolbox with runtime controls. The editor opens as a floating window.

### Editor Workflow

1. Click **Open Editor** in the Flexo panel
2. Select a vehicle from the dropdown and click **Load** — the vehicle's Parts appear in the 3D editor
3. Click **New Hinge** to start the creation workflow
4. Click a Part in the list or 3D view to select the **fixed** Part (the stationary one)
5. Click another Part to select the **moving** Part (the one that rotates)
6. Configure hinge parameters: axis, min/max degrees, resting angle, motor speed
7. Use the **Preview** slider to see the rotation in real-time
8. Enter a display name and click **Save Flexo Part**
9. Close the editor — use **Reload Definitions** + **Scan Vehicle** in the runtime panel to test

### Runtime Controls

1. Click **Scan Vehicle** to detect flexo parts on the active vehicle
2. Each detected hinge shows Open/Close/Reset buttons and an angle slider
3. Click **Reload Definitions** to pick up new or edited TOML files

## Hotkey

| Key | Action |
|-----|--------|
| F11 | Toggle flexo window (standalone mode) |
