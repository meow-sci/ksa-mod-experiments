using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("blinken");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinken: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("blinken");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinken: Error removing patches: {ex.Message}");
        }
    }

}
