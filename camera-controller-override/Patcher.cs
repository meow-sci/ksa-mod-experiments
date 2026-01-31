using System;
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

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            Console.WriteLine("camera-controller-override: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error applying Harmony patches: {ex}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("camera-controller-override");
            _harmony = null;
            Console.WriteLine("camera-controller-override: Harmony patches removed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error removing Harmony patches: {ex}");
        }
    }

    public static bool IsAnimationEnabled
    {
        get => _isAnimationEnabled;
        set => _isAnimationEnabled = value;
    }

    public static bool IsAnimationActive => _isAnimationActive;

    public static double AnimationElapsedTime => _animationElapsedTime;

    public static double AnimationSpeedMetersPerSecond
    {
        get => _animationSpeedMetersPerSecond;
        set => _animationSpeedMetersPerSecond = Math.Max(0.5, value);
    }

    // Example patch (commented out):
    // Uncomment and modify to add your own patches
    /*
    [HarmonyPatch(typeof(KSA.Program), "SomeMethod")]
    [HarmonyPrefix]
    private static bool SomeMethodPrefix(ref bool __result)
    {
        try
        {
            // Your patch logic here
            Console.WriteLine("Skunkworks: SomeMethod called");

            // Return false to skip original method, true to run original method
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skunkworks: Error in SomeMethodPrefix: {ex}");
            return true;
        }
    }

    [HarmonyPatch(typeof(KSA.Program), "SomeOtherMethod")]
    [HarmonyPostfix]
    private static void SomeOtherMethodPostfix()
    {
        try
        {
            // Your post-patch logic here
            Console.WriteLine("Skunkworks: SomeOtherMethod completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skunkworks: Error in SomeOtherMethodPostfix: {ex}");
        }
    }
    */

    [HarmonyPatch(typeof(Controller), "OnFrame")]
    [HarmonyPrefix]
    private static bool Controller_OnFrame_Prefix(Controller __instance, Viewport inViewport, double inDeltaTime)
    {
        try
        {
            // If animation not enabled, run original method
            if (!_isAnimationEnabled)
            {
                return true;
            }

            // Access protected Transform field using Traverse
            var transformTraverse = Traverse.Create(__instance).Field<Transform3D>("Transform");
            Transform3D transform = transformTraverse.Value;

            // First frame of animation
            if (!_isAnimationActive)
            {
                _isAnimationActive = true;
                _animationStartPosition = transform.PositionEcl;
                
                // Get camera rotation and calculate backward direction
                doubleQuat rotation = transform.LocalRotation;
                double3 forward = (-double3.UnitZ).Transform(rotation);
                _animationDirection = double3.Normalize(-forward);
                
                _animationElapsedTime = 0.0;
                
                Console.WriteLine($"camera-controller-override: Animation started at position {_animationStartPosition}, direction {_animationDirection}, speed {_animationSpeedMetersPerSecond} m/s");
            }
            
            // Update animation on each frame
            _animationElapsedTime += inDeltaTime;
            
            // Calculate and apply new position
            double3 newPos = transform.PositionEcl + (_animationDirection * _animationSpeedMetersPerSecond * inDeltaTime);
            transform.PositionEcl = newPos;
            
            // Check if animation is complete
            if (_animationElapsedTime >= 5.0)
            {
                _isAnimationEnabled = false;
                _isAnimationActive = false;
                
                double3 finalPosition = transform.PositionEcl;
                double distanceTraveled = (finalPosition - _animationStartPosition).Length();
                Console.WriteLine($"camera-controller-override: Animation completed. Final position: {finalPosition}, Distance traveled: {distanceTraveled:F2} meters");
            }
            
            // Skip original OnFrame method
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in Controller_OnFrame_Prefix: {ex}");
            return true;
        }
    }
}
