using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Linear animation that moves the camera toward the target at a constant speed.
/// Each frame, calculates direction from CURRENT camera position to CURRENT target position.
/// This ensures the animation always moves directly toward the target, even if the target moves.
/// Includes minimum distance safeguard to prevent collision with target.
/// </summary>
public class ZoomInAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double SpeedMetersPerSecond { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    
    // Runtime state - only track progress, not positions
    private double _distanceTraveled;
    private double _lastEasedProgress;
    
    // Interface properties
    public string Name => "Zoom In";
    public string Description => "Linear movement toward target";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    // Minimum distance safeguard (1 meter from target)
    private const double MinimumDistance = 1.0;
    
    /// <summary>
    /// Create a new zoom in animation.
    /// </summary>
    /// <param name="speedMetersPerSecond">Movement speed in meters per second.</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to the movement.</param>
    public ZoomInAnimation(double speedMetersPerSecond, double durationSeconds, EasingType easing)
    {
        SpeedMetersPerSecond = speedMetersPerSecond;
        DurationSeconds = durationSeconds;
        Easing = easing;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Only reset progress tracking - no position capture needed
        // Direction will be calculated each frame from current positions
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
        
        Console.WriteLine($"[ZoomInAnimation] Initialize: pos={transform.PositionEcl}");
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Calculate total movement distance for the entire animation
        double totalDistance = SpeedMetersPerSecond * DurationSeconds;
        
        // Get current eased progress (0.0 to 1.0)
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double currentEasedProgress = AnimationHelpers.ApplyEasing(t, Easing);
        
        // Calculate how much progress we should make THIS frame
        double frameProgress = currentEasedProgress - _lastEasedProgress;
        _lastEasedProgress = currentEasedProgress;
        
        // Get CURRENT target position and calculate direction toward it
        double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 towardTarget = targetPos - transform.PositionEcl;
        double distanceToTarget = towardTarget.Length();
        
        // Handle edge case: camera is very close to target
        if (distanceToTarget < MinimumDistance)
        {
            Console.WriteLine($"[ZoomInAnimation] Stopping: Too close to target (distance={distanceToTarget:F4}m)");
            return true; // Complete animation early
        }
        
        // Calculate displacement for this frame
        double3 direction = double3.Normalize(towardTarget);
        double frameDistance = totalDistance * frameProgress;
        
        // Clamp displacement to not overshoot minimum distance
        if (distanceToTarget - frameDistance < MinimumDistance)
        {
            frameDistance = distanceToTarget - MinimumDistance;
            Console.WriteLine($"[ZoomInAnimation] Clamping displacement to maintain minimum distance");
        }
        
        double3 displacement = direction * frameDistance;
        transform.PositionEcl += displacement;
        
        // Log on first frame to verify correct initialization
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[ZoomInAnimation] First frame: elapsed={elapsedTime:F4}, frameProgress={frameProgress:F6}, displacement={displacement.Length():F4}");
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
            Console.WriteLine($"[ZoomInAnimation] Complete: totalDistanceTraveled={_distanceTraveled:F2}, finalPos={transform.PositionEcl}");
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
