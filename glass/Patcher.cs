using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.Glass;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("glass");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("glass");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error removing patches: {ex.Message}");
        }
    }

}
