using System;
using HarmonyLib;

namespace MeowSci.Skittles;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("skittles");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("skittles");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error removing patches: {ex.Message}");
        }
    }

}
