using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Combines zoom-in movement toward the target with spiral rotation around the look-at axis.
/// Creates a "corkscrew" effect as the camera moves closer to the target while spinning.
/// </summary>
public class SpiralZoomInAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double SpeedMetersPerSecond { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }
    public double SpiralDegrees { get; }
    
    // Runtime state
    private double _distanceTraveled;
    private double _lastEasedProgress;
    private double _totalDegreesRotated;
    
    // Interface properties
    public string Name => "Spiral Zoom In";
    public string Description => "Zoom toward target with spiral rotation";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    // Minimum distance safeguard (1 meter from target)
    private const double MinimumDistance = 1.0;
    
    /// <summary>
    /// Create a new spiral zoom in animation.
    /// </summary>
    /// <param name="speedMetersPerSecond">Movement speed in meters per second.</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to both movement and rotation.</param>
    /// <param name="spiralDegrees">Total rotation degrees during zoom (positive = clockwise when looking at target).</param>
    /// <param name="easingPowerStart">Power parameter for easing at animation start (default 3.0).</param>
    /// <param name="easingPowerEnd">Power parameter for easing at animation end (default 3.0).</param>
    public SpiralZoomInAnimation(double speedMetersPerSecond, double durationSeconds, EasingType easing, double spiralDegrees, double easingPowerStart = 3.0, double easingPowerEnd = 3.0)
    {
        SpeedMetersPerSecond = speedMetersPerSecond;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
        SpiralDegrees = spiralDegrees;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Reset all runtime state
        _distanceTraveled = 0.0;
        _lastEasedProgress = 0.0;
        _totalDegreesRotated = 0.0;
        
        Console.WriteLine($"[SpiralZoomInAnimation] Initialize: pos={transform.PositionEcl}, spiralDegrees={SpiralDegrees}");
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Calculate total movement distance for the entire animation
        double totalDistance = SpeedMetersPerSecond * DurationSeconds;
        
        // Get current eased progress (0.0 to 1.0)
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double currentEasedProgress = AnimationHelpers.ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd);
        
        // Snap to exactly 1.0 on completion to eliminate floating-point drift
        if (elapsedTime >= DurationSeconds)
        {
            currentEasedProgress = 1.0;
        }
        
        // Calculate how much progress we should make THIS frame
        double frameProgress = currentEasedProgress - _lastEasedProgress;
        _lastEasedProgress = currentEasedProgress;
        
        // === ZOOM COMPONENT (from ZoomInAnimation) ===
        
        // Get CURRENT target position and calculate direction toward it
        double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
        double3 towardTarget = targetPos - transform.PositionEcl;
        double distanceToTarget = towardTarget.Length();
        
        // Handle edge case: camera is very close to target
        if (distanceToTarget < MinimumDistance)
        {
            Console.WriteLine($"[SpiralZoomInAnimation] Stopping: Too close to target (distance={distanceToTarget:F4}m)");
            return true; // Complete animation early
        }
        
        // Calculate displacement for this frame
        double3 direction = double3.Normalize(towardTarget);
        double frameDistance = totalDistance * frameProgress;
        
        // Clamp displacement to not overshoot minimum distance
        if (distanceToTarget - frameDistance < MinimumDistance)
        {
            frameDistance = distanceToTarget - MinimumDistance;
        }
        
        double3 displacement = direction * frameDistance;
        transform.PositionEcl += displacement;
        _distanceTraveled += displacement.Length();
        
        // === SPIRAL COMPONENT (rotate camera orientation around look-at axis) ===
        
        // Calculate the angle to rotate THIS frame
        double frameAngleDegrees = SpiralDegrees * frameProgress;
        double frameAngleRadians = frameAngleDegrees * Math.PI / 180.0;
        
        // Calculate look-at direction (from camera to target)
        double3 lookDirection = targetPos - transform.PositionEcl;
        double lookDirectionLength = lookDirection.Length();
        
        if (lookDirectionLength > 0.0001)
        {
            // Normalize to get spiral axis (the look-at direction)
            double3 spiralAxis = double3.Normalize(lookDirection);
            
            // Get current up vector
            double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
            
            // Rotate up vector around spiral axis using Rodrigues' formula
            double3 k = spiralAxis;
            double cos = Math.Cos(frameAngleRadians);
            double sin = Math.Sin(frameAngleRadians);
            double3 rotatedUp = currentUp * cos 
                + double3.Cross(k, currentUp) * sin 
                + k * double3.Dot(k, currentUp) * (1.0 - cos);
            
            // Apply rotation with rotated up vector
            transform.LocalRotation = Camera.LookAtRotation(lookDirection, rotatedUp);
            
            _totalDegreesRotated += frameAngleDegrees;
        }
        
        // Log on first frame to verify correct initialization
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[SpiralZoomInAnimation] First frame: elapsed={elapsedTime:F4}, frameProgress={frameProgress:F6}, displacement={displacement.Length():F4}, rotation={frameAngleDegrees:F4}°");
        }
        
        // Log on completion
        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            Console.WriteLine($"[SpiralZoomInAnimation] Complete: totalDistanceTraveled={_distanceTraveled:F2}, totalDegreesRotated={_totalDegreesRotated:F2}°, finalPos={transform.PositionEcl}");
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
            { "Easing", Easing.ToString() },
            { "Easing Power (Start)", $"{EasingPowerStart:F1}" },
            { "Easing Power (End)", $"{EasingPowerEnd:F1}" }
        };
    }
}
