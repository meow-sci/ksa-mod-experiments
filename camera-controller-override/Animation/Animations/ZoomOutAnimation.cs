using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Linear animation that moves the camera away from the target at a constant speed.
/// Each frame, calculates direction from CURRENT camera position to CURRENT target position.
/// This ensures the animation always moves directly away from the target, even if the target moves.
/// </summary>
public class ZoomOutAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double SpeedMetersPerSecond { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double EasingPower { get; }
    
    // Runtime state - only track progress, not positions
    private double _distanceTraveled;
    private double _lastEasedProgress;
    
    // Interface properties
    public string Name => "Zoom Out";
    public string Description => "Linear movement away from target";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    /// <summary>
    /// Create a new zoom out animation.
    /// </summary>
    /// <param name="speedMetersPerSecond">Movement speed in meters per second.</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to the movement.</param>
    /// <param name="easingPower">Power parameter for easing strength (default 3.0).</param>
    public ZoomOutAnimation(double speedMetersPerSecond, double durationSeconds, EasingType easing, double easingPower = 3.0)
    {
        SpeedMetersPerSecond = speedMetersPerSecond;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPower = easingPower;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Only reset progress tracking - no position capture needed
        // Direction will be calculated each frame from current positions
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
        
        Console.WriteLine($"[ZoomOutAnimation] Initialize: pos={transform.PositionEcl}");
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Calculate total movement distance for the entire animation
        double totalDistance = SpeedMetersPerSecond * DurationSeconds;
        
        // Get current eased progress (0.0 to 1.0)
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double currentEasedProgress = AnimationHelpers.ApplyEasing(t, Easing, EasingPower);
        
        // Calculate how much progress we should make THIS frame
        double frameProgress = currentEasedProgress - _lastEasedProgress;
        _lastEasedProgress = currentEasedProgress;
        
        // Get CURRENT target position and calculate direction away from it
        double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 towardTarget = targetPos - transform.PositionEcl;
        
        // Handle edge case: camera is at target position
        if (towardTarget.LengthSquared() < 0.0001)
        {
            // Use a default direction (away from origin, or up if at origin)
            towardTarget = transform.PositionEcl.LengthSquared() > 0.0001 
                ? -transform.PositionEcl 
                : new double3(0, 1, 0);
        }
        
        double3 direction = double3.Normalize(-towardTarget);
        
        // Apply displacement for this frame
        double3 displacement = direction * (totalDistance * frameProgress);
        transform.PositionEcl += displacement;
        
        // Log on first frame to verify correct initialization
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[ZoomOutAnimation] First frame: elapsed={elapsedTime:F4}, frameProgress={frameProgress:F6}, displacement={displacement.Length():F4}");
        }
        
        // Maintain look-at behavior - use current target
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? targetPos;
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);
        
        // Track distance traveled
        _distanceTraveled += displacement.Length();
        
        // Log on completion
        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            Console.WriteLine($"[ZoomOutAnimation] Complete: totalDistanceTraveled={_distanceTraveled:F2}, finalPos={transform.PositionEcl}");
        }
        
        return isComplete;
    }
    
    public void Reset()
    {
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
    }
    
    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            { "Speed", $"{SpeedMetersPerSecond:F1} m/s" },
            { "Duration", $"{DurationSeconds:F1}s" },
            { "Easing", Easing.ToString() }
        };
    }
}
