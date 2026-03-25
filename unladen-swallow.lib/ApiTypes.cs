namespace MeowSci.UnladenSwallowLib;

/// <summary>Standard API response envelope.</summary>
public record ApiResponse<T>(string Status, T? Data);

/// <summary>Request body for POST /fov.</summary>
public record FovRequest(float Fov);

/// <summary>FOV state returned by GET and POST /fov.</summary>
public record FovState(float CurrentFovDegrees, float OverrideFovDegrees, bool IsOverrideActive);
