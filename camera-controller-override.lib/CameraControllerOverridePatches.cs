using System;
using System.Reflection;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.CameraControllerOverrideLib.Animation;

namespace MeowSci.CameraControllerOverrideLib;

public static class CameraControllerOverridePatches
{
    private static KeyframeSequencePlayer? _sequencePlayer;

    public static KeyframeSequencePlayer? SequencePlayer
    {
        get => _sequencePlayer;
        set => _sequencePlayer = value;
    }

    public static void Apply(Harmony harmony)
    {
        var prefix = new HarmonyMethod(typeof(CameraControllerOverridePatches)
            .GetMethod(nameof(OnFramePrefix), BindingFlags.Static | BindingFlags.NonPublic));

        var orbitOnFrame = AccessTools.Method(typeof(OrbitController), "OnFrame");
        var flyOnFrame = AccessTools.Method(typeof(FlyController), "OnFrame");

        if (orbitOnFrame != null)
            harmony.Patch(orbitOnFrame, prefix: prefix);
        if (flyOnFrame != null)
            harmony.Patch(flyOnFrame, prefix: prefix);

        Console.WriteLine("camera-controller-override.lib: patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        harmony.UnpatchAll(harmony.Id);
        Console.WriteLine("camera-controller-override.lib: patches removed");
    }

    private static bool OnFramePrefix(Controller __instance, double inDeltaTime, Transform3D ___Transform)
    {
        try
        {
            if (_sequencePlayer != null && _sequencePlayer.State == PlaybackState.Playing)
            {
                bool shouldSkip = _sequencePlayer.Update(__instance, ___Transform, inDeltaTime);
                return !shouldSkip;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error in prefix: {ex.Message}");
            return true;
        }
    }
}
