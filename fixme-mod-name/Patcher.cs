using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace mod;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("fixme-mod-name");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fixme-mod-name: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("fixme-mod-name");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fixme-mod-name: Error removing patches: {ex.Message}");
        }
    }

}
