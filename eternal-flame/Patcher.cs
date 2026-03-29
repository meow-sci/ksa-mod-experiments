using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlame;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("eternal-flame");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("eternal-flame");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error removing patches: {ex.Message}");
        }
    }

}
