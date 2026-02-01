using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Linear animation that moves the camera away from the target at a constant speed.
/// Extracted from Patcher.cs linear animation logic.
/// </summary>
public class ZoomOutAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double SpeedMetersPerSecond { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    
    // Runtime state
    private double3 _direction;
    private double _distanceTraveled;
    
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
    public ZoomOutAnimation(double speedMetersPerSecond, double durationSeconds, EasingType easing)
    {
        SpeedMetersPerSecond = speedMetersPerSecond;
        DurationSeconds = durationSeconds;
        Easing = easing;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Capture direction away from target (same logic as Patcher.cs)
        double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 towardTarget = targetPos - transform.PositionEcl;
        _direction = double3.Normalize(-towardTarget);
        _distanceTraveled = 0.0;
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Calculate total movement distance
        double totalDistance = SpeedMetersPerSecond * DurationSeconds;
        
        // Get eased frame-by-frame progress
        double frameProgress = AnimationHelpers.GetEasedFrameProgress(elapsedTime, DurationSeconds, deltaTime, Easing);
        
        // Apply displacement for this frame
        double3 displacement = _direction * (totalDistance * frameProgress);
        transform.PositionEcl += displacement;
        
        // Maintain look-at behavior
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? AnimationHelpers.GetTargetPosition(controller);
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);
        
        // Track distance traveled
        _distanceTraveled += displacement.Length();
        
        // Animation complete when elapsed time reaches duration
        return elapsedTime >= DurationSeconds;
    }
    
    public void Reset()
    {
        _direction = double3.Zero;
        _distanceTraveled = 0.0;
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
