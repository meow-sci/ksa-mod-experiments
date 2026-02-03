using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Zoom out animation combined with spiral rotation around the look-at axis.
/// Moves camera away from target while rotating around the camera-to-target vector,
/// creating a corkscrew movement effect.
/// </summary>
public class SpiralZoomOutAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double SpeedMetersPerSecond { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double SpiralDegrees { get; }
    
    // Runtime state
    private double _distanceTraveled;
    private double _lastEasedProgress;
    private double _totalDegreesRotated;
    
    // Interface properties
    public string Name => "Spiral Zoom Out";
    public string Description => "Zoom away from target with spiral rotation";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    /// <summary>
    /// Create a new spiral zoom out animation.
    /// </summary>
    /// <param name="speedMetersPerSecond">Movement speed away from target.</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to both movement and rotation.</param>
    /// <param name="spiralDegrees">Total rotation degrees during zoom (positive = clockwise when looking at target).</param>
    public SpiralZoomOutAnimation(double speedMetersPerSecond, double durationSeconds, EasingType easing, double spiralDegrees)
    {
        SpeedMetersPerSecond = speedMetersPerSecond;
        DurationSeconds = durationSeconds;
        Easing = easing;
        SpiralDegrees = spiralDegrees;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Reset runtime state
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
        _totalDegreesRotated = 0.0;
        
        Console.WriteLine($"[SpiralZoomOutAnimation] Initialize: pos={transform.PositionEcl}, spiralDegrees={SpiralDegrees:F1}°");
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
        
        // Get CURRENT target position
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
        
        // === ZOOM OUT COMPONENT ===
        // Direction is AWAY from target (opposite of towardTarget)
        double3 zoomDirection = double3.Normalize(-towardTarget);
        
        // Apply zoom displacement for this frame
        double frameDistance = totalDistance * frameProgress;
        double3 displacement = zoomDirection * frameDistance;
        transform.PositionEcl += displacement;
        _distanceTraveled += frameDistance;
        
        // === SPIRAL COMPONENT ===
        // Spiral axis is the direction TOWARD target (the look-at vector)
        double3 spiralAxis = double3.Normalize(towardTarget);
        
        // Calculate frame rotation angle
        double frameAngleDegrees = SpiralDegrees * frameProgress;
        double frameAngleRadians = frameAngleDegrees * Math.PI / 180.0;
        _totalDegreesRotated += frameAngleDegrees;
        
        // Apply Rodrigues' rotation formula to rotate camera's up vector around spiral axis
        double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
        
        double3 k = spiralAxis;
        double cos = Math.Cos(frameAngleRadians);
        double sin = Math.Sin(frameAngleRadians);
        
        // Rotate the up vector around the spiral axis
        double3 rotatedUp = currentUp * cos 
            + double3.Cross(k, currentUp) * sin 
            + k * double3.Dot(k, currentUp) * (1.0 - cos);
        
        // Log on first frame
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[SpiralZoomOutAnimation] First frame: elapsed={elapsedTime:F4}, frameProgress={frameProgress:F6}, frameAngle={frameAngleDegrees:F4}°");
        }
        
        // Maintain look-at behavior with spiraled rotation
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? targetPos;
        double3 forward = double3.Normalize(lookAtTarget - transform.PositionEcl);
        transform.LocalRotation = Camera.LookAtRotation(forward, rotatedUp);
        
        // Log on completion
        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            Console.WriteLine($"[SpiralZoomOutAnimation] Complete: distance={_distanceTraveled:F2}m, rotation={_totalDegreesRotated:F2}°, finalPos={transform.PositionEcl}");
        }
        
        return isComplete;
    }
    
    public void Reset()
    {
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
        _totalDegreesRotated = 0.0;
    }
    
    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            { "Speed", $"{SpeedMetersPerSecond:F1} m/s" },
            { "Duration", $"{DurationSeconds:F1}s" },
            { "Spiral", $"{SpiralDegrees:F0}°" },
            { "Easing", Easing.ToString() }
        };
    }
}
