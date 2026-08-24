using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.KiwisMarblesLib;

namespace MeowSci.KiwisMarbles;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("kiwis-marbles");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null)
            {
                HotkeyGuard.Patch(_harmony);
                KiwisMarblesPatches.Apply(_harmony);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                KiwisMarblesPatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
            }
            _harmony?.UnpatchAll("kiwis-marbles");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error removing patches: {ex.Message}");
        }
    }

}
