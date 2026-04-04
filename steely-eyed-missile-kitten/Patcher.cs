using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SteelyEyedMissileKitten;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("steely-eyed-missile-kitten");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"steely-eyed-missile-kitten: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("steely-eyed-missile-kitten");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"steely-eyed-missile-kitten: Error removing patches: {ex.Message}");
        }
    }

}
