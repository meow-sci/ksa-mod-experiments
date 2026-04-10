# Flexo — Robotics Mod

Introduces articulated Parts (hinges, rotors) to KSA's static Part system. Design robotic parts in a dedicated editor, then control them at runtime through the grant panel.

## Features

- **Hinge Creator**: Select fixed and moving Parts from a vehicle, define rotation axis, degree range, resting position, and motor speed
- **Runtime Control**: Open/close/reset buttons, manual angle slider, speed control for each hinge on the active vehicle
- **Vehicle Scanning**: Automatically detects flexo parts on the controlled vehicle by matching Part template IDs and connectivity
- **TOML Persistence**: Definitions saved to `~/.flexo/flexo_part_*.toml`

## Usage

### Standalone
Press F11 to toggle the Flexo window.

### Grant Integration
The Flexo panel appears in Grant's Toolbox with runtime controls. The editor opens as a floating window.

## Hotkey

| Key | Action |
|-----|--------|
| F11 | Toggle flexo window (standalone mode) |
