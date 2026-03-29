using System;
using HarmonyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.KsaAbstractions;

namespace MeowSci.CameraControllerOverride;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch(KeyframeSequencePlayer sequencePlayer)
    {
        try
        {
            _harmony = new Harmony("camera-controller-override");
            CameraControllerOverridePatches.SequencePlayer = sequencePlayer;
            CameraControllerOverridePatches.Apply(_harmony);
            HotkeyGuard.Patch(_harmony);
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
            if (_harmony != null)
            {
                CameraControllerOverridePatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
            }
            _harmony = null;
            CameraControllerOverridePatches.SequencePlayer = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"camera-controller-override: Error removing patches: {ex.Message}");
        }
    }
}