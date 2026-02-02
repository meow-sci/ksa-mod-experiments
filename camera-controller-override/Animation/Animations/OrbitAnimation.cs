using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Circular orbit animation that rotates the camera around a target.
/// Uses incremental rotation each frame from CURRENT camera position.
/// The orbit axis is captured at initialization to maintain consistent rotation direction.
/// </summary>
public class OrbitAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double Degrees { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    
    // Runtime state
    private double3 _orbitAxis;
    private double _lastEasedProgress;
    private double _totalDegreesRotated;
    private bool _isInitialized;
    
    // Interface properties
    public string Name => "Orbit";
    public string Description => "Circular orbit around target";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    /// <summary>
    /// Create a new orbit animation.
    /// </summary>
    /// <param name="degrees">Total rotation angle in degrees.</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to the rotation.</param>
    public OrbitAnimation(double degrees, double durationSeconds, EasingType easing)
    {
        Degrees = degrees;
        DurationSeconds = durationSeconds;
        Easing = easing;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Get current state to determine orbit axis
        double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 currentOffset = transform.PositionEcl - targetPos;
        
        // Validate radius
        double radius = currentOffset.Length();
        if (radius < 0.01)
        {
            Console.WriteLine("camera-controller-override: Orbit radius too small, cancelling.");
            _isInitialized = false;
            return;
        }
        
        // Calculate orbit axis (perpendicular to offset - determines rotation direction)
        _orbitAxis = AnimationHelpers.CalculateOrbitAxis(currentOffset, transform.LocalRotation);
        _lastEasedProgress = 0.0;
        _totalDegreesRotated = 0.0;
        _isInitialized = true;
        
        Console.WriteLine($"[OrbitAnimation] Initialize: pos={transform.PositionEcl}, target={targetPos}, radius={radius:F2}");
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Skip animation if initialization failed
        if (!_isInitialized)
        {
            return true;
        }
        
        // Get current eased progress (0.0 to 1.0)
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double currentEasedProgress = AnimationHelpers.ApplyEasing(t, Easing);
        
        // Calculate how much rotation we should make THIS frame
        double frameProgress = currentEasedProgress - _lastEasedProgress;
        _lastEasedProgress = currentEasedProgress;
        
        // Calculate the angle to rotate THIS frame
        double frameAngleDegrees = Degrees * frameProgress;
        double frameAngleRadians = frameAngleDegrees * Math.PI / 180.0;
        
        // Get CURRENT target position and offset
        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 currentOffset = transform.PositionEcl - currentTargetPos;
        
        // Apply incremental Rodrigues' rotation for this frame's angle
        double3 k = _orbitAxis;
        double cos = Math.Cos(frameAngleRadians);
        double sin = Math.Sin(frameAngleRadians);
        double3 rotatedOffset = currentOffset * cos 
            + double3.Cross(k, currentOffset) * sin 
            + k * double3.Dot(k, currentOffset) * (1.0 - cos);
        
        // Update position relative to CURRENT target
        transform.PositionEcl = currentTargetPos + rotatedOffset;
        
        // Log on first frame
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[OrbitAnimation] First frame: elapsed={elapsedTime:F4}, frameAngle={frameAngleDegrees:F4}°");
        }
        
        // Maintain look-at behavior
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? currentTargetPos;
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);
        
        _totalDegreesRotated += frameAngleDegrees;
        
        // Log on completion
        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            Console.WriteLine($"[OrbitAnimation] Complete: totalDegreesRotated={_totalDegreesRotated:F2}°, finalPos={transform.PositionEcl}");
        }
        
        return isComplete;
    }
    
    public void Reset()
    {
        _orbitAxis = double3.Zero;
        _lastEasedProgress = 0.0;
        _totalDegreesRotated = 0.0;
        _isInitialized = false;
    }
    
    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            { "Degrees", $"{Degrees:F1}°" },
            { "Duration", $"{DurationSeconds:F1}s" },
            { "Easing", Easing.ToString() }
        };
    }
}
