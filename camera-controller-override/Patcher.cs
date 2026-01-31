using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

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

    public static bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set
        {
            Console.WriteLine($"camera-controller-override: [STATE] IsAnimationEnabled changing from {_isAnimationEnabled} to {value}");
            _isAnimationEnabled = value;
            
            // Reset animation state when disabled
            if (!value && _isAnimationActive)
            {
                Console.WriteLine("camera-controller-override: [STATE] Animation disabled while active - resetting animation state");
                _isAnimationActive = false;
                _animationElapsedTime = 0.0;
            }
        }
    }

    public static bool IsAnimationActive => _isAnimationActive;

    public static double AnimationElapsedTime => _animationElapsedTime;

    public static double AnimationSpeedMetersPerSecond
    {
        get => _animationSpeedMetersPerSecond;
        set => _animationSpeedMetersPerSecond = Math.Max(0.5, value);
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
                Console.WriteLine($"camera-controller-override: [PREFIX-ENTRY] deltaTime: {inDeltaTime:F6}s");
                _lastLogTime = DateTime.Now;
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
                Console.WriteLine($"camera-controller-override: [ANIM-START] === STARTING ANIMATION (start count: {_animationStartCount}) ===");
                
                _isAnimationActive = true;
                _animationStartPosition = transform.PositionEcl;
                Console.WriteLine($"camera-controller-override: [ANIM-START] Start position: {_animationStartPosition}");
                
                // Get camera rotation and calculate backward direction
                doubleQuat rotation = transform.LocalRotation;
                Console.WriteLine($"camera-controller-override: [ANIM-START] Camera rotation: {rotation}");
                
                double3 forward = (-double3.UnitZ).Transform(rotation);
                Console.WriteLine($"camera-controller-override: [ANIM-START] Forward vector: {forward}");
                
                _animationDirection = double3.Normalize(-forward);
                double directionLength = _animationDirection.Length();
                Console.WriteLine($"camera-controller-override: [ANIM-START] Backward direction (normalized): {_animationDirection}");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Direction length (should be ~1.0): {directionLength:F6}");
                
                if (directionLength < 0.001)
                {
                    Console.WriteLine($"camera-controller-override: [ANIM-START] WARNING: Direction vector is near-zero! Animation may not work!");
                }
                
                _animationElapsedTime = 0.0;
                Console.WriteLine($"camera-controller-override: [ANIM-START] Speed: {_animationSpeedMetersPerSecond} m/s, Duration: 5.0s");
                Console.WriteLine($"camera-controller-override: [ANIM-START] Expected total distance: {_animationSpeedMetersPerSecond * 5.0:F2} meters");
            }
            
            // Update animation on each frame
            double oldElapsed = _animationElapsedTime;
            _animationElapsedTime += inDeltaTime;
            
            // Rate-limited progress logging
            if (shouldLog || _frameCounter % 10 == 0)
            {
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] Frame #{_frameCounter}: Elapsed {_animationElapsedTime:F3}s (delta: {inDeltaTime:F6}s)");
            }
            
            // Calculate and apply new position
            double3 oldPos = transform.PositionEcl;
            double3 displacement = _animationDirection * _animationSpeedMetersPerSecond * inDeltaTime;
            double3 newPos = oldPos + displacement;
            
            if (shouldLog || _frameCounter % 10 == 0)
            {
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] Old pos: {oldPos}");
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] Displacement: {displacement} (length: {displacement.Length():F6})");
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] New pos: {newPos}");
            }
            
            transform.PositionEcl = newPos;
            
            // Verify the position was actually set
            double3 verifyPos = transform.PositionEcl;
            if (shouldLog || _frameCounter % 10 == 0)
            {
                double positionChange = (verifyPos - oldPos).Length();
                Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] Position change verified: {positionChange:F6} meters");
                
                if (positionChange < 0.0001)
                {
                    Console.WriteLine($"camera-controller-override: [ANIM-PROGRESS] WARNING: Position barely changed! Position may not be writable!");
                }
            }
            
            // Check if animation is complete
            if (_animationElapsedTime >= 5.0)
            {
                _animationCompleteCount++;
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] === ANIMATION COMPLETE (complete count: {_animationCompleteCount}) ===");
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Total elapsed time: {_animationElapsedTime:F3}s");
                
                _isAnimationEnabled = false;
                _isAnimationActive = false;
                
                double3 finalPosition = transform.PositionEcl;
                double distanceTraveled = (finalPosition - _animationStartPosition).Length();
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Start position: {_animationStartPosition}");
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Final position: {finalPosition}");
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Distance traveled: {distanceTraveled:F2} meters");
                Console.WriteLine($"camera-controller-override: [ANIM-COMPLETE] Expected distance: {_animationSpeedMetersPerSecond * 5.0:F2} meters");
            }
            
            // Skip original OnFrame method
            Console.WriteLine($"camera-controller-override: [PREFIX-RETURN] Returning false to skip original OnFrame (frame #{_frameCounter})");
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
