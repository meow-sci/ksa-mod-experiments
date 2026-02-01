using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

public enum EasingType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("camera-controller-override");

    // Linear animation state
    private static bool _isAnimationEnabled = false;
    private static bool _isAnimationActive = false;
    private static double _animationElapsedTime = 0.0;
    private static double3 _animationDirection;
    private static double _animationSpeedMetersPerSecond = 25.0;
    private static double _animationDurationSeconds = 5.0;
    private static EasingType _mainAnimationEasingType = EasingType.EaseOut;
    
    // Linear lerp back state
    private static bool _lerpBackEnabled = true;
    private static bool _isLerpingBack = false;
    private static double _lerpBackElapsedTime = 0.0;
    private static double _lerpBackDurationSeconds = 3.0;
    private static EasingType _lerpBackEasingType = EasingType.EaseInOut;
    private static double _distanceTraveledForward = 0.0;
    private static double _distanceTraveledReturn = 0.0;
    
    // Orbit animation state
    private static bool _isOrbitAnimationEnabled = false;
    private static bool _isOrbitAnimationActive = false;
    private static double _orbitAnimationElapsedTime = 0.0;
    private static double _orbitDegrees = 270.0;
    private static double _orbitDurationSeconds = 5.0;
    private static EasingType _orbitEasingType = EasingType.EaseOut;
    private static double3 _orbitStartPosition;
    private static doubleQuat _orbitStartRotation;
    private static double3 _orbitTargetPosition;
    private static double3 _orbitAxis;
    
    // Orbit lerp back state
    private static bool _orbitLerpBackEnabled = true;
    private static bool _isOrbitLerpingBack = false;
    private static double _orbitLerpBackElapsedTime = 0.0;
    private static double _orbitLerpBackDurationSeconds = 3.0;
    private static EasingType _orbitLerpBackEasingType = EasingType.EaseInOut;
    private static double3 _orbitLerpStartOffset;
    private static double3 _orbitLerpEndOffset;
    
    // Loopy orbit animation state
    private static bool _isLoopyOrbitEnabled = false;
    private static bool _isLoopyOrbitActive = false;
    private static double _loopyOrbitElapsedTime = 0.0;
    private static double _loopyOrbitDegrees = 270.0;           // Total orbit angle
    private static double _loopyOrbitDurationSeconds = 8.0;      // Longer default for complex motion
    private static EasingType _loopyOrbitEasingType = EasingType.EaseOut;
    private static double3 _loopyOrbitStartPosition;
    private static doubleQuat _loopyOrbitStartRotation;
    private static double3 _loopyOrbitTargetPosition;
    private static double3 _loopyOrbitAxis;                      // Main orbit axis
    private static double3 _loopyOrbitVerticalAxis;              // Perpendicular oscillation axis
    private static double _loopyLoopIntervalDegrees = 90.0;      // How often to complete one up-down cycle
    private static double _loopyAmplitudeMeters = 50.0;          // How far up/down to oscillate
    
    // Loopy orbit lerp back state
    private static bool _loopyLerpBackEnabled = true;
    private static bool _isLoopyLerpingBack = false;
    private static double _loopyLerpBackElapsedTime = 0.0;
    private static double _loopyLerpBackDurationSeconds = 3.0;
    private static EasingType _loopyLerpBackEasingType = EasingType.EaseInOut;
    private static double3 _loopyLerpStartOffset;
    private static double3 _loopyLerpEndOffset;

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("camera-controller-override");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error removing patches: {ex.Message}");
        }
    }
    
    private static double ApplyEasing(double t, EasingType easingType)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return easingType switch
        {
            EasingType.EaseIn => t * t * t,
            EasingType.EaseOut => 1.0 - Math.Pow(1.0 - t, 3),
            EasingType.EaseInOut => t * t * (3.0 - 2.0 * t),
            _ => t
        };
    }
    
    private static double3 GetTargetPosition(Controller controller, double3 fallback = default)
        => controller?.Camera?.Following?.GetPositionEcl() ?? fallback;
    
    private static void LookAtTarget(Transform3D transform, double3 targetPos)
    {
        double3 lookDirection = targetPos - transform.PositionEcl;
        if (lookDirection.LengthSquared() > 0.0001)
        {
            double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
            transform.LocalRotation = Camera.LookAtRotation(lookDirection, currentUp);
        }
    }
    
    private static double GetEasedFrameProgress(double elapsed, double duration, double deltaTime, EasingType easingType)
    {
        double t = Math.Min(1.0, elapsed / duration);
        double lastT = Math.Max(0.0, (elapsed - deltaTime) / duration);
        return ApplyEasing(t, easingType) - ApplyEasing(lastT, easingType);
    }
    
    private static double3 CalculateOrbitAxis(double3 startOffset, doubleQuat startRotation)
    {
        double3 startUp = double3.UnitY.Transform(startRotation);
        if (startUp.LengthSquared() < 0.00000001) startUp = double3.UnitY;
        
        double3 right = double3.Cross(startUp, startOffset);
        if (right.LengthSquared() < 0.0001)
        {
            double3 offsetDir = double3.Normalize(startOffset);
            double3 fallback = Math.Abs(double3.Dot(offsetDir, double3.UnitY)) < 0.99 ? double3.UnitY : double3.UnitX;
            right = double3.Cross(fallback, startOffset);
        }
        return double3.Normalize(double3.Cross(startOffset, right));
    }
    


    public static bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set
        {
            _isAnimationEnabled = value;
            if (!value && (_isAnimationActive || _isLerpingBack))
            {
                _isAnimationActive = false;
                _isLerpingBack = false;
                _animationElapsedTime = 0.0;
                _lerpBackElapsedTime = 0.0;
            }
        }
    }

    public static bool IsAnimationActive => _isAnimationActive;
    public static double AnimationElapsedTime => _animationElapsedTime;
    public static double AnimationSpeedMetersPerSecond
    {
        get => _animationSpeedMetersPerSecond;
        set => _animationSpeedMetersPerSecond = Math.Max(1.0, value);
    }
    public static double AnimationDurationSeconds
    {
        get => _animationDurationSeconds;
        set => _animationDurationSeconds = Math.Clamp(value, 1.0, 30.0);
    }
    public static EasingType MainAnimationEasingType { get => _mainAnimationEasingType; set => _mainAnimationEasingType = value; }
    
    public static bool LerpBackEnabled { get => _lerpBackEnabled; set => _lerpBackEnabled = value; }
    public static bool IsLerpingBack => _isLerpingBack;
    public static double LerpBackElapsedTime => _lerpBackElapsedTime;
    public static double DistanceTraveledForward => _distanceTraveledForward;
    public static double DistanceTraveledReturn => _distanceTraveledReturn;
    public static double LerpBackProgress => _distanceTraveledForward > 0 ? Math.Min(1.0, _distanceTraveledReturn / _distanceTraveledForward) : 0.0;
    public static double LerpBackDurationSeconds
    {
        get => _lerpBackDurationSeconds;
        set => _lerpBackDurationSeconds = Math.Clamp(value, 1.0, 10.0);
    }
    public static EasingType LerpBackEasingType { get => _lerpBackEasingType; set => _lerpBackEasingType = value; }
    
    public static bool IsOrbitAnimationEnabled
    {
        get => _isOrbitAnimationEnabled;
        set
        {
            _isOrbitAnimationEnabled = value;
            if (!value && (_isOrbitAnimationActive || _isOrbitLerpingBack))
            {
                _isOrbitAnimationActive = false;
                _isOrbitLerpingBack = false;
                _orbitAnimationElapsedTime = 0.0;
                _orbitLerpBackElapsedTime = 0.0;
            }
        }
    }
    
    public static bool IsOrbitAnimationActive => _isOrbitAnimationActive;
    public static double OrbitAnimationElapsedTime => _orbitAnimationElapsedTime;
    public static bool IsOrbitLerpingBack => _isOrbitLerpingBack;
    public static double OrbitLerpBackElapsedTime => _orbitLerpBackElapsedTime;
    public static double OrbitDegrees
    {
        get => _orbitDegrees;
        set => _orbitDegrees = Math.Clamp(value, 90.0, 1080.0);
    }
    public static double OrbitDurationSeconds
    {
        get => _orbitDurationSeconds;
        set => _orbitDurationSeconds = Math.Clamp(value, 1.0, 30.0);
    }
    public static EasingType OrbitEasingType { get => _orbitEasingType; set => _orbitEasingType = value; }
    public static bool OrbitLerpBackEnabled { get => _orbitLerpBackEnabled; set => _orbitLerpBackEnabled = value; }
    public static double OrbitLerpBackDurationSeconds
    {
        get => _orbitLerpBackDurationSeconds;
        set => _orbitLerpBackDurationSeconds = Math.Clamp(value, 1.0, 10.0);
    }
    public static EasingType OrbitLerpBackEasingType { get => _orbitLerpBackEasingType; set => _orbitLerpBackEasingType = value; }
    
    public static bool IsLoopyOrbitEnabled
    {
        get => _isLoopyOrbitEnabled;
        set
        {
            _isLoopyOrbitEnabled = value;
            if (!value && (_isLoopyOrbitActive || _isLoopyLerpingBack))
            {
                _isLoopyOrbitActive = false;
                _isLoopyLerpingBack = false;
                _loopyOrbitElapsedTime = 0.0;
                _loopyLerpBackElapsedTime = 0.0;
            }
        }
    }
    
    public static bool IsLoopyOrbitActive => _isLoopyOrbitActive;
    public static double LoopyOrbitElapsedTime => _loopyOrbitElapsedTime;
    public static bool IsLoopyLerpingBack => _isLoopyLerpingBack;
    public static double LoopyLerpBackElapsedTime => _loopyLerpBackElapsedTime;
    public static double LoopyOrbitDegrees
    {
        get => _loopyOrbitDegrees;
        set => _loopyOrbitDegrees = Math.Clamp(value, 90.0, 1080.0);
    }
    public static double LoopyOrbitDurationSeconds
    {
        get => _loopyOrbitDurationSeconds;
        set => _loopyOrbitDurationSeconds = Math.Clamp(value, 1.0, 60.0);
    }
    public static EasingType LoopyOrbitEasingType { get => _loopyOrbitEasingType; set => _loopyOrbitEasingType = value; }
    public static bool LoopyLerpBackEnabled { get => _loopyLerpBackEnabled; set => _loopyLerpBackEnabled = value; }
    public static double LoopyLerpBackDurationSeconds
    {
        get => _loopyLerpBackDurationSeconds;
        set => _loopyLerpBackDurationSeconds = Math.Clamp(value, 1.0, 10.0);
    }
    public static EasingType LoopyLerpBackEasingType { get => _loopyLerpBackEasingType; set => _loopyLerpBackEasingType = value; }
    public static double LoopyLoopIntervalDegrees
    {
        get => _loopyLoopIntervalDegrees;
        set => _loopyLoopIntervalDegrees = Math.Clamp(value, 30.0, 180.0);
    }
    public static double LoopyAmplitudeMeters
    {
        get => _loopyAmplitudeMeters;
        set => _loopyAmplitudeMeters = Math.Clamp(value, 1.0, 500.0);
    }

    [HarmonyPatch(typeof(OrbitController), "OnFrame")]
    [HarmonyPrefix]
    private static bool OrbitController_OnFrame_Prefix(OrbitController __instance, double inDeltaTime, Transform3D ___Transform)
        => HandleOnFramePrefix(__instance, inDeltaTime, ___Transform);

    [HarmonyPatch(typeof(FlyController), "OnFrame")]
    [HarmonyPrefix]
    private static bool FlyController_OnFrame_Prefix(FlyController __instance, double inDeltaTime, Transform3D ___Transform)
        => HandleOnFramePrefix(__instance, inDeltaTime, ___Transform);

    private static bool HandleOnFramePrefix(Controller controller, double deltaTime, Transform3D transform)
    {
        try
        {
            // Handle orbit lerp back
            if (_isOrbitLerpingBack)
            {
                double3 currentTargetPos = GetTargetPosition(controller, _orbitTargetPosition);
                double t = _orbitLerpBackElapsedTime / _orbitLerpBackDurationSeconds;
                double easedT = ApplyEasing(t, _orbitLerpBackEasingType);
                
                double3 currentOffset = double3.Lerp(_orbitLerpEndOffset, _orbitLerpStartOffset, easedT);
                transform.PositionEcl = currentTargetPos + currentOffset;
                LookAtTarget(transform, currentTargetPos);
                
                _orbitLerpBackElapsedTime += deltaTime;
                if (_orbitLerpBackElapsedTime >= _orbitLerpBackDurationSeconds)
                {
                    _isOrbitAnimationEnabled = false;
                    _isOrbitAnimationActive = false;
                    _isOrbitLerpingBack = false;
                }
                return false;
            }
            
            // Handle loopy orbit lerp back
            if (_isLoopyLerpingBack)
            {
                double3 currentTargetPos = GetTargetPosition(controller, _loopyOrbitTargetPosition);
                double t = _loopyLerpBackElapsedTime / _loopyLerpBackDurationSeconds;
                double easedT = ApplyEasing(t, _loopyLerpBackEasingType);
                
                double3 currentOffset = double3.Lerp(_loopyLerpEndOffset, _loopyLerpStartOffset, easedT);
                transform.PositionEcl = currentTargetPos + currentOffset;
                LookAtTarget(transform, currentTargetPos);
                
                _loopyLerpBackElapsedTime += deltaTime;
                if (_loopyLerpBackElapsedTime >= _loopyLerpBackDurationSeconds)
                {
                    _isLoopyOrbitEnabled = false;
                    _isLoopyOrbitActive = false;
                    _isLoopyLerpingBack = false;
                }
                return false;
            }
            
            // Handle loopy orbit animation
            if (_isLoopyOrbitEnabled)
            {
                if (transform == null) return true;
                
                // Initialize on first frame
                if (!_isLoopyOrbitActive)
                {
                    _isLoopyOrbitActive = true;
                    _loopyOrbitStartPosition = transform.PositionEcl;
                    _loopyOrbitStartRotation = transform.LocalRotation;
                    _loopyOrbitElapsedTime = 0.0;
                    _loopyOrbitTargetPosition = GetTargetPosition(controller);
                    _loopyLerpStartOffset = _loopyOrbitStartPosition - _loopyOrbitTargetPosition;
                    
                    double radius = _loopyLerpStartOffset.Length();
                    if (radius < 0.01)
                    {
                        Console.WriteLine("camera-controller-override: Loopy orbit radius too small, cancelling.");
                        _isLoopyOrbitEnabled = false;
                        _isLoopyOrbitActive = false;
                        return true;
                    }
                    
                    _loopyOrbitAxis = CalculateOrbitAxis(_loopyLerpStartOffset, _loopyOrbitStartRotation);
                    _loopyOrbitVerticalAxis = _loopyOrbitAxis;
                }
                
                _loopyOrbitElapsedTime += deltaTime;
                double t = Math.Min(1.0, _loopyOrbitElapsedTime / _loopyOrbitDurationSeconds);
                double easedT = ApplyEasing(t, _loopyOrbitEasingType);
                double angleDegrees = _loopyOrbitDegrees * easedT;
                double angleRadians = angleDegrees * Math.PI / 180.0;
                
                // Base orbit using Rodrigues' rotation formula
                double3 startOffset = _loopyOrbitStartPosition - _loopyOrbitTargetPosition;
                double3 k = _loopyOrbitAxis;
                double cos = Math.Cos(angleRadians);
                double sin = Math.Sin(angleRadians);
                double3 baseOrbitOffset = startOffset * cos + double3.Cross(k, startOffset) * sin + k * double3.Dot(k, startOffset) * (1.0 - cos);
                
                // Vertical oscillation: sin wave based on current angle
                double loopsPerRevolution = 360.0 / _loopyLoopIntervalDegrees;
                double oscillationPhase = angleDegrees * loopsPerRevolution * Math.PI / 180.0;
                double oscillationAmount = Math.Sin(oscillationPhase) * _loopyAmplitudeMeters;
                double3 verticalOscillation = _loopyOrbitVerticalAxis * oscillationAmount;
                
                // Combined position
                double3 currentTargetPos = GetTargetPosition(controller, _loopyOrbitTargetPosition);
                transform.PositionEcl = currentTargetPos + baseOrbitOffset + verticalOscillation;
                LookAtTarget(transform, currentTargetPos);
                
                if (_loopyOrbitElapsedTime >= _loopyOrbitDurationSeconds)
                {
                    if (_loopyLerpBackEnabled)
                    {
                        _loopyLerpEndOffset = transform.PositionEcl - currentTargetPos;
                        _isLoopyLerpingBack = true;
                        _isLoopyOrbitActive = false;
                        _loopyLerpBackElapsedTime = 0.0;
                    }
                    else
                    {
                        _isLoopyOrbitEnabled = false;
                        _isLoopyOrbitActive = false;
                    }
                }
                return false;
            }
            
            // Handle orbit animation
            if (_isOrbitAnimationEnabled)
            {
                if (transform == null) return true;
                
                // Initialize on first frame
                if (!_isOrbitAnimationActive)
                {
                    _isOrbitAnimationActive = true;
                    _orbitStartPosition = transform.PositionEcl;
                    _orbitStartRotation = transform.LocalRotation;
                    _orbitAnimationElapsedTime = 0.0;
                    _orbitTargetPosition = GetTargetPosition(controller);
                    _orbitLerpStartOffset = _orbitStartPosition - _orbitTargetPosition;
                    
                    double radius = (_orbitTargetPosition - _orbitStartPosition).Length();
                    if (radius < 0.01)
                    {
                        Console.WriteLine("camera-controller-override: Orbit radius too small, cancelling.");
                        _isOrbitAnimationEnabled = false;
                        _isOrbitAnimationActive = false;
                        return true;
                    }
                    _orbitAxis = CalculateOrbitAxis(_orbitLerpStartOffset, _orbitStartRotation);
                }
                
                _orbitAnimationElapsedTime += deltaTime;
                double t = Math.Min(1.0, _orbitAnimationElapsedTime / _orbitDurationSeconds);
                double easedT = ApplyEasing(t, _orbitEasingType);
                double angleRadians = _orbitDegrees * easedT * Math.PI / 180.0;
                
                // Rodrigues' rotation formula
                double3 startOffset = _orbitStartPosition - _orbitTargetPosition;
                double3 k = _orbitAxis;
                double cos = Math.Cos(angleRadians);
                double sin = Math.Sin(angleRadians);
                double3 rotatedOffset = startOffset * cos + double3.Cross(k, startOffset) * sin + k * double3.Dot(k, startOffset) * (1.0 - cos);
                
                double3 currentTargetPos = GetTargetPosition(controller, _orbitTargetPosition);
                transform.PositionEcl = currentTargetPos + rotatedOffset;
                LookAtTarget(transform, currentTargetPos);
                
                if (_orbitAnimationElapsedTime >= _orbitDurationSeconds)
                {
                    if (_orbitLerpBackEnabled)
                    {
                        _orbitLerpEndOffset = transform.PositionEcl - currentTargetPos;
                        _isOrbitLerpingBack = true;
                        _isOrbitAnimationActive = false;
                        _orbitLerpBackElapsedTime = 0.0;
                    }
                    else
                    {
                        _isOrbitAnimationEnabled = false;
                        _isOrbitAnimationActive = false;
                    }
                }
                return false;
            }
            
            // Handle linear lerp back
            if (_isLerpingBack)
            {
                double easedFrameProgress = GetEasedFrameProgress(_lerpBackElapsedTime, _lerpBackDurationSeconds, deltaTime, _lerpBackEasingType);
                double3 returnDisplacement = -_animationDirection * (_distanceTraveledForward * easedFrameProgress);
                transform.PositionEcl += returnDisplacement;
                LookAtTarget(transform, GetTargetPosition(controller));
                
                _distanceTraveledReturn += returnDisplacement.Length();
                _lerpBackElapsedTime += deltaTime;
                
                if (_distanceTraveledReturn >= _distanceTraveledForward || _lerpBackElapsedTime >= _lerpBackDurationSeconds)
                {
                    _isAnimationEnabled = false;
                    _isAnimationActive = false;
                    _isLerpingBack = false;
                }
                return false;
            }
            
            // If no animation enabled, run original
            if (!_isAnimationEnabled) return true;
            if (transform == null) return true;
            
            // Initialize linear animation on first frame
            if (!_isAnimationActive)
            {
                _isAnimationActive = true;
                _distanceTraveledForward = 0.0;
                _animationElapsedTime = 0.0;
                
                double3 targetPos = GetTargetPosition(controller);
                double3 towardTarget = targetPos - transform.PositionEcl;
                _animationDirection = double3.Normalize(-towardTarget);
            }
            
            _animationElapsedTime += deltaTime;
            double totalDistance = _animationSpeedMetersPerSecond * _animationDurationSeconds;
            double frameProgress = GetEasedFrameProgress(_animationElapsedTime, _animationDurationSeconds, deltaTime, _mainAnimationEasingType);
            double3 displacement = _animationDirection * (totalDistance * frameProgress);
            transform.PositionEcl += displacement;
            LookAtTarget(transform, GetTargetPosition(controller));
            _distanceTraveledForward += displacement.Length();
            
            if (_animationElapsedTime >= _animationDurationSeconds)
            {
                if (_lerpBackEnabled)
                {
                    _isLerpingBack = true;
                    _isAnimationActive = false;
                    _lerpBackElapsedTime = 0.0;
                    _distanceTraveledReturn = 0.0;
                }
                else
                {
                    _isAnimationEnabled = false;
                    _isAnimationActive = false;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
            return true;
        }
    }
}
