using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.ConMan;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("con-man");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"con-man: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("con-man");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"con-man: Error removing patches: {ex.Message}");
        }
    }

}
