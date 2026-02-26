using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("stampy");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"stampy: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("stampy");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"stampy: Error removing patches: {ex.Message}");
        }
    }

}
