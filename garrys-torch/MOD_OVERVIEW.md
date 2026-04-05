# Garry's Torch — Mod Overview

## What It Does

Garry's Torch is a vehicle welding mod for KSA. It lets you attach ("weld") one vehicle to another in-game so the source vehicle follows the target vehicle's position and orientation each frame. Welded vehicles can be repositioned, rotated, and scaled via an ImGui control panel toggled with F11. Multiple simultaneous welds are supported.

## Features

### 1. Vehicle Welding
Select any two vehicles in the current system and weld the source to the target. The source's orbit is overwritten each frame to track the target's position/velocity, effectively making it a rigid child of the target.

### 2. Position Offset
Per-weld XYZ offset (metres) in the target's body frame, adjustable via drag-float sliders.

### 3. Rotation Offset
Per-weld pitch/yaw/roll (degrees) layered on top of the orientation captured at weld time. Can be toggled off ("Lock Rotation" checkbox) to let the source rotate freely while still tracking position.

### 4. Vehicle Scaling
Per-weld uniform scale factor applied to all parts (and sub-parts) of the source vehicle. Special-case handling scales KittenEva characters via reflection into `CharacterAvatar.Core.Scale`.

### 5. ImGui Control Panel
F11-toggled window listing all active welds with collapsible sections, plus an "Add New Weld" combo-box UI.

### 6. Auto-Unweld on Parent Mismatch
If the source and target end up orbiting different parent bodies the weld is automatically removed to avoid nonsensical state.

## Code Map

### Mod.cs — Mod lifecycle + UI + weld logic

| Symbol | Purpose |
|---|---|
| `Mod` class | StarMap mod entry point; holds weld list and UI state |
| `OnFullyLoaded()` | Initialises Harmony patches |
| `OnAfterUi(dt)` | Per-frame loop: toggle window on F11, update all welds, render UI |
| `Unload()` | Unpatches Harmony, marks disposed |
| `RenderWindow()` | Draws the ImGui window — active weld editors + new-weld combo UI |
| `InitiateWeld(source, target)` | Captures rotation offset and creates a `WeldEntry` |
| `UpdateWeld(entry)` → `bool` | Per-frame: computes new orbit + orientation for source from target + offsets, calls `Teleport`. Returns `false` on parent mismatch to trigger removal |
| `RemoveWeld(entry)` | Resets source scale to 1 and removes the weld |
| `ApplyVehicleScale(vehicle, factor)` | Sets `Part.Scale` recursively; reflection path for KittenEva avatar scaling |
| `SetPartScaleRecursive(part, factor)` | Recursive helper for part + sub-part scale |
| `EulerDegreesToQuat(pitch, yaw, roll)` | Converts Euler degrees (ZYX intrinsic) to `doubleQuat` |
| `WeldEntry` class | Data object: Source, Target, RotationOffset, Position, Rotation, Scale, LockRotation |

### Patcher.cs — Harmony setup

| Symbol | Purpose |
|---|---|
| `Patcher.Patch()` | Applies all `[HarmonyPatch]` patches in the assembly |
| `Patcher.Unload()` | Unpatches all and nulls the Harmony instance |

> Note: No individual patch methods currently exist — Patcher is scaffolding for future Harmony patches.

### Key KSA APIs Used

- `Vehicle.GetPositionCci()` / `GetVelocityCci()` / `GetBody2Cci()` — read vehicle state in CCI frame
- `Vehicle.Teleport(orbit, body2Cce, bodyRates)` — reposition a vehicle
- `Orbit.CreateFromStateCci(...)` — build an orbit from position + velocity
- `Vehicle.Parts.Parts` / `Part.Scale` / `Part.SubParts` — part tree traversal
- `Universe.CurrentSystem.Vehicles.GetList()` — enumerate vehicles
- `Universe.GetElapsedSimTime()` — current sim time for orbit creation
