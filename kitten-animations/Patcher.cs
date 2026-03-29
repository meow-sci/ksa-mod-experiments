using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimations;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("kitten-animations");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
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
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("kitten-animations");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error removing patches: {ex.Message}");
        }
    }

}
