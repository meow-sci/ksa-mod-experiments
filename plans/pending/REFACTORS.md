# refactors

perform a /git-commit after each task is complete

ensure each task fully compiles

## task 1

re-organize exising ImGui elements.

no functional changes.

### Simple Movement

- Rename "Simple Movement" to "Zoom Out Animation"
- Collapsed by default
- Change order of elements to and make all labels on left side for consistency:
  - Speed - change default to 25
  - Duration
  - Animation Easing - change default to ease-out
  - Lerp checkbox
  - Lerp duration
  - Lerp Easing - default to ease-in-out
  - Progress bar
  - Start Patching - hange to "Run Animation"

### Orbit Animation

- Collapsed by default
- Change order of elements to and make all labels on left side for consistency:
  - Orbit Degrees - change default to 270, change max range to 1,080
  - Duration
  - Animation Easing - change default to ease-out
  - Lerp checkbox
  - Lerp duration
  - Lerp Easing - default to ease-in-out
  - Progress bar
  - Start Orbit - hange to "Run Animation"

## task 2

Loopy Orbit Animation

An all-new animation that orbits the target while simultaneously oscillating on a perpendicular axis, creating a wave/loop pattern.

### Current System Analysis

The existing orbit animation works as follows:
1. **Initialization**: Captures start position, calculates orbit axis (perpendicular to camera-to-target vector and camera up)
2. **Per-frame update**: Uses Rodrigues' rotation formula to rotate the camera offset around the orbit axis by the eased angle
3. **Position calculation**: `newPosition = targetPosition + rotatedOffset`
4. **Look-at**: Always points camera at the target using `Camera.LookAtRotation`

The key math for orbit is in `HandleOnFramePrefix`:
```csharp
// Rodrigues' rotation formula
double3 rotatedOffset = startOffset * cos(θ) + cross(axis, startOffset) * sin(θ) + axis * dot(axis, startOffset) * (1 - cos(θ))
```

### Loopy Orbit Design

The loopy orbit adds a **secondary oscillation** perpendicular to the main orbit plane:

```
finalPosition = target + baseOrbitOffset + verticalOscillation
```

Where:
- `baseOrbitOffset` = standard orbit rotation (existing logic)
- `verticalOscillation` = `amplitude * sin(θ * loopsPerRevolution) * verticalAxis`
- `loopsPerRevolution` = 360 / loopIntervalDegrees (e.g., 360/90 = 4 loops per full orbit)
- `verticalAxis` = axis perpendicular to both orbit axis and current offset direction

For a 270° orbit with 90° loop interval: 3 complete up-down oscillations occur.

### Subtasks

#### 2.1 - Add Loopy Orbit State Variables to Patcher.cs

Add new static fields after the existing orbit lerp back state:

```csharp
// Loopy orbit animation state
private static bool _isLoopyOrbitEnabled = false;
private static bool _isLoopyOrbitActive = false;
private static double _loopyOrbitElapsedTime = 0.0;
private static double _loopyOrbitDegrees = 270.0;           // Total orbit angle
private static double _loopyOrbitDurationSeconds = 8.0;      // Longer default for complex motion
private static EasingType _loopyOrbitEasingType = EasingType.EaseOut;
private static double3 _loopyOrbitStartPosition;
private static doubleQuat _loopyOrbitStartRotation;
private static double3 _loopyOrbitTargetPosition;
private static double3 _loopyOrbitAxis;                      // Main orbit axis
private static double3 _loopyOrbitVerticalAxis;              // Perpendicular oscillation axis
private static double _loopyLoopIntervalDegrees = 90.0;      // How often to complete one up-down cycle
private static double _loopyAmplitudeMeters = 50.0;          // How far up/down to oscillate

// Loopy orbit lerp back state
private static bool _loopyLerpBackEnabled = true;
private static bool _isLoopyLerpingBack = false;
private static double _loopyLerpBackElapsedTime = 0.0;
private static double _loopyLerpBackDurationSeconds = 3.0;
private static EasingType _loopyLerpBackEasingType = EasingType.EaseInOut;
private static double3 _loopyLerpStartOffset;
private static double3 _loopyLerpEndOffset;
```

#### 2.2 - Add Loopy Orbit Public Properties to Patcher.cs

Add property accessors following the pattern of existing orbit properties:

```csharp
public static bool IsLoopyOrbitEnabled { get; set; }          // With reset logic like IsOrbitAnimationEnabled
public static bool IsLoopyOrbitActive => _isLoopyOrbitActive;
public static double LoopyOrbitElapsedTime => _loopyOrbitElapsedTime;
public static bool IsLoopyLerpingBack => _isLoopyLerpingBack;
public static double LoopyLerpBackElapsedTime => _loopyLerpBackElapsedTime;
public static double LoopyOrbitDegrees { get; set; }          // Clamp 90-1080
public static double LoopyOrbitDurationSeconds { get; set; }  // Clamp 1-60
public static EasingType LoopyOrbitEasingType { get; set; }
public static bool LoopyLerpBackEnabled { get; set; }
public static double LoopyLerpBackDurationSeconds { get; set; } // Clamp 1-10
public static EasingType LoopyLerpBackEasingType { get; set; }
public static double LoopyLoopIntervalDegrees { get; set; }   // Clamp 30-180
public static double LoopyAmplitudeMeters { get; set; }       // Clamp 1-500
```

#### 2.3 - Add CalculateLoopyOrbitVerticalAxis Helper Method

Add a helper to calculate the oscillation axis (perpendicular to orbit plane):

```csharp
private static double3 CalculateLoopyVerticalAxis(double3 orbitAxis, double3 currentOffset)
{
    // The vertical axis should be perpendicular to both:
    // 1. The main orbit axis (so oscillation is out of the orbit plane)
    // 2. The current camera-to-target direction (so it's "up" relative to camera view)
    
    double3 offsetDir = double3.Normalize(currentOffset);
    double3 vertical = double3.Cross(orbitAxis, offsetDir);
    
    if (vertical.LengthSquared() < 0.0001)
    {
        // Fallback if parallel
        vertical = double3.UnitY;
    }
    
    return double3.Normalize(vertical);
}
```

#### 2.4 - Add Loopy Orbit Animation Logic to HandleOnFramePrefix

Insert loopy orbit handling after orbit lerp back handling, before regular orbit handling:

```csharp
// Handle loopy orbit lerp back
if (_isLoopyLerpingBack)
{
    double3 currentTargetPos = GetTargetPosition(controller, _loopyOrbitTargetPosition);
    double t = _loopyLerpBackElapsedTime / _loopyLerpBackDurationSeconds;
    double easedT = ApplyEasing(t, _loopyLerpBackEasingType);
    
    double3 currentOffset = double3.Lerp(_loopyLerpEndOffset, _loopyLerpStartOffset, easedT);
    transform.PositionEcl = currentTargetPos + currentOffset;
    LookAtTarget(transform, currentTargetPos);
    
    _loopyLerpBackElapsedTime += deltaTime;
    if (_loopyLerpBackElapsedTime >= _loopyLerpBackDurationSeconds)
    {
        _isLoopyOrbitEnabled = false;
        _isLoopyOrbitActive = false;
        _isLoopyLerpingBack = false;
    }
    return false;
}

// Handle loopy orbit animation
if (_isLoopyOrbitEnabled)
{
    if (transform == null) return true;
    
    // Initialize on first frame
    if (!_isLoopyOrbitActive)
    {
        _isLoopyOrbitActive = true;
        _loopyOrbitStartPosition = transform.PositionEcl;
        _loopyOrbitStartRotation = transform.LocalRotation;
        _loopyOrbitElapsedTime = 0.0;
        _loopyOrbitTargetPosition = GetTargetPosition(controller);
        _loopyLerpStartOffset = _loopyOrbitStartPosition - _loopyOrbitTargetPosition;
        
        double radius = _loopyLerpStartOffset.Length();
        if (radius < 0.01)
        {
            Console.WriteLine("camera-controller-override: Loopy orbit radius too small, cancelling.");
            _isLoopyOrbitEnabled = false;
            _isLoopyOrbitActive = false;
            return true;
        }
        
        _loopyOrbitAxis = CalculateOrbitAxis(_loopyLerpStartOffset, _loopyOrbitStartRotation);
        _loopyOrbitVerticalAxis = CalculateLoopyVerticalAxis(_loopyOrbitAxis, _loopyLerpStartOffset);
    }
    
    _loopyOrbitElapsedTime += deltaTime;
    double t = Math.Min(1.0, _loopyOrbitElapsedTime / _loopyOrbitDurationSeconds);
    double easedT = ApplyEasing(t, _loopyOrbitEasingType);
    double angleDegrees = _loopyOrbitDegrees * easedT;
    double angleRadians = angleDegrees * Math.PI / 180.0;
    
    // Base orbit using Rodrigues' rotation formula
    double3 startOffset = _loopyOrbitStartPosition - _loopyOrbitTargetPosition;
    double3 k = _loopyOrbitAxis;
    double cos = Math.Cos(angleRadians);
    double sin = Math.Sin(angleRadians);
    double3 baseOrbitOffset = startOffset * cos + double3.Cross(k, startOffset) * sin + k * double3.Dot(k, startOffset) * (1.0 - cos);
    
    // Vertical oscillation: sin wave based on current angle
    double loopsPerRevolution = 360.0 / _loopyLoopIntervalDegrees;
    double oscillationPhase = angleDegrees * loopsPerRevolution * Math.PI / 180.0;
    double oscillationAmount = Math.Sin(oscillationPhase) * _loopyAmplitudeMeters;
    double3 verticalOscillation = _loopyOrbitVerticalAxis * oscillationAmount;
    
    // Combined position
    double3 currentTargetPos = GetTargetPosition(controller, _loopyOrbitTargetPosition);
    transform.PositionEcl = currentTargetPos + baseOrbitOffset + verticalOscillation;
    LookAtTarget(transform, currentTargetPos);
    
    if (_loopyOrbitElapsedTime >= _loopyOrbitDurationSeconds)
    {
        if (_loopyLerpBackEnabled)
        {
            _loopyLerpEndOffset = transform.PositionEcl - currentTargetPos;
            _isLoopyLerpingBack = true;
            _isLoopyOrbitActive = false;
            _loopyLerpBackElapsedTime = 0.0;
        }
        else
        {
            _isLoopyOrbitEnabled = false;
            _isLoopyOrbitActive = false;
        }
    }
    return false;
}
```

#### 2.5 - Add Loopy Orbit UI Panel to Mod.cs

Add a new collapsible panel in `RenderWindow()` after the Orbit Animation panel:

```csharp
// Loopy Orbit Animation Panel
if (ImGui.CollapsingHeader("Loopy Orbit Animation"))
{
    ImGui.Indent();
    
    // Status display
    string loopyStatus = Patcher.IsLoopyOrbitEnabled 
        ? (Patcher.IsLoopyLerpingBack ? "Lerping Back..." : (Patcher.IsLoopyOrbitActive ? "Loopy Orbiting..." : "Animation Starting...")) 
        : "Inactive";
    ImGui.Text($"Status: {loopyStatus}");
    
    // UI elements in order:
    // - Orbit Degrees (default 270, max 1080)
    // - Loop Interval (default 90, range 30-180) - NEW parameter
    // - Amplitude (default 50, range 1-500) - NEW parameter  
    // - Duration
    // - Animation Easing (default ease-out)
    // - Lerp checkbox
    // - Lerp duration
    // - Lerp Easing (default ease-in-out)
    // - Progress bar
    // - Run Animation button
    
    ImGui.Unindent();
}
```

UI element details:
- **Orbit Degrees**: SliderFloat, range 90-1080, default 270
- **Loop Interval (deg)**: SliderFloat, range 30-180, default 90 (controls how often one up-down cycle completes)
- **Amplitude (m)**: SliderFloat, range 1-500, default 50 (how far up/down the camera oscillates)
- **Duration (s)**: SliderFloat, range 1-60, default 8
- **Animation Easing**: Combo dropdown, default Ease Out
- **Lerp Back**: Checkbox, default true
- **Lerp Duration (s)**: SliderFloat, range 1-10, default 3
- **Lerp Easing**: Combo dropdown, default Ease In-Out
- **Progress bar**: Shows elapsed/total or lerp progress
- **Run Animation** button: Toggles `IsLoopyOrbitEnabled`

#### 2.6 - Build and Test

1. Run `dotnet build` to verify compilation
2. Test in-game:
   - Verify animation runs smooth 360° orbit with visible up/down oscillation
   - Test with different loop intervals (90° = 4 loops per revolution, 180° = 2 loops)
   - Test amplitude scaling
   - Verify lerp back returns to start position smoothly
   - Ensure camera always looks at target throughout

### Future Keyframe System (Deferred)

The current implementation uses a parametric approach for the loopy orbit. A more general keyframe system could be extracted later if needed for additional animation types. The keyframe architecture would look like:

```csharp
public interface IAnimationKeyframe
{
    double NormalizedTime { get; }                    // 0.0 to 1.0
    double3 CalculateOffset(AnimationContext ctx);   // Camera offset from target
    double3 GetLookAtPosition(AnimationContext ctx); // What to look at
    EasingType EasingToNext { get; }                 // Easing to interpolate to next keyframe
}

public class AnimationSequence
{
    public List<IAnimationKeyframe> Keyframes { get; }
    public double TotalDurationSeconds { get; }
    public bool LerpBackEnabled { get; }
    
    public (double3 offset, double3 lookAt) Evaluate(double normalizedTime);
}
```

This would allow composing complex animations from discrete keyframes, supporting:
- Multiple look-at targets with interpolation
- Arbitrary position curves
- Per-segment easing
- Reusable animation components

However, for Task 2, the parametric sinusoidal approach is simpler and more appropriate.

