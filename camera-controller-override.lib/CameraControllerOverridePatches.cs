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

        if (orbitOnFrame == null || flyOnFrame == null) throw new MissingMethodException("Camera controller OnFrame target is unavailable.");
        harmony.Patch(orbitOnFrame, prefix: prefix);
        harmony.Patch(flyOnFrame, prefix: prefix);

        Console.WriteLine("camera-controller-override.lib: patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        var prefix = AccessTools.Method(typeof(CameraControllerOverridePatches), nameof(OnFramePrefix));
        foreach (var type in new[] { typeof(OrbitController), typeof(FlyController) })
        {
            var target = AccessTools.Method(type, "OnFrame");
            if (target != null) harmony.Unpatch(target, prefix);
        }
        Console.WriteLine("camera-controller-override.lib: patches removed");
    }

    private static bool OnFramePrefix(Controller __instance, double inDeltaTime)
    {
        try
        {
            if (_sequencePlayer != null && _sequencePlayer.State == PlaybackState.Playing)
            {
                // The camera transform IS the controller's public `Camera` field
                // (KSA.Camera : Transform3D). KSA.Controller/OrbitController/FlyController have
                // no private `Transform` field, so the previous `Transform3D ___Transform`
                // Harmony field injector bound to nothing and threw at patch time — which also
                // aborted the rest of the supermod's patch chain. Reading __instance.Camera
                // mutates the real camera by reference, so animation moves the live view.
                bool shouldSkip = _sequencePlayer.Update(__instance, __instance.Camera, inDeltaTime);
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
