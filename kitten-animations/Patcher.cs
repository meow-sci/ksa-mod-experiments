using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("kitten-animations");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("kitten-animations");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error removing patches: {ex.Message}");
        }
    }

}
