using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.CameraControllerOverrideLib.Animation;

namespace MeowSci.CameraControllerOverride;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("camera-controller-override");
    private static KeyframeSequencePlayer _sequencePlayer = new KeyframeSequencePlayer();

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
            // Only check sequence player
            if (_sequencePlayer.State == PlaybackState.Playing)
            {
                bool shouldSkip = _sequencePlayer.Update(controller, transform, deltaTime);
                return !shouldSkip;
            }
            
            // Allow normal camera control when sequence not playing
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
            return true;
        }
    }
}
