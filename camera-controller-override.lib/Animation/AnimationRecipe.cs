using MeowSci.KsaAbstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using MeowSci.CameraControllerOverrideLib.Animation.Animations;
namespace MeowSci.CameraControllerOverrideLib.Animation;
/// <summary>Versioned authoring recipe: no controller, target delegate or playback cursor.</summary>
public sealed class AnimationRecipe
{
    public string Kind { get; set; } = "";
    public double[] Values { get; set; } = Array.Empty<double>();
    public List<AnimationRecipe> Children { get; set; } = new();
    public static AnimationRecipe Capture(IKeyframeAnimation animation) => animation switch
    {
        AnimationGroup g => new() { Kind = "Group", Children = Enumerable.Range(0, g.Count).Select(i => Capture(g.GetAnimation(i))).ToList() },
            SpiralZoomInAnimation v => new() { Kind = "SpiralZoomInAnimation", Values = new double[] { (double)v.SpeedMetersPerSecond, (double)v.DurationSeconds, (double)v.Easing, (double)v.SpiralDegrees, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            OrbitAnimation v => new() { Kind = "OrbitAnimation", Values = new double[] { (double)v.Degrees, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            ZoomOutAnimation v => new() { Kind = "ZoomOutAnimation", Values = new double[] { (double)v.SpeedMetersPerSecond, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            LoopyOrbitAnimation v => new() { Kind = "LoopyOrbitAnimation", Values = new double[] { (double)v.Degrees, (double)v.LoopIntervalDegrees, (double)v.AmplitudeMeters, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            PanAnimation v => new() { Kind = "PanAnimation", Values = new double[] { (double)v.OffsetX, (double)v.OffsetY, (double)v.OffsetZ, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            RotateAnimation v => new() { Kind = "RotateAnimation", Values = new double[] { (double)v.YawDegrees, (double)v.PitchDegrees, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            ZoomInToOffsetAnimation v => new() { Kind = "ZoomInToOffsetAnimation", Values = new double[] { (double)v.SpeedMetersPerSecond, (double)v.DurationSeconds, (double)v.Easing, (double)v.OffsetX, (double)v.OffsetY, (double)v.OffsetZ, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            ShakeAnimation v => new() { Kind = "ShakeAnimation", Values = new double[] { (double)v.DurationSeconds, (double)v.ShakeCount, (double)v.AmplitudeDegrees, (double)v.ShakeSpeed, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            ZoomInAnimation v => new() { Kind = "ZoomInAnimation", Values = new double[] { (double)v.SpeedMetersPerSecond, (double)v.DurationSeconds, (double)v.Easing, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
            SpiralZoomOutAnimation v => new() { Kind = "SpiralZoomOutAnimation", Values = new double[] { (double)v.SpeedMetersPerSecond, (double)v.DurationSeconds, (double)v.Easing, (double)v.SpiralDegrees, (double)v.EasingPowerStart, (double)v.EasingPowerEnd } },
        _ => throw new InvalidOperationException("Unsupported camera recipe.")
    };
    public IKeyframeAnimation Create(int depth = 0)
    {
        if (depth > 12 || Values == null || Children == null || Values.Any(v => !double.IsFinite(v)) || Children.Count > 256)
            throw new InvalidOperationException("Invalid camera recipe.");
        if (Kind == "Group") { var group = new AnimationGroup(); foreach (var child in Children) group.Add(child.Create(depth + 1)); return group; }
        IKeyframeAnimation animation = Kind switch
        {
            "SpiralZoomInAnimation" when Values.Length == 6 => new SpiralZoomInAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4], Values[5]),
            "OrbitAnimation" when Values.Length == 5 => new OrbitAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4]),
            "ZoomOutAnimation" when Values.Length == 5 => new ZoomOutAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4]),
            "LoopyOrbitAnimation" when Values.Length == 7 => new LoopyOrbitAnimation(Values[0], Values[1], Values[2], Values[3], (EasingType)Values[4], Values[5], Values[6]),
            "PanAnimation" when Values.Length == 7 => new PanAnimation(Values[0], Values[1], Values[2], Values[3], (EasingType)Values[4], Values[5], Values[6]),
            "RotateAnimation" when Values.Length == 6 => new RotateAnimation(Values[0], Values[1], Values[2], (EasingType)Values[3], Values[4], Values[5]),
            "ZoomInToOffsetAnimation" when Values.Length == 8 => new ZoomInToOffsetAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4], Values[5], Values[6], Values[7]),
            "ShakeAnimation" when Values.Length == 7 => new ShakeAnimation(Values[0], (int)Values[1], Values[2], Values[3], (EasingType)Values[4], Values[5], Values[6]),
            "ZoomInAnimation" when Values.Length == 5 => new ZoomInAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4]),
            "SpiralZoomOutAnimation" when Values.Length == 6 => new SpiralZoomOutAnimation(Values[0], Values[1], (EasingType)Values[2], Values[3], Values[4], Values[5]),
            _ => throw new InvalidOperationException("Unknown camera recipe or invalid parameter count.")
        };
        if (animation.DurationSeconds <= 0 || !Enum.IsDefined(animation.Easing)) throw new InvalidOperationException("Invalid animation duration/easing.");
        return animation;
    }
}
