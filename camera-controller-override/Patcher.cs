using System;
using HarmonyLib;
using Brutal.Numerics;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("camera-controller-override");

    // Animation state
#pragma warning disable CS0169, CS0414 // Field is never used / assigned but never used (will be used in future tasks)
    private static bool _isAnimationEnabled = false;
    private static bool _isAnimationActive = false;
    private static double _animationElapsedTime = 0.0;
    private static double3 _animationStartPosition;
    private static double3 _animationDirection;
    private static double _animationSpeedMetersPerSecond = 1.0;
#pragma warning restore CS0169, CS0414

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
}
