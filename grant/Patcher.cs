using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.Grant;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("MeowSci.Grant");

    /// <summary>When false, pixel-engine meshes are hidden for better performance.</summary>
    public static bool RenderPixelParts = false;

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("grant");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error removing patches: {ex.Message}");
        }
    }

}
