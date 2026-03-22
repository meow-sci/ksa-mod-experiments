using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.BlinkyLib;

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

    /// <summary>
    /// Suppresses PartTree.RecomputeAllDerivedData() during batch part creation.
    /// This prevents N² recomputations when adding N pixel engine parts to the vehicle.
    /// The mod calls RecomputeAllDerivedData() manually once after all parts are added.
    /// </summary>
    [HarmonyPatch(typeof(PartTree), "RecomputeAllDerivedData")]
    [HarmonyPrefix]
    private static bool SuppressRecomputeAllDerivedData()
    {
        if (ResourceGraphSuppressor.IsSuppressed)
        {
            Console.WriteLine("blinky: suppressed RecomputeAllDerivedData() call");
            return false; // skip original
        }
        return true; // let it run
    }
}

