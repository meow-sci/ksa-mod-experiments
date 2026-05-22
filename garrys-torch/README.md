# Garry's Torch - Vehicle Welding System

A vehicle docking/attached system that welds one vehicle to another with full support for position offsets, rotation alignment, and uniform scaling. Welds are persistent per-frame—children move relative to their parent vehicle.

## Overview

Garry's Torch allows you to:
- **Weld two vehicles together** - Attach a source vehicle to a target vehicle
- **Anchor to a specific part** - Pick any part on the target vehicle as the anchor point; offsets are relative to that part, not the vehicle CoM
- **Configure relative position** - Separate the vehicles on XYZ axes in the target part's local frame
- **Rotate freely** - Apply pitch/yaw/roll rotations relative to the target part's orientation
- **Scale uniformly** - Resize the source vehicle (supports avatar scaling)
- **Manage multiple welds** - A vehicle can have multiple welds simultaneously
- **Use presets** - Built-in configurations for common docking scenarios

## Features

- **Real-time vehicle positioning** - Welds update every frame to maintain relative position/rotation
- **Part-anchored welding** - Anchor to any part on the target vehicle; the weld tracks that part, not the vehicle CoM. Immune to CoM drift as fuel burns, and naturally follows robotics-moved parts
- **Physics-loop safe updates** - Welds run immediately before KSA queues vehicle solver jobs, avoiding worker-thread state races in the refactored physics loop
- **Part-frame coordinates** - Positions and rotations specified in the target part's local coordinate system
- **Rotation locking** - Option to prevent source vehicle from rotating relative to target
- **Parent validation** - Welds automatically break if vehicles cross celestial body boundaries
- **Quaternion-based math** - Proper 3D rotation handling with Euler angle conversion
- **Physics safety** - Guards against NaN values in body rates to prevent simulation corruption
- **Preset system** - Quick apply common configurations (Ridin' Dirty 1-3, Shotgun, Not Shotgun)

## Architecture

### Core Classes

#### Weld update timing

The mod runs all weld physics from the StarMap `OnAfterUi` callback in `Mod.cs` (and from unscience's `OnAfterUi` when bundled in the supermod). That callback fires after the current frame's render, by which point the vehicle solver workers queued at the end of `Universe.ExecuteNextVehicleSolvers` have usually finished naturally.

To make the timing safe regardless of how long workers take, `GarrysTorchSubmod.UpdateWelds(dt)` explicitly calls `KSA.JobSystems.VehicleSolvers.Wait()` before touching any vehicle state. That blocks until all in-flight vehicle worker jobs complete, eliminating the two races that any uncoordinated `Vehicle.Teleport` call from a UI callback produces:

- **`Called SnapToLeader with body time X but origin time Y`** — `Vehicle.Teleport` advances the source's `_kinematicStates.Origin.Time` past `body.Time` while a worker still holds the old body snapshot.
- **`System.InvalidOperationException: Collection was modified`** thrown from `VehicleUpdateTask.DoWorkAndStageResults` — `Vehicle.Teleport` → `RemoveFromTask` → `_vehicleStates.Remove(...)` while a worker iterates that list.

After our `Wait()` returns, the workers are done; we can call `Vehicle.Teleport` safely. The teleport itself calls `RemoveFromTask`, so the next frame's `Universe.ApplyVehicleSolvers` doesn't overwrite our teleport (the source is no longer in any task). The next `Universe.ExecuteNextVehicleSolvers` then calls `AddVehiclesToTasks` which re-attaches the source to a task and copies our teleported `_kinematicStates` into the worker state — so the following physics tick starts from the welded position.

Earlier versions of this mod tried a Harmony prefix on `Universe.ExecuteNextVehicleSolvers` and later a postfix on `Universe.ApplyVehicleSolvers`. Both approaches silently stopped firing after the recent KSA build (root cause not pinned down — other mods' Harmony patches on `ExecuteNextVehicleSolvers` still work, suggesting a build-specific quirk), so the mod no longer relies on Harmony for weld timing.

#### WeldEngine
Stateless computation engine for vehicle welding. Contains all physics/math logic.

**Key Methods**:
- `UpdateWeld(WeldEntry weld)` - Teleports source vehicle to maintain relative position/rotation to target, then refreshes per-frame vehicle caches
- `EulerDegreesToQuat(float pitch, float yaw, float roll)` - Converts Euler angles to quaternion with ZYX intrinsic convention
- `ApplyVehicleScale(Vehicle vehicle, float scale)` - Applies uniform scale to all parts

**Key Logic**:
- Uses quaternion multiplication: `worldRotation = targetRotation * relativeRotation`
- Position computed in body frame then transformed to world space
- NaN guard for body rates: prevents physics corruption from invalid angular velocities

#### WeldEntry
Container for an active weld between two vehicles.

```csharp
public class WeldEntry
{
    public Vehicle Source { get; set; }           // Vehicle being welded
    public Vehicle Target { get; set; }           // Vehicle being welded to
    public Part? TargetPart { get; set; }         // Anchor part on target (null = vehicle CoM fallback)
    public float3 RelativePosition { get; set; }  // Offset relative to anchor (part frame or body frame)
    public float3 RelativeRotation { get; set; }  // Pitch/Yaw/Roll relative to anchor orientation (degrees)
    public float UniformScale { get; set; }       // Scaling factor (0.05 to 20.0)
    public bool LockRotation { get; set; }        // Prevent relative rotation
}
```

#### WeldPreset
Data container for preset weld configuration (position, rotation, scale, lock rotation).

#### PresetManager
Manages named presets persisted to a TOML file at `My Games/Kitten Space Agency/.unscience/garrys-torch-presets.toml`.
- Load/save/delete named presets
- Cached preset name list for UI performance
- TOML format via Tomlyn library

### UI (Mod.cs / GarrysTorchSubmod)

`Mod.OnBeforeUi` intentionally does **not** run weld physics. Weld physics runs from `Mod.OnAfterUi` (or unscience's `OnAfterUi` when bundled) via `GarrysTorchSubmod.UpdateWelds(dt)`, which calls `KSA.JobSystems.VehicleSolvers.Wait()` first to synchronise with the vehicle worker threads.

ImGui window with:
- **Create Weld section** - Collapsible header with filterable source/target vehicle combos
- **Preset system** - Filterable preset combo with delete button and confirmation modal
- **Position Controls** - Full-width 3-axis drag float inputs for body-frame offset
- **Rotation Controls** - Full-width 3-axis drag float inputs for pitch/yaw/roll
- **Scale + Lock Rotation** - Table row with scale slider and rotation lock checkbox
- **Active Welds list** - Bordered child windows per weld with live-edit controls
- **Save as preset** - Modal popup to save active weld settings as a named preset
- **Weld Management** - Create/unweld with validation and error messages

## Key Implementation Details

### Rotation Handling
Rotations use the **ZYX intrinsic Euler convention**:
```csharp
// Pitch (rotation around vehicle's forward/X axis)
// Yaw (rotation around vehicle's up/Z axis)
// Roll (rotation around vehicle's right/Y axis)
```

Conversion to quaternion:
1. Convert each angle (degrees) to radians
2. Create three quaternions for each axis rotation
3. Multiply in order: `Qz * Qy * Qx` (intrinsic ZYX)

### Position Calculation
```
anchorPosCci   = targetVehicleCoM + (targetPart.PositionVehicleAsmb - vehicleCoMInAsmb).Transform(body2Cci)
anchorOrientation = targetPart.Asmb2VehicleAsmb * vehicleBody2Cci
worldPosition  = anchorPosCci + relativePosition.Transform(anchorOrientation)
```

When no `TargetPart` is set (legacy path), `anchorPosCci = vehicleCoMPosCci` and `anchorOrientation = vehicleBody2Cci`.

The part anchor means a +10 offset on Z moves the source vehicle along the target **part's** local Z axis, tracking changes in that part's orientation (e.g., from robotics).

### Parent Body Validation
Welds automatically break if:
- Target vehicle changes parent body
- Source vehicle's parent body doesn't match target's

This prevents welds from stretching across planetary bodies.

### Scaling
Scales affect part size by multiplying part templates' visual and physical properties. KittenEva avatar scaling is handled specially to maintain proper proportions.

## Configuration Options

All weld parameters are configured through the ImGui window:

| Parameter | Range | Notes |
|-----------|-------|-------|
| Position X | -50 to +50 m | Body frame offset |
| Position Y | -50 to +50 m | Body frame offset |
| Position Z | -50 to +50 m | Body frame offset |
| Pitch | -180 to +180° | Rotation around forward axis |
| Yaw | -180 to +180° | Rotation around up axis |
| Roll | -180 to +180° | Rotation around right axis |
| Scale | 0.05 to 20.0x | Uniform scaling |
| Lock Rotation | true/false | Freeze relative orientation |

## Usage Example

```csharp
// Create a new weld
var weld = new WeldEntry
{
    Source = sourceVehicle,
    Target = targetVehicle,
    RelativePosition = new float3(0, 0, 5),  // 5m above target
    RelativeRotation = new float3(0, 0, 0),   // No rotation offset
    UniformScale = 1.0f,
    LockRotation = false
};

// Update the weld each frame
WeldEngine.UpdateWeld(weld);
```

## Math Reference

### Quaternion Multiplication
```
q_result = q1 * q2  (Hamilton product)
```

Composing rotations:
```
q_world = q_target * q_relative
```

### Euler to Quaternion (ZYX Intrinsic)
```
q_z = cos(yaw/2) + sin(yaw/2)*k
q_y = cos(pitch/2) + sin(pitch/2)*j  
q_x = cos(roll/2) + sin(roll/2)*i
q_result = q_z * q_y * q_x
```

## HTTP RPC API

The `garrys-torch.lib` exposes a public API surface on `GarrysTorchSubmod` that is consumed by the `unladen-swallow.lib` HTTP RPC server. Through the unladen-swallow server (port 7887), the following operations are available:

| Method | Path | Description |
|--------|------|-------------|
| GET | `/torch/welds` | List all active welds |
| POST | `/torch/welds` | Create a new weld (supply `data` or `presetName`) |
| DELETE | `/torch/welds` | Remove a weld |
| POST | `/torch/welds/modify` | Immediately modify an active weld (partial update) |
| POST | `/torch/welds/animate` | Smoothly animate a weld to a new state |
| GET | `/torch/presets` | List all saved weld presets |
| POST | `/torch/presets` | Save a preset |
| DELETE | `/torch/presets` | Delete a preset |

### Create Weld Example

```json
POST /torch/welds
{
  "sourceVehicleId": "my-lander",
  "targetVehicleId": "station-core",
  "data": {
    "position": { "x": 0, "y": 0, "z": 2.5 },
    "rotation": { "x": 0, "y": 0, "z": 0 },
    "scale": 1.0,
    "lockRotation": true
  }
}
```

### Animate Weld Example

Smoothly interpolate a weld's position/rotation/scale over 2 seconds with ease-in-out:

```json
POST /torch/welds/animate
{
  "sourceVehicleId": "my-lander",
  "durationSeconds": 2.0,
  "data": {
    "position": { "x": 0, "y": 0, "z": 5.0 },
    "rotation": { "x": 0, "y": 45, "z": 0 },
    "scale": 0.5,
    "lockRotation": true
  },
  "easing": {
    "easing": "easeInOut",
    "easingPowerStart": 3.0,
    "easingPowerEnd": 3.0
  }
}
```

### Animation System

The animation system (`WeldAnimation`, `WeldAnimationManager`) enables smooth interpolation of all weld parameters:

- **Easing types**: Linear, EaseIn, EaseOut, EaseInOut
- **Configurable power**: `easingPowerStart` and `easingPowerEnd` control the sharpness of the ease function
- **Queue**: Multiple animations can be queued per weld; each starts when the previous completes
- **Frame update**: Animations run in `GarrysTorchSubmod.UpdateBeforeVehicleSolvers(dt)` before the weld engine teleport, ensuring smooth motion without racing KSA vehicle solver jobs
- **Snap to target**: Animation completes by snapping to exact target values to prevent floating-point drift

See `garrys-torch.lib/openapi/garrystorch.yml` (in `unladen-swallow.lib/openapi/`) for the full OpenAPI 3.1.0 specification.

## Notes for Future Development

- **Performance**: Welds update every frame—high weld counts may impact performance
- **Physics**: The weld system teleports vehicles; no actual physics constraints are applied
- **Unwelds**: Welds break automatically on parent body mismatch; implement manual unweld via UI button
- **Animation**: Consider smooth transitions when applying presets vs. sharp position changes
- **Save/Load**: Persistent welds would require save/load system integration
