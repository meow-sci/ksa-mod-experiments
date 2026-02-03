using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Loopy orbit animation that combines circular orbit with sinusoidal vertical oscillation.
/// Creates a helical path around the target with perpendicular up-down motion.
/// Uses incremental rotation each frame from CURRENT camera position.
/// </summary>
public class LoopyOrbitAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double Degrees { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double EasingPowerStart { get; }
    public double EasingPowerEnd { get; }
    public double LoopIntervalDegrees { get; }
    public double AmplitudeMeters { get; }
    
    // Runtime state
    private double3 _orbitAxis;
    private double3 _verticalAxis;
    private double3 _startOffset;
    private double _totalDegreesRotated;
    private double _lastOscillationOffset;
    private bool _isInitialized;
    
    // Interface properties
    public string Name => "Loopy Orbit";
    public string Description => "Orbit with vertical oscillation";
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    /// <summary>
    /// Create a new loopy orbit animation.
    /// </summary>
    /// <param name="degrees">Total rotation angle in degrees.</param>
    /// <param name="loopIntervalDegrees">How often to complete one up-down cycle (default 90.0).</param>
    /// <param name="amplitudeMeters">Oscillation amplitude in meters (default 50.0).</param>
    /// <param name="durationSeconds">Total duration of the animation.</param>
    /// <param name="easing">Easing function to apply to the rotation.</param>
    /// <param name="easingPowerStart">Power parameter for easing at animation start (default 3.0).</param>
    /// <param name="easingPowerEnd">Power parameter for easing at animation end (default 3.0).</param>
    public LoopyOrbitAnimation(
        double degrees, 
        double loopIntervalDegrees, 
        double amplitudeMeters, 
        double durationSeconds, 
        EasingType easing,
        double easingPowerStart = 3.0,
        double easingPowerEnd = 3.0)
    {
        Degrees = degrees;
        LoopIntervalDegrees = loopIntervalDegrees;
        AmplitudeMeters = amplitudeMeters;
        DurationSeconds = durationSeconds;
        Easing = easing;
        EasingPowerStart = easingPowerStart;
        EasingPowerEnd = easingPowerEnd;
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
            Console.WriteLine("camera-controller-override: Loopy orbit radius too small, cancelling.");
            _isInitialized = false;
            return;
        }
        
        // Calculate orbit axis (perpendicular to offset - determines rotation direction)
        _orbitAxis = AnimationHelpers.CalculateOrbitAxis(currentOffset, transform.LocalRotation);
        
        // Set vertical axis = orbit axis for perpendicular oscillation
        _verticalAxis = _orbitAxis;
        
        // Store original offset from target to camera
        _startOffset = currentOffset;
        
        _totalDegreesRotated = 0.0;
        _lastOscillationOffset = 0.0;
        _isInitialized = true;
        
        Console.WriteLine($"[LoopyOrbitAnimation] Initialize: pos={transform.PositionEcl}, target={targetPos}, radius={radius:F2}");
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
        double currentEasedProgress = AnimationHelpers.ApplyEasing(t, Easing, EasingPowerStart, EasingPowerEnd);
        
        // Snap to exactly 1.0 on completion to eliminate floating-point drift
        if (elapsedTime >= DurationSeconds)
        {
            currentEasedProgress = 1.0;
        }
        
        // Calculate TOTAL rotation angle based on absolute progress (not incremental)
        double totalAngleDegrees = Degrees * currentEasedProgress;
        double totalAngleRadians = totalAngleDegrees * Math.PI / 180.0;
        
        // Apply TOTAL rotation to ORIGINAL offset using Rodrigues' formula
        double3 k = _orbitAxis;
        double cos = Math.Cos(totalAngleRadians);
        double sin = Math.Sin(totalAngleRadians);
        double3 rotatedOffset = _startOffset * cos 
            + double3.Cross(k, _startOffset) * sin 
            + k * double3.Dot(k, _startOffset) * (1.0 - cos);
        
        // Update total rotation for logging and oscillation calculation
        _totalDegreesRotated = totalAngleDegrees;
        
        // Calculate new vertical oscillation based on total rotation
        double loopsPerRevolution = 360.0 / LoopIntervalDegrees;
        double oscillationPhase = totalAngleDegrees * loopsPerRevolution * Math.PI / 180.0;
        double currentOscillationOffset = Math.Sin(oscillationPhase) * AmplitudeMeters;
        _lastOscillationOffset = currentOscillationOffset;
        
        // Apply oscillation to the rotated offset
        double3 finalOffset = rotatedOffset + _verticalAxis * currentOscillationOffset;
        
        // Get CURRENT target position and update position relative to it
        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller);
        transform.PositionEcl = currentTargetPos + finalOffset;
        
        // Log on first frame
        if (elapsedTime < deltaTime * 1.5)
        {
            Console.WriteLine($"[LoopyOrbitAnimation] First frame: elapsed={elapsedTime:F4}, totalAngle={totalAngleDegrees:F4}°");
        }
        
        // Maintain look-at behavior
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? currentTargetPos;
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);
        
        // Log on completion
        bool isComplete = elapsedTime >= DurationSeconds;
        if (isComplete)
        {
            Console.WriteLine($"[LoopyOrbitAnimation] Complete: totalDegreesRotated={_totalDegreesRotated:F2}°, finalPos={transform.PositionEcl}");
        }
        
        return isComplete;
    }
    
    public void Reset()
    {
        _orbitAxis = double3.Zero;
        _verticalAxis = double3.Zero;
        _startOffset = double3.Zero;
        _totalDegreesRotated = 0.0;
        _lastOscillationOffset = 0.0;
        _isInitialized = false;
    }
    
    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            { "Degrees", $"{Degrees:F1}°" },
            { "Loop Interval", $"{LoopIntervalDegrees:F1}°" },
            { "Amplitude", $"{AmplitudeMeters:F1}m" },
            { "Duration", $"{DurationSeconds:F1}s" },
            { "Easing", Easing.ToString() },
            { "Easing Power (Start)", $"{EasingPowerStart:F1}" },
            { "Easing Power (End)", $"{EasingPowerEnd:F1}" }
        };
    }
}
