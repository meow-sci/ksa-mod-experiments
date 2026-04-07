namespace MeowSci.ZippoLib;

using Brutal.Numerics;

/// <summary>Describes the current state of a light part on a vehicle.</summary>
public record LightPartInfo(
    string PartId,
    string DisplayName,
    float Intensity,
    float3 Color,
    bool IsEnabled,
    bool IsAnimating,
    int QueuedAnimations);
