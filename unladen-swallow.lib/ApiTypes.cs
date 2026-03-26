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
public record BlinkyScrollRequest(string VehicleId, PixelCoord[] Pixels, float Speed);

/// <summary>Request body for POST /blinky/static — displays a static pixel set.</summary>
public record BlinkyStaticRequest(string VehicleId, PixelCoord[] Pixels, bool Reset);

/// <summary>Request body for POST /blinky/off — turns off all pixels.</summary>
public record BlinkyOffRequest(string VehicleId);

/// <summary>Result returned by blinky endpoints.</summary>
public record BlinkyResult(string VehicleId, string Action);
