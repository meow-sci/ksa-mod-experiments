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
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] Lerp back: {_isLerpingBack}");
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] deltaTime: {inDeltaTime:F6}s");
                _lastLogTime = DateTime.Now;
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
