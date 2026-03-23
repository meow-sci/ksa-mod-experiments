using System;
using HarmonyLib;

namespace MeowSci.Blinky;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("blinky");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("blinky");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error removing patches: {ex.Message}");
        }
    }

    // RecomputeAllDerivedData suppression removed — letting each Merge trigger it normally.
    // Re-enable as an optimisation once the basic part-creation flow is confirmed working.
}

