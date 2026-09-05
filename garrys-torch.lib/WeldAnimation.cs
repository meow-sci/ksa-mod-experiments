using System;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
using WeldEasingType = MeowSci.KsaAbstractions.EasingType;

namespace MeowSci.GarrysTorchLib;

/// <summary>
/// Represents an active animation that interpolates a weld's position, rotation, and scale
/// from start values to target values over a specified duration using configurable easing.
/// </summary>
public class WeldAnimation
{
    public float3 StartPosition { get; }
    public float3 StartRotation { get; }
    public float StartScale { get; }
    public float3 TargetPosition { get; }
    public float3 TargetRotation { get; }
    public float TargetScale { get; }
    public double DurationSeconds { get; }
    public double ElapsedSeconds { get; private set; }
    public WeldEasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }
    public bool IsComplete => ElapsedSeconds >= DurationSeconds;

    public WeldAnimation(
        float3 startPosition, float3 startRotation, float startScale,
        float3 targetPosition, float3 targetRotation, float targetScale,
        double durationSeconds, WeldEasingType easing,
        double easingPowerStart = 3.0, double easingPowerEnd = 3.0)
    {
        StartPosition = startPosition;
        StartRotation = startRotation;
        StartScale = startScale;
        TargetPosition = targetPosition;
        TargetRotation = targetRotation;
        TargetScale = targetScale;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
    }

    /// <summary>
    /// Advances the animation by dt seconds and applies interpolated values to the weld.
    /// Returns true if still running, false when complete.
    /// </summary>
    public bool Update(WeldEntry weld, double dt)
    {
        ElapsedSeconds += dt;

        if (ElapsedSeconds >= DurationSeconds)
        {
            // Snap to exact target values on completion
            weld.Position = TargetPosition;
            weld.Rotation = TargetRotation;
            if (weld.Scale != TargetScale)
            {
                weld.Scale = TargetScale;
                WeldEngine.ApplyVehicleScale(weld, TargetScale);
            }
            return false;
        }

        double rawT = ElapsedSeconds / DurationSeconds;
        float t = (float)ApplyEasing(rawT, Easing, EasingPowerStart, EasingPowerEnd);

        weld.Position = Lerp(StartPosition, TargetPosition, t);
        weld.Rotation = Lerp(StartRotation, TargetRotation, t);

        float newScale = StartScale + (TargetScale - StartScale) * t;
        if (weld.Scale != newScale)
        {
            weld.Scale = newScale;
            WeldEngine.ApplyVehicleScale(weld, newScale);
        }

        return true;
    }

    internal static double ApplyEasing(double t, WeldEasingType easingType,
        double powerStart = 3.0, double powerEnd = 3.0)
        => EasingHelper.ApplyEasing(t, easingType, powerStart, powerEnd);

    private static float3 Lerp(float3 a, float3 b, float t)
    {
        return new float3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }
}
