using System;
using HarmonyLib;
using MeowSci.BlinkyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Grant;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static VehicleTracker? IFeelSeenTracker { private get; set; }
    public static KeyframeSequencePlayer? CameraSequencePlayer { private get; set; }

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("MeowSci.Grant");
            HotkeyGuard.Patch(_harmony);
            BlinkyPatches.Apply(_harmony);
            CameraControllerOverridePatches.SequencePlayer = CameraSequencePlayer;
            CameraControllerOverridePatches.Apply(_harmony);
            GlassPatches.Apply(_harmony);
            IFeelSeenPatches.Apply(_harmony, IFeelSeenTracker!);
            Console.WriteLine("grant: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                BlinkyPatches.Remove(_harmony);
                CameraControllerOverridePatches.Remove(_harmony);
                GlassPatches.Remove(_harmony);
                IFeelSeenPatches.Remove(_harmony);
            }
            _harmony = null;
            IFeelSeenTracker = null;
            CameraSequencePlayer = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error removing patches: {ex.Message}");
        }
    }
}
