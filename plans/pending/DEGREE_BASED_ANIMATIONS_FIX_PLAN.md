# Fix Plan: Degree-Based Animations Cumulative Error

## Problem

Three animations use incremental rotation that accumulates floating-point error over hundreds of frames:
1. **SpiralZoomInAnimation** - Spiral rotation during zoom in
2. **LoopyOrbitAnimation** - Orbit with oscillation
3. **SpiralZoomOutAnimation** - Spiral rotation during zoom out

## Root Cause

All three use the same buggy pattern as OrbitAnimation had:
```csharp
double frameProgress = currentEasedProgress - _lastEasedProgress;
_lastEasedProgress = currentEasedProgress;
double frameAngleDegrees = Degrees * frameProgress;  // Incremental!
_totalDegreesRotated += frameAngleDegrees;
```

This causes:
- Floating-point errors compound over 250+ frames
- 360° rotations don't complete fully
- Final position drifts from expected

## Solution Pattern

Apply the same fix as OrbitAnimation:

### Before (Incremental):
```csharp
double frameProgress = currentEasedProgress - _lastEasedProgress;
double frameAngle = Degrees * frameProgress;
// Apply incremental rotation
```

### After (Absolute):
```csharp
double totalAngle = Degrees * currentEasedProgress;
// Apply total rotation from start
```

## Specific Fixes Required

### 1. SpiralZoomInAnimation

**Current State:**
- Uses `_lastEasedProgress` and `_totalDegreesRotated`
- Applies incremental rotation to up vector each frame
- Rotates around the look-at axis (spiral during zoom)

**Fix:**
- Remove `_lastEasedProgress` tracking
- Store `_startUpVector` during Initialize()
- Each frame: calculate total rotation angle from start
- Apply total rotation to `_startUpVector` (not current up vector)

### 2. LoopyOrbitAnimation

**Current State:**
- Uses `_lastEasedProgress` and `_totalDegreesRotated`
- Combines orbit rotation with sinusoidal oscillation
- Uses incremental Rodrigues rotation like OrbitAnimation

**Fix:**
- Remove `_lastEasedProgress` tracking
- Store `_startOffset` during Initialize()
- Each frame: calculate total rotation angle from start
- Apply total rotation to `_startOffset` (not current offset)
- Oscillation calculation already uses `_totalDegreesRotated`, can now use `totalAngle` directly

### 3. SpiralZoomOutAnimation

**Current State:**
- Uses `_lastEasedProgress` and `_totalDegreesRotated`
- Applies incremental rotation to up vector each frame
- Rotates around the look-at axis (spiral during zoom out)

**Fix:**
- Remove `_lastEasedProgress` tracking
- Store `_startUpVector` during Initialize()
- Each frame: calculate total rotation angle from start
- Apply total rotation to `_startUpVector` (not current up vector)

## Implementation Notes

### Common Changes for All Three:

1. **Remove fields:**
   - `_lastEasedProgress` - no longer needed
   - `_totalDegreesRotated` - replaced with calculated value

2. **Add fields (if rotation-based):**
   - `_startOffset` (for orbit-style) or `_startUpVector` (for spiral-style)

3. **In Update():**
   - Replace incremental calculation with absolute:
     ```csharp
     // OLD:
     double frameProgress = currentEasedProgress - _lastEasedProgress;
     double frameAngle = Degrees * frameProgress;
     _lastEasedProgress = currentEasedProgress;
     
     // NEW:
     double totalAngle = Degrees * currentEasedProgress;
     ```

4. **Apply rotation to stored start state, not current state**

## Testing After Fix

Verify each animation:
- [ ] 360° spiral completes exactly
- [ ] 720° loopy orbit completes exactly
- [ ] Multiple runs produce identical results
- [ ] Animation follows moving targets correctly
