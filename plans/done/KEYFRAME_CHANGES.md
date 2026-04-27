# problems with existing

- the consecutive animations are not behaving as expected.  for example, if i run zoom out three times in a row, i expect the main animation to play three times consecutively, but the starting point of each animation should be where the camera is at the end of the previous animation, this does not appear to be working this way
- lerp back to start should only take place after the last animation, and should take place from where the last animation ended back to the ORIGINAL start camera offset before the animation sequence started

this means that when pushing an animation into the keyframe list it should only encapsulate that animation itself

it also must encapsulte the ability to start from wherever the camera is at that time, whether its the starting point of the first animation or the end point of any previous animation.

# tasks

---

## Task 1: Simplify Animation Classes - Remove Position Capture

**Goal**: Ensure all animations start fresh from current camera position, not from captured start positions.

**Files to modify**:
- `Animation/Animations/ZoomOutAnimation.cs`
- `Animation/Animations/OrbitAnimation.cs`
- `Animation/Animations/LoopyOrbitAnimation.cs`

**Changes**:

### ZoomOutAnimation
- `Initialize()` should ONLY capture direction from current position, not store starting position
- Works correctly already - verify it doesn't store absolute positions

### OrbitAnimation
- `Initialize()` captures current position as orbit center reference
- This is correct - it needs to know radius from target
- Verify it recalculates from current Transform3D position each time Initialize() is called

### LoopyOrbitAnimation
- Same as OrbitAnimation
- Verify fresh initialization from current position

**Validation**:
- Each animation's Initialize() must use `transform.PositionEcl` directly
- No assumptions about where the camera "should" be
- Multiple consecutive animations of same type should work correctly

**Acceptance Criteria**:
- Three ZoomOut animations in sequence move progressively further away
- Three Orbit animations in sequence each orbit from wherever camera currently is
- Solution compiles

---

## Task 2: Remove Transition System

**Goal**: Remove all transition-related code as it's not needed - animations chain naturally.

**Files to modify**:
- `Animation/Keyframe.cs`
- `Animation/KeyframeSequencePlayer.cs`
- `Animation/Animations/TransitionAnimation.cs` (delete this file)

**Changes**:

### Keyframe.cs
- Remove properties: `IncludeTransitionIn`, `TransitionInDurationSeconds`, `TransitionInEasing`
- Keep only: `Id`, `Animation`

### KeyframeSequencePlayer.cs
- Remove `_activeTransition` field
- Remove `_transitionElapsedTime` field
- Remove all transition handling logic in Update()
- Simplify to just: initialize current keyframe → update it → when complete, move to next keyframe
- When last keyframe completes, handle return-to-start (Task 3)
- Remove transition parameters from `AddKeyframe()` - should only take `IKeyframeAnimation`

### Delete TransitionAnimation.cs
- No longer needed

**Acceptance Criteria**:
- Keyframes play sequentially without transitions
- Each animation starts from current camera position
- Solution compiles

---

## Task 3: Add Sequence-Level Return to Start

**Goal**: Add optional "return to start" that happens AFTER the entire sequence completes.

**Files to modify**:
- `Animation/KeyframeSequencePlayer.cs`

**Changes**:

### KeyframeSequencePlayer.cs

Add new fields:
```csharp
private double3 _sequenceStartPosition;
private doubleQuat _sequenceStartRotation;
private bool _returnToStartEnabled = true;
private double _returnToStartDuration = 3.0;
private EasingType _returnToStartEasing = EasingType.EaseInOut;
private bool _isReturningToStart = false;
private double _returnElapsedTime = 0.0;
```

Add public properties:
```csharp
public bool ReturnToStartEnabled { get; set; }
public double ReturnToStartDuration { get; set; }
public EasingType ReturnToStartEasing { get; set; }
public bool IsReturningToStart => _isReturningToStart;
```

Modify `Play()`:
- Capture `_sequenceStartPosition` and `_sequenceStartRotation` from current transform

Modify `Update()`:
- After last keyframe completes, if `ReturnToStartEnabled`:
  - Set `_isReturningToStart = true`
  - Lerp camera from current position back to `_sequenceStartPosition`/`_sequenceStartRotation`
  - Use easing with `_returnToStartDuration`
  - When complete, call Stop()

**Acceptance Criteria**:
- Sequence captures start position when Play() is called
- After all keyframes, camera returns to original position
- Return-to-start can be disabled
- Solution compiles

---

## Task 4: Remove Standalone Animations from Patcher

**Goal**: Simplify Patcher to only use sequence player, remove all standalone animation code.

**Files to modify**:
- `Patcher.cs`

**Changes**:

Remove fields:
- All `_isAnimationEnabled`, `_isOrbitAnimationEnabled`, `_isLoopyOrbitEnabled` and related state
- All `_standaloneZoomOut`, `_standaloneOrbit`, `_standaloneLoopyOrbit` instances
- All lerp-back related fields for standalone animations
- Keep ONLY: `_harmony`, `_sequencePlayer`

Remove properties:
- All standalone animation property accessors (IsAnimationEnabled, AnimationSpeedMetersPerSecond, etc.)
- These are no longer needed since everything is configured and added to sequence
- Keep ONLY: `SequencePlayer` property

Simplify `HandleOnFramePrefix()`:
```csharp
private static bool HandleOnFramePrefix(Controller controller, double deltaTime, Transform3D transform)
{
    try
    {
        if (_sequencePlayer.State == PlaybackState.Playing)
        {
            bool shouldSkip = _sequencePlayer.Update(controller, transform, deltaTime);
            return !shouldSkip;
        }
        return true; // Allow normal camera control
    }
    catch (Exception ex)
    {
        Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
        return true;
    }
}
```

**Acceptance Criteria**:
- Patcher is drastically simplified (~50 lines instead of ~500)
- Only sequence player remains
- Solution compiles

---

## Task 5: Redesign UI - Configuration Sections Instead of Standalone Panels

**Goal**: Replace standalone animation panels with configuration sections that only feed into "Add to Sequence".

**Files to modify**:
- `Mod.cs`

**Changes**:

### Remove old panel structure
- Remove all state tracking for standalone animations
- Remove animation progress displays for standalone
- Remove "Run Animation" buttons
- Remove individual animation status displays

### Create new structure
Each animation type gets a simple configuration section:

**Zoom Out Configuration**:
- Speed slider
- Duration slider
- Easing dropdown
- [Add to Sequence] button → creates ZoomOutAnimation and calls SequencePlayer.AddKeyframe()

**Orbit Configuration**:
- Degrees slider
- Duration slider
- Easing dropdown
- [Add to Sequence] button

**Loopy Orbit Configuration**:
- Degrees slider
- Loop Interval slider
- Amplitude slider
- Duration slider
- Easing dropdown
- [Add to Sequence] button

**Keyframe Sequence Panel** (existing, but modify):
- Keep all existing functionality
- Add "Return to Start" checkbox
- Add "Return Duration" slider
- Add "Return Easing" dropdown
- These control SequencePlayer.ReturnToStartEnabled/Duration/Easing

### Store configuration in Mod.cs fields
```csharp
// Zoom Out config
private float _zoomOutSpeed = 25.0f;
private float _zoomOutDuration = 5.0f;
private int _zoomOutEasing = (int)EasingType.EaseOut;

// Orbit config
private float _orbitDegrees = 270.0f;
private float _orbitDuration = 5.0f;
private int _orbitEasing = (int)EasingType.EaseOut;

// Loopy Orbit config
private float _loopyOrbitDegrees = 270.0f;
private float _loopyLoopInterval = 90.0f;
private float _loopyAmplitude = 50.0f;
private float _loopyDuration = 8.0f;
private int _loopyEasing = (int)EasingType.EaseOut;
```

### Update window size
- Reduce height back down (maybe 800?) since we removed lots of UI

**Acceptance Criteria**:
- UI is simpler and clearer
- Each configuration section has "Add to Sequence" button
- No "Run Animation" buttons
- Sequence panel has return-to-start controls
- Solution compiles

---

## Task 6: Update KeyframeSequencePanel UI

**Goal**: Add return-to-start controls to sequence panel, remove transition displays.

**Files to modify**:
- `UI/KeyframeSequencePanel.cs`

**Changes**:

### Remove from keyframe display:
- Transition indicators (`↕ Transition: ...`)
- `keyframe.IncludeTransitionIn` checks

### Add return-to-start controls:
After control buttons (Play/Pause/Resume/Stop), add:
```
ImGui.Spacing()
ImGui.Text("Return to Start Settings:")
- Checkbox for player.ReturnToStartEnabled
- Slider for player.ReturnToStartDuration (1.0 - 10.0s)
- Dropdown for player.ReturnToStartEasing
```

### Update status display:
- If `player.IsReturningToStart`, show "Returning to start..." status

**Acceptance Criteria**:
- Return-to-start controls visible and functional
- No transition-related UI elements
- Solution compiles

---

## Task 7: Final Testing and Cleanup

**Goal**: Verify all changes work correctly and clean up any remaining issues.

**Test cases**:
1. Add three ZoomOut animations → Play → Should zoom out progressively further
2. Add three Orbit animations → Play → Should orbit three times from consecutive positions
3. Add mixed animations (Zoom, Orbit, Loopy) → Play → Should chain smoothly
4. Enable "Return to Start" → sequence should return to original position
5. Disable "Return to Start" → sequence should stop at final position
6. Pause/Resume → should maintain state correctly
7. Stop mid-sequence → should stop immediately
8. Clear sequence → should empty list
9. Remove keyframe → should work without breaking playback
10. Move keyframes → should reorder correctly

**Files to check**:
- Verify no references to removed code
- Verify no unused imports
- Clean up any TODO comments

**Acceptance Criteria**:
- All test cases pass
- Solution compiles
- No runtime errors
- Code is clean and maintainable

---

## Implementation Order

Execute tasks in order (1 → 7), committing after each successful compilation:

1. **Task 1**: Verify/fix animation initialization
2. **Task 2**: Remove transition system
3. **Task 3**: Add return-to-start to sequence player
4. **Task 4**: Simplify Patcher
5. **Task 5**: Redesign Mod UI
6. **Task 6**: Update sequence panel UI
7. **Task 7**: Final testing and cleanup

Each task should be implemented by a subagent and verified with `dotnet build` before committing.
