# RPC Camera Animation Endpoint — Implementation Plan

## Overview

Expose the `camera-controller-override` animation system over the `unladen-swallow.lib` RPC server. Clients will be able to POST a sequence of animations (including groups), optionally with a return-to-start configuration, and the server will execute them on the game's camera. GET returns current playback status, DELETE stops a running animation.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/camera/animate` | Start a camera animation sequence |
| `GET` | `/camera/status` | Get current playback state |
| `DELETE` | `/camera/stop` | Stop any running animation |

Base URL: `http://localhost:7887`

---

## Task List

### Task 1: Add static accessor to `CameraControllerOverrideSubmod`

**File:** `camera-controller-override.lib/CameraControllerOverrideSubmod.cs`

Add a static `Instance` property so the RPC server can access the submod's `SequencePlayer` without needing a direct reference to the submod instance.

```csharp
/// <summary>
/// Static accessor for RPC integration. Set when the submod initializes, cleared on dispose.
/// </summary>
public static CameraControllerOverrideSubmod? Instance { get; private set; }
```

- In the constructor or `Initialize()` method, set `Instance = this;`
- In the `Dispose()` method (or wherever cleanup happens), set `Instance = null;`
- This follows the pattern the user chose (static accessor on submod rather than a separate API class)

---

### Task 2: Add `ProjectReference` from `unladen-swallow.lib` to `camera-controller-override.lib`

**File:** `unladen-swallow.lib/unladen-swallow.lib.csproj`

Add this inside the existing `<ItemGroup>` that has other `<ProjectReference>` entries:

```xml
<ProjectReference Include="..\camera-controller-override.lib\camera-controller-override.lib.csproj" />
```

This gives `unladen-swallow.lib` compile-time access to:
- `CameraControllerOverrideSubmod` (and its `Instance` property)
- `KeyframeSequencePlayer`
- All `IKeyframeAnimation` implementations
- `AnimationGroup`
- `EasingType`, `PlaybackState`
- `AnimationHelpers`

No `mod.toml` `ImportedAssemblies` changes needed (build-time ref only, matching the blinky pattern).

---

### Task 3: Define RPC data models in `ApiTypes.cs`

**File:** `unladen-swallow.lib/ApiTypes.cs`

Add the following record types at the end of the file, under a new section comment:

```csharp
// ── Camera Animation API Types ──────────────────────────────────────────────

/// <summary>
/// Easing type for camera animations. Maps to CameraControllerOverrideLib.Animation.EasingType.
/// </summary>
public enum CameraEasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

/// <summary>
/// Base properties shared by all animation step types.
/// Every animation step has duration, easing, and easing power settings.
/// </summary>
public record CameraAnimationBase(
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A zoom-out animation step. Moves camera away from target.</summary>
public record CameraZoomOut(
    double SpeedMetersPerSecond,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A zoom-in animation step. Moves camera toward target.</summary>
public record CameraZoomIn(
    double SpeedMetersPerSecond,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A zoom-in-to-offset animation step. Zooms toward a point offset from target.</summary>
public record CameraZoomInToOffset(
    double SpeedMetersPerSecond,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A circular orbit animation step around the target.</summary>
public record CameraOrbit(
    double Degrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>An orbit with sinusoidal in/out oscillation.</summary>
public record CameraLoopyOrbit(
    double Degrees,
    double LoopIntervalDegrees,
    double AmplitudeMeters,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>Zoom in while spiraling around the look axis.</summary>
public record CameraSpiralZoomIn(
    double SpeedMetersPerSecond,
    double SpiralDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>Zoom out while spiraling around the look axis.</summary>
public record CameraSpiralZoomOut(
    double SpeedMetersPerSecond,
    double SpiralDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A head-shaking yaw rotation effect.</summary>
public record CameraShake(
    int ShakeCount,
    double AmplitudeDegrees,
    double ShakeSpeed,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A camera-local offset movement (left/right, up/down, forward/back).</summary>
public record CameraPan(
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>A yaw/pitch rotation from a fixed camera position.</summary>
public record CameraRotate(
    double YawDegrees,
    double PitchDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
) : CameraAnimationBase(DurationSeconds, Easing, EasingPowerStart, EasingPowerEnd);

/// <summary>
/// A sequence step: either a single animation or a group of simultaneous animations.
/// Exactly one of the fields should be non-null.
/// If "group" is set, it's a group step; otherwise exactly one animation type field is set.
/// </summary>
public record CameraSequenceStep(
    CameraZoomOut? ZoomOut = null,
    CameraZoomIn? ZoomIn = null,
    CameraZoomInToOffset? ZoomInToOffset = null,
    CameraOrbit? Orbit = null,
    CameraLoopyOrbit? LoopyOrbit = null,
    CameraSpiralZoomIn? SpiralZoomIn = null,
    CameraSpiralZoomOut? SpiralZoomOut = null,
    CameraShake? Shake = null,
    CameraPan? Pan = null,
    CameraRotate? Rotate = null,
    CameraSequenceStep[]? Group = null
);

/// <summary>
/// Optional return-to-start configuration. If provided in the request,
/// the camera will animate back to its starting position after the sequence completes.
/// </summary>
public record CameraReturnToStart(
    double DurationSeconds = 3.0,
    CameraEasingType Easing = CameraEasingType.EaseInOut,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>
/// Request body for POST /camera/animate.
/// Contains a sequence of animation steps and an optional return-to-start configuration.
/// </summary>
public record CameraAnimateRequest(
    CameraSequenceStep[] Sequence,
    CameraReturnToStart? ReturnToStart = null
);

/// <summary>
/// Result returned by POST /camera/animate on success.
/// </summary>
public record CameraAnimateResult(
    int KeyframeCount,
    double TotalDurationSeconds,
    bool ReturnToStartEnabled
);

/// <summary>
/// Current camera animation playback status returned by GET /camera/status.
/// </summary>
public record CameraPlaybackStatus(
    string State,
    bool IsReturningToStart,
    int CurrentKeyframeIndex,
    int TotalKeyframes,
    double TotalElapsedTime,
    double TotalDurationSeconds
);

/// <summary>
/// Result returned by DELETE /camera/stop.
/// </summary>
public record CameraStopResult(string PreviousState);
```

### Key design decisions for data models:

1. **`CameraSequenceStep` uses nullable fields as a discriminated union** — exactly one animation type field is set per step. When `Group` is set, it's a group containing sub-steps that play simultaneously. This mirrors the ImGui UI's two modes: single animation or group.

2. **`CameraEasingType` is a separate enum** — mirrors `EasingType` from the animation lib but lives in the RPC layer to avoid tight coupling. The endpoint code maps between them.

3. **`CameraReturnToStart` is optional** — when `null`/omitted in the JSON, no return-to-start animation plays. When present, it configures the return animation. All fields have sensible defaults.

4. **Groups are recursive** — `CameraSequenceStep.Group` is an array of `CameraSequenceStep`, but implementer should validate that group entries cannot themselves be groups (only single animations allowed inside groups, matching the lib's `AnimationGroup` behavior).

---

### Task 4: Create `CameraAnimateEndpoint.cs` (POST /camera/animate)

**File:** `unladen-swallow.lib/CameraAnimateEndpoint.cs`

This is the main endpoint. It must:

1. Accept a `CameraAnimateRequest` body
2. Validate the request (non-empty sequence, exactly one animation type per step, etc.)
3. Schedule work on the game thread via `GameThread.Scheduler.Schedule()`
4. On the game thread:
   a. Get `CameraControllerOverrideSubmod.Instance` — throw 503 if null (mod not loaded)
   b. Get `Instance.SequencePlayer`
   c. If currently playing, stop first (`SequencePlayer.Stop()`) then clear (`SequencePlayer.Clear()`)
   d. Convert each `CameraSequenceStep` to an `IKeyframeAnimation` instance:
      - For single animations: instantiate the matching animation class
      - For group steps: create `AnimationGroup`, add converted sub-animations
   e. Call `SequencePlayer.AddKeyframe(animation)` for each converted step
   f. Configure return-to-start:
      - If `ReturnToStart` is not null: enable and set duration/easing/power
      - If `ReturnToStart` is null: disable return-to-start
   g. Call `SequencePlayer.Play()`
   h. Return `CameraAnimateResult` with keyframe count, total duration, and return-to-start flag
5. Wrap in standard error handling (ProviderException passthrough + catch-all)

**Animation conversion function** — a private helper method that maps `CameraSequenceStep` → `IKeyframeAnimation`:

```csharp
private static IKeyframeAnimation ConvertStep(CameraSequenceStep step)
{
    // Count how many non-null animation fields are set
    // Exactly one must be set (or Group must be set)
    
    if (step.Group != null)
    {
        var group = new AnimationGroup();
        foreach (var subStep in step.Group)
        {
            if (subStep.Group != null)
                throw new ProviderException(ResponseStatus.BadRequest,
                    "Nested groups are not allowed. Groups may only contain single animations.");
            group.Add(ConvertStep(subStep));
        }
        if (group.Count == 0)
            throw new ProviderException(ResponseStatus.BadRequest, "Group must contain at least one animation.");
        return group;
    }
    
    // Map easing type: (CameraEasingType) → (EasingType)
    // Cast works because enum values match
    
    if (step.ZoomOut != null) return new ZoomOutAnimation(
        step.ZoomOut.SpeedMetersPerSecond, step.ZoomOut.DurationSeconds,
        (EasingType)step.ZoomOut.Easing, step.ZoomOut.EasingPowerStart, step.ZoomOut.EasingPowerEnd);
    if (step.ZoomIn != null) return new ZoomInAnimation(
        step.ZoomIn.SpeedMetersPerSecond, step.ZoomIn.DurationSeconds,
        (EasingType)step.ZoomIn.Easing, step.ZoomIn.EasingPowerStart, step.ZoomIn.EasingPowerEnd);
    // ... etc for all 10 types ...
    
    throw new ProviderException(ResponseStatus.BadRequest,
        "Each sequence step must have exactly one animation type set.");
}
```

**Endpoint structure** follows the established pattern:

```csharp
public static class CameraAnimateEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (CameraAnimateRequest body) =>
            {
                // Validate
                if (body.Sequence == null || body.Sequence.Length == 0)
                    throw new ProviderException(ResponseStatus.BadRequest, "Sequence must not be empty.");
                
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");
                        
                        var player = submod.SequencePlayer;
                        
                        // Stop and clear any running animation
                        if (player.State != PlaybackState.Stopped)
                            player.Stop();
                        player.Clear();
                        
                        // Convert and add each step
                        foreach (var step in body.Sequence)
                            player.AddKeyframe(ConvertStep(step));
                        
                        // Configure return-to-start
                        if (body.ReturnToStart != null)
                        {
                            player.ReturnToStartEnabled = true;
                            player.ReturnToStartDuration = body.ReturnToStart.DurationSeconds;
                            player.ReturnToStartEasing = (EasingType)body.ReturnToStart.Easing;
                            player.ReturnToStartEasingPowerStart = body.ReturnToStart.EasingPowerStart;
                            player.ReturnToStartEasingPowerEnd = body.ReturnToStart.EasingPowerEnd;
                        }
                        else
                        {
                            player.ReturnToStartEnabled = false;
                        }
                        
                        // Start playback
                        player.Play();
                        
                        return new CameraAnimateResult(
                            player.Keyframes.Count,
                            player.TotalDuration,
                            player.ReturnToStartEnabled);
                    });
                    return (object)new ApiResponse<CameraAnimateResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error starting camera animation.", ex);
                }
            })
            .Build();
    }
    
    private static IKeyframeAnimation ConvertStep(CameraSequenceStep step) { /* ... */ }
}
```

**Required using statements:**
```csharp
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.CameraControllerOverrideLib.Animation.Animations;
using MeowSci.KsaAbstractions;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
```

---

### Task 5: Create `CameraStatusEndpoint.cs` (GET /camera/status)

**File:** `unladen-swallow.lib/CameraStatusEndpoint.cs`

Simple GET endpoint that reads the current playback status from the SequencePlayer.

```csharp
public static class CameraStatusEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                try
                {
                    var status = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");
                        
                        var player = submod.SequencePlayer;
                        return new CameraPlaybackStatus(
                            State: player.State.ToString(),
                            IsReturningToStart: player.IsReturningToStart,
                            CurrentKeyframeIndex: player.CurrentKeyframeIndex,
                            TotalKeyframes: player.Keyframes.Count,
                            TotalElapsedTime: player.TotalElapsedTime,
                            TotalDurationSeconds: player.TotalDuration);
                    });
                    return (object)new ApiResponse<CameraPlaybackStatus>("ok", status);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error reading camera status.", ex);
                }
            })
            .Build();
    }
}
```

---

### Task 6: Create `CameraStopEndpoint.cs` (DELETE /camera/stop)

**File:** `unladen-swallow.lib/CameraStopEndpoint.cs`

Stops any running animation and returns the previous state.

```csharp
public static class CameraStopEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Delete(async () =>
            {
                try
                {
                    var result = await GameThread.Scheduler.Schedule(() =>
                    {
                        var submod = CameraControllerOverrideSubmod.Instance;
                        if (submod == null)
                            throw new ProviderException(ResponseStatus.ServiceUnavailable,
                                "Camera controller override mod is not loaded.");
                        
                        var player = submod.SequencePlayer;
                        var previousState = player.State.ToString();
                        
                        if (player.State != PlaybackState.Stopped)
                            player.Stop();
                        
                        return new CameraStopResult(previousState);
                    });
                    return (object)new ApiResponse<CameraStopResult>("ok", result);
                }
                catch (ProviderException) { throw; }
                catch (Exception ex)
                {
                    throw new ProviderException(ResponseStatus.InternalServerError,
                        "Unexpected error stopping camera animation.", ex);
                }
            })
            .Build();
    }
}
```

---

### Task 7: Register routes in `SwallowServer.RegisterRoutes()`

**File:** `unladen-swallow.lib/SwallowServer.cs`

Add the camera routes inside `RegisterRoutes()`, after the existing blinky routes:

```csharp
// POST /camera/animate
// GET  /camera/status
// DELETE /camera/stop
api.Add("camera", Layout.Create()
    .Add("animate", CameraAnimateEndpoint.Create())
    .Add("status", CameraStatusEndpoint.Create())
    .Add("stop", CameraStopEndpoint.Create()));
```

---

### Task 8: Create OpenAPI spec `camera.yml`

**File:** `unladen-swallow.lib/openapi/camera.yml`

Create the OpenAPI 3.1.0 spec following the same style as `fov.yml`. The spec must document:

- `POST /camera/animate` — with full `CameraAnimateRequest` schema
- `GET /camera/status` — with `CameraPlaybackStatus` response
- `DELETE /camera/stop` — with `CameraStopResult` response

Key schema points:

1. The `CameraSequenceStep` schema uses `oneOf` semantics — exactly one animation property or `group` must be set.
2. Each animation type schema inherits common base properties (duration, easing, easingPowerStart, easingPowerEnd).
3. The `group` field is an array of `CameraSequenceStep` (but with a note that nested groups are not allowed).
4. `CameraReturnToStart` is optional with defaults documented.
5. `CameraEasingType` is an enum: `linear`, `easeIn`, `easeOut`, `easeInOut` (values 0-3).

Full spec contents:

```yaml
openapi: 3.1.0

info:
  title: Unladen Swallow — Camera Animation API
  version: 1.0.0
  description: >
    Camera animation control endpoints for the Unladen Swallow RPC server.
    Allows running sequenced camera animations with optional grouped (simultaneous)
    steps and return-to-start behavior.
    Listens on http://localhost:7887.

servers:
  - url: http://localhost:7887
    description: Local KSA mod server

paths:

  /camera/animate:
    post:
      summary: Start a camera animation sequence
      description: >
        Submits a sequence of camera animation steps for playback.
        Each step is either a single animation or a group of animations
        that play simultaneously. If an animation is already running, it
        is stopped and replaced. Optionally configure a return-to-start
        animation that plays after the sequence completes.
      operationId: startCameraAnimation
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CameraAnimateRequest'
            examples:
              simpleOrbit:
                summary: Simple orbit
                value:
                  sequence:
                    - orbit:
                        degrees: 360
                        durationSeconds: 10.0
                        easing: easeInOut
                  returnToStart:
                    durationSeconds: 3.0
                    easing: easeInOut
              zoomAndOrbit:
                summary: Zoom out then orbit
                value:
                  sequence:
                    - zoomOut:
                        speedMetersPerSecond: 5.0
                        durationSeconds: 3.0
                        easing: easeOut
                    - orbit:
                        degrees: 180
                        durationSeconds: 8.0
                        easing: easeInOut
              groupExample:
                summary: Simultaneous orbit + zoom out
                value:
                  sequence:
                    - group:
                        - orbit:
                            degrees: 360
                            durationSeconds: 10.0
                            easing: linear
                        - zoomOut:
                            speedMetersPerSecond: 2.0
                            durationSeconds: 10.0
                            easing: easeIn
      responses:
        '200':
          description: Animation started successfully.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CameraAnimateResponse'
        '400':
          $ref: '#/components/responses/BadRequest'
        '503':
          $ref: '#/components/responses/ModNotLoaded'
        '500':
          $ref: '#/components/responses/InternalError'

  /camera/status:
    get:
      summary: Get current camera animation status
      description: >
        Returns the current playback state of the camera animation system,
        including whether an animation is playing, the current keyframe index,
        and elapsed time.
      operationId: getCameraStatus
      responses:
        '200':
          description: Current playback status.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CameraStatusResponse'
        '503':
          $ref: '#/components/responses/ModNotLoaded'
        '500':
          $ref: '#/components/responses/InternalError'

  /camera/stop:
    delete:
      summary: Stop running camera animation
      description: >
        Stops any running camera animation immediately and returns the
        previous playback state. If no animation is running, returns
        "Stopped" as the previous state.
      operationId: stopCameraAnimation
      responses:
        '200':
          description: Animation stopped.
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/CameraStopResponse'
        '503':
          $ref: '#/components/responses/ModNotLoaded'
        '500':
          $ref: '#/components/responses/InternalError'

components:

  schemas:

    # ── Easing ──────────────────────────────────────────────────────────────

    CameraEasingType:
      type: string
      enum: [linear, easeIn, easeOut, easeInOut]
      description: >
        Easing function for animation interpolation.
        - linear: constant speed
        - easeIn: slow start, fast end
        - easeOut: fast start, slow end
        - easeInOut: smooth acceleration and deceleration

    # ── Animation Common Base ───────────────────────────────────────────────

    AnimationBase:
      type: object
      description: Common properties shared by all animation types.
      properties:
        durationSeconds:
          type: number
          format: double
          description: Total duration of the animation in seconds.
          example: 5.0
        easing:
          $ref: '#/components/schemas/CameraEasingType'
          default: linear
        easingPowerStart:
          type: number
          format: double
          description: >
            Power parameter for acceleration phase (1.0=linear, 2.0=quadratic,
            3.0=cubic). Higher = more extreme curve.
          default: 3.0
          example: 3.0
        easingPowerEnd:
          type: number
          format: double
          description: Power parameter for deceleration phase.
          default: 3.0
          example: 3.0
      required:
        - durationSeconds

    # ── Individual Animation Types ─────────────────────────────────────────

    CameraZoomOut:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Move camera away from the target at a given speed.
          properties:
            speedMetersPerSecond:
              type: number
              format: double
              description: Movement speed away from target (m/s).
              example: 5.0
          required:
            - speedMetersPerSecond

    CameraZoomIn:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Move camera toward the target at a given speed (min 1m distance).
          properties:
            speedMetersPerSecond:
              type: number
              format: double
              description: Movement speed toward target (m/s).
              example: 5.0
          required:
            - speedMetersPerSecond

    CameraZoomInToOffset:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Zoom toward a point offset from the target.
          properties:
            speedMetersPerSecond:
              type: number
              format: double
              example: 5.0
            offsetX:
              type: number
              format: double
              description: X offset from target (meters).
              example: 10.0
            offsetY:
              type: number
              format: double
              description: Y offset from target (meters).
              example: 0.0
            offsetZ:
              type: number
              format: double
              description: Z offset from target (meters).
              example: 5.0
          required:
            - speedMetersPerSecond
            - offsetX
            - offsetY
            - offsetZ

    CameraOrbit:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Circular orbit around the camera target.
          properties:
            degrees:
              type: number
              format: double
              description: Total orbit angle in degrees (positive=counterclockwise).
              example: 360.0
          required:
            - degrees

    CameraLoopyOrbit:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Orbit with sinusoidal in/out oscillation.
          properties:
            degrees:
              type: number
              format: double
              description: Total orbit angle in degrees.
              example: 360.0
            loopIntervalDegrees:
              type: number
              format: double
              description: Degrees between oscillation peaks.
              example: 90.0
            amplitudeMeters:
              type: number
              format: double
              description: Oscillation amplitude in meters.
              example: 50.0
          required:
            - degrees
            - loopIntervalDegrees
            - amplitudeMeters

    CameraSpiralZoomIn:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Zoom in while spiraling around the look axis.
          properties:
            speedMetersPerSecond:
              type: number
              format: double
              example: 5.0
            spiralDegrees:
              type: number
              format: double
              description: Total spiral rotation in degrees.
              example: 720.0
          required:
            - speedMetersPerSecond
            - spiralDegrees

    CameraSpiralZoomOut:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Zoom out while spiraling around the look axis.
          properties:
            speedMetersPerSecond:
              type: number
              format: double
              example: 5.0
            spiralDegrees:
              type: number
              format: double
              description: Total spiral rotation in degrees.
              example: 720.0
          required:
            - speedMetersPerSecond
            - spiralDegrees

    CameraShake:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: >
            Head-shaking yaw rotation effect. Uses sinusoidal oscillation
            modulated by easing.
          properties:
            shakeCount:
              type: integer
              description: Number of full shake oscillations.
              example: 5
            amplitudeDegrees:
              type: number
              format: double
              description: Maximum yaw deflection in degrees.
              example: 10.0
            shakeSpeed:
              type: number
              format: double
              description: Speed multiplier for shake oscillation.
              example: 1.0
          required:
            - shakeCount
            - amplitudeDegrees
            - shakeSpeed

    CameraPan:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: >
            Camera-local offset movement. X=right, Y=up, Z=forward
            relative to current camera orientation.
          properties:
            offsetX:
              type: number
              format: double
              description: Right offset in meters.
              example: 5.0
            offsetY:
              type: number
              format: double
              description: Up offset in meters.
              example: 0.0
            offsetZ:
              type: number
              format: double
              description: Forward offset in meters.
              example: 0.0
          required:
            - offsetX
            - offsetY
            - offsetZ

    CameraRotate:
      allOf:
        - $ref: '#/components/schemas/AnimationBase'
        - type: object
          description: Yaw and pitch rotation from a fixed camera position.
          properties:
            yawDegrees:
              type: number
              format: double
              description: Total yaw rotation in degrees.
              example: 45.0
            pitchDegrees:
              type: number
              format: double
              description: Total pitch rotation in degrees.
              example: -15.0
          required:
            - yawDegrees
            - pitchDegrees

    # ── Sequence Step ──────────────────────────────────────────────────────

    CameraSequenceStep:
      type: object
      description: >
        A single step in a camera animation sequence. Set exactly ONE animation
        type property, OR set "group" to run multiple animations simultaneously.
        Nested groups (groups inside groups) are not allowed.
      properties:
        zoomOut:
          $ref: '#/components/schemas/CameraZoomOut'
        zoomIn:
          $ref: '#/components/schemas/CameraZoomIn'
        zoomInToOffset:
          $ref: '#/components/schemas/CameraZoomInToOffset'
        orbit:
          $ref: '#/components/schemas/CameraOrbit'
        loopyOrbit:
          $ref: '#/components/schemas/CameraLoopyOrbit'
        spiralZoomIn:
          $ref: '#/components/schemas/CameraSpiralZoomIn'
        spiralZoomOut:
          $ref: '#/components/schemas/CameraSpiralZoomOut'
        shake:
          $ref: '#/components/schemas/CameraShake'
        pan:
          $ref: '#/components/schemas/CameraPan'
        rotate:
          $ref: '#/components/schemas/CameraRotate'
        group:
          type: array
          description: >
            Array of animation steps to play simultaneously. Each entry must
            be a single animation (no nested groups). Duration of the group
            equals the longest child animation.
          items:
            $ref: '#/components/schemas/CameraSequenceStep'

    # ── Return to Start ────────────────────────────────────────────────────

    CameraReturnToStart:
      type: object
      description: >
        Configuration for the return-to-start animation that plays after the
        sequence completes. Smoothly animates position and rotation back to
        where the camera was when the sequence began.
      properties:
        durationSeconds:
          type: number
          format: double
          description: Duration of the return animation in seconds (1.0–10.0).
          default: 3.0
          example: 3.0
        easing:
          $ref: '#/components/schemas/CameraEasingType'
          default: easeInOut
        easingPowerStart:
          type: number
          format: double
          default: 3.0
        easingPowerEnd:
          type: number
          format: double
          default: 3.0

    # ── Request / Response ─────────────────────────────────────────────────

    CameraAnimateRequest:
      type: object
      description: Request body for POST /camera/animate.
      properties:
        sequence:
          type: array
          description: Ordered list of animation steps to execute sequentially.
          items:
            $ref: '#/components/schemas/CameraSequenceStep'
          minItems: 1
        returnToStart:
          $ref: '#/components/schemas/CameraReturnToStart'
          description: >
            Optional. If provided, camera returns to its starting position/rotation
            after the sequence completes. If omitted, camera stays at final position.
      required:
        - sequence

    CameraAnimateResponse:
      allOf:
        - $ref: '#/components/schemas/ApiResponseBase'
        - type: object
          properties:
            data:
              $ref: '#/components/schemas/CameraAnimateResult'

    CameraAnimateResult:
      type: object
      properties:
        keyframeCount:
          type: integer
          description: Number of keyframes in the submitted sequence.
          example: 3
        totalDurationSeconds:
          type: number
          format: double
          description: Total duration of all keyframes combined (excludes return-to-start).
          example: 18.0
        returnToStartEnabled:
          type: boolean
          description: Whether return-to-start was configured.
          example: true
      required:
        - keyframeCount
        - totalDurationSeconds
        - returnToStartEnabled

    CameraStatusResponse:
      allOf:
        - $ref: '#/components/schemas/ApiResponseBase'
        - type: object
          properties:
            data:
              $ref: '#/components/schemas/CameraPlaybackStatus'

    CameraPlaybackStatus:
      type: object
      properties:
        state:
          type: string
          enum: [Stopped, Playing, Paused]
          description: Current playback state.
          example: Playing
        isReturningToStart:
          type: boolean
          description: Whether the return-to-start animation is currently playing.
          example: false
        currentKeyframeIndex:
          type: integer
          description: 0-based index of the currently playing keyframe.
          example: 1
        totalKeyframes:
          type: integer
          description: Total number of keyframes in the sequence.
          example: 3
        totalElapsedTime:
          type: number
          format: double
          description: Total seconds elapsed since playback started.
          example: 5.2
        totalDurationSeconds:
          type: number
          format: double
          description: Total duration of all keyframes.
          example: 18.0
      required:
        - state
        - isReturningToStart
        - currentKeyframeIndex
        - totalKeyframes
        - totalElapsedTime
        - totalDurationSeconds

    CameraStopResponse:
      allOf:
        - $ref: '#/components/schemas/ApiResponseBase'
        - type: object
          properties:
            data:
              $ref: '#/components/schemas/CameraStopResult'

    CameraStopResult:
      type: object
      properties:
        previousState:
          type: string
          enum: [Stopped, Playing, Paused]
          description: The playback state before the stop was issued.
          example: Playing
      required:
        - previousState

    # ── Shared ─────────────────────────────────────────────────────────────

    ApiResponseBase:
      type: object
      description: Standard API response envelope.
      properties:
        status:
          type: string
          example: ok
        data:
          description: Response payload (type varies by endpoint).
      required:
        - status
        - data

    ErrorResponse:
      type: object
      description: Error response envelope.
      properties:
        status:
          type: string
          example: error
        message:
          type: string
          example: "An unexpected error occurred."
      required:
        - status
        - message

  responses:

    BadRequest:
      description: Bad request — missing or invalid parameters.
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ErrorResponse'

    ModNotLoaded:
      description: Camera controller override mod is not currently loaded.
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ErrorResponse'

    InternalError:
      description: Unexpected server-side error.
      content:
        application/json:
          schema:
            $ref: '#/components/schemas/ErrorResponse'
```

---

### Task 9: Build and verify compilation

Run `dotnet build` from the solution root and fix any compilation errors.

---

### Task 10: Update documentation

**Files to update:**
- `REPOSITORY_INDEX.md` — add a note that `unladen-swallow.lib` now includes camera animation endpoints
- `unladen-swallow/README.md` — document the three new camera endpoints
- `camera-controller-override/README.md` — note the RPC integration via static Instance accessor

---

## Example API Usage

### Simple orbit with return-to-start

```json
POST /camera/animate
{
  "sequence": [
    {
      "orbit": {
        "degrees": 360,
        "durationSeconds": 10.0,
        "easing": "easeInOut"
      }
    }
  ],
  "returnToStart": {
    "durationSeconds": 3.0,
    "easing": "easeInOut"
  }
}
```

### Multi-step cinematic (zoom out → orbit → zoom in)

```json
POST /camera/animate
{
  "sequence": [
    {
      "zoomOut": {
        "speedMetersPerSecond": 10.0,
        "durationSeconds": 3.0,
        "easing": "easeOut"
      }
    },
    {
      "orbit": {
        "degrees": 180,
        "durationSeconds": 8.0,
        "easing": "easeInOut"
      }
    },
    {
      "zoomIn": {
        "speedMetersPerSecond": 10.0,
        "durationSeconds": 3.0,
        "easing": "easeIn"
      }
    }
  ]
}
```

### Group: simultaneous orbit + zoom out

```json
POST /camera/animate
{
  "sequence": [
    {
      "group": [
        {
          "orbit": {
            "degrees": 360,
            "durationSeconds": 12.0,
            "easing": "linear"
          }
        },
        {
          "zoomOut": {
            "speedMetersPerSecond": 3.0,
            "durationSeconds": 12.0,
            "easing": "easeIn"
          }
        }
      ]
    }
  ],
  "returnToStart": {
    "durationSeconds": 5.0,
    "easing": "easeInOut",
    "easingPowerStart": 2.0,
    "easingPowerEnd": 4.0
  }
}
```

### Group with shake overlay

```json
POST /camera/animate
{
  "sequence": [
    {
      "group": [
        {
          "orbit": {
            "degrees": 90,
            "durationSeconds": 5.0,
            "easing": "easeInOut"
          }
        },
        {
          "shake": {
            "shakeCount": 10,
            "amplitudeDegrees": 3.0,
            "shakeSpeed": 1.5,
            "durationSeconds": 5.0,
            "easing": "linear"
          }
        }
      ]
    }
  ]
}
```

### Check status

```
GET /camera/status
→ { "status": "ok", "data": { "state": "Playing", "isReturningToStart": false, "currentKeyframeIndex": 1, "totalKeyframes": 3, "totalElapsedTime": 5.2, "totalDurationSeconds": 18.0 } }
```

### Stop animation

```
DELETE /camera/stop
→ { "status": "ok", "data": { "previousState": "Playing" } }
```

---

## Validation Rules (for CameraAnimateEndpoint)

1. `sequence` must be non-null and non-empty
2. Each `CameraSequenceStep` must have exactly one animation type property set, OR `group` set (not both)
3. Inside a `group`, each entry must be a single animation (no nested groups)
4. Groups must contain at least one animation
5. All `durationSeconds` values must be > 0
6. If `CameraControllerOverrideSubmod.Instance` is null, return 503 Service Unavailable
7. `ReturnToStart.DurationSeconds` is clamped to 1.0–10.0 by the `KeyframeSequencePlayer` setter

---

## File Summary

| File | Action | Description |
|------|--------|-------------|
| `camera-controller-override.lib/CameraControllerOverrideSubmod.cs` | Edit | Add `static Instance` property, set in init, clear in dispose |
| `unladen-swallow.lib/unladen-swallow.lib.csproj` | Edit | Add ProjectReference to camera-controller-override.lib |
| `unladen-swallow.lib/ApiTypes.cs` | Edit | Add all Camera* record types and CameraEasingType enum |
| `unladen-swallow.lib/CameraAnimateEndpoint.cs` | Create | POST /camera/animate handler |
| `unladen-swallow.lib/CameraStatusEndpoint.cs` | Create | GET /camera/status handler |
| `unladen-swallow.lib/CameraStopEndpoint.cs` | Create | DELETE /camera/stop handler |
| `unladen-swallow.lib/SwallowServer.cs` | Edit | Register /camera/* routes |
| `unladen-swallow.lib/openapi/camera.yml` | Create | OpenAPI 3.1.0 spec |
| `REPOSITORY_INDEX.md` | Edit | Note camera endpoints |
| `unladen-swallow/README.md` | Edit | Document camera endpoints |
| `camera-controller-override/README.md` | Edit | Note RPC integration |

---

## Dependencies

```
unladen-swallow.lib
  ├── ksa-abstractions.lib (GameThread.Scheduler)
  ├── glass.lib (FovController)
  ├── blinky.lib (BlinkyGridManager)
  └── camera-controller-override.lib (NEW)
       ├── ksa-abstractions.lib
       └── KSA game DLLs
```

No NuGet packages need to be added. No mod.toml changes needed for unladen-swallow (build-time reference only).
