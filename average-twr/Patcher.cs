using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.AverageTwr;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("average-twr");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"average-twr: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("average-twr");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"average-twr: Error removing patches: {ex.Message}");
        }
    }

}
