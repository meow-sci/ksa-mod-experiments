using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace mod.Animation;

/// <summary>
/// Keyframe animation for smooth interpolation between camera states.
/// Supports position/rotation lerping with optional look-at target interpolation.
/// </summary>
public class TransitionAnimation : IKeyframeAnimation
{
    // Configuration properties
    private readonly double _durationSeconds;
    private readonly EasingType _easing;
    
    // Runtime target state (set externally by KeyframeSequencePlayer)
    public double3 StartPosition { get; set; }
    public doubleQuat StartRotation { get; set; }
    public double3 EndPosition { get; set; }
    public doubleQuat EndRotation { get; set; }
    public double3? StartLookAtTarget { get; set; }
    public double3? EndLookAtTarget { get; set; }
    
    // Runtime state
    private bool _isInitialized;
    
    // Interface properties
    public string Name => "Transition";
    
    public string Description => "Smooth interpolation between positions and rotations";
    
    public double DurationSeconds => _durationSeconds;
    
    public EasingType Easing => _easing;
    
    public Func<Controller, double3>? LookAtTargetProvider { get; set; }
    
    /// <summary>
    /// Create a new transition animation.
    /// </summary>
    /// <param name="durationSeconds">Duration of the transition in seconds.</param>
    /// <param name="easing">Easing function to apply to interpolation.</param>
    public TransitionAnimation(double durationSeconds, EasingType easing = EasingType.EaseInOut)
    {
        _durationSeconds = durationSeconds;
        _easing = easing;
    }
    
    /// <summary>
    /// Helper method for sequence player to set target end state.
    /// </summary>
    /// <param name="position">Target end position.</param>
    /// <param name="rotation">Target end rotation.</param>
    /// <param name="lookAtTarget">Optional look-at target position.</param>
    public void SetEndState(double3 position, doubleQuat rotation, double3? lookAtTarget = null)
    {
        EndPosition = position;
        EndRotation = rotation;
        EndLookAtTarget = lookAtTarget;
    }
    
    public void Initialize(Controller controller, Transform3D transform)
    {
        if (!_isInitialized)
        {
            // Capture start position/rotation if not already set externally
            if (StartPosition == default)
                StartPosition = transform.PositionEcl;
            if (StartRotation == default)
                StartRotation = transform.LocalRotation;
            
            _isInitialized = true;
        }
    }
    
    public bool Update(Controller controller, Transform3D transform, double deltaTime, double elapsedTime)
    {
        // Calculate eased interpolation parameter
        double t = Math.Min(1.0, elapsedTime / _durationSeconds);
        double easedT = AnimationHelpers.ApplyEasing(t, _easing);
        
        // Interpolate position
        transform.PositionEcl = double3.Lerp(StartPosition, EndPosition, easedT);
        
        // Interpolate rotation using Slerp for smooth rotation
        transform.LocalRotation = doubleQuat.Slerp(StartRotation, EndRotation, easedT);
        
        // Handle optional look-at target interpolation
        if (StartLookAtTarget.HasValue && EndLookAtTarget.HasValue)
        {
            double3 interpolatedTarget = double3.Lerp(StartLookAtTarget.Value, EndLookAtTarget.Value, easedT);
            AnimationHelpers.LookAtTarget(transform, interpolatedTarget);
        }
        
        // Animation complete when elapsed time reaches duration
        return elapsedTime >= _durationSeconds;
    }
    
    public void Reset()
    {
        _isInitialized = false;
    }
    
    public Dictionary<string, string> GetDisplayProperties()
    {
        return new Dictionary<string, string>
        {
            ["Duration"] = $"{_durationSeconds:F2}s",
            ["Easing"] = _easing.ToString()
        };
    }
}
