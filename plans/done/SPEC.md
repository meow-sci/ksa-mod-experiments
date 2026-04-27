# Simple Movement Feature Specification

## Overview
Implement a simple camera animation that moves the camera away from its current target over 5 seconds using Harmony runtime patching of the Controller.OnFrame method.

## Requirements Reference
- Feature: "simple movement (via Controller patching)" from IDEA.md
- Target Method: `Controller.OnFrame(Viewport inViewport, double inDeltaTime)`
- Patch Type: Prefix patch (to intercept and replace camera positioning logic)
- Animation: 5 second linear movement away from target
- Default Speed: 1 meter per second (configurable)
- Runtime Control: Toggle button to enable/disable patching

## Architecture

### Component Overview
```
Mod.cs
├── UI: ImGui collapsing section "Simple Movement"
├── UI: "Patch Controller" toggle button
└── State: Animation enabled flag (shared with Patcher)

Patcher.cs
├── Harmony instance lifecycle (setup/teardown)
├── Patch: Controller_OnFrame_Prefix
├── State: Animation tracking variables
└── Logic: 5-second linear camera movement
```

### Data Flow
1. User clicks "Patch Controller" button → Toggles `_isAnimationEnabled`
2. If enabled: Patcher applies prefix patch to Controller.OnFrame
3. Each frame: Prefix patch intercepts OnFrame, calculates new camera position
4. After 5 seconds: Patch sets `_isAnimationEnabled = false` and stops modification
5. Button state reflects `_isAnimationEnabled` for cancel capability

## Design Decisions

### Patch Strategy: Prefix with Conditional Skip
- **Why Prefix**: Allows complete control over camera positioning without running original
- **Return false**: Skips original Controller.OnFrame when animation is active
- **Return true**: Allows original to run when animation is inactive or complete

### Animation State Management
Store in static variables in Patcher class (required for Harmony static patch methods):
```csharp
private static bool _isAnimationEnabled = false;
private static bool _isAnimationActive = false;  
private static double _animationElapsedTime = 0.0;
private static double3 _animationStartPosition;
private static double3 _animationDirection;
private static double _animationSpeedMetersPerSecond = 1.0;
```

### Movement Logic
1. **Start**: Capture current camera rotation to determine backward direction
2. **Direction**: Camera's backward vector (opposite of forward/look direction)
   - Camera forward is `-Z` transformed by camera rotation
   - Backward direction: `(+Z).Transform(rotation)` or equivalent
   - This maintains the same viewing angle while increasing distance
3. **Each Frame**: Move camera backward by `speed * deltaTime`, maintaining look-at
4. **Stop**: After 5 seconds elapsed, disable animation

### Target Position Determination
- For OrbitController: Target is focused astronomical object position
- For FlyController: Target is tracked celestial position
- Fallback: Use Camera.Following.GetPositionEcl()

## Implementation Tasks

### Task 1: Add Animation State to Patcher
**File**: `camera-controller-override/Patcher.cs`

Add static fields for animation state tracking:
- `_isAnimationEnabled`: Toggled by UI button, controls patch application
- `_isAnimationActive`: Animation in progress flag
- `_animationElapsedTime`: Time elapsed since animation start
- `_animationStartPosition`: Camera position when animation started
- `_animationDirection`: Normalized vector indicating movement direction
- `_animationSpeedMetersPerSecond`: Configurable speed (default 1.0)

Add public property:
- `IsAnimationEnabled` (get/set) - Thread-safe access to `_isAnimationEnabled`

### Task 2: Implement Controller.OnFrame Prefix Patch
**File**: `camera-controller-override/Patcher.cs`

Create patch method: `Controller_OnFrame_Prefix`
- Signature: `static bool Prefix(Controller __instance, Viewport inViewport, double inDeltaTime)`
- Attributes: `[HarmonyPatch(typeof(Controller), "OnFrame")]`, `[HarmonyPrefix]`

Logic:
1. Check if animation enabled; if not, return true (run original)
2. On first frame (`!_isAnimationActive`):
   - Set `_isAnimationActive = true`
   - Capture `_animationStartPosition = __instance.Transform.PositionEcl`
   - Get camera rotation: `rotation = __instance.Transform.LocalRotation`
   - Calculate backward direction (opposite of camera's forward/look direction):
     - Forward: `forward = (-double3.UnitZ).Transform(rotation)`
     - Backward: `_animationDirection = -forward` (or `(double3.UnitZ).Transform(rotation)`)
   - This ensures camera moves away while maintaining the same viewing angle
   - Reset `_animationElapsedTime = 0.0`
3. On subsequent frames:
   - Increment `_animationElapsedTime += inDeltaTime`
   - Calculate new position: `newPos = currentPos + (_animationDirection * _animationSpeedMetersPerSecond * inDeltaTime)`
   - Set `__instance.Transform.PositionEcl = newPos`
   - Camera automatically maintains look-at through original Controller logic on next frame
   - If `_animationElapsedTime >= 5.0`:
     - Set `_isAnimationEnabled = false`
     - Set `_isAnimationActive = false`
     - Log completion
4. Return false (skip original OnFrame)

Error handling:
- Wrap in try/catch, log exceptions, return true on error

### Task 3: Add ImGui UI Section
**File**: `camera-controller-override/Mod.cs`

In `RenderWindow()` method, add collapsing section:

```csharp
if (ImGui.CollapsingHeader("Simple Movement"))
{
    ImGui.Indent();
    
    // Status display
    string status = Patcher.IsAnimationEnabled 
        ? (Patcher.IsAnimationActive ? "Animation Running" : "Animation Starting...") 
        : "Inactive";
    ImGui.Text($"Status: {status}");
    
    if (Patcher.IsAnimationActive)
    {
        ImGui.Text($"Elapsed: {Patcher.AnimationElapsedTime:F2}s / 5.00s");
        ImGui.ProgressBar((float)(Patcher.AnimationElapsedTime / 5.0), new float2(200, 0));
    }
    
    ImGui.Spacing();
    
    // Speed configuration
    float speed = (float)Patcher.AnimationSpeedMetersPerSecond;
    if (ImGui.SliderFloat("Speed (m/s)", ref speed, 0.5f, 50.0f))
    {
        Patcher.AnimationSpeedMetersPerSecond = speed;
    }
    
    ImGui.Spacing();
    
    // Toggle button
    string buttonLabel = Patcher.IsAnimationEnabled ? "Stop Patching" : "Start Patching";
    if (ImGui.Button(buttonLabel))
    {
        Patcher.IsAnimationEnabled = !Patcher.IsAnimationEnabled;
        Console.WriteLine($"camera-controller-override: Animation {(Patcher.IsAnimationEnabled ? "enabled" : "disabled")}");
    }
    
    ImGui.Unindent();
}
```

### Task 4: Expose Animation Properties for UI
**File**: `camera-controller-override/Patcher.cs`

Add public accessors for UI display:
```csharp
public static bool IsAnimationActive => _isAnimationActive;
public static double AnimationElapsedTime => _animationElapsedTime;
public static double AnimationSpeedMetersPerSecond 
{ 
    get => _animationSpeedMetersPerSecond; 
    set => _animationSpeedMetersPerSecond = Math.Max(0.5, value); 
}
```

### Task 5: Add Logging and Debugging
**File**: `camera-controller-override/Patcher.cs`

Add console output for:
- Animation start (log start position, target position, direction)
- Animation completion (log final position, total distance traveled)
- Errors during patching

Optional: Add `[HarmonyDebug]` attribute during development for Harmony diagnostics

## Testing Plan

### Manual Testing Steps
1. **Initialization Test**
   - Launch game, verify mod loads
   - Press F11, verify window opens
   - Expand "Simple Movement" section
   - Verify status shows "Inactive"

2. **Animation Start Test**
   - Click "Start Patching" button
   - Verify button changes to "Stop Patching"
   - Verify status shows "Animation Running"
   - Verify camera begins moving away from target

3. **Animation Progress Test**
   - Observe progress bar advancing
   - Verify elapsed time displays correctly
   - Verify camera movement is smooth and linear
   - After 5 seconds, verify animation stops automatically
   - Verify button resets to "Start Patching"

4. **Manual Cancel Test**
   - Start animation
   - Click "Stop Patching" before completion
   - Verify animation stops immediately
   - Verify camera stops moving

5. **Speed Configuration Test**
   - Adjust speed slider to different values
   - Start animation
   - Verify camera moves at different speeds

6. **Edge Cases**
   - Test with different camera controllers (OrbitController vs FlyController)
   - Test starting animation while camera is moving
   - Test rapid enable/disable toggling

## Known Limitations

1. **Single Direction**: Movement only goes backward (away from target), not customizable direction
2. **No Pause/Resume**: Animation cannot be paused, only stopped and restarted
3. **Fixed Duration**: 5 seconds is hardcoded, not configurable
4. **Simple Easing**: Linear motion, no easing functions
5. **No Collision**: Camera can move through celestial bodies
6. **View Angle Drift**: Camera position updates but relies on next frame's original Controller logic to maintain look-at (may cause slight drift if original is skipped)

## Future Enhancements (Out of Scope)
- Configurable animation duration
- Direction selection (away/toward/up/down/orbit)
- Easing functions (ease-in, ease-out, smoothstep)
- Multiple waypoints/keyframes
- Save/load animation presets
- Animation sequencing

## Dependencies

### KSA Game APIs
- `KSA.Controller` - Base camera controller class
- `KSA.Program.OnFrameViewport.GetActiveController()` - Current controller access
- `Controller.Transform.PositionEcl` - Camera position in ecliptic coordinates
- `Controller.Camera.Following` - Target astronomical object
- `Astronomical.GetPositionEcl()` - Target position retrieval

### Libraries
- `HarmonyLib` - Runtime patching (already included)
- `Brutal.ImGuiApi.ImGui` - UI rendering (already included)
- `Brutal.Numerics` - Vector math types (double3, etc.)

### Existing Infrastructure
- `Patcher.Patch()` - Called in Mod.OnFullyLoaded
- `Patcher.Unload()` - Called in Mod.Unload
- `Mod.RenderWindow()` - ImGui rendering location

## Success Criteria
- [ ] UI section displays in mod window
- [ ] Toggle button enables/disables animation
- [ ] Camera moves smoothly away from target for 5 seconds
- [ ] Animation stops automatically after 5 seconds
- [ ] User can cancel animation at any time
- [ ] Speed is configurable via UI slider
- [ ] Progress indicator shows accurate time remaining
- [ ] No crashes or errors during animation
- [ ] Original camera behavior resumes after animation
