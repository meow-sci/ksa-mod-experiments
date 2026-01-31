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
public double DistancePower = 3.0; // Logarithmic distance from target
```

The camera position is determined by:
1. **Azimuth** - Horizontal angle around the target (unlimited rotation)
2. **Elevation** - Vertical angle, clamped to [-π/2, π/2] to prevent gimbal lock
3. **Distance** - Calculated as `pow(base, DistancePower)` for smooth zoom

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

**The Camera Look-At Implementation:**

While not fully visible in the decompiled code (due to omitted lines), the system works as follows:

1. **Target Position**: Get `focused.GetPositionEcl()` - target's position in ecliptic coordinates
2. **Reference Frame**: Apply `GetFrame2Ecl()` to get the rotation basis
3. **Spherical to Cartesian**: Convert (Azimuth, Elevation, Distance) to offset vector
4. **Camera Position**: `cameraPos = targetPos + rotatedOffset`
5. **Camera Orientation**: Point camera at target using look-at transformation

**Pseudo-code reconstruction:**
```csharp
// Get target position in ecliptic space
double3 targetPosEcl = focused.GetPositionEcl();

// Get reference frame rotation
doubleQuat frame2Ecl = GetFrame2Ecl(focused, referenceFrame);

// Convert spherical coordinates to cartesian offset in reference frame
double distance = Math.Pow(someBase, DistancePower);
double3 offsetInFrame = new double3(
    distance * cos(Elevation) * cos(Azimuth),
    distance * cos(Elevation) * sin(Azimuth),
    distance * sin(Elevation)
);

// Transform offset to ecliptic space
double3 offsetEcl = offsetInFrame.Transform(frame2Ecl);

// Calculate camera position and orientation
camera.Position = targetPosEcl + offsetEcl;
camera.LookAt(targetPosEcl);  // Orient to look at target
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
    
    // Clamp to minimum distance
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

**This provides smooth camera transitions between targets.**

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

The camera smoothly interpolates toward `lookTgt` using `lookSharpness` as the lerp factor.

### Reference Frame System

FlyController also uses `GetFrame2Ecl()` similar to OrbitController, supporting the same reference frames. This keeps the camera's movement relative to the selected frame.

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
    // Store camera's offset from tracked celestial
    // Maintains relative position when switching modes
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
- Smooth interpolation (slerp)

**Key operations:**
- `doubleQuat.Concatenate(q1, q2)` - Combine rotations
- `vector.Transform(quat)` - Rotate vector by quaternion
- `doubleQuat.CreateFromAxisAngle(axis, angle)` - Create rotation
- Interpolation: `doubleQuat.Slerp(start, end, t)`

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
    public double DistancePower;          // Zoom level
    public CameraReferenceFrame Frame;    // Reference frame for this keyframe
    public EasingFunction Easing;         // Interpolation curve
}
```

### Keyframe Interpolation

Between keyframes, interpolate:
1. **Azimuth/Elevation**: Linear or spherical interpolation
2. **Distance**: Linear interpolation of DistancePower (logarithmic zoom)
3. **Reference Frame**: Either snap at keyframe or use quaternion slerp between frame rotations

### Animation Sequences

Define common animation patterns:

**Orbit Around Target:**
```csharp
void CreateOrbitSequence(double duration, double radius, double startAngle)
{
    int steps = 60;
    for (int i = 0; i <= steps; i++)
    {
        double t = i / (double)steps;
        AddKeyframe(new CameraKeyframe {
            TimeStamp = duration * t,
            Azimuth = startAngle + (2 * Math.PI * t),
            Elevation = 0,
            DistancePower = Math.Log(radius) / Math.Log(BASE),
            Frame = CameraReferenceFrame.Stars
        });
    }
}
```

**Zoom In/Out:**
```csharp
void CreateZoomSequence(double duration, double startDist, double endDist)
{
    AddKeyframe(TimeStamp: 0, DistancePower: Log(startDist));
    AddKeyframe(TimeStamp: duration, DistancePower: Log(endDist));
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
    
    // Calculate camera offset in spherical coordinates
    double distance = Math.Pow(BASE, distPower);
    double3 offsetInFrame = SphericalToCartesian(azimuth, elevation, distance);
    double3 offsetEcl = offsetInFrame.Transform(frame2Ecl);
    
    // Position and orient camera
    Camera.Position = targetPos + offsetEcl;
    Camera.LookAt(targetPos);
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

1. **Quaternion Slerp**: More expensive than lerp, use sparingly
2. **GetFrame2Ecl()**: Caches `_lastFrame2Ecl`, only recalculates when needed
3. **Animation State**: Uses `_animProgress` to track completion, avoids recalculation
4. **Input Handling**: Early returns prevent unnecessary processing

---

## Summary

The KSA camera system uses:
1. **Spherical coordinates** (Azimuth, Elevation, Distance) for intuitive orbiting
2. **Reference frames** via quaternion transformations to provide context-aware camera behavior
3. **Look-at targeting** to keep camera pointed at the focused object
4. **Smooth interpolation** for transitions between states
5. **Coordinate transformations** between reference frames and ecliptic space

For a custom keyframe animation controller:
- Inherit from `Controller`
- Store keyframes with spherical coordinates + reference frame
- Interpolate between keyframes using easing functions
- Use `GetFrame2Ecl()` to handle reference frame transformations
- Calculate camera position as `targetPos + Transform(sphericalOffset, frame2Ecl)`
- Orient camera with look-at to always face the target

This approach maintains compatibility with the existing system while enabling scripted camera movements.
