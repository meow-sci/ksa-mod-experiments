# overview

to add new animations that can be supported in the keyframe animation system.

each one should get a new collapisble header and follow similar patterns to the existing animations.

# animations

## spiral zoom in

like the exisitng zoom in animation but also "spiral" the camera at the same time, meaning rotate the camera on a perpendicular vector to the "look at" vector, so the result is a zoom in and "spin" a the same time


## spiral zoom out

like the exisitng zoom out animation but also "spiral" the camera at the same time, meaning rotate the camera on a perpendicular vector to the "look at" vector, so the result is a zoom out and "spin" a the same time


# tasks

## task 1: implement SpiralZoomInAnimation class + UI

### objective
Create a new `SpiralZoomInAnimation` that combines the existing ZoomInAnimation's movement toward the target with a rotation around the look-at axis (camera-to-target vector), producing a "spiral zoom in" effect. Add corresponding UI controls in Mod.cs.

### files to create
- `camera-controller-override/Animation/Animations/SpiralZoomInAnimation.cs`

### files to modify
- `camera-controller-override/Mod.cs` (add UI section)

### reference implementations
- [ZoomInAnimation.cs](camera-controller-override/Animation/Animations/ZoomInAnimation.cs) - base zoom-in logic to replicate
- [OrbitAnimation.cs](camera-controller-override/Animation/Animations/OrbitAnimation.cs) - Rodrigues' rotation formula for spinning around an axis

### animation parameters (constructor)
| Parameter | Type | Description |
|-----------|------|-------------|
| `speedMetersPerSecond` | `double` | Movement speed toward target |
| `durationSeconds` | `double` | Total animation duration |
| `easing` | `EasingType` | Easing function for both movement and rotation |
| `spiralDegrees` | `double` | Total rotation degrees during zoom (positive = clockwise when looking at target) |

### implementation details

1. **Implement `IKeyframeAnimation` interface** with:
   - `Name` => `"Spiral Zoom In"`
   - `Description` => `"Zoom toward target with spiral rotation"`

2. **Runtime state fields**:
   - `_distanceTraveled` (double) - track total distance moved
   - `_lastEasedProgress` (double) - for frame delta calculation
   - `_totalDegreesRotated` (double) - track rotation progress

3. **`Initialize()` method**:
   - Reset all runtime state to 0
   - Log initialization with `Console.WriteLine`

4. **`Update()` method** - each frame:
   - Calculate total movement distance: `speedMetersPerSecond * durationSeconds`
   - Calculate eased progress using `AnimationHelpers.ApplyEasing()`
   - Calculate frame progress delta: `currentEasedProgress - _lastEasedProgress`
   
   **Zoom component** (from ZoomInAnimation):
   - Get target position via `AnimationHelpers.GetTargetPosition(controller)`
   - Calculate direction toward target: `normalize(targetPos - cameraPos)`
   - Calculate frame distance and clamp to avoid overshooting minimum distance (1m)
   - Apply displacement: `transform.PositionEcl += direction * frameDistance`
   
   **Spiral component** (from OrbitAnimation):
   - Calculate spiral axis = direction toward target (the look-at vector)
   - Calculate frame rotation angle: `spiralDegrees * frameProgress` (in radians)
   - The spiral rotates the camera's "offset from look-at axis" perpendicular to that axis
   - Use camera's local up/right vectors to determine the rotation plane
   - Apply rotation using Rodrigues' formula on the "up" vector relative to look-at
   
   **Finalize**:
   - Call `AnimationHelpers.LookAtTarget(transform, lookAtTarget)` to maintain look-at
   - Return `true` when `elapsedTime >= DurationSeconds`

5. **`GetDisplayProperties()`** returns:
   - `"Speed"` => `"{SpeedMetersPerSecond:F1} m/s"`
   - `"Duration"` => `"{DurationSeconds:F1}s"`
   - `"Spiral"` => `"{SpiralDegrees:F0}°"`
   - `"Easing"` => `Easing.ToString()`

### UI implementation in Mod.cs

1. **Add configuration fields** (after existing zoom fields ~line 35):
   ```csharp
   // Spiral Zoom In configuration
   private float _spiralZoomInSpeed = 25.0f;
   private float _spiralZoomInDuration = 5.0f;
   private int _spiralZoomInEasing = (int)Animation.EasingType.EaseOut;
   private float _spiralZoomInDegrees = 360.0f;
   ```

2. **Add collapsible header section** in `RenderWindow()` (after "Zoom In To Offset Animation" section):
   - Header: `"Spiral Zoom In Animation"`
   - Controls:
     - `SliderFloat("Speed (m/s)##SpiralZoomIn", ref _spiralZoomInSpeed, 1.0f, 250.0f)`
     - `SliderFloat("Duration (s)##SpiralZoomIn", ref _spiralZoomInDuration, 1.0f, 30.0f)`
     - `Combo("Easing##SpiralZoomIn", ref _spiralZoomInEasing, easingNames, ...)`
     - `SliderFloat("Spiral Degrees##SpiralZoomIn", ref _spiralZoomInDegrees, -1080.0f, 1080.0f)` (negative = counter-clockwise)
   - Button: `"Add to Sequence##SpiralZoomIn"` that creates `SpiralZoomInAnimation` with current values

### acceptance criteria
- [ ] Animation class compiles (`dotnet build`)
- [ ] Animation implements all `IKeyframeAnimation` methods
- [ ] UI section appears with all controls
- [ ] Animation can be added to keyframe sequence via UI

---

## task 2: implement SpiralZoomOutAnimation class + UI

### objective
Create a new `SpiralZoomOutAnimation` that combines the existing ZoomOutAnimation's movement away from the target with a rotation around the look-at axis, producing a "spiral zoom out" effect. Add corresponding UI controls in Mod.cs.

### files to create
- `camera-controller-override/Animation/Animations/SpiralZoomOutAnimation.cs`

### files to modify
- `camera-controller-override/Mod.cs` (add UI section)

### reference implementations
- [ZoomOutAnimation.cs](camera-controller-override/Animation/Animations/ZoomOutAnimation.cs) - base zoom-out logic to replicate
- [OrbitAnimation.cs](camera-controller-override/Animation/Animations/OrbitAnimation.cs) - Rodrigues' rotation formula
- Task 1's `SpiralZoomInAnimation` - similar structure but reversed direction

### animation parameters (constructor)
| Parameter | Type | Description |
|-----------|------|-------------|
| `speedMetersPerSecond` | `double` | Movement speed away from target |
| `durationSeconds` | `double` | Total animation duration |
| `easing` | `EasingType` | Easing function for both movement and rotation |
| `spiralDegrees` | `double` | Total rotation degrees during zoom (positive = clockwise when looking at target) |

### implementation details

1. **Implement `IKeyframeAnimation` interface** with:
   - `Name` => `"Spiral Zoom Out"`
   - `Description` => `"Zoom away from target with spiral rotation"`

2. **Runtime state fields**:
   - `_distanceTraveled` (double) - track total distance moved
   - `_lastEasedProgress` (double) - for frame delta calculation
   - `_totalDegreesRotated` (double) - track rotation progress

3. **`Initialize()` method**:
   - Reset all runtime state to 0
   - Log initialization with `Console.WriteLine`

4. **`Update()` method** - each frame:
   - Calculate total movement distance: `speedMetersPerSecond * durationSeconds`
   - Calculate eased progress using `AnimationHelpers.ApplyEasing()`
   - Calculate frame progress delta: `currentEasedProgress - _lastEasedProgress`
   
   **Zoom component** (from ZoomOutAnimation):
   - Get target position via `AnimationHelpers.GetTargetPosition(controller)`
   - Calculate direction away from target: `normalize(cameraPos - targetPos)`
   - Handle edge case: camera at target position (use fallback direction)
   - Apply displacement: `transform.PositionEcl += direction * frameDistance`
   
   **Spiral component**:
   - Same approach as SpiralZoomInAnimation
   - Calculate spiral axis = direction toward target (the look-at vector)
   - Calculate frame rotation angle: `spiralDegrees * frameProgress` (in radians)
   - Apply rotation to maintain consistent spiral effect while zooming out
   
   **Finalize**:
   - Call `AnimationHelpers.LookAtTarget(transform, lookAtTarget)` to maintain look-at
   - Return `true` when `elapsedTime >= DurationSeconds`

5. **`GetDisplayProperties()`** returns:
   - `"Speed"` => `"{SpeedMetersPerSecond:F1} m/s"`
   - `"Duration"` => `"{DurationSeconds:F1}s"`
   - `"Spiral"` => `"{SpiralDegrees:F0}°"`
   - `"Easing"` => `Easing.ToString()`

### UI implementation in Mod.cs

1. **Add configuration fields** (after Spiral Zoom In fields):
   ```csharp
   // Spiral Zoom Out configuration
   private float _spiralZoomOutSpeed = 25.0f;
   private float _spiralZoomOutDuration = 5.0f;
   private int _spiralZoomOutEasing = (int)Animation.EasingType.EaseOut;
   private float _spiralZoomOutDegrees = 360.0f;
   ```

2. **Add collapsible header section** in `RenderWindow()` (after "Spiral Zoom In Animation" section):
   - Header: `"Spiral Zoom Out Animation"`
   - Controls:
     - `SliderFloat("Speed (m/s)##SpiralZoomOut", ref _spiralZoomOutSpeed, 1.0f, 250.0f)`
     - `SliderFloat("Duration (s)##SpiralZoomOut", ref _spiralZoomOutDuration, 1.0f, 30.0f)`
     - `Combo("Easing##SpiralZoomOut", ref _spiralZoomOutEasing, easingNames, ...)`
     - `SliderFloat("Spiral Degrees##SpiralZoomOut", ref _spiralZoomOutDegrees, -1080.0f, 1080.0f)` (negative = counter-clockwise)
   - Button: `"Add to Sequence##SpiralZoomOut"` that creates `SpiralZoomOutAnimation` with current values

### acceptance criteria
- [ ] Animation class compiles (`dotnet build`)
- [ ] Animation implements all `IKeyframeAnimation` methods
- [ ] UI section appears with all controls
- [ ] Animation can be added to keyframe sequence via UI

---

## implementation notes for subagents

### key math concepts

**Rodrigues' rotation formula** (used to rotate a vector around an axis):
```csharp
double3 k = normalizedAxis;  // unit vector of rotation axis
double cos = Math.Cos(angleRadians);
double sin = Math.Sin(angleRadians);
double3 rotated = v * cos + double3.Cross(k, v) * sin + k * double3.Dot(k, v) * (1.0 - cos);
```

**Spiral axis calculation**:
The spiral axis is the look-at direction (from camera to target). The camera should rotate around this axis while moving along it.

```csharp
double3 towardTarget = targetPos - transform.PositionEcl;
double3 spiralAxis = double3.Normalize(towardTarget);
```

**Perpendicular rotation effect**:
To achieve the spiral effect, after moving the camera, rotate the camera's orientation around the spiral axis. This creates the "corkscrew" movement effect.

### common pitfalls to avoid
1. Don't capture positions at initialization - recalculate each frame for moving targets
2. Use frame progress delta (`currentEasedProgress - _lastEasedProgress`) not raw elapsed time for easing
3. Always call `AnimationHelpers.LookAtTarget()` after position changes to maintain look-at
4. Handle edge cases: camera at target position, zero-length vectors
5. Use unique ImGui IDs with `##` suffix to avoid widget conflicts
