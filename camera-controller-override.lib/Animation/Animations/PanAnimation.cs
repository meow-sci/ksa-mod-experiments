using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace MeowSci.CameraControllerOverrideLib.Animation.Animations;

/// <summary>
/// Moves the camera from its current position by a specified (X, Y, Z) offset
/// in ecliptic coordinates, with easing. The camera continues to look at the
/// target throughout the pan.
/// </summary>
public class PanAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double OffsetZ { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }

    // Runtime state
    private double3 _startPosition;

    // Interface properties
    public string Name => "Pan";
    public string Description => "Linear movement by offset from starting position";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }

    /// <summary>
    /// Creates a pan animation that moves the camera by the specified offset.
    /// </summary>
    /// <param name="offsetX">X displacement in meters (ecliptic coordinates).</param>
    /// <param name="offsetY">Y displacement in meters (ecliptic coordinates).</param>
    /// <param name="offsetZ">Z displacement in meters (ecliptic coordinates).</param>
    /// <param name="durationSeconds">Total animation duration in seconds.</param>
    /// <param name="easing">Easing function type.</param>
    /// <param name="easingPowerStart">Power for the acceleration phase.</param>
    /// <param name="easingPowerEnd">Power for the deceleration phase.</param>
    public PanAnimation(
        double offsetX,
        double offsetY,
        double offsetZ,
        double durationSeconds,
        EasingType easing,
        double easingPowerStart = 3.0,
        double easingPowerEnd = 3.0)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        OffsetZ = offsetZ;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
    }

    public void Initialize(Controller controller, Transform3D transform)
    {
        _startPosition = transform.PositionEcl;
        Console.WriteLine($"[PanAnimation] Initialize: startPosition={_startPosition}, offset=({OffsetX:F1}, {OffsetY:F1}, {OffsetZ:F1}), duration={DurationSeconds:F1}s, easing={Easing}");
    }

    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double easedT = AnimationHelpers.ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd);

        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            easedT = 1.0;
        }

        var targetOffset = new double3(OffsetX, OffsetY, OffsetZ);
        var currentOffset = targetOffset * easedT;
        transform.PositionEcl = _startPosition + currentOffset;

        var lookAtTarget = LookAtTargetProvider != null
            ? LookAtTargetProvider(controller)
            : AnimationHelpers.GetTargetPosition(controller);
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);

        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[PanAnimation] First frame: t={t:F4}, easedT={easedT:F4}, position={transform.PositionEcl}");
        }

        if (isComplete)
        {
            Console.WriteLine($"[PanAnimation] Complete: finalPosition={transform.PositionEcl}");
        }

        return isComplete;
    }

    public void Reset()
    {
        _startPosition = double3.Zero;
    }

    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            { "Offset", $"({OffsetX:F1}, {OffsetY:F1}, {OffsetZ:F1})m" },
            { "Duration", $"{DurationSeconds:F1}s" },
            { "Easing", Easing.ToString() },
            { "Easing Power (Start)", $"{EasingPowerStart:F1}" },
            { "Easing Power (End)", $"{EasingPowerEnd:F1}" }
        };
    }
}
