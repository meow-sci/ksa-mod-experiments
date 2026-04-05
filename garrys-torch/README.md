# Garry's Torch - Vehicle Welding System

A vehicle docking/attached system that welds one vehicle to another with full support for position offsets, rotation alignment, and uniform scaling. Welds are persistent per-frame—children move relative to their parent vehicle.

## Overview

Garry's Torch allows you to:
- **Weld two vehicles together** - Attach a source vehicle to a target vehicle
- **Configure relative position** - Separate the vehicles on XYZ axes in the target's body frame
- **Rotate freely** - Apply pitch/yaw/roll rotations independently
- **Scale uniformly** - Resize the source vehicle (supports avatar scaling)
- **Manage multiple welds** - A vehicle can have multiple welds simultaneously
- **Use presets** - Built-in configurations for common docking scenarios

## Features

- **Real-time vehicle positioning** - Welds update every frame to maintain relative position/rotation
- **Body-frame coordinates** - Positions specified in the target vehicle's local coordinate system
- **Rotation locking** - Option to prevent source vehicle from rotating relative to target
- **Parent validation** - Welds automatically break if vehicles cross celestial body boundaries
- **Quaternion-based math** - Proper 3D rotation handling with Euler angle conversion
- **Physics safety** - Guards against NaN values in body rates to prevent simulation corruption
- **Preset system** - Quick apply common configurations (Ridin' Dirty 1-3, Shotgun, Not Shotgun)

## Architecture

### Core Classes

#### WeldEngine
Stateless computation engine for vehicle welding. Contains all physics/math logic.

**Key Methods**:
- `UpdateWeld(WeldEntry weld)` - Teleports source vehicle to maintain relative position/rotation to target
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
    public float3 RelativePosition { get; set; }  // Offset in target's body frame (XYZ)
    public float3 RelativeRotation { get; set; }  // Pitch/Yaw/Roll in degrees
    public float UniformScale { get; set; }       // Scaling factor (0.05 to 20.0)
    public bool LockRotation { get; set; }        // Prevent relative rotation
}
```

#### WeldPreset
Data container for preset weld configuration (position, rotation, scale, lock rotation).

#### PresetManager
Manages named presets persisted to a TOML file at `My Games/Kitten Space Agency/.iryr/garrys-torch-presets.toml`.
- Load/save/delete named presets
- Cached preset name list for UI performance
- TOML format via Tomlyn library

### UI (Mod.cs / GarrysTorchSubmod)

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
worldPosition = targetPosition + targetRotation * (relativePosition + scale adjustment)
```

The relative position is in the **target vehicle's body frame**, so a +10 offset on Z moves the source vehicle upward relative to the target, regardless of the target's world orientation.

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
- **Frame update**: Animations run in `GarrysTorchSubmod.Update(dt)` before the weld engine teleport, ensuring smooth motion
- **Snap to target**: Animation completes by snapping to exact target values to prevent floating-point drift

See `garrys-torch.lib/openapi/garrystorch.yml` (in `unladen-swallow.lib/openapi/`) for the full OpenAPI 3.1.0 specification.

## Notes for Future Development

- **Performance**: Welds update every frame—high weld counts may impact performance
- **Physics**: The weld system teleports vehicles; no actual physics constraints are applied
- **Unwelds**: Welds break automatically on parent body mismatch; implement manual unweld via UI button
- **Animation**: Consider smooth transitions when applying presets vs. sharp position changes
- **Save/Load**: Persistent welds would require save/load system integration
