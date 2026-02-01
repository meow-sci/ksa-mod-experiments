using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using mod.Animation;
using mod.Animation.Animations;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("camera-controller-override");

    // Keyframe sequence player
    private static KeyframeSequencePlayer _sequencePlayer = new KeyframeSequencePlayer();

    // Standalone animation instances
    private static ZoomOutAnimation? _standaloneZoomOut = null;
    private static OrbitAnimation? _standaloneOrbit = null;
    private static LoopyOrbitAnimation? _standaloneLoopyOrbit = null;

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
    private static double3 _orbitTargetPosition;
    
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
    private static double3 _loopyOrbitTargetPosition;
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
    
    public static KeyframeSequencePlayer SequencePlayer => _sequencePlayer;

    public static bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set
        {
            _isAnimationEnabled = value;
            if (value)
            {
                if (_sequencePlayer.State == PlaybackState.Playing)
                {
                    _sequencePlayer.Stop();
                }
                // Create new animation instance with current settings
                _standaloneZoomOut = new ZoomOutAnimation(
                    _animationSpeedMetersPerSecond,
                    _animationDurationSeconds,
                    _mainAnimationEasingType);
            }
            if (!value && (_isAnimationActive || _isLerpingBack))
            {
                _isAnimationActive = false;
                _isLerpingBack = false;
                _animationElapsedTime = 0.0;
                _lerpBackElapsedTime = 0.0;
                _standaloneZoomOut = null;
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
            if (value)
            {
                if (_sequencePlayer.State == PlaybackState.Playing)
                {
                    _sequencePlayer.Stop();
                }
                // Create new animation instance with current settings
                _standaloneOrbit = new OrbitAnimation(
                    _orbitDegrees,
                    _orbitDurationSeconds,
                    _orbitEasingType);
            }
            if (!value && (_isOrbitAnimationActive || _isOrbitLerpingBack))
            {
                _isOrbitAnimationActive = false;
                _isOrbitLerpingBack = false;
                _orbitAnimationElapsedTime = 0.0;
                _orbitLerpBackElapsedTime = 0.0;
                _standaloneOrbit = null;
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
            if (value)
            {
                if (_sequencePlayer.State == PlaybackState.Playing)
                {
                    _sequencePlayer.Stop();
                }
                // Create new animation instance with current settings
                _standaloneLoopyOrbit = new LoopyOrbitAnimation(
                    _loopyOrbitDegrees,
                    _loopyLoopIntervalDegrees,
                    _loopyAmplitudeMeters,
                    _loopyOrbitDurationSeconds,
                    _loopyOrbitEasingType);
            }
            if (!value && (_isLoopyOrbitActive || _isLoopyLerpingBack))
            {
                _isLoopyOrbitActive = false;
                _isLoopyLerpingBack = false;
                _loopyOrbitElapsedTime = 0.0;
                _loopyLerpBackElapsedTime = 0.0;
                _standaloneLoopyOrbit = null;
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
            // Check sequence player first - takes precedence over standalone animations
            if (_sequencePlayer.State == PlaybackState.Playing)
            {
                bool shouldSkip = _sequencePlayer.Update(controller, transform, deltaTime);
                return !shouldSkip;
            }
            
            // Handle orbit lerp back
            if (_isOrbitLerpingBack)
            {
                double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _orbitTargetPosition);
                double t = _orbitLerpBackElapsedTime / _orbitLerpBackDurationSeconds;
                double easedT = AnimationHelpers.ApplyEasing(t, _orbitLerpBackEasingType);
                
                double3 currentOffset = double3.Lerp(_orbitLerpEndOffset, _orbitLerpStartOffset, easedT);
                transform.PositionEcl = currentTargetPos + currentOffset;
                AnimationHelpers.LookAtTarget(transform, currentTargetPos);
                
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
                double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _loopyOrbitTargetPosition);
                double t = _loopyLerpBackElapsedTime / _loopyLerpBackDurationSeconds;
                double easedT = AnimationHelpers.ApplyEasing(t, _loopyLerpBackEasingType);
                
                double3 currentOffset = double3.Lerp(_loopyLerpEndOffset, _loopyLerpStartOffset, easedT);
                transform.PositionEcl = currentTargetPos + currentOffset;
                AnimationHelpers.LookAtTarget(transform, currentTargetPos);
                
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
            if (_isLoopyOrbitEnabled && _standaloneLoopyOrbit != null)
            {
                if (transform == null) return true;
                
                // Initialize on first frame
                if (!_isLoopyOrbitActive)
                {
                    _standaloneLoopyOrbit.Initialize(controller, transform);
                    _isLoopyOrbitActive = true;
                    _loopyOrbitElapsedTime = 0.0;
                    _loopyOrbitTargetPosition = AnimationHelpers.GetTargetPosition(controller);
                    _loopyLerpStartOffset = transform.PositionEcl - _loopyOrbitTargetPosition;
                }
                
                bool complete = _standaloneLoopyOrbit.Update(controller, transform, deltaTime, _loopyOrbitElapsedTime);
                _loopyOrbitElapsedTime += deltaTime;
                
                if (complete)
                {
                    if (_loopyLerpBackEnabled)
                    {
                        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _loopyOrbitTargetPosition);
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
            if (_isOrbitAnimationEnabled && _standaloneOrbit != null)
            {
                if (transform == null) return true;
                
                // Initialize on first frame
                if (!_isOrbitAnimationActive)
                {
                    _standaloneOrbit.Initialize(controller, transform);
                    _isOrbitAnimationActive = true;
                    _orbitAnimationElapsedTime = 0.0;
                    _orbitTargetPosition = AnimationHelpers.GetTargetPosition(controller);
                    _orbitLerpStartOffset = transform.PositionEcl - _orbitTargetPosition;
                }
                
                bool complete = _standaloneOrbit.Update(controller, transform, deltaTime, _orbitAnimationElapsedTime);
                _orbitAnimationElapsedTime += deltaTime;
                
                if (complete)
                {
                    if (_orbitLerpBackEnabled)
                    {
                        double3 currentTargetPos = AnimationHelpers.GetTargetPosition(controller, _orbitTargetPosition);
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
                double easedFrameProgress = AnimationHelpers.GetEasedFrameProgress(_lerpBackElapsedTime, _lerpBackDurationSeconds, deltaTime, _lerpBackEasingType);
                double3 returnDisplacement = -_animationDirection * (_distanceTraveledForward * easedFrameProgress);
                transform.PositionEcl += returnDisplacement;
                AnimationHelpers.LookAtTarget(transform, AnimationHelpers.GetTargetPosition(controller));
                
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
            
            // Handle linear animation
            if (_isAnimationEnabled && _standaloneZoomOut != null)
            {
                if (transform == null) return true;
                
                // Initialize on first frame
                if (!_isAnimationActive)
                {
                    _standaloneZoomOut.Initialize(controller, transform);
                    _isAnimationActive = true;
                    _distanceTraveledForward = 0.0;
                    _animationElapsedTime = 0.0;
                    
                    double3 targetPos = AnimationHelpers.GetTargetPosition(controller);
                    double3 towardTarget = targetPos - transform.PositionEcl;
                    _animationDirection = double3.Normalize(-towardTarget);
                }
                
                bool complete = _standaloneZoomOut.Update(controller, transform, deltaTime, _animationElapsedTime);
                _animationElapsedTime += deltaTime;
                
                // Track distance traveled for lerp back (legacy behavior)
                double totalDistance = _animationSpeedMetersPerSecond * _animationDurationSeconds;
                double t = Math.Min(1.0, _animationElapsedTime / _animationDurationSeconds);
                _distanceTraveledForward = totalDistance * AnimationHelpers.ApplyEasing(t, _mainAnimationEasingType);
                
                if (complete)
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
            
            // If no animation enabled, run original
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
            return true;
        }
    }
}
