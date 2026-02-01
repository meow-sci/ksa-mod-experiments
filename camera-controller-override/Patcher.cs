using System;
using System.Linq;
using System.Reflection;
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

    // Animation state
    private static bool _isAnimationEnabled = false;
    private static bool _isAnimationActive = false;
    private static double _animationElapsedTime = 0.0;
    private static double3 _animationStartPosition;
    private static double3 _animationDirection;
    private static double _animationSpeedMetersPerSecond = 1.0;
    private static double _animationDurationSeconds = 5.0;
    
    // Orbit animation state
    private static bool _isOrbitAnimationEnabled = false;
    private static bool _isOrbitAnimationActive = false;
    private static double _orbitAnimationElapsedTime = 0.0;
    private static double _orbitDegrees = 360.0;
    private static double _orbitDurationSeconds = 5.0;
    private static EasingType _orbitEasingType = EasingType.Linear;
    private static bool _orbitLerpBackEnabled = true;
    private static double _orbitLerpBackDurationSeconds = 3.0;
    private static EasingType _orbitLerpBackEasingType = EasingType.Linear;
    private static double3 _orbitStartPosition;
    private static doubleQuat _orbitStartRotation;
    private static double3 _orbitTargetPosition;
    private static double _orbitRadius;
    private static double3 _orbitAxis;
    private static bool _isOrbitLerpingBack = false;
    private static double _orbitLerpBackElapsedTime = 0.0;
    
    // Orbit lerp back: store OFFSETS from target (not absolute positions)
    // This ensures lerp back works correctly even if target is moving
    private static double3 _orbitLerpStartOffset;  // Offset from target to camera at START of orbit
    private static double3 _orbitLerpEndOffset;    // Offset from target to camera at END of orbit
    
    // Lerp back state
    private static bool _lerpBackEnabled = true;  // Default enabled
    private static bool _isLerpingBack = false;
    private static double _lerpBackElapsedTime = 0.0;
    private static double _lerpBackDurationSeconds = 3.0;
    private static EasingType _lerpBackEasingType = EasingType.Linear;
    
    // Main animation easing
    private static EasingType _mainAnimationEasingType = EasingType.Linear;
    
    // Distance tracking (replaces absolute position tracking)
    private static double _distanceTraveledForward = 0.0;  // How far we moved during forward animation
    private static double _distanceTraveledReturn = 0.0;   // How far we've moved during return

    // Debug tracking
    private static int _frameCounter = 0;
    private static DateTime _lastLogTime = DateTime.MinValue;
    private static int _prefixCallCount = 0;
    private static int _animationStartCount = 0;
    private static int _animationCompleteCount = 0;

    public static void Patch()
    {
        try
        {
            Console.WriteLine("camera-controller-override: [PATCH] Starting patch application...");
            
            // Verify method existence before patching
            VerifyMethodExists(typeof(Controller), "OnFrame", typeof(Viewport), typeof(double));
            VerifyMethodExists(typeof(OrbitController), "OnFrame", typeof(Viewport), typeof(double));
            VerifyMethodExists(typeof(FlyController), "OnFrame", typeof(Viewport), typeof(double));
            
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            
            Console.WriteLine("camera-controller-override: [PATCH] PatchAll completed");
            
            // List all patched methods using Harmony.GetAllPatchedMethods()
            var patchedMethods = Harmony.GetAllPatchedMethods().ToList();
            Console.WriteLine($"camera-controller-override: [PATCH] Total patched methods: {patchedMethods.Count}");
            
            if (patchedMethods.Count == 0)
            {
                Console.WriteLine("camera-controller-override: [PATCH] WARNING: No methods were patched! Patch attributes may not match target methods.");
            }
            
            foreach (var method in patchedMethods)
            {
                Console.WriteLine($"camera-controller-override: [PATCH]   - {method.DeclaringType?.FullName}.{method.Name}");
                var patches = Harmony.GetPatchInfo(method);
                if (patches != null)
                {
                    Console.WriteLine($"camera-controller-override: [PATCH]       Prefixes: {patches.Prefixes.Count}, Postfixes: {patches.Postfixes.Count}, Transpilers: {patches.Transpilers.Count}");
                    foreach (var prefix in patches.Prefixes)
                    {
                        Console.WriteLine($"camera-controller-override: [PATCH]         Prefix: {prefix.owner}.{prefix.PatchMethod.Name}");
                    }
                }
            }
            
            Console.WriteLine("camera-controller-override: [PATCH] Harmony patches applied successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: [PATCH-EXCEPTION] Error applying Harmony patches: {ex}");
            Console.WriteLine($"camera-controller-override: [PATCH-EXCEPTION] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"camera-controller-override: [PATCH-EXCEPTION] Inner exception: {ex.InnerException}");
            }
        }
    }

    private static void VerifyMethodExists(Type targetType, string methodName, params Type[] parameterTypes)
    {
        var method = targetType.GetMethod(methodName, 
            BindingFlags.Public | BindingFlags.Instance, 
            null, 
            parameterTypes, 
            null);

        if (method == null)
        {
            Console.WriteLine($"camera-controller-override: [PATCH-VERIFY] ERROR: {targetType.FullName}.{methodName} method not found!");
            Console.WriteLine($"camera-controller-override: [PATCH-VERIFY]   Parameters: {string.Join(", ", parameterTypes.Select(t => t.Name))}");
        }
        else
        {
            Console.WriteLine($"camera-controller-override: [PATCH-VERIFY] Found {targetType.FullName}.{methodName}: {method}");
            Console.WriteLine($"camera-controller-override: [PATCH-VERIFY]   Virtual: {method.IsVirtual}, Abstract: {method.IsAbstract}");
        }
    }

    public static void Unload()
    {
        try
        {
            Console.WriteLine("camera-controller-override: [UNPATCH] Removing all patches...");
            _harmony?.UnpatchAll("camera-controller-override");
            _harmony = null;
            Console.WriteLine("camera-controller-override: [UNPATCH] Successfully removed all patches");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: [UNPATCH] ERROR removing Harmony patches: {ex.Message}");
            Console.WriteLine($"camera-controller-override: [UNPATCH] Stack trace: {ex.StackTrace}");
        }
    }
    
    // Apply easing function to normalized time t (0.0 to 1.0)
    private static double ApplyEasing(double t, EasingType easingType)
    {
        t = Math.Max(0.0, Math.Min(1.0, t)); // Clamp
        
        switch (easingType)
        {
            case EasingType.Linear:
                return t;
                
            case EasingType.EaseIn:
                // Cubic ease-in: t^3
                return t * t * t;
                
            case EasingType.EaseOut:
                // Cubic ease-out: 1 - (1-t)^3
                double f = 1.0 - t;
                return 1.0 - (f * f * f);
                
            case EasingType.EaseInOut:
                // Smoothstep: 3t^2 - 2t^3
                return t * t * (3.0 - 2.0 * t);
                
            default:
                return t;
        }
    }

    public static bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set
        {
            Console.WriteLine($"camera-controller-override: [STATE] IsAnimationEnabled changing from {_isAnimationEnabled} to {value}");
            _isAnimationEnabled = value;
            
            // Reset animation state when disabled
            if (!value && (_isAnimationActive || _isLerpingBack))
            {
                Console.WriteLine("camera-controller-override: [STATE] Animation disabled while active - resetting animation state");
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
        set => _animationDurationSeconds = Math.Max(1.0, Math.Min(30.0, value));
    }
    
    public static bool LerpBackEnabled
    {
        get => _lerpBackEnabled;
        set => _lerpBackEnabled = value;
    }
    
    public static bool IsLerpingBack => _isLerpingBack;
    
    public static double LerpBackElapsedTime => _lerpBackElapsedTime;
    
    public static double DistanceTraveledForward => _distanceTraveledForward;
    
    public static double DistanceTraveledReturn => _distanceTraveledReturn;
    
    public static double LerpBackProgress => _distanceTraveledForward > 0 ? Math.Min(1.0, _distanceTraveledReturn / _distanceTraveledForward) : 0.0;
    
    public static double LerpBackDurationSeconds
    {
        get => _lerpBackDurationSeconds;
        set => _lerpBackDurationSeconds = Math.Max(1.0, Math.Min(10.0, value));
    }
    
    public static EasingType LerpBackEasingType
    {
        get => _lerpBackEasingType;
        set => _lerpBackEasingType = value;
    }
    
    public static EasingType MainAnimationEasingType
    {
        get => _mainAnimationEasingType;
        set => _mainAnimationEasingType = value;
    }
    
    // Orbit animation properties
    public static bool IsOrbitAnimationEnabled
    {
        get => _isOrbitAnimationEnabled;
        set
        {
            Console.WriteLine($"camera-controller-override: [ORBIT-STATE] IsOrbitAnimationEnabled changing from {_isOrbitAnimationEnabled} to {value}");
            _isOrbitAnimationEnabled = value;
            
            // Reset orbit state when disabled
            if (!value && (_isOrbitAnimationActive || _isOrbitLerpingBack))
            {
                Console.WriteLine("camera-controller-override: [ORBIT-STATE] Orbit animation disabled while active - resetting state");
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
        set => _orbitDegrees = Math.Max(90.0, Math.Min(720.0, value));
    }
    
    public static double OrbitDurationSeconds
    {
        get => _orbitDurationSeconds;
        set => _orbitDurationSeconds = Math.Max(1.0, Math.Min(30.0, value));
    }
    
    public static EasingType OrbitEasingType
    {
        get => _orbitEasingType;
        set => _orbitEasingType = value;
    }
    
    public static bool OrbitLerpBackEnabled
    {
        get => _orbitLerpBackEnabled;
        set => _orbitLerpBackEnabled = value;
    }
    
    public static double OrbitLerpBackDurationSeconds
    {
        get => _orbitLerpBackDurationSeconds;
        set => _orbitLerpBackDurationSeconds = Math.Max(1.0, Math.Min(10.0, value));
    }
    
    public static EasingType OrbitLerpBackEasingType
    {
        get => _orbitLerpBackEasingType;
        set => _orbitLerpBackEasingType = value;
    }
    
    // CRITICAL: Controller.OnFrame is virtual and overridden by OrbitController and FlyController
    // Patches on the base Controller class won't execute for the overrides
    // We must patch the concrete implementations instead

    [HarmonyPatch(typeof(OrbitController), "OnFrame")]
    [HarmonyPrefix]
    private static bool OrbitController_OnFrame_Prefix(OrbitController __instance, Viewport inViewport, double inDeltaTime, Transform3D ___Transform)
    {
        // Delegate to common handler
        return HandleOnFramePrefix(__instance, inViewport, inDeltaTime, ___Transform, "OrbitController");
    }

    [HarmonyPatch(typeof(FlyController), "OnFrame")]
    [HarmonyPrefix]
    private static bool FlyController_OnFrame_Prefix(FlyController __instance, Viewport inViewport, double inDeltaTime, Transform3D ___Transform)
    {
        // Delegate to common handler
        return HandleOnFramePrefix(__instance, inViewport, inDeltaTime, ___Transform, "FlyController");
    }

    private static bool HandleOnFramePrefix(Controller __instance, Viewport inViewport, double inDeltaTime, Transform3D transform, string controllerType)
    {
        try
        {
            _prefixCallCount++;
            _frameCounter++;
            
            // Rate-limited logging for prefix entry
            bool shouldLog = _prefixCallCount <= 5 || (DateTime.Now - _lastLogTime).TotalSeconds >= 1.0;
            if (shouldLog)
            {
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] {controllerType} Call #{_prefixCallCount}, Frame #{_frameCounter}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] __instance type: {__instance?.GetType().Name ?? "NULL"}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] Animation enabled: {_isAnimationEnabled}, active: {_isAnimationActive}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] Orbit enabled: {_isOrbitAnimationEnabled}, active: {_isOrbitAnimationActive}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] Lerp back: {_isLerpingBack}, Orbit lerp back: {_isOrbitLerpingBack}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] deltaTime: {inDeltaTime:F6}s");
                _lastLogTime = DateTime.Now;
            }
            
            // Check if orbit lerping back
            if (_isOrbitLerpingBack)
            {
                // Get CURRENT target position (vessel may be moving)
                double3 currentTargetPos = _orbitTargetPosition;
                if (__instance != null)
                {
                    var orbitCamera = __instance.Camera;
                    if (orbitCamera != null && orbitCamera.Following != null)
                    {
                        currentTargetPos = orbitCamera.Following.GetPositionEcl();
                    }
                }
                
                // Calculate time progress (0 to 1)
                double t = _orbitLerpBackElapsedTime / _orbitLerpBackDurationSeconds;
                double easedT = ApplyEasing(t, _orbitLerpBackEasingType);
                
                // Lerp between end offset and start offset, apply to current target
                double3 currentOffset = double3.Lerp(_orbitLerpEndOffset, _orbitLerpStartOffset, easedT);
                double3 newPos = currentTargetPos + currentOffset;
                transform.PositionEcl = newPos;
                
                // Make camera look at target
                double3 lookDirection = currentTargetPos - newPos;
                if (lookDirection.LengthSquared() > 0.0001)
                {
                    double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
                    doubleQuat lookAtRotation = Camera.LookAtRotation(lookDirection, currentUp);
                    transform.LocalRotation = lookAtRotation;
                }
                
                _orbitLerpBackElapsedTime += inDeltaTime;
                
                // Complete when time is up
                if (_orbitLerpBackElapsedTime >= _orbitLerpBackDurationSeconds)
                {
                    _isOrbitAnimationEnabled = false;
                    _isOrbitAnimationActive = false;
                    _isOrbitLerpingBack = false;
                    Console.WriteLine($"camera-controller-override: [ORBIT-LERP-COMPLETE] Orbit lerp back complete.");
                }
                else if (shouldLog || _frameCounter % 30 == 0)
                {
                    double progressPercent = easedT * 100.0;
                    Console.WriteLine($"camera-controller-override: [ORBIT-LERP-PROGRESS] elapsed={_orbitLerpBackElapsedTime:F2}s/{_orbitLerpBackDurationSeconds:F1}s ({progressPercent:F1}%)");
                }
                
                return false; // Skip original
            }
            
            // Check if orbit animation is active
            if (_isOrbitAnimationEnabled)
            {
                // Validate injected Transform field
                if (transform == null)
                {
                    Console.WriteLine($"camera-controller-override: [ORBIT-ERROR] Injected Transform field is null on {__instance?.GetType().Name ?? "NULL"}!");
                    return true;
                }
                
                // First frame of orbit animation
                if (!_isOrbitAnimationActive)
                {
                    _isOrbitAnimationActive = true;
                    _orbitStartPosition = transform.PositionEcl;
                    _orbitStartRotation = transform.LocalRotation;
                    _orbitAnimationElapsedTime = 0.0;
                    
                    // Get target position
                    _orbitTargetPosition = double3.Zero;
                    if (__instance != null)
                    {
                        var camera = __instance.Camera;
                        if (camera != null && camera.Following != null)
                        {
                            _orbitTargetPosition = camera.Following.GetPositionEcl();
                        }
                    }
                    
                    // Save start offset for lerp back (camera position relative to target)
                    _orbitLerpStartOffset = _orbitStartPosition - _orbitTargetPosition;
                    
                    // Calculate orbit parameters
                    double3 cameraToTarget = _orbitTargetPosition - _orbitStartPosition;
                    _orbitRadius = cameraToTarget.Length();

                    // Guard: can't orbit with (near) zero radius
                    if (_orbitRadius < 0.01)
                    {
                        Console.WriteLine($"camera-controller-override: [ORBIT-ERROR] Orbit radius too small ({_orbitRadius:F6}m) - cancelling orbit.");
                        _isOrbitAnimationEnabled = false;
                        _isOrbitAnimationActive = false;
                        return true;
                    }

                    // Prefer camera's start up vector for the orbit plane, but avoid degeneracy when it is
                    // (nearly) parallel to the camera-to-target offset.
                    double3 startUp = double3.UnitY.Transform(_orbitStartRotation);
                    if (startUp.LengthSquared() < 0.00000001)
                    {
                        startUp = double3.UnitY;
                    }

                    double3 startOffset0 = _orbitStartPosition - _orbitTargetPosition;
                    double3 right = double3.Cross(startUp, startOffset0);
                    if (right.LengthSquared() < 0.0001)
                    {
                        // Pick a fallback reference axis that isn't parallel to the start offset
                        double3 startOffsetDir = double3.Normalize(startOffset0);
                        double3 fallbackRef = Math.Abs(double3.Dot(startOffsetDir, double3.UnitY)) < 0.99 ? double3.UnitY : double3.UnitX;
                        right = double3.Cross(fallbackRef, startOffset0);
                    }

                    _orbitAxis = double3.Normalize(double3.Cross(startOffset0, right));
                    
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Orbit animation started");
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Degrees: {_orbitDegrees}, Duration: {_orbitDurationSeconds:F1}s");
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Target: {_orbitTargetPosition}");
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Camera: {_orbitStartPosition}");
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Radius: {_orbitRadius:F2}m");
                    Console.WriteLine($"camera-controller-override: [ORBIT-START] Orbit axis: {_orbitAxis}");
                }
                
                // Update orbit animation
                _orbitAnimationElapsedTime += inDeltaTime;
                
                // Calculate angle progress (0 to 1)
                double t = Math.Min(1.0, _orbitAnimationElapsedTime / _orbitDurationSeconds);
                double easedT = ApplyEasing(t, _orbitEasingType);
                
                // Calculate current angle in radians
                double targetAngleDegrees = _orbitDegrees * easedT;
                double currentAngleRadians = targetAngleDegrees * Math.PI / 180.0;
                
                // Calculate position on orbit
                // Start with vector from target to camera start position
                double3 startOffset = _orbitStartPosition - _orbitTargetPosition;
                
                // Rotate around orbit axis
                // Using Rodrigues' rotation formula: v' = v*cos(θ) + (k×v)*sin(θ) + k*(k·v)*(1-cos(θ))
                double3 k = _orbitAxis;
                double cosTheta = Math.Cos(currentAngleRadians);
                double sinTheta = Math.Sin(currentAngleRadians);
                double3 kCrossV = double3.Cross(k, startOffset);
                double kDotV = double3.Dot(k, startOffset);
                double3 rotatedOffset = startOffset * cosTheta + kCrossV * sinTheta + k * kDotV * (1.0 - cosTheta);

                // Get CURRENT target position (vessel may be moving during orbit)
                double3 currentTargetPos = _orbitTargetPosition;
                if (__instance != null)
                {
                    var orbitCamera = __instance.Camera;
                    if (orbitCamera != null && orbitCamera.Following != null)
                    {
                        currentTargetPos = orbitCamera.Following.GetPositionEcl();
                    }
                }

                // Set new camera position (orbit around the CURRENT target)
                double3 newPosition = currentTargetPos + rotatedOffset;
                transform.PositionEcl = newPosition;
                
                // Make camera look at target
                double3 lookDirection = currentTargetPos - newPosition;
                if (lookDirection.LengthSquared() > 0.0001)
                {
                    // Use camera's current up vector to prevent flipping (consistent with other animations)
                    double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
                    doubleQuat lookAtRotation = Camera.LookAtRotation(lookDirection, currentUp);
                    transform.LocalRotation = lookAtRotation;
                }
                
                // Log progress
                if (shouldLog || _frameCounter % 30 == 0)
                {
                    double progressPercent = t * 100.0;
                    Console.WriteLine($"camera-controller-override: [ORBIT-PROGRESS] Elapsed {_orbitAnimationElapsedTime:F2}s/{_orbitDurationSeconds:F1}s ({progressPercent:F1}%), Angle: {targetAngleDegrees:F1}°");
                }
                
                // Check if orbit is complete
                if (_orbitAnimationElapsedTime >= _orbitDurationSeconds)
                {
                    Console.WriteLine($"camera-controller-override: [ORBIT-COMPLETE] Orbit animation complete (elapsed: {_orbitAnimationElapsedTime:F2}s)");
                    
                    // Check if lerp back is enabled
                    if (_orbitLerpBackEnabled)
                    {
                        // Save end offset for lerp back (camera position relative to current target)
                        _orbitLerpEndOffset = transform.PositionEcl - currentTargetPos;
                        
                        Console.WriteLine($"camera-controller-override: [ORBIT-COMPLETE] Starting orbit lerp back phase");
                        Console.WriteLine($"camera-controller-override: [ORBIT-LERP-START] Start offset: {_orbitLerpStartOffset}");
                        Console.WriteLine($"camera-controller-override: [ORBIT-LERP-START] End offset: {_orbitLerpEndOffset}");
                        
                        _isOrbitLerpingBack = true;
                        _isOrbitAnimationActive = false;
                        _orbitLerpBackElapsedTime = 0.0;
                    }
                    else
                    {
                        // Complete without lerp back
                        _isOrbitAnimationEnabled = false;
                        _isOrbitAnimationActive = false;
                        Console.WriteLine($"camera-controller-override: [ORBIT-COMPLETE] Orbit complete (lerp back disabled)");
                    }
                }
                
                return false; // Skip original
            }
            
            // Check if lerping back
            if (_isLerpingBack)
            {
                // Calculate return direction (toward target = opposite of animation direction)
                double3 returnDirection = -_animationDirection;
                
                // Calculate time progress (0 to 1)
                double t = _lerpBackElapsedTime / _lerpBackDurationSeconds;
                double lastT = Math.Max(0.0, (_lerpBackElapsedTime - inDeltaTime) / _lerpBackDurationSeconds);
                
                // Apply easing to get progress through the total distance
                double easedT = ApplyEasing(t, _lerpBackEasingType);
                double lastEasedT = ApplyEasing(lastT, _lerpBackEasingType);
                double easedFrameProgress = easedT - lastEasedT;
                
                // Calculate displacement for this frame based on eased progress
                double frameDistance = _distanceTraveledForward * easedFrameProgress;
                double3 returnDisplacement = returnDirection * frameDistance;
                
                // Add to current position
                double3 newPos = transform.PositionEcl + returnDisplacement;
                transform.PositionEcl = newPos;
                
                // MAKE CAMERA LOOK AT TARGET during return animation
                // Use camera's current up vector (not world Y-up) to prevent flipping
                if (__instance != null)
                {
                    var lerpCamera = __instance.Camera;
                    if (lerpCamera != null && lerpCamera.Following != null)
                    {
                        var lerpFollowing = lerpCamera.Following;
                        double3 targetPos = lerpFollowing.GetPositionEcl();
                        double3 cameraPos = transform.PositionEcl;
                        double3 lookDirection = targetPos - cameraPos;
                        
                        if (lookDirection.LengthSquared() > 0.0001)
                        {
                            // Extract current camera up vector from existing rotation
                            // Camera up is typically +Y in local space, transformed by current rotation
                            double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
                            doubleQuat lookAtRotation = Camera.LookAtRotation(lookDirection, currentUp);
                            transform.LocalRotation = lookAtRotation;
                            
                            if (shouldLog)
                            {
                                Console.WriteLine($"camera-controller-override: [LERP-LOOKAT] Updated camera rotation to look at target (preserved up vector)");
                            }
                        }
                    }
                }
                
                // Track distance and time
                _distanceTraveledReturn += returnDisplacement.Length();
                _lerpBackElapsedTime += inDeltaTime;
                
                // Complete when we've traveled far enough OR time is up
                if (_distanceTraveledReturn >= _distanceTraveledForward || _lerpBackElapsedTime >= _lerpBackDurationSeconds)
                {
                    _isAnimationEnabled = false;
                    _isAnimationActive = false;
                    _isLerpingBack = false;
                    Console.WriteLine($"camera-controller-override: [LERP-COMPLETE] Return animation complete. Distance: {_distanceTraveledReturn:F2}m / {_distanceTraveledForward:F2}m");
                }
                else
                {
                    // Log progress occasionally
                    if (_frameCounter <= 5 || _frameCounter % 30 == 0 || (DateTime.Now - _lastLogTime).TotalSeconds >= 1.0)
                    {
                        double progressPercent = (_distanceTraveledReturn / _distanceTraveledForward) * 100.0;
                        Console.WriteLine($"camera-controller-override: [LERP-PROGRESS] distance={_distanceTraveledReturn:F2}m/{_distanceTraveledForward:F2}m ({progressPercent:F1}%), elapsed={_lerpBackElapsedTime:F2}s/{_lerpBackDurationSeconds:F1}s");
                        _lastLogTime = DateTime.Now;
                    }
                }
                
                return false; // Skip original to prevent interference
            }
            
            // If animation not enabled, run original method
            if (!_isAnimationEnabled)
            {
                if (_prefixCallCount <= 5)
                {
                    Console.WriteLine($"camera-controller-override: [PREFIX-SKIP] Animation not enabled, running original (call #{_prefixCallCount})");
                }
                return true;
            }

            Console.WriteLine($"camera-controller-override: [PREFIX-ACTIVE] Animation enabled, processing frame #{_frameCounter}");

            // Validate injected Transform field
            if (transform == null)
            {
                Console.WriteLine($"camera-controller-override: [PREFIX-ERROR] Injected Transform field is null on {__instance?.GetType().Name ?? "NULL"}!");
                return true;
            }

            // First frame of animation
            if (!_isAnimationActive)
            {
                _animationStartCount++;
                
                _isAnimationActive = true;
                _animationStartPosition = transform.PositionEcl;
                _distanceTraveledForward = 0.0;  // Reset distance tracking
                
                // Get target position
                double3 targetPos = double3.Zero;
                if (__instance != null)
                {
                    var camera = __instance.Camera;
                    if (camera != null && camera.Following != null)
                    {
                        targetPos = camera.Following.GetPositionEcl();
                    }
                }
                
                // Calculate direction AWAY from target (not based on camera rotation)
                double3 cameraPos = transform.PositionEcl;
                double3 towardTarget = targetPos - cameraPos;  // Forward direction
                _animationDirection = double3.Normalize(-towardTarget);  // Backward = away from target
                _animationElapsedTime = 0.0;
                
                double expectedDistance = _animationSpeedMetersPerSecond * _animationDurationSeconds;
                Console.WriteLine($"camera-controller-override: [ANIM-START] Animation started (speed: {_animationSpeedMetersPerSecond}m/s, duration: {_animationDurationSeconds:F1}s, expected distance: {expectedDistance:F2}m)");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Target position: {targetPos}");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Camera position: {cameraPos}");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Toward target (forward): {double3.Normalize(towardTarget)}");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Animation direction (backward): {_animationDirection}");
            }
            
            // Update animation on each frame
            _animationElapsedTime += inDeltaTime;
            
            // Calculate time progress (0 to 1)
            double mainT = _animationElapsedTime / _animationDurationSeconds;
            double mainLastT = Math.Max(0.0, (_animationElapsedTime - inDeltaTime) / _animationDurationSeconds);
            
            // Apply easing to get progress
            double mainEasedT = ApplyEasing(mainT, _mainAnimationEasingType);
            double mainLastEasedT = ApplyEasing(mainLastT, _mainAnimationEasingType);
            double mainEasedFrameProgress = mainEasedT - mainLastEasedT;
            
            // Calculate displacement based on eased progress
            // Total distance = speed * duration, frame distance = total * eased progress delta
            double totalDistance = _animationSpeedMetersPerSecond * _animationDurationSeconds;
            double mainFrameDistance = totalDistance * mainEasedFrameProgress;
            double3 displacement = _animationDirection * mainFrameDistance;
            transform.PositionEcl = transform.PositionEcl + displacement;
            
            // MAKE CAMERA LOOK AT TARGET during forward animation
            // Use camera's current up vector (not world Y-up) to prevent flipping
            if (__instance != null)
            {
                var animCamera = __instance.Camera;
                if (animCamera != null && animCamera.Following != null)
                {
                    var animFollowing = animCamera.Following;
                    double3 targetPos = animFollowing.GetPositionEcl();
                    double3 cameraPos = transform.PositionEcl;
                    double3 lookDirection = targetPos - cameraPos;
                    
                    if (lookDirection.LengthSquared() > 0.0001)
                    {
                        // Extract current camera up vector from existing rotation
                        // Camera up is typically +Y in local space, transformed by current rotation
                        double3 currentUp = double3.UnitY.Transform(transform.LocalRotation);
                        doubleQuat lookAtRotation = Camera.LookAtRotation(lookDirection, currentUp);
                        transform.LocalRotation = lookAtRotation;
                        
                        if (shouldLog)
                        {
                            Console.WriteLine($"camera-controller-override: [ANIM-LOOKAT] Updated camera rotation to look at target (preserved up vector)");
                        }
                    }
                }
            }
            
            // Track distance traveled for return animation
            _distanceTraveledForward += displacement.Length();
            
            // Log progress occasionally
            if (shouldLog || _frameCounter % 30 == 0)
            {
                double progressPercent = (_animationElapsedTime / _animationDurationSeconds) * 100.0;
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] Elapsed {_animationElapsedTime:F2}s/{_animationDurationSeconds:F1}s ({progressPercent:F1}%)");
            }
            
            // Check if animation is complete
            if (_animationElapsedTime >= _animationDurationSeconds)
            {
                _animationCompleteCount++;
                
                double3 finalPosition = transform.PositionEcl;
                double distanceTraveled = (finalPosition - _animationStartPosition).Length();
                double expectedDistance = _animationSpeedMetersPerSecond * _animationDurationSeconds;
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Forward animation complete (elapsed: {_animationElapsedTime:F2}s, distance: {distanceTraveled:F2}m, expected: {expectedDistance:F2}m)");
                
                // Check if lerp back is enabled
                if (_lerpBackEnabled)
                {
                    Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Starting lerp back phase");
                    
                    // Start lerp back phase
                    _isLerpingBack = true;
                    _isAnimationActive = false; // Forward phase done
                    _lerpBackElapsedTime = 0.0;
                    _distanceTraveledReturn = 0.0;  // Reset return distance tracking
                    
                    double returnSpeed = _distanceTraveledForward / _lerpBackDurationSeconds;
                    Console.WriteLine($"camera-controller-override: [LERP-START] Distance to travel back: {_distanceTraveledForward:F2}m");
                    Console.WriteLine($"camera-controller-override: [LERP-START] Return speed: {returnSpeed:F2}m/s over {_lerpBackDurationSeconds:F1}s");
                    Console.WriteLine($"camera-controller-override: [LERP-START] Direction: toward target (opposite of forward)");
                }
                else
                {
                    // Complete without lerp back
                    _isAnimationEnabled = false;
                    _isAnimationActive = false;
                    
                    Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Animation complete (lerp back disabled)");
                }
            }
            
            // Skip original OnFrame method to prevent interference
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] === EXCEPTION IN PREFIX ===");
            Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] Message: {ex.Message}");
            Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] Type: {ex.GetType().FullName}");
            Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] Stack trace:");
            Console.WriteLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"camera-controller-override: [PREFIX-EXCEPTION] Inner stack trace:");
                Console.WriteLine(ex.InnerException.StackTrace);
            }
            
            return true;
        }
    }
}
