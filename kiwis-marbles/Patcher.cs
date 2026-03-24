using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.KiwisMarbles;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("kiwis-marbles");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("kiwis-marbles");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error removing patches: {ex.Message}");
        }
    }

}
