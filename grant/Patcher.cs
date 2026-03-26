using System;
using HarmonyLib;
using MeowSci.BlinkyLib;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;
using MeowSci.SkittlesLib;

namespace MeowSci.Grant;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static VehicleTracker? IFeelSeenTracker { private get; set; }
    public static Func<bool>? SkittlesHasFocusedTextInput { private get; set; }

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("MeowSci.Grant");
            BlinkyPatches.Apply(_harmony);
            GlassPatches.Apply(_harmony);
            IFeelSeenPatches.Apply(_harmony, IFeelSeenTracker!);
            SkittlesPatches.Apply(_harmony, SkittlesHasFocusedTextInput!);
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
                BlinkyPatches.Remove(_harmony);
                GlassPatches.Remove(_harmony);
                IFeelSeenPatches.Remove(_harmony);
                SkittlesPatches.Remove(_harmony);
            }
            _harmony = null;
            IFeelSeenTracker = null;
            SkittlesHasFocusedTextInput = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error removing patches: {ex.Message}");
        }
    }
}
