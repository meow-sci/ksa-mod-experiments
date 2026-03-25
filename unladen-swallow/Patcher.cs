using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.UnladenSwallow;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("unladen-swallow");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("unladen-swallow");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error removing patches: {ex.Message}");
        }
    }

}
