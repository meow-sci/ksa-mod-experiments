using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.HumbleArteestLib.Experiments;

namespace MeowSci.HumbleArteest;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("humble-arteest");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
            if (_harmony != null) PaddingTest.ApplyPatches(_harmony);
            if (_harmony != null) TemperatureTest.ApplyPatches(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) TemperatureTest.RemovePatches(_harmony);
            if (_harmony != null) PaddingTest.RemovePatches(_harmony);
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("humble-arteest");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error removing patches: {ex.Message}");
        }
    }

}
