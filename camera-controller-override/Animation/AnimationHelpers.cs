using System;
using Brutal.Numerics;
using KSA;

namespace mod.Animation;

/// <summary>
/// Easing function types for animation interpolation.
/// </summary>
public enum EasingType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

/// <summary>
/// Shared helper methods for camera animations.
/// Extracted from Patcher.cs for reusability across animation implementations.
/// </summary>
public static class AnimationHelpers
{
    /// <summary>
    /// Apply an easing function to a normalized time value [0, 1].
    /// </summary>
    /// <param name="t">Normalized time in range [0, 1].</param>
    /// <param name="easingType">The easing function to apply.</param>
    /// <returns>Eased time value.</returns>
    public static double ApplyEasing(double t, EasingType easingType)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return easingType switch
        {
            EasingType.EaseIn => t * t * t,
            EasingType.EaseOut => 1.0 - Math.Pow(1.0 - t, 3),
            EasingType.EaseInOut => t * t * (3.0 - 2.0 * t),
            _ => t
        };
    }
    
    /// <summary>
    /// Get the target position from the controller's Following object.
    /// </summary>
    /// <param name="controller">The camera controller.</param>
    /// <param name="fallback">Fallback position if no target available.</param>
    /// <returns>Target position in ecliptic coordinates.</returns>
    public static double3 GetTargetPosition(Controller controller, double3 fallback = default)
        => controller?.Camera?.Following?.GetPositionEcl() ?? fallback;
    
    /// <summary>
    /// Orient the transform to look at a target position.
    /// </summary>
    /// <param name="transform">The transform to modify.</param>
    /// <param name="targetPos">The position to look at in ecliptic coordinates.</param>
    public static void LookAtTarget(Transform3D transform, double3 targetPos)
    {
        double3 lookDirection = targetPos - transform.PositionEcl;
        if (lookDirection.LengthSquared() > 0.0001)
        {
            double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
            transform.LocalRotation = Camera.LookAtRotation(lookDirection, currentUp);
        }
    }
    
    /// <summary>
    /// Calculate the eased progress for the current frame.
    /// Returns the delta between current eased progress and previous frame's eased progress.
    /// </summary>
    /// <param name="elapsed">Total elapsed time.</param>
    /// <param name="duration">Total animation duration.</param>
    /// <param name="deltaTime">Time since last frame.</param>
    /// <param name="easingType">Easing function to apply.</param>
    /// <returns>Eased progress delta for this frame.</returns>
    public static double GetEasedFrameProgress(double elapsed, double duration, double deltaTime, EasingType easingType)
    {
        double t = Math.Min(1.0, elapsed / duration);
        double lastT = Math.Max(0.0, (elapsed - deltaTime) / duration);
        return ApplyEasing(t, easingType) - ApplyEasing(lastT, easingType);
    }
    
    /// <summary>
    /// Calculate the orbit axis perpendicular to the offset from target.
    /// Uses camera's up vector and offset to determine rotation axis.
    /// </summary>
    /// <param name="startOffset">Offset vector from target to camera start position.</param>
    /// <param name="startRotation">Camera's starting rotation quaternion.</param>
    /// <returns>Normalized orbit axis vector.</returns>
    public static double3 CalculateOrbitAxis(double3 startOffset, doubleQuat startRotation)
    {
        double3 startUp = double3.UnitY.Transform(startRotation);
        if (startUp.LengthSquared() < 0.00000001) startUp = double3.UnitY;
        
        double3 right = double3.Cross(startUp, startOffset);
        if (right.LengthSquared() < 0.0001)
        {
            double3 offsetDir = double3.Normalize(startOffset);
            double3 fallback = Math.Abs(double3.Dot(offsetDir, double3.UnitY)) < 0.99 ? double3.UnitY : double3.UnitX;
            right = double3.Cross(fallback, startOffset);
        }
        return double3.Normalize(double3.Cross(startOffset, right));
    }
}
