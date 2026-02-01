using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Loopy orbit animation that combines circular orbit with sinusoidal vertical oscillation.
/// Creates a helical path around the target with perpendicular up-down motion.
/// Extracted from Patcher.cs loopy orbit animation logic.
/// </summary>
public class LoopyOrbitAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double Degrees { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    public double LoopIntervalDegrees { get; }
    public double AmplitudeMeters { get; }
    
    // Runtime state
    private double3 _startPosition;
    private doubleQuat _startRotation;
    private double3 _targetPosition;
    private double3 _orbitAxis;
    private double3 _verticalAxis;
    private double3 _startOffset;
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
    public LoopyOrbitAnimation(
        double degrees, 
        double loopIntervalDegrees, 
        double amplitudeMeters, 
        double durationSeconds, 
        EasingType easing)
    {
        Degrees = degrees;
        LoopIntervalDegrees = loopIntervalDegrees;
        AmplitudeMeters = amplitudeMeters;
        DurationSeconds = durationSeconds;
        Easing = easing;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        // Capture starting state (same logic as Patcher.cs)
        _startPosition = transform.PositionEcl;
        _startRotation = transform.LocalRotation;
        _targetPosition = AnimationHelpers.GetTargetPosition(controller);
        _startOffset = _startPosition - _targetPosition;
        
        // Validate radius
        double radius = _startOffset.Length();
        if (radius < 0.01)
        {
            Console.WriteLine("camera-controller-override: Loopy orbit radius too small, cancelling.");
            _isInitialized = false;
            return;
        }
        
        // Calculate orbit axis perpendicular to the offset
        _orbitAxis = AnimationHelpers.CalculateOrbitAxis(_startOffset, _startRotation);
        
        // Set vertical axis = orbit axis for perpendicular oscillation
        _verticalAxis = _orbitAxis;
        
        _isInitialized = true;
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Skip animation if initialization failed
        if (!_isInitialized)
        {
            return true;
        }
        
        // Calculate eased progress and angle
        double t = Math.Min(1.0, elapsedTime / DurationSeconds);
        double easedT = AnimationHelpers.ApplyEasing(t, Easing);
        double angleDegrees = Degrees * easedT;
        double angleRadians = angleDegrees * Math.PI / 180.0;
        
        // Base orbit using Rodrigues' rotation formula (exact logic from Patcher.cs)
        double3 startOffset = _startPosition - _targetPosition;
        double3 k = _orbitAxis;
        double cos = Math.Cos(angleRadians);
        double sin = Math.Sin(angleRadians);
        double3 baseOrbitOffset = startOffset * cos + double3.Cross(k, startOffset) * sin + k * double3.Dot(k, startOffset) * (1.0 - cos);
        
        // Vertical oscillation: sin wave based on current angle (exact logic from Patcher.cs)
        double loopsPerRevolution = 360.0 / LoopIntervalDegrees;
        double oscillationPhase = angleDegrees * loopsPerRevolution * Math.PI / 180.0;
        double oscillationAmount = Math.Sin(oscillationPhase) * AmplitudeMeters;
        double3 verticalOscillation = _verticalAxis * oscillationAmount;
        
        // Combined position (exact logic from Patcher.cs)
        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _targetPosition);
        transform.PositionEcl = currentTargetPos + baseOrbitOffset + verticalOscillation;
        
        // Maintain look-at behavior
        double3 lookAtTarget = LookAtTargetProvider?.Invoke(controller) ?? currentTargetPos;
        AnimationHelpers.LookAtTarget(transform, lookAtTarget);
        
        // Animation complete when elapsed time reaches duration
        return elapsedTime >= DurationSeconds;
    }
    
    public void Reset()
    {
        _startPosition = double3.Zero;
        _startRotation = default;
        _targetPosition = double3.Zero;
        _orbitAxis = double3.Zero;
        _verticalAxis = double3.Zero;
        _startOffset = double3.Zero;
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
            { "Easing", Easing.ToString() }
        };
    }
}
