# KSA Camera Controller Analysis

## Overview

This document analyzes the camera controller system in Kitten Space Agency (KSA), focusing on how the camera targets and tracks objects in 3D space. This analysis supports the development of a custom keyframe animation camera controller.

## Architecture

### Class Hierarchy

```
Controller (abstract base)
├── OrbitController (orbiting camera, always looks at target)
└── FlyController (free camera, stays relative to target)
```

### Base Controller Class

**Key Properties:**
- `Camera` - Reference to the camera object
- `Transform3D Transform` - 3D transformation for camera positioning
- `string Name` - Controller identifier

**Core Methods:**
- Input handlers: `OnKey()`, `OnMouseButton()`, `OnCursorPos()`, `OnScroll()`
- Frame update: `OnFrame(Viewport, double deltaTime)`
- Mode switching: `OnSwitchOn()`, `OnSwitchOff()`
- UI: `OnDrawUi()`, `OnDrawStatisticsUi()`
- State queries: `GetCursorMode()`, `IsMouseDrag()`, `CancelMouseDrag()`

---

## OrbitController - Primary Analysis

The OrbitController implements an orbiting camera that maintains focus on a target object (Astronomical entity) while allowing the user to rotate around it.

### Core Positioning System

**Spherical Coordinate System:**
```csharp
public double Azimuth = 0.0;      // Horizontal rotation angle (radians)
public double Elevation = 0.0;     // Vertical rotation angle (radians)
public double DistancePower = 3.0; // Dimensionless distance multiplier (scaled by scroll)
```

The camera position is determined by:
1. **Azimuth** - Horizontal angle around the target (unlimited rotation)
2. **Elevation** - Vertical angle, clamped to [-π/2, π/2] to prevent gimbal lock
3. **Distance** - Calculated per-frame as `distanceMeters = focusedRadius * DistancePower`

### Reference Frame System

The camera can operate in multiple reference frames, each providing a different rotation basis:

```csharp
public enum CameraReferenceFrame
{
    Surface,  // Aligned with object's surface (ENU - East-North-Up for vehicles)
    Orbit,    // Aligned with orbital trajectory (LVLH for vehicles)
    Parent,   // Aligned with parent body's orbital frame
    Poles,    // Aligned with body's rotation axis (CCI - Celestial Body Inertial)
    Stars,    // Inertial frame (no rotation, ecliptic coordinates)
    Chase     // Body-fixed frame following vehicle orientation
}
```

### Key Method: GetFrame2Ecl()

This method returns the quaternion transformation from the selected reference frame to ecliptic coordinates (ECL):

**For Vehicles:**
- **Surface**: Uses ENU (East-North-Up) frame via `GetEnu2Cce()`
- **Orbit**: Uses LVLH (Local Vertical Local Horizontal) with 180° pitch rotation via `GetLvlh2Cce()`
- **Parent**: Uses orbital carousel frame (perpendicular to orbital plane)
- **Stars**: Identity quaternion (inertial space)
- **Chase**: Vehicle body frame with 180° pitch rotation

**For Celestial Bodies:**
- **Surface**: Uses CCF (Celestial Coordinate Frame) via `GetCcf2Cce()`
- **Parent**: Uses orbital carousel frame
- **Poles**: Uses CCI (Body's inertial frame) via `GetCci2Cce()`
- **Stars**: Identity quaternion

### GetCarousel2Cce() - Orbital Reference Frame

This method creates a reference frame perpendicular to the orbital plane:

```csharp
private doubleQuat GetCarousel2Cce(IOrbiting celestial)
{
    // Get position and velocity in parent's inertial frame
    double3 position = celestial.Orbit.StateVectors.PositionCci.Transform(cci2Cce);
    double3 velocity = celestial.Orbit.StateVectors.VelocityCci.Transform(cci2Cce);
    
    // Orbital angular momentum vector (perpendicular to orbital plane)
    double3 normal = Cross(position, velocity);
    
    // Build orthonormal basis:
    // X-axis: towards parent body (-position direction)
    // Y-axis: perpendicular to orbital plane
    // Z-axis: completes right-handed system
    
    return CreateRotationMatrix(xAxis, yAxis, zAxis);
}
```

### Camera Targeting - How It Works

OrbitController computes a *look direction* and *up vector* in ECL/CCE, then derives camera rotation with `Camera.LookAtRotation(dir, up)`. The camera is placed behind the target along the camera's forward axis (KSA appears to use local forward = `-Z`).

High-level steps in `OnFrame()`:

1. **Target position (ECL)**: `positionEcl = focused.GetPositionEcl()`
2. **Choose a reference frame**: usually `orbitView.ReferenceFrame` (but when `Program.Editor != null`, it forces `CameraReferenceFrame.Chase`).
3. **Compute frame axes in ECL**:
    - `xAxis = UnitX.Transform(frame2Ecl)`
    - `zAxis = UnitZ.Transform(frame2Ecl)` (used as the polar axis for azimuth rotation)
4. **Compute look direction** using axis-angle rotations:
    - `dir0 = RotateAroundAxis(xAxis, zAxis, Azimuth)`
    - `elevAxis = Normalize(Cross(dir0, zAxis))`
    - `dir = RotateAroundAxis(dir0, elevAxis, Elevation)`
    - `up = Normalize(Cross(elevAxis, dir))`
5. **Compute camera rotation**: `rotation = Camera.LookAtRotation(dir, up)`
6. **Compute camera distance**: `distanceMeters = DistancePower * focusedRadius`
7. **Compute camera position**:
    - `forward = (-UnitZ).Transform(rotation)` (direction from camera toward the target)
    - `cameraPosEcl = positionEcl + offsetEcl + editorOffset - forward * distanceMeters`

Pseudo-code (close to the decompiled logic):

```csharp
double3 positionEcl = focused.GetPositionEcl();

doubleQuat frame2Ecl = GetFrame2Ecl(focused, referenceFrame);
double3 xAxis = double3.UnitX.Transform(frame2Ecl);
double3 zAxis = double3.UnitZ.Transform(frame2Ecl);

double3 dir0 = xAxis.Transform(QuaternionEx.AngleAxis(Azimuth, zAxis));
double3 elevAxis = double3.Cross(dir0, zAxis).Normalized();
double3 dir = dir0.Transform(QuaternionEx.AngleAxis(Elevation, elevAxis));
double3 up = double3.Cross(elevAxis, dir).Normalized();

doubleQuat rotation = Camera.LookAtRotation(dir, up);

double distanceMeters = DistancePower * focusedRadius;
double3 forward = (-double3.UnitZ).Transform(rotation);

Transform.LocalRotation = rotation;
Transform.PositionEcl = positionEcl + offsetEcl + editorOffset - forward * distanceMeters;
```

### Input Handling

**Mouse Dragging:**
```csharp
public override bool OnCursorPos(GlfwWindow window, double2 pos)
{
    if (IsDragging)
    {
        orbitView.Azimuth -= delta.X * PAN_SENSITIVITY;
        orbitView.Elevation -= delta.Y * PAN_SENSITIVITY;
        orbitView.Elevation = Clamp(orbitView.Elevation, -PI/2, PI/2);
    }
}
```

**Mouse Scrolling (Zoom):**
```csharp
public override bool OnScroll(GlfwWindow window, double2 offset)
{
    if (offset.Y > 0)
        orbitView.DistancePower /= (SprintFlag ? 2.2 : 1.1);
    else
        orbitView.DistancePower *= (SprintFlag ? 2.2 : 1.1);
    
    // Clamp to minimum distance:
    // - Non-Sol targets: at least 0.5
    // - Sol: at least SunRenderer.OrbitCamDistPow
    orbitView.DistancePower = Max(orbitView.DistancePower, minDistancePower);
}
```

### Animation System

**Focus Change Animation:**
```csharp
private Astronomical? _animStartFocused = null;
private doubleQuat _animStartRotationEcl = doubleQuat.Identity;
private double _animStartDistance = 0.0;
private double _animProgress = 1.0;  // 0.0 = start, 1.0 = complete

public bool AnimateFocusChange = false;
```

When the focused object changes:
1. Store starting state (_animStartFocused, _animStartRotationEcl, _animStartDistance)
2. Set _animProgress = 0.0
3. Each frame, increment _animProgress based on deltaTime
4. Interpolate between start and target state using _animProgress
5. When _animProgress >= 1.0, animation complete

More specifically in the decompiled code:

- `_animProgress` advances by `deltaTime / GameSettings.Current.Interface.CameraJumpTime`.
- It clamps to `1.0 + 0.2`, then uses `t = _animProgress - 0.2`.
- Rotation uses `doubleQuat.Lerp(startRotation, targetRotation, Smootherstep(t))`.
- Position / distance / offsets use `Smootherstep(_animProgress)`.

This delays rotation slightly relative to translation, which tends to feel smoother.

### State Tracking

The controller maintains history to detect changes:
```csharp
private Astronomical? _lastFocused = null;
private CameraReferenceFrame? _lastReference = null;
private doubleQuat _lastFrame2Ecl = doubleQuat.Identity;
private double _lastDistance = 0.0;
private double3 _lastOffsetEcl = double3.Zero;
```

This allows the controller to:
- Detect when focus changes (trigger animation)
- Detect reference frame changes (update orientation)
- Maintain smooth camera behavior across frames

---

## FlyController - Free Camera Analysis

The FlyController provides a free-flying camera that remains relative to a tracked celestial body.

### Movement System

**Input Flags:**
```csharp
[Flags]
public enum KeyInputFlags
{
    None = 0,
    Forward = 1,
    Backward = 2,
    Left = 4,
    Right = 8,
    Up = 16,
    Down = 32,
    Sprint = 64,
    RollLeft = 128,
    RollRight = 256
}
```

**Speed Control:**
```csharp
public double SpeedMultiplier { get; set; } = 1.0;
public double Speed { get; set; } = 50.0;     // Base speed (m/s)
public double FastSpeed { get; set; } = 100.0; // Sprint speed (m/s)

// Actual speed calculation
double currentSpeed = SpeedMultiplier * 
                      (sprint ? FastSpeed : Speed) * 
                      Math.Pow(2.0, _scrollPower);
```

The scroll wheel adjusts `_scrollPower` to exponentially scale speed, clamped to [-50, 50].

### Mouse Look System

```csharp
public float3 lookTgt = new float3(0.0f, 0.0f, 0.0f);
public float lookSharpness = 8f;

public override bool OnCursorPos(GlfwWindow window, double2 pos)
{
    if (MouseMove && !IsAltDown)
    {
        lookTgt.X += delta.Y * deltaTime * lookSensitivity;
        lookTgt.Y += delta.X * deltaTime * lookSensitivity;
    }
}
```

Mouse deltas accumulate into `lookTgt` (pitch/yaw), then `lookTgt` is damped back toward zero each frame:

```csharp
lookTgt = float3.Lerp(lookTgt, float3.Zero, lookSharpness * (float)deltaTime);
```

That impulse is integrated into `_offsetEcl` as incremental rotations (roll, yaw, pitch) scaled by `0.01`, then applied on top of `_frame2Ecl`:

```csharp
_offsetEcl *= AngleAxis( lookTgt.Z * 0.01, Forward)
           * AngleAxis(-lookTgt.Y * 0.01, Up)
           * AngleAxis(-lookTgt.X * 0.01, Right);

Transform.LocalRotation = _frame2Ecl * _offsetEcl;
```

### Reference Frame System

FlyController supports a narrower set of reference frames than OrbitController:

- For **Vehicles**:
    - `Surface`, `Orbit`, `Stars`, `Chase` supported.
    - `Parent` throws `NotImplementedException`.
    - `Poles` throws `InvalidOperationException`.
- For **Celestials**:
    - `Surface`, `Poles`, `Stars` supported.
    - `Orbit` and `Chase` throw `InvalidOperationException`.

In practice, `OnFrame()` sets `_frame2Ecl` to **Surface** when `Camera.Following is Celestial`, otherwise identity.

### Camera Clamping

```csharp
private void ClampCamera()
{
    // Prevent camera from going inside celestial bodies
    if (distanceFromSurface < MINIMUM_ALTITUDE_METERS)
    {
        // Push camera back to minimum altitude
    }
}
```

Ensures the camera stays at least 2 meters above surfaces.

### Offset Caching

```csharp
private doubleQuat _offsetEcl = doubleQuat.Identity;
private Celestial? _trackedCelestial;

public void CacheOffset()
{
    // Store the camera rotation offset relative to the tracked frame.
    if (Camera.Following is Celestial following)
        _offsetEcl = GetFrame2Ecl((Astronomical)following, CameraReferenceFrame.Surface).Inverse() * Transform.LocalRotation;
    else
        _offsetEcl = Transform.LocalRotation;
}
```

---

## Key Concepts for Custom Animation Controller

### 1. Coordinate Systems

**Ecliptic Coordinates (ECL/CCE):** 
- Primary coordinate system for the engine
- All positions ultimately expressed in this frame
- Heliocentric (Sun-centered) inertial frame

**Reference Frames:**
- Local rotation bases for intuitive camera control
- Transformed to ecliptic via quaternions
- Allow camera to "rotate with" the object's motion

### 2. Quaternion Transformations

**doubleQuat** is used throughout for:
- Rotation representation (avoids gimbal lock)
- Coordinate frame transformations
- Smooth interpolation (lerp/slerp depending on controller)

**Key operations:**
- `doubleQuat.Concatenate(q1, q2)` - Combine rotations
- `vector.Transform(quat)` - Rotate vector by quaternion
- `doubleQuat.CreateFromAxisAngle(axis, angle)` - Create rotation
- Interpolation: `doubleQuat.Lerp(start, end, t)` (OrbitController) and/or `doubleQuat.Slerp(start, end, t)`

### 3. Spherical Coordinates

OrbitController uses spherical coordinates for intuitive orbiting:
- **Azimuth (θ)**: Horizontal angle [0, 2π) or unlimited
- **Elevation (φ)**: Vertical angle [-π/2, π/2]
- **Distance (r)**: Radial distance (often stored as power/logarithm)

Conversion to Cartesian:
```
x = r * cos(φ) * cos(θ)
y = r * cos(φ) * sin(θ)
z = r * sin(φ)
```

Note: OrbitController doesn’t explicitly compute this with trig; it constructs the equivalent direction using axis-angle rotations (see the earlier `OnFrame()` breakdown).

### 4. Camera Look-At

The camera is positioned using:
1. Calculate camera position from target + offset
2. Orient camera to look at target
3. Apply any additional rotations (roll, reference frame, etc.)

---

## Recommendations for Custom Keyframe Controller

### Architecture

Create a new controller class inheriting from `Controller`:

```csharp
public class KeyframeController : Controller
{
    private List<CameraKeyframe> _keyframes;
    private int _currentKeyframe;
    private double _animationTime;
    
    public KeyframeController(Camera camera) : base(camera, "Keyframe") { }
}
```

### Keyframe Structure

```csharp
public class CameraKeyframe
{
    public double TimeStamp;              // When this keyframe occurs
    public double Azimuth;                // Horizontal angle around target
    public double Elevation;              // Vertical angle
    public double DistancePower;          // Distance multiplier (distanceMeters = focusedRadius * DistancePower)
    public CameraReferenceFrame Frame;    // Reference frame for this keyframe
    public EasingFunction Easing;         // Interpolation curve
}
```

### Keyframe Interpolation

Between keyframes, interpolate:
1. **Azimuth/Elevation**: Linear or spherical interpolation
2. **Distance**: Linear interpolation of DistancePower (it behaves like a scale factor)
3. **Reference Frame**: Either snap at keyframe or use quaternion slerp between frame rotations

### Animation Sequences

Define common animation patterns:

**Orbit Around Target:**
```csharp
void CreateOrbitSequence(double duration, double distanceMeters, double startAngle)
{
    int steps = 60;
    for (int i = 0; i <= steps; i++)
    {
        double t = i / (double)steps;
        AddKeyframe(new CameraKeyframe {
            TimeStamp = duration * t,
            Azimuth = startAngle + (2 * Math.PI * t),
            Elevation = 0,
            // OrbitController uses: distanceMeters = focusedRadiusMeters * DistancePower
            // so DistancePower is best thought of as a scale factor.
            DistancePower = distanceMeters / currentFocusedRadiusMeters,
            Frame = CameraReferenceFrame.Stars
        });
    }
}
```

**Zoom In/Out:**
```csharp
void CreateZoomSequence(double duration, double startDistMeters, double endDistMeters)
{
    AddKeyframe(TimeStamp: 0, DistancePower: startDistMeters / currentFocusedRadiusMeters);
    AddKeyframe(TimeStamp: duration, DistancePower: endDistMeters / currentFocusedRadiusMeters);
}
```

**Figure-8 Pattern:**
```csharp
void CreateFigure8(double duration, double amplitude)
{
    int steps = 120;
    for (int i = 0; i <= steps; i++)
    {
        double t = i / (double)steps;
        double angle = 2 * Math.PI * t;
        AddKeyframe(new CameraKeyframe {
            TimeStamp = duration * t,
            Azimuth = sin(angle) * amplitude,
            Elevation = sin(2 * angle) * amplitude * 0.5,
            // ... 
        });
    }
}
```

### Integration Points

**Use OrbitController's methods:**
1. `GetFrame2Ecl()` - Transform reference frame to ecliptic
2. `GetCarousel2Cce()` - For orbital reference frames
3. Reference frame cycling logic - For keyframe frame changes

**Camera positioning pattern:**
```csharp
public override void OnFrame(Viewport viewport, double deltaTime)
{
    _animationTime += deltaTime;
    
    // Find current keyframes to interpolate between
    var (kf1, kf2, t) = GetInterpolationKeyframes(_animationTime);
    
    // Interpolate camera parameters
    double azimuth = Lerp(kf1.Azimuth, kf2.Azimuth, t);
    double elevation = Lerp(kf1.Elevation, kf2.Elevation, t);
    double distPower = Lerp(kf1.DistancePower, kf2.DistancePower, t);
    
    // Get target position and reference frame
    double3 targetPos = Camera.Following.GetPositionEcl();
    doubleQuat frame2Ecl = GetFrame2Ecl(Camera.Following, kf1.Frame);
    double focusedRadiusMeters = Camera.Following.MeanRadius; // vehicles use BoundingSphereRadius in OrbitController
    
    // OrbitController-style distance conversion
    double distanceMeters = distPower * focusedRadiusMeters;
    
    // Position and orient camera (OrbitController pattern)
    // 1) compute dir/up in the chosen frame
    // 2) rotation = Camera.LookAtRotation(dir, up)
    // 3) forward = (-UnitZ).Transform(rotation)
    // 4) position = targetPos - forward * distanceMeters
    var rotation = Camera.LookAtRotation(dir, up);
    var forward = (-double3.UnitZ).Transform(rotation);
    Transform.LocalRotation = rotation;
    Transform.PositionEcl = targetPos - forward * distanceMeters;
}
```

### Easing Functions

For smooth animations, use easing functions:
```csharp
public enum EasingFunction
{
    Linear,
    EaseInOut,
    EaseIn,
    EaseOut,
    Bounce,
    Elastic
}

double ApplyEasing(double t, EasingFunction easing)
{
    switch (easing)
    {
        case EasingFunction.EaseInOut:
            return t < 0.5 
                ? 2 * t * t 
                : 1 - Math.Pow(-2 * t + 2, 2) / 2;
        // ... other easing functions
    }
}
```

---

## Technical Notes

### Coordinate Frame Abbreviations

- **ECL/CCE**: Ecliptic Celestial Coordinates (inertial, Sun-centered)
- **CCF**: Celestial Coordinate Frame (body-fixed, surface-aligned)
- **CCI**: Celestial Celestial Inertial (body's rotation-aligned inertial frame)
- **ENU**: East-North-Up (local surface frame for vehicles)
- **LVLH**: Local Vertical Local Horizontal (orbital frame for vehicles)

### Important Constants

```csharp
const double PAN_SENSITIVITY = 0.003;           // Mouse drag sensitivity
const double DISTANCE_POWER_SENSITIVITY = 1.1;  // Zoom scroll multiplier
const double MIN_DISTANCE_POWER = 0.5;          // Minimum zoom level
const double MINIMUM_ALTITUDE_METERS = 2.0;     // Surface clearance
```

### Performance Considerations

1. **GetFrame2Ecl()**: OrbitController caches `_lastFrame2Ecl` and reuses it as a default
2. **Animation State**: Transition path lerps position/rotation/distance/offsets using cached “last” values
3. **Interpolation Choice**: OrbitController uses `doubleQuat.Lerp` (not slerp) plus `Smootherstep`
4. **Input Handling**: Early returns prevent unnecessary processing

---

## Summary

The KSA camera system uses:
1. **Orbit angles** (Azimuth, Elevation) to build a look direction via axis-angle rotations
2. **Reference frames** via quaternion transformations to provide context-aware camera behavior
3. **Look-at rotation** (`Camera.LookAtRotation(dir, up)`) to keep the camera pointed at the target
4. **Distance scaling** as `distanceMeters = focusedRadius * DistancePower`
5. **Smooth interpolation** (lerps + `Smootherstep`) for transitions between focus/reference/editing states

For a custom keyframe animation controller:
- Inherit from `Controller`
- Store keyframes with spherical coordinates + reference frame
- Interpolate between keyframes using easing functions
- Use `GetFrame2Ecl()` to handle reference frame transformations
- Calculate camera position as `targetPos + Transform(sphericalOffset, frame2Ecl)`
- Orient camera with look-at to always face the target

This approach maintains compatibility with the existing system while enabling scripted camera movements.
