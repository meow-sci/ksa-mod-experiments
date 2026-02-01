# Keyframe Animation System Implementation Plan

## Overview

This document outlines the implementation of a comprehensive keyframe animation system that allows sequencing multiple camera animations into scripted "mini movie" sequences. The system will encapsulate all existing animation types (Zoom Out, Orbit, Loopy Orbit) and allow them to be chained together with optional transitions.

## Current State Analysis

The existing `Patcher.cs` implements three standalone animations:
1. **Zoom Out Animation** - Linear movement away from target with configurable speed, duration, easing
2. **Orbit Animation** - Circular orbit around target using Rodrigues' rotation formula
3. **Loopy Orbit Animation** - Orbit with sinusoidal vertical oscillation

Each animation has:
- Enable/Active states
- Start position/rotation capture on first frame
- Target position tracking (camera's "Following" object)
- Elapsed time tracking with duration
- Easing type support
- Optional lerp-back to start position

## Architecture Design

### Core Interface

```csharp
public interface IKeyframeAnimation
{
    string Name { get; }
    string Description { get; }
    double DurationSeconds { get; }
    EasingType Easing { get; }
    
    // Lifecycle
    void Initialize(Controller controller, Transform3D transform);
    bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime);
    void Reset();
    
    // Optional: Look-at target override (null = use controller's Following target)
    Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    // Serialization for UI display
    Dictionary<string, string> GetDisplayProperties();
}
```

### Keyframe Wrapper

```csharp
public class Keyframe
{
    public int Id { get; set; }
    public IKeyframeAnimation Animation { get; set; }
    public bool IncludeTransitionIn { get; set; } = false;
    public double TransitionInDurationSeconds { get; set; } = 1.0;
    public EasingType TransitionInEasing { get; set; } = EasingType.EaseInOut;
}
```

### Sequence Player

```csharp
public class KeyframeSequencePlayer
{
    public List<Keyframe> Keyframes { get; }
    public int CurrentKeyframeIndex { get; }
    public PlaybackState State { get; } // Stopped, Playing, Paused
    public double CurrentKeyframeElapsedTime { get; }
    public double TotalElapsedTime { get; }
    public double TotalDuration { get; }
    
    public void Play();
    public void Pause();
    public void Resume();
    public void Stop();
    
    public void AddKeyframe(IKeyframeAnimation animation);
    public void RemoveKeyframe(int index);
    public void MoveKeyframe(int fromIndex, int toIndex);
    public void Clear();
    
    // Called from Harmony patch
    public bool Update(Controller controller, Transform3D transform, double deltaTime);
}
```

### Animation Implementations

Each existing animation type becomes a class implementing `IKeyframeAnimation`:

1. **ZoomOutKeyframe** - Stores speed, duration, easing, direction calculation
2. **OrbitKeyframe** - Stores degrees, duration, easing, axis calculation
3. **LoopyOrbitKeyframe** - Stores all loopy params (degrees, loop interval, amplitude, etc.)
4. **TransitionKeyframe** - Smooth lerp between two positions (can be auto-inserted or explicit)

### TransitionKeyframe Details

The transition keyframe handles smooth interpolation between the end state of one animation and the start of the next:

```csharp
public class TransitionKeyframe : IKeyframeAnimation
{
    public double DurationSeconds { get; set; }
    public EasingType Easing { get; set; }
    
    // Set by player when transitioning
    public double3 StartPosition { get; set; }
    public doubleQuat StartRotation { get; set; }
    public double3 EndPosition { get; set; }  // Typically captured from next keyframe's init
    public doubleQuat EndRotation { get; set; }
}
```

## File Structure

```
camera-controller-override/
├── Mod.cs                          # Main mod, UI rendering
├── Patcher.cs                      # Harmony patches (simplified)
├── Animation/
│   ├── IKeyframeAnimation.cs       # Core interface
│   ├── Keyframe.cs                 # Keyframe wrapper class
│   ├── KeyframeSequencePlayer.cs   # Sequence playback controller
│   ├── Animations/
│   │   ├── ZoomOutAnimation.cs     # Zoom out implementation
│   │   ├── OrbitAnimation.cs       # Orbit implementation
│   │   ├── LoopyOrbitAnimation.cs  # Loopy orbit implementation
│   │   └── TransitionAnimation.cs  # Position/rotation lerp
│   └── AnimationHelpers.cs         # Shared easing, LookAt, etc.
└── UI/
    └── KeyframeSequencePanel.cs    # ImGui panel for sequence editor
```

## UI Design

### Keyframe Sequence Panel (New Collapsible Section)

```
┌─ Keyframe Sequence ─────────────────────────────────────────────┐
│ Status: Playing [2/5] | Elapsed: 12.5s / 45.0s                  │
│ ┌───────────────────────────────────────────────────────────┐   │
│ │ Progress: [████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░] 28%   │   │
│ └───────────────────────────────────────────────────────────┘   │
│                                                                 │
│ [▶ Play] [⏸ Pause] [⏹ Stop] [Clear All]                        │
│                                                                 │
│ Keyframes:                                                      │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ ▶ 1. Orbit Animation (5.0s)                            [X] │ │
│ │     Degrees: 270° | Easing: EaseOut                        │ │
│ │   ↕ Transition: 1.0s EaseInOut                             │ │
│ │   2. Zoom Out Animation (3.0s)                         [X] │ │
│ │     Speed: 25 m/s | Easing: EaseOut                        │ │
│ │   ↕ Transition: 1.5s EaseInOut                             │ │
│ │   3. Loopy Orbit (8.0s)                                [X] │ │
│ │     Degrees: 360° | Loops: 90° interval | Amp: 50m         │ │
│ └─────────────────────────────────────────────────────────────┘ │
│                                                                 │
│ [+ Add Transition] [↑ Move Up] [↓ Move Down]                    │
└─────────────────────────────────────────────────────────────────┘
```

### Existing Animation Panels - New Button

Each existing animation panel (Zoom Out, Orbit, Loopy Orbit) gets an additional button:

```
[Run Animation] [Add to Sequence]
```

The "Add to Sequence" button captures current configuration and appends a new keyframe.

## Implementation Tasks

---

### Task 1: Create Animation Infrastructure

**Goal**: Create the core interface, base helpers, and file structure.

**Files to create**:
- `Animation/IKeyframeAnimation.cs` - Interface definition
- `Animation/AnimationHelpers.cs` - Move shared methods (ApplyEasing, LookAtTarget, CalculateOrbitAxis, GetTargetPosition) from Patcher.cs
- `Animation/Keyframe.cs` - Keyframe wrapper class with transition settings

**Details**:
1. Define `IKeyframeAnimation` interface with all required members
2. Create `AnimationHelpers` static class with:
   - `ApplyEasing(double t, EasingType type)` 
   - `LookAtTarget(Transform3D transform, double3 targetPos)`
   - `GetTargetPosition(Controller controller, double3 fallback)`
   - `CalculateOrbitAxis(double3 startOffset, doubleQuat startRotation)`
   - `GetEasedFrameProgress(double elapsed, double duration, double deltaTime, EasingType easing)`
3. Create `Keyframe` class that wraps an `IKeyframeAnimation` with optional transition-in settings
4. Move `EasingType` enum to its own file or AnimationHelpers

**Acceptance Criteria**:
- Solution compiles
- Existing functionality unchanged

---

### Task 2: Implement ZoomOutAnimation Class

**Goal**: Extract zoom out animation logic into reusable class.

**File to create**: `Animation/Animations/ZoomOutAnimation.cs`

**Details**:
1. Implement `IKeyframeAnimation` for zoom out behavior
2. Store configuration: `SpeedMetersPerSecond`, `DurationSeconds`, `Easing`
3. Track runtime state: `_direction`, `_distanceTraveled`, `_isInitialized`
4. Implement:
   - `Initialize()` - capture direction from current position toward target
   - `Update()` - apply eased movement, return true when complete
   - `Reset()` - clear runtime state
   - `GetDisplayProperties()` - return speed, duration, easing for UI

**Acceptance Criteria**:
- Solution compiles
- Class can be instantiated with current Patcher settings

---

### Task 3: Implement OrbitAnimation Class

**Goal**: Extract orbit animation logic into reusable class.

**File to create**: `Animation/Animations/OrbitAnimation.cs`

**Details**:
1. Implement `IKeyframeAnimation` for orbit behavior
2. Store configuration: `Degrees`, `DurationSeconds`, `Easing`
3. Track runtime state: `_startPosition`, `_startRotation`, `_targetPosition`, `_orbitAxis`, `_startOffset`, `_isInitialized`
4. Implement Rodrigues' rotation formula in Update()
5. Implement all interface methods

**Acceptance Criteria**:
- Solution compiles
- Class can be instantiated with current Patcher settings

---

### Task 4: Implement LoopyOrbitAnimation Class

**Goal**: Extract loopy orbit animation logic into reusable class.

**File to create**: `Animation/Animations/LoopyOrbitAnimation.cs`

**Details**:
1. Implement `IKeyframeAnimation` for loopy orbit behavior
2. Store configuration: `Degrees`, `DurationSeconds`, `Easing`, `LoopIntervalDegrees`, `AmplitudeMeters`
3. Track runtime state: all orbit state plus vertical oscillation
4. Implement combined orbit + sinusoidal oscillation in Update()
5. Implement all interface methods

**Acceptance Criteria**:
- Solution compiles
- Class can be instantiated with current Patcher settings

---

### Task 5: Implement TransitionAnimation Class

**Goal**: Create a transition animation for smooth interpolation between keyframes.

**File to create**: `Animation/Animations/TransitionAnimation.cs`

**Details**:
1. Implement `IKeyframeAnimation` for position/rotation lerp
2. Store configuration: `DurationSeconds`, `Easing`
3. Store runtime targets: `StartPosition`, `StartRotation`, `EndPosition`, `EndRotation` (set by sequence player)
4. Optionally store look-at target positions for start/end to smoothly transition look direction
5. Implement:
   - `Initialize()` - capture start state if not already set
   - `Update()` - lerp position and rotation using easing
   - `Reset()` - clear state
   - `SetEndState(double3 position, doubleQuat rotation)` - called by player when next keyframe initializes

**Acceptance Criteria**:
- Solution compiles
- Can smoothly lerp between any two camera states

---

### Task 6: Implement KeyframeSequencePlayer

**Goal**: Create the sequence playback controller.

**File to create**: `Animation/KeyframeSequencePlayer.cs`

**Details**:
1. Implement playback state machine: `Stopped`, `Playing`, `Paused`
2. Manage `List<Keyframe>` with add/remove/move/clear operations
3. Track current keyframe index and elapsed time
4. Implement `Update()` method that:
   - Returns false if stopped/paused (allow normal camera control)
   - Initializes current keyframe if needed
   - Calls current keyframe's Update()
   - When keyframe completes, handles transition to next:
     - If next keyframe has transition enabled, insert/activate transition
     - Otherwise advance directly
   - Returns true to skip normal camera controller
5. Calculate `TotalDuration` by summing all keyframe durations + transitions
6. Provide progress information for UI

**Public API**:
```csharp
public void Play()      // Start from beginning
public void Pause()     // Freeze at current point
public void Resume()    // Continue from pause
public void Stop()      // Stop and reset to beginning
public void AddKeyframe(IKeyframeAnimation animation, bool includeTransitionIn = false, double transitionDuration = 1.0)
public void RemoveKeyframe(int index)
public void MoveKeyframe(int fromIndex, int toIndex)
public void Clear()
public bool Update(Controller controller, Transform3D transform, double deltaTime)
```

**Acceptance Criteria**:
- Solution compiles
- Can create sequence, add keyframes, play through them

---

### Task 7: Integrate KeyframeSequencePlayer with Patcher

**Goal**: Connect the sequence player to the Harmony patch system.

**Modify**: `Patcher.cs`

**Details**:
1. Add static `KeyframeSequencePlayer` instance
2. Expose it via public property for UI access
3. Modify `HandleOnFramePrefix()` to check sequence player first:
   ```csharp
   // At top of HandleOnFramePrefix:
   if (SequencePlayer.State == PlaybackState.Playing)
   {
       return !SequencePlayer.Update(controller, transform, deltaTime);
   }
   ```
4. Keep existing standalone animation logic for backward compatibility
5. Ensure sequence player and standalone animations are mutually exclusive

**Acceptance Criteria**:
- Solution compiles
- Existing animations still work
- Sequence player can take control when playing

---

### Task 8: Create KeyframeSequencePanel UI

**Goal**: Create ImGui panel for sequence management.

**File to create**: `UI/KeyframeSequencePanel.cs`

**Details**:
1. Create static class with `Render()` method
2. Implement collapsible header "Keyframe Sequence"
3. Display:
   - Playback status and progress bar
   - Control buttons: Play, Pause, Resume, Stop, Clear All
   - List of keyframes with:
     - Index and name
     - Duration
     - Key properties as secondary text
     - Remove button [X]
     - Visual indicator for current playing keyframe
   - Transition indicators between keyframes (togglable)
   - Move Up/Move Down buttons for selected keyframe
4. Add "Add Transition" button to insert explicit transition keyframes
5. Selection state for which keyframe is selected (for move operations)

**Acceptance Criteria**:
- Solution compiles
- UI renders and displays keyframe list
- All buttons functional

---

### Task 9: Add "Add to Sequence" Buttons to Existing Panels

**Goal**: Allow users to push configured animations into the sequence.

**Modify**: `Mod.cs` (RenderWindow method)

**Details**:
1. In each animation section (Zoom Out, Orbit, Loopy Orbit), add "Add to Sequence" button next to "Run Animation"
2. When clicked:
   - Create new animation instance with current configuration
   - Add to `Patcher.SequencePlayer` via `AddKeyframe()`
   - Optionally show checkbox for "Include Transition" before the button
3. Show confirmation or keyframe count update

**Acceptance Criteria**:
- Solution compiles
- Can add any animation type to sequence from its panel
- Sequence panel shows newly added keyframes

---

### Task 10: Refactor Patcher to Use Animation Classes (Optional Cleanup)

**Goal**: Reduce code duplication by having standalone animations use the same classes.

**Modify**: `Patcher.cs`

**Details**:
1. Create static instances of each animation class for standalone use
2. Modify enabled property setters to initialize/reset the animation instance
3. Modify HandleOnFramePrefix standalone animation handling to delegate to animation instances
4. Remove duplicated animation logic, keeping only the property accessors for UI binding

**Benefits**:
- Single source of truth for animation logic
- Easier to maintain
- Guaranteed consistency between standalone and sequenced animations

**Acceptance Criteria**:
- Solution compiles
- All existing standalone animations work identically
- Code is cleaner and deduplicated

---

## Implementation Order

**Phase 1: Foundation (Tasks 1-5)**
Build the core infrastructure and all animation types.

**Phase 2: Playback (Tasks 6-7)**
Implement sequence player and integrate with Patcher.

**Phase 3: UI (Tasks 8-9)**
Create the sequence panel and add integration buttons.

**Phase 4: Polish (Task 10)**
Refactor for cleaner code and maintainability.

## Future Considerations

### Not in Scope (but designed for extensibility)

1. **Save/Load Sequences** - Could serialize `List<Keyframe>` to JSON
2. **Custom Look-At Targets** - Interface supports `LookAtTargetProvider` for future use
3. **Absolute Position Keyframes** - New animation type that moves to fixed coordinates
4. **Speed Curves** - More complex easing with bezier curves
5. **Events/Callbacks** - Fire events at keyframe transitions for game integration
6. **Loop/Ping-Pong Modes** - Repeat sequence or play forward then backward

## Testing Checklist

- [ ] Each animation type works standalone
- [ ] Each animation type works in sequence
- [ ] Transitions smoothly interpolate between different animation types
- [ ] Play/Pause/Resume/Stop all work correctly
- [ ] UI updates in real-time during playback
- [ ] "Add to Sequence" captures correct configuration
- [ ] Remove keyframe works without breaking playback
- [ ] Empty sequence handles gracefully
- [ ] Single keyframe sequence works
- [ ] Solution compiles with `dotnet build`
