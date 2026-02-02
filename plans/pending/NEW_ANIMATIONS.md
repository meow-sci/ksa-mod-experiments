# overview

to add new animations that can be supported in the keyframe animation system.

each one should get a new collapisble header and follow similar patterns to the existing animations.

# animations

## shake (left/right)

"shake" the camera left/right (from the viewers perspective) N times with an an easing function that can be selected for between the back/forth (or use a similar oscilation function like the loopy orbit does), the goal is to make the shake back/forth motion be smooth.

it should appear as if someone was shaking their head back and forth.

the animation length, number of shakes and some kind of input into how harsh the shaking motion is if that's possible.

## zoom in

the opposite of the current zoom out animation, zoom in to the target.  support same parameters as the existing zoom out.

## zoom in to target offset

this is like zoom in except we want to zoom into some fixed offset of the center of the target.

the goal here is to enable zooming into the helmet of an astronaut game model by e.g. specifying 0.25m offset on Z axis for the target offset, meaning that this would be how far up their face is from their center on the Z axis.

support setting the offset on all three axis with a scale of 0.25m to 20m defaulting to 0.5m

# tasks

## 1. Shake Animation Implementation

### 1.1 Create `ShakeAnimation.cs`
**File**: `camera-controller-override/Animation/Animations/ShakeAnimation.cs`

Create a new animation class implementing `IKeyframeAnimation` with rotational yaw-based head-shaking:

**Configuration Parameters**:
- `DurationSeconds` (double) - total animation length
- `ShakeCount` (int) - number of back/forth oscillations
- `AmplitudeDegrees` (double) - how far the yaw rotates from center (extent)
- `ShakeSpeed` (double) - acceleration/snap factor affecting how quickly it transitions between positions
- `Easing` (EasingType) - easing function for overall animation progress

**Implementation Notes**:
- Use sinusoidal oscillation similar to `LoopyOrbitAnimation` pattern
- Apply yaw rotation to camera's `LocalRotation` quaternion
- Track `_lastYawOffset` to apply incremental changes each frame
- The oscillation formula should incorporate both amplitude and speed modifiers
- Shake should be relative to camera's current forward direction (screen left/right)
- Maintain look-at target after shake completes (restore original orientation)

**Required Methods**:
- `Initialize()` - capture starting rotation, reset progress tracking
- `Update()` - calculate shake oscillation, apply incremental yaw rotation
- `Reset()` - clear all runtime state
- `GetDisplayProperties()` - return dict with all parameters for UI display

---

### 1.2 Add Shake Animation UI Section in `Mod.cs`
**File**: `camera-controller-override/Mod.cs`

**Add Class Fields**:
```csharp
// Shake configuration
private float _shakeDuration = 2.0f;
private int _shakeCount = 4;
private float _shakeAmplitude = 5.0f;  // degrees
private float _shakeSpeed = 1.0f;       // speed modifier
private int _shakeEasing = (int)Animation.EasingType.EaseInOut;
```

**Add Collapsible Header Section** (after existing animation sections):
- Header: "Shake Animation"
- Slider: Duration (1.0 - 10.0s)
- SliderInt: Shake Count (1 - 20)
- Slider: Amplitude (1.0 - 45.0 degrees)
- Slider: Speed (0.5 - 3.0)
- Combo: Easing dropdown
- Button: "Add to Sequence##Shake"

**Follow Existing Pattern**: Mirror the structure of `Loopy Orbit Animation` section.

---

## 2. Zoom In Animation Implementation

### 2.1 Create `ZoomInAnimation.cs`
**File**: `camera-controller-override/Animation/Animations/ZoomInAnimation.cs`

Create a fresh implementation moving camera toward target (opposite of `ZoomOutAnimation`):

**Configuration Parameters**:
- `SpeedMetersPerSecond` (double) - movement speed
- `DurationSeconds` (double) - total animation length
- `Easing` (EasingType) - easing function for movement

**Implementation Notes**:
- Calculate direction FROM camera TOWARD target each frame
- Apply frame-based eased progress like `ZoomOutAnimation`
- Handle edge case: camera very close to target (stop before collision)
- Add minimum distance safeguard (e.g., 1.0m from target)
- Log initialization and completion for debugging

**Required Methods**:
- `Initialize()` - reset progress tracking
- `Update()` - calculate direction toward target, apply displacement
- `Reset()` - clear runtime state
- `GetDisplayProperties()` - return Speed, Duration, Easing

---

### 2.2 Add Zoom In Animation UI Section in `Mod.cs`
**File**: `camera-controller-override/Mod.cs`

**Add Class Fields**:
```csharp
// Zoom In configuration
private float _zoomInSpeed = 25.0f;
private float _zoomInDuration = 5.0f;
private int _zoomInEasing = (int)Animation.EasingType.EaseOut;
```

**Add Collapsible Header Section** (place adjacent to Zoom Out section):
- Header: "Zoom In Animation"
- Slider: Speed (1.0 - 250.0 m/s)
- Slider: Duration (1.0 - 30.0s)
- Combo: Easing dropdown
- Button: "Add to Sequence##ZoomIn"

**Follow Existing Pattern**: Mirror the structure of `Zoom Out Animation` section.

---

## 3. Zoom In To Target Offset Animation Implementation

### 3.1 Create `ZoomInToOffsetAnimation.cs`
**File**: `camera-controller-override/Animation/Animations/ZoomInToOffsetAnimation.cs`

Create animation that zooms toward an offset point from target center:

**Configuration Parameters**:
- `SpeedMetersPerSecond` (double) - movement speed
- `DurationSeconds` (double) - total animation length
- `Easing` (EasingType) - easing function
- `OffsetX` (double) - X-axis offset from target center (meters)
- `OffsetY` (double) - Y-axis offset from target center (meters)
- `OffsetZ` (double) - Z-axis offset from target center (meters)

**Implementation Notes**:
- Destination = `targetPosition + offset` where offset is in target-local or world coordinates
- Similar structure to `ZoomInAnimation` but with offset destination
- The offset should be applied in the target's coordinate frame if possible, else world coordinates
- Consider using target's rotation to transform offset into world space
- Add minimum distance safeguard to prevent camera clipping
- Final look-at should point at `targetPosition + offset` (the offset point)

**Required Methods**:
- `Initialize()` - capture initial state, calculate final destination
- `Update()` - move toward offset destination, maintain look-at to offset point
- `Reset()` - clear runtime state
- `GetDisplayProperties()` - return Speed, Duration, Easing, Offset values

---

### 3.2 Add Zoom In To Offset Animation UI Section in `Mod.cs`
**File**: `camera-controller-override/Mod.cs`

**Add Class Fields**:
```csharp
// Zoom In To Offset configuration
private float _zoomInOffsetSpeed = 25.0f;
private float _zoomInOffsetDuration = 5.0f;
private int _zoomInOffsetEasing = (int)Animation.EasingType.EaseOut;
private float _zoomInOffsetX = 0.0f;   // meters
private float _zoomInOffsetY = 0.5f;   // meters (default: slightly above center)
private float _zoomInOffsetZ = 0.0f;   // meters
```

**Add Collapsible Header Section** (after Zoom In section):
- Header: "Zoom In To Offset Animation"
- Slider: Speed (1.0 - 250.0 m/s)
- Slider: Duration (1.0 - 30.0s)
- Combo: Easing dropdown
- Slider: X Offset (0.25 - 20.0 m, or negative range -20.0 to 20.0)
- Slider: Y Offset (0.25 - 20.0 m, or negative range -20.0 to 20.0)
- Slider: Z Offset (0.25 - 20.0 m, or negative range -20.0 to 20.0)
- Button: "Add to Sequence##ZoomInOffset"

**Note**: Consider whether offset sliders need negative range for positioning below/behind target.

---

## 4. Validation & Testing

### 4.1 Compile and Verify Build
Run `dotnet build` to ensure all new code compiles without errors.

### 4.2 Verify Animation Interface Compliance
Ensure each new animation class:
- Properly implements all `IKeyframeAnimation` methods
- Returns meaningful `GetDisplayProperties()` output
- Handles edge cases (zero duration, target at camera position, etc.)
- Logs appropriately on initialize/complete

---

## Implementation Order Recommendation

1. **Task 2.1 + 2.2** (Zoom In) - Simplest, closely mirrors existing ZoomOut
2. **Task 3.1 + 3.2** (Zoom In To Offset) - Builds on Zoom In with offset concept
3. **Task 1.1 + 1.2** (Shake) - Most complex, requires rotational math
4. **Task 4** (Validation) - Final verification after all implementations