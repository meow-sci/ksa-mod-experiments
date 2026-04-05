namespace MeowSci.UnladenSwallowLib;

/// <summary>Standard API response envelope.</summary>
public record ApiResponse<T>(string Status, T? Data);

/// <summary>Request body for POST /fov.</summary>
public record FovRequest(float Fov);

/// <summary>FOV state returned by GET and POST /fov.</summary>
public record FovState(float CurrentFovDegrees, float OverrideFovDegrees, bool IsOverrideActive);

/// <summary>Request body for vehicle action endpoints.</summary>
public record VehicleActionRequest(string VehicleId);

/// <summary>Result returned by vehicle action endpoints.</summary>
public record VehicleActionResult(string VehicleId, string Action);

// ── Blinky API Types ────────────────────────────────────────────────────────

/// <summary>A single pixel coordinate (x=column, y=row).</summary>
public record PixelCoord(int X, int Y);

/// <summary>Request body for POST /blinky/animate — starts a scrolling animation.</summary>
public record BlinkyScrollRequest(string VehicleId, string GridName, PixelCoord[] Pixels, float Speed);

/// <summary>Request body for POST /blinky/static — displays a static pixel set.</summary>
public record BlinkyStaticRequest(string VehicleId, string GridName, PixelCoord[] Pixels, bool Reset);

/// <summary>Request body for POST /blinky/off — turns off all pixels.</summary>
public record BlinkyOffRequest(string VehicleId, string GridName);

/// <summary>Result returned by blinky endpoints.</summary>
public record BlinkyResult(string VehicleId, string GridName, string Action);

// ── Blinky Grid List Types ──────────────────────────────────────────────────

/// <summary>Information about a registered blinky grid.</summary>
public record BlinkyGridInfo(string VehicleId, string GridName, int Rows, int Cols, bool IsScrolling);

/// <summary>Result returned by GET /blinky/grids.</summary>
public record BlinkyGridListResult(BlinkyGridInfo[] Grids);

// ── Camera Animation API Types ──────────────────────────────────────────────

/// <summary>
/// Easing type for camera animations. Values match CameraControllerOverrideLib.Animation.EasingType.
/// </summary>
public enum CameraEasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

/// <summary>A zoom-out animation step. Moves camera away from target.</summary>
public record CameraZoomOut(
    double SpeedMetersPerSecond,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A zoom-in animation step. Moves camera toward target (min 1m distance).</summary>
public record CameraZoomIn(
    double SpeedMetersPerSecond,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A zoom-in-to-offset animation step. Zooms toward a point offset from the target.</summary>
public record CameraZoomInToOffset(
    double SpeedMetersPerSecond,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A circular orbit animation step around the camera target.</summary>
public record CameraOrbit(
    double Degrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>An orbit with sinusoidal in/out oscillation (loopy orbit).</summary>
public record CameraLoopyOrbit(
    double Degrees,
    double LoopIntervalDegrees,
    double AmplitudeMeters,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>Zoom in while spiraling around the camera look axis.</summary>
public record CameraSpiralZoomIn(
    double SpeedMetersPerSecond,
    double SpiralDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>Zoom out while spiraling around the camera look axis.</summary>
public record CameraSpiralZoomOut(
    double SpeedMetersPerSecond,
    double SpiralDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A head-shaking yaw rotation effect using sinusoidal oscillation.</summary>
public record CameraShake(
    int ShakeCount,
    double AmplitudeDegrees,
    double ShakeSpeed,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A camera-local offset movement (X=right, Y=up, Z=forward).</summary>
public record CameraPan(
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>A yaw and pitch rotation from a fixed camera position.</summary>
public record CameraRotate(
    double YawDegrees,
    double PitchDegrees,
    double DurationSeconds,
    CameraEasingType Easing = CameraEasingType.Linear,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>
/// A single step in a camera animation sequence.
/// Set exactly ONE animation type property, OR set Group to run animations simultaneously.
/// Nested groups (groups inside groups) are not allowed.
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
/// Optional return-to-start configuration. When included in requests, the camera
/// animates back to its starting position and rotation after the sequence completes.
/// </summary>
public record CameraReturnToStart(
    double DurationSeconds = 3.0,
    CameraEasingType Easing = CameraEasingType.EaseInOut,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>Request body for POST /camera/animate.</summary>
public record CameraAnimateRequest(
    CameraSequenceStep[] Sequence,
    CameraReturnToStart? ReturnToStart = null
);

/// <summary>Result returned by POST /camera/animate on success.</summary>
public record CameraAnimateResult(
    int KeyframeCount,
    double TotalDurationSeconds,
    bool ReturnToStartEnabled
);

/// <summary>Current camera animation playback status returned by GET /camera/status.</summary>
public record CameraPlaybackStatus(
    string State,
    bool IsReturningToStart,
    int CurrentKeyframeIndex,
    int TotalKeyframes,
    double TotalElapsedTime,
    double TotalDurationSeconds
);

/// <summary>Result returned by DELETE /camera/stop.</summary>
public record CameraStopResult(string PreviousState);

// ── Garry's Torch — Easing ──────────────────────────────────────────────────

public enum TorchEasingType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

// ── Garry's Torch — Core Data Models ────────────────────────────────────────

/// <summary>3D vector for position (meters) or rotation (degrees).</summary>
public record Vec3(float X, float Y, float Z);

/// <summary>Full weld configuration data (used in create, modify, presets).</summary>
public record WeldData(
    Vec3 Position,
    Vec3 Rotation,
    float Scale = 1f,
    bool LockRotation = true
);

/// <summary>Describes an active weld in API responses.</summary>
public record WeldInfo(
    string SourceVehicleId,
    string TargetVehicleId,
    Vec3 Position,
    Vec3 Rotation,
    float Scale,
    bool LockRotation
);

// ── Garry's Torch — Weld CRUD ───────────────────────────────────────────────

/// <summary>Request body for creating a new weld.</summary>
public record TorchCreateWeldRequest(
    string SourceVehicleId,
    string TargetVehicleId,
    WeldData? Data = null,
    string? PresetName = null
);

/// <summary>
/// Request body for modifying an existing weld (immediate).
/// Only provided fields are updated; omitted fields remain unchanged.
/// </summary>
public record TorchModifyWeldRequest(
    string SourceVehicleId,
    Vec3? Position = null,
    Vec3? Rotation = null,
    float? Scale = null,
    bool? LockRotation = null
);

/// <summary>Request body for deleting/unwelding.</summary>
public record TorchDeleteWeldRequest(string SourceVehicleId);

/// <summary>Easing configuration for animated weld transitions.</summary>
public record TorchEasingConfig(
    TorchEasingType Easing = TorchEasingType.EaseInOut,
    double EasingPowerStart = 3.0,
    double EasingPowerEnd = 3.0
);

/// <summary>Request body for animating a weld transition over time.</summary>
public record TorchAnimateWeldRequest(
    string SourceVehicleId,
    double DurationSeconds,
    WeldData? Data = null,
    string? PresetName = null,
    TorchEasingConfig? Easing = null
);

// ── Garry's Torch — Preset CRUD ─────────────────────────────────────────────

/// <summary>Preset data as returned by the API.</summary>
public record TorchPresetInfo(
    string Name,
    Vec3 Position,
    Vec3 Rotation,
    float Scale,
    bool LockRotation
);

/// <summary>Request body for creating or updating a preset.</summary>
public record TorchSavePresetRequest(
    string Name,
    WeldData Data
);

/// <summary>Request body for deleting a preset.</summary>
public record TorchDeletePresetRequest(string Name);

// ── Garry's Torch — API Responses ───────────────────────────────────────────

public record TorchWeldResult(WeldInfo Weld);
public record TorchWeldListResult(WeldInfo[] Welds);
public record TorchPresetResult(TorchPresetInfo Preset);
public record TorchPresetListResult(TorchPresetInfo[] Presets);
public record TorchDeleteResult(string Message);
public record TorchAnimateResult(string SourceVehicleId, string Status);
