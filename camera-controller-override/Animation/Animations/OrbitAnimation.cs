using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation.Animations;

/// <summary>
/// Circular orbit animation that rotates the camera around a target.
/// Extracted from Patcher.cs orbit animation logic.
/// Uses Rodrigues' rotation formula for precise rotation.
/// </summary>
public class OrbitAnimation : IKeyframeAnimation
{
    // Configuration properties
    public double Degrees { get; }
    public double DurationSeconds { get; }
    public EasingType Easing { get; }
    
    // Runtime state
    private double3 _startPosition;
    private doubleQuat _startRotation;
    private double3 _targetPosition;
    private double3 _orbitAxis;
    private double3 _startOffset;
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
        // Capture starting state (same logic as Patcher.cs)
        _startPosition = transform.PositionEcl;
        _startRotation = transform.LocalRotation;
        _targetPosition = AnimationHelpers.GetTargetPosition(controller);
        _startOffset = _startPosition - _targetPosition;
        
        // Validate radius
        double radius = _startOffset.Length();
        if (radius < 0.01)
        {
            Console.WriteLine("camera-controller-override: Orbit radius too small, cancelling.");
            _isInitialized = false;
            return;
        }
        
        // Calculate orbit axis perpendicular to the offset
        _orbitAxis = AnimationHelpers.CalculateOrbitAxis(_startOffset, _startRotation);
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
        double angleRadians = Degrees * easedT * Math.PI / 180.0;
        
        // Apply Rodrigues' rotation formula (exact logic from Patcher.cs)
        double3 startOffset = _startPosition - _targetPosition;
        double3 k = _orbitAxis;
        double cos = Math.Cos(angleRadians);
        double sin = Math.Sin(angleRadians);
        double3 rotatedOffset = startOffset * cos + double3.Cross(k, startOffset) * sin + k * double3.Dot(k, startOffset) * (1.0 - cos);
        
        // Update position
        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _targetPosition);
        transform.PositionEcl = currentTargetPos + rotatedOffset;
        
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
        _startOffset = double3.Zero;
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
