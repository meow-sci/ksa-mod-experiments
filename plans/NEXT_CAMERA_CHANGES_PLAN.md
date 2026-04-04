# Camera Controller Override — Next Changes Plan

## Summary

Three areas of work:

1. **Architecture assessment** for RPC-readiness (programmatic control from unladen-swallow)
2. **New animation: Pan** — translate camera position by an x/y/z offset
3. **New animation: Rotate** — rotate camera look-direction (yaw/pitch) while keeping position fixed

---

## Part 1: Architecture Assessment — RPC Readiness

### Current State

The animation system in `camera-controller-override.lib` already has **clean separation** between:

- **Data models** (`IKeyframeAnimation` implementations) — immutable configuration + runtime state
- **Playback engine** (`KeyframeSequencePlayer`) — manages sequencing, timing, state transitions
- **UI layer** (`CameraControllerOverrideSubmod` + `KeyframeSequencePanel`) — ImGui configuration fields + rendering
- **Game integration** (`CameraControllerOverridePatches`) — Harmony prefix that delegates to `KeyframeSequencePlayer.Update()`

The `KeyframeSequencePlayer` is **already suitable for programmatic control**. It has public methods:
- `AddKeyframe(IKeyframeAnimation)` — add any animation to the sequence
- `Play()` / `Pause()` / `Resume()` / `Stop()` — full playback control
- `Clear()` — remove all keyframes
- `State` / `CurrentKeyframeIndex` / `TotalElapsedTime` / `TotalDuration` — status read-back

All animation types are instantiated with constructor parameters (no UI dependency). Example:
```csharp
new OrbitAnimation(degrees: 360, durationSeconds: 5, easing: EasingType.EaseOut)
```

### Assessment: No Architecture Refactors Needed

The existing architecture is already RPC-ready. To wire it up to unladen-swallow, the integration pattern matches exactly what `glass.lib` and `blinky.lib` already do:

1. Create a **static public API class** in `camera-controller-override.lib` (like `FovController` in `glass.lib`)
2. Add a `<ProjectReference>` from `unladen-swallow.lib` to `camera-controller-override.lib`
3. Create endpoint handlers in `unladen-swallow.lib`
4. Register routes in `SwallowServer.RegisterRoutes()`

The `KeyframeSequencePlayer` already supports building a chain of multiple animations and playing them in sequence — this is its core purpose. An RPC endpoint can:
- `Clear()` the sequence
- `AddKeyframe()` for each animation in the requested chain
- Configure `ReturnToStartEnabled`, `ReturnToStartDuration`, etc.
- Call `Play()` to start

**No refactors to the animation system architecture are required.** The data models, animation interface, and playback engine are already decoupled from the UI.

### Future RPC Integration Task (Out of Scope, Documented for Reference)

When the time comes to add RPC endpoints, the work is:

1. Add `CameraAnimationController` static class to `camera-controller-override.lib` with methods like:
   - `PlaySequence(List<IKeyframeAnimation> animations, bool returnToStart, double returnDuration, EasingType returnEasing)`
   - `Stop()`
   - `GetState()` → returns `PlaybackState`, current index, elapsed time, etc.
2. Add `<ProjectReference>` to `camera-controller-override.lib` in `unladen-swallow.lib.csproj`
3. Add DTOs to `unladen-swallow.lib/ApiTypes.cs`
4. Create `CameraAnimateEndpoint.cs` in `unladen-swallow.lib/`
5. Register `/camera/animate` and `/camera/state` routes in `SwallowServer.RegisterRoutes()`

---

## Part 2: New Animation — Pan

### Overview

`PanAnimation` moves the camera from its current position to a position offset by a specified `(X, Y, Z)` vector, with easing. The camera continues to look at the target throughout the pan.

### File Location

`camera-controller-override.lib/Animation/Animations/PanAnimation.cs`

### Namespace

`MeowSci.CameraControllerOverrideLib.Animation.Animations`

### Class Signature

```csharp
public class PanAnimation : IKeyframeAnimation
```

### Constructor Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `offsetX` | `double` | X displacement in meters (rightward in camera-relative or ecliptic) | — |
| `offsetY` | `double` | Y displacement in meters (upward) | — |
| `offsetZ` | `double` | Z displacement in meters (forward, toward target) | — |
| `durationSeconds` | `double` | Total animation duration | — |
| `easing` | `EasingType` | Easing function type | — |
| `easingPowerStart` | `double` | Power for acceleration phase | `3.0` |
| `easingPowerEnd` | `double` | Power for deceleration phase | `3.0` |

### Algorithm

This animation uses **absolute offset interpolation** (not incremental) to avoid floating-point drift:

1. **`Initialize()`**: Capture the starting camera position as `_startPosition = transform.PositionEcl`. Store the target offset as `_targetOffset = new double3(OffsetX, OffsetY, OffsetZ)`.

2. **`Update()`** each frame:
   - Calculate normalized time: `t = Math.Min(1.0, elapsedTime / DurationSeconds)`
   - Apply easing: `easedT = AnimationHelpers.ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd)`
   - Snap to 1.0 on completion: if `elapsedTime >= DurationSeconds`, set `easedT = 1.0`
   - Compute interpolated offset: `currentOffset = _targetOffset * easedT`
   - Set position absolutely: `transform.PositionEcl = _startPosition + currentOffset`
   - Note: `_startPosition` is captured once, and offset is computed absolutely, NOT incrementally. This follows the same principle as `OrbitAnimation` which rotates the original `_startOffset` by the total angle rather than accumulating incremental rotations.
   - **Look-at**: Get current target position via `AnimationHelpers.GetTargetPosition(controller)`. Call `AnimationHelpers.LookAtTarget(transform, targetPos)` (or use `LookAtTargetProvider` if set).
   - Return `true` when `elapsedTime >= DurationSeconds`.

3. **`Reset()`**: Zero out `_startPosition` and `_targetOffset`.

### Runtime State Fields

```csharp
private double3 _startPosition;   // captured in Initialize()
private double3 _targetOffset;    // set from constructor params
```

### `GetDisplayProperties()` Return

```csharp
{ "Offset", $"({OffsetX:F1}, {OffsetY:F1}, {OffsetZ:F1})m" },
{ "Duration", $"{DurationSeconds:F1}s" },
{ "Easing", Easing.ToString() },
{ "Easing Power (Start)", $"{EasingPowerStart:F1}" },
{ "Easing Power (End)", $"{EasingPowerEnd:F1}" }
```

### Interface Properties

```csharp
public string Name => "Pan";
public string Description => "Linear movement by offset from starting position";
```

### Important Implementation Notes

- **Use absolute interpolation, NOT incremental**: Unlike `ZoomInToOffsetAnimation` which recalculates direction each frame from current position, `PanAnimation` should capture `_startPosition` once during `Initialize()` and compute `transform.PositionEcl = _startPosition + _targetOffset * easedT` each frame. This prevents floating-point accumulation errors over long animations.
- **The offset is in ecliptic (world) coordinates**, not camera-relative coordinates. This is consistent with how `ZoomInToOffsetAnimation` handles its offset (`new double3(OffsetX, OffsetY, OffsetZ)`).
- **Always call `AnimationHelpers.LookAtTarget()`** after setting position so the camera continues looking at the target throughout the pan.
- Follow the logging pattern: log on `Initialize()`, log on first frame (`elapsedTime < deltaTime * 1.5`), log on completion.

---

## Part 3: New Animation — Rotate

### Overview

`RotateAnimation` rotates the camera's look-direction by specified **yaw** (horizontal, left/right) and **pitch** (vertical, up/down) angles while keeping the camera in its current position. This creates the appearance of the camera "looking around" from a fixed point.

### File Location

`camera-controller-override.lib/Animation/Animations/RotateAnimation.cs`

### Namespace

`MeowSci.CameraControllerOverrideLib.Animation.Animations`

### Class Signature

```csharp
public class RotateAnimation : IKeyframeAnimation
```

### Constructor Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `yawDegrees` | `double` | Horizontal rotation angle (positive = look right, negative = look left) | — |
| `pitchDegrees` | `double` | Vertical rotation angle (positive = look up, negative = look down) | — |
| `durationSeconds` | `double` | Total animation duration | — |
| `easing` | `EasingType` | Easing function type | — |
| `easingPowerStart` | `double` | Power for acceleration phase | `3.0` |
| `easingPowerEnd` | `double` | Power for deceleration phase | `3.0` |

### Design Rationale: Yaw + Pitch

Using yaw/pitch as the interface (rather than target position, quaternion, or axis-angle) because:
- **Intuitive**: "look left 45 degrees" = `yawDegrees: -45, pitchDegrees: 0`
- **Trivial for callers**: No need to understand the coordinate system, just specify left/right/up/down
- **Composable**: Chain a yaw rotate then a pitch rotate for complex camera movements
- **RPC-friendly**: Easy to specify as simple numeric parameters in a JSON request

Conventions:
- **Yaw positive = look right** (clockwise when viewed from above)
- **Yaw negative = look left** (counterclockwise when viewed from above)
- **Pitch positive = look up**
- **Pitch negative = look down**

### Algorithm

Uses **absolute rotation from start** to avoid cumulative floating-point error (same principle as `OrbitAnimation`):

1. **`Initialize()`**:
   - Capture starting rotation: `_startRotation = transform.LocalRotation`
   - Capture starting position: `_startPosition = transform.PositionEcl`
   - Derive the camera's local axes from `_startRotation`:
     - `_upAxis = double3.UnitY.Transform(_startRotation)` — camera's up vector
     - `_rightAxis = double3.UnitX.Transform(_startRotation)` — camera's right vector
   - Set `_isInitialized = true`

2. **`Update()`** each frame:
   - If not initialized, return `true` (skip, like `OrbitAnimation` does).
   - Calculate normalized time: `t = Math.Min(1.0, elapsedTime / DurationSeconds)`
   - Apply easing: `easedT = AnimationHelpers.ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd)`
   - Snap to 1.0 on completion.
   - Calculate total rotation angles for this point in time:
     - `currentYawRad = YawDegrees * easedT * Math.PI / 180.0`
     - `currentPitchRad = PitchDegrees * easedT * Math.PI / 180.0`
   - Build rotation quaternions from the **original** axes (not current frame axes):
     - `yawQuat = doubleQuat.CreateFromAxisAngle(_upAxis, currentYawRad)`
     - `pitchQuat = doubleQuat.CreateFromAxisAngle(_rightAxis, currentPitchRad)`
   - Compose: `totalRotation = yawQuat * pitchQuat` (apply pitch first, then yaw)
   - Apply to starting rotation: `transform.LocalRotation = totalRotation * _startRotation`
   - **Keep position fixed**: `transform.PositionEcl = _startPosition` (enforce no position drift since other game systems might try to move the camera)
   - Return `true` when `elapsedTime >= DurationSeconds`.

3. **`Reset()`**: Set `_startRotation = doubleQuat.Identity`, `_startPosition = double3.Zero`, zero out axis vectors, `_isInitialized = false`.

### Runtime State Fields

```csharp
private doubleQuat _startRotation;   // captured in Initialize()
private double3 _startPosition;      // captured in Initialize()
private double3 _upAxis;             // camera up at start
private double3 _rightAxis;          // camera right at start
private bool _isInitialized;
```

### `GetDisplayProperties()` Return

```csharp
{ "Yaw", $"{YawDegrees:F1}°" },
{ "Pitch", $"{PitchDegrees:F1}°" },
{ "Duration", $"{DurationSeconds:F1}s" },
{ "Easing", Easing.ToString() },
{ "Easing Power (Start)", $"{EasingPowerStart:F1}" },
{ "Easing Power (End)", $"{EasingPowerEnd:F1}" }
```

### Interface Properties

```csharp
public string Name => "Rotate";
public string Description => "Rotate camera look-direction (yaw/pitch) from fixed position";
```

### Usage Examples for Callers

| Desired Effect | Parameters |
|---|---|
| Look left 45° | `yawDegrees: -45, pitchDegrees: 0` |
| Look right 90° | `yawDegrees: 90, pitchDegrees: 0` |
| Look up 30° | `yawDegrees: 0, pitchDegrees: 30` |
| Look down 20° | `yawDegrees: 0, pitchDegrees: -20` |
| Look left and up | `yawDegrees: -45, pitchDegrees: 30` |
| Slow pan left 180° | `yawDegrees: -180, pitchDegrees: 0, durationSeconds: 10, easing: EaseInOut` |

### Important Implementation Notes

- **Position must be pinned**: Set `transform.PositionEcl = _startPosition` every frame. Even though we're only rotating, the game engine might try to apply drift. This ensures the camera stays put.
- **Axes are captured from _startRotation at Initialize() time** and reused. Do NOT re-derive axes from `transform.LocalRotation` each frame — that would cause cumulative error.
- **Rotation is applied absolutely from `_startRotation`**: The formula is `transform.LocalRotation = totalRotation * _startRotation` where `totalRotation` is the composed yaw+pitch for the TOTAL eased angle at this point in time. This matches the `OrbitAnimation` pattern of absolute rotation from original offset.
- **DO NOT call `AnimationHelpers.LookAtTarget()`**: This animation explicitly controls rotation and must not have its rotation overridden by the look-at helper. The `LookAtTargetProvider` should be ignored (or simply not invoked) for this animation type.
- Follow logging pattern: log on `Initialize()`, first frame, and completion.

---

## Part 4: UI Integration for New Animations

### Task 4A: Add Pan UI to `CameraControllerOverrideSubmod`

#### File

`camera-controller-override.lib/CameraControllerOverrideSubmod.cs`

#### Changes

1. **Add configuration fields** at the class level (alongside the existing animation config fields):

```csharp
// Pan configuration
private float _panOffsetX = 0.0f;      // meters
private float _panOffsetY = 0.0f;      // meters
private float _panOffsetZ = 0.0f;      // meters
private float _panDuration = 5.0f;     // seconds
private int _panEasing = (int)EasingType.EaseInOut;
private float _panEasingPowerStart = 3.0f;
private float _panEasingPowerEnd = 3.0f;
```

2. **Add `using` for `PanAnimation`** (already covered by existing `using MeowSci.CameraControllerOverrideLib.Animation.Animations;`).

3. **Add a collapsing header section in `RenderContent()`** under a new separator text `"Movement Animations"` placed BEFORE the existing `"Zoom Animations"` separator, or alternatively append under the existing "Zoom Animations" section. The recommended placement is just before the `"Keyframe Sequence"` separator so all animation types are grouped above it. Place it under a new `ImGui.SeparatorText("Movement Animations")` section:

```csharp
ImGui.SeparatorText("Movement Animations");

if (ImGui.CollapsingHeader("Pan"))
{
    if (RenderPanParamsTable("pan", ref _panOffsetX, ref _panOffsetY, ref _panOffsetZ,
        ref _panDuration, ref _panEasing, ref _panEasingPowerStart, ref _panEasingPowerEnd))
        _sequencePlayer.AddKeyframe(new PanAnimation(
            offsetX: _panOffsetX,
            offsetY: _panOffsetY,
            offsetZ: _panOffsetZ,
            durationSeconds: _panDuration,
            easing: (EasingType)_panEasing,
            easingPowerStart: _panEasingPowerStart,
            easingPowerEnd: _panEasingPowerEnd));
}
```

4. **Add `RenderPanParamsTable` method** — follows the same pattern as `RenderZoomInToOffsetSection` but with only offset and easing parameters (no speed). Use the existing 2-column table layout:

```csharp
private bool RenderPanParamsTable(string id, ref float offsetX, ref float offsetY, ref float offsetZ,
    ref float duration, ref int easing, ref float powerStart, ref float powerEnd)
{
    var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
    if (ImGui.BeginTable($"##cco_pan_{id}", 2, tableFlags))
    {
        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

        // Offset X
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Offset X (m)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##ox_{id}", ref offsetX, 0.5f, -500f, 500f);

        // Offset Y
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Offset Y (m)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##oy_{id}", ref offsetY, 0.5f, -500f, 500f);

        // Offset Z
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Offset Z (m)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##oz_{id}", ref offsetZ, 0.5f, -500f, 500f);

        // Duration
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Duration (s)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##dur_{id}", ref duration, 0.1f, 1f, 30f);

        // Easing (reuse existing EasingNames array)
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Easing");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.Combo($"##eas_{id}", ref easing, EasingNames, EasingNames.Length);

        // Conditional easing power sliders (same pattern as all other animations)
        var easingType = (EasingType)easing;
        if (easingType == EasingType.EaseIn || easingType == EasingType.EaseInOut)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Power (Start)");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat($"##ps_{id}", ref powerStart, 0.1f, 1f, 6f);
        }
        if (easingType == EasingType.EaseOut || easingType == EasingType.EaseInOut)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Power (End)");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat($"##pe_{id}", ref powerEnd, 0.1f, 1f, 6f);
        }

        ImGui.EndTable();
    }
    ImGui.PopStyleVar();

    ImGui.Spacing();
    return ImGui.Button($" + Add to Sequence ##{id}");
}
```

### Task 4B: Add Rotate UI to `CameraControllerOverrideSubmod`

#### File

`camera-controller-override.lib/CameraControllerOverrideSubmod.cs`

#### Changes

1. **Add configuration fields**:

```csharp
// Rotate configuration
private float _rotateYaw = 0.0f;          // degrees (+ = right, - = left)
private float _rotatePitch = 0.0f;        // degrees (+ = up, - = down)
private float _rotateDuration = 3.0f;     // seconds
private int _rotateEasing = (int)EasingType.EaseInOut;
private float _rotateEasingPowerStart = 3.0f;
private float _rotateEasingPowerEnd = 3.0f;
```

2. **Add collapsing header in `RenderContent()`** under the same `"Movement Animations"` separator as Pan:

```csharp
if (ImGui.CollapsingHeader("Rotate"))
{
    if (RenderRotateParamsTable("rotate", ref _rotateYaw, ref _rotatePitch,
        ref _rotateDuration, ref _rotateEasing, ref _rotateEasingPowerStart, ref _rotateEasingPowerEnd))
        _sequencePlayer.AddKeyframe(new RotateAnimation(
            yawDegrees: _rotateYaw,
            pitchDegrees: _rotatePitch,
            durationSeconds: _rotateDuration,
            easing: (EasingType)_rotateEasing,
            easingPowerStart: _rotateEasingPowerStart,
            easingPowerEnd: _rotateEasingPowerEnd));
}
```

3. **Add `RenderRotateParamsTable` method**:

```csharp
private bool RenderRotateParamsTable(string id, ref float yaw, ref float pitch,
    ref float duration, ref int easing, ref float powerStart, ref float powerEnd)
{
    var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
    if (ImGui.BeginTable($"##cco_rotate_{id}", 2, tableFlags))
    {
        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

        // Yaw
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Yaw (°)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##yaw_{id}", ref yaw, 1f, -360f, 360f);

        // Pitch
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Pitch (°)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##pitch_{id}", ref pitch, 1f, -90f, 90f);

        // Duration
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Duration (s)");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.DragFloat($"##dur_{id}", ref duration, 0.1f, 1f, 30f);

        // Easing
        ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding(); ImGui.Text("Easing");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        ImGui.Combo($"##eas_{id}", ref easing, EasingNames, EasingNames.Length);

        var easingType = (EasingType)easing;
        if (easingType == EasingType.EaseIn || easingType == EasingType.EaseInOut)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Power (Start)");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat($"##ps_{id}", ref powerStart, 0.1f, 1f, 6f);
        }
        if (easingType == EasingType.EaseOut || easingType == EasingType.EaseInOut)
        {
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Power (End)");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat($"##pe_{id}", ref powerEnd, 0.1f, 1f, 6f);
        }

        ImGui.EndTable();
    }
    ImGui.PopStyleVar();

    ImGui.Spacing();
    return ImGui.Button($" + Add to Sequence ##{id}");
}
```

---

## Part 5: README Update

### File

`camera-controller-override/README.md`

### Changes

Add Pan and Rotate to the list of animation types in the README. Include parameter descriptions matching the existing documentation format for other animations.

---

## Task Checklist (Ordered)

Each task below is independently implementable and should compile successfully after completion.

### Task 1: Create `PanAnimation.cs`

- **File**: `camera-controller-override.lib/Animation/Animations/PanAnimation.cs`
- **Action**: Create new file implementing `IKeyframeAnimation`
- **Details**: See [Part 2](#part-2-new-animation--pan) above for full specification
- **Template reference**: Follow the structure of `OrbitAnimation.cs` for the class skeleton (constructor, Initialize, Update, Reset, GetDisplayProperties). Use the absolute-interpolation approach (compute position from `_startPosition + offset * easedT` each frame, NOT incremental).
- **Verification**: `dotnet build` must pass

### Task 2: Create `RotateAnimation.cs`

- **File**: `camera-controller-override.lib/Animation/Animations/RotateAnimation.cs`
- **Action**: Create new file implementing `IKeyframeAnimation`
- **Details**: See [Part 3](#part-3-new-animation--rotate) above for full specification
- **Template reference**: Follow `ShakeAnimation.cs` for the rotation pattern, but use absolute rotation from `_startRotation` instead of incremental. Key difference from Shake: this animation does NOT call `AnimationHelpers.LookAtTarget()` — it explicitly controls rotation.
- **Verification**: `dotnet build` must pass

### Task 3: Add Pan UI section to `CameraControllerOverrideSubmod.cs`

- **File**: `camera-controller-override.lib/CameraControllerOverrideSubmod.cs`
- **Action**: Add configuration fields, `RenderPanParamsTable` method, and collapsing header section
- **Details**: See [Task 4A](#task-4a-add-pan-ui-to-cameracontrolleroverridesubmod) above
- **Placement**: Add a new `ImGui.SeparatorText("Movement Animations")` section AFTER the existing `"Effects"` section and BEFORE the existing `"Keyframe Sequence"` section. Place Pan and Rotate headers inside it.
- **Verification**: `dotnet build` must pass

### Task 4: Add Rotate UI section to `CameraControllerOverrideSubmod.cs`

- **File**: `camera-controller-override.lib/CameraControllerOverrideSubmod.cs`
- **Action**: Add configuration fields, `RenderRotateParamsTable` method, and collapsing header inside the `"Movement Animations"` section added in Task 3
- **Details**: See [Task 4B](#task-4b-add-rotate-ui-to-cameracontrolleroverridesubmod) above
- **Verification**: `dotnet build` must pass

### Task 5: Update README

- **File**: `camera-controller-override/README.md`
- **Action**: Add Pan and Rotate animation documentation to the existing animation list
- **Verification**: Review documentation for accuracy and completeness

### Task 6: Verify full build

- **Action**: Run `dotnet build` from the solution root to verify everything compiles cleanly
- **Verification**: Zero errors, zero warnings (or only pre-existing warnings)
