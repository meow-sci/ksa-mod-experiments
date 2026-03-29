using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.FixmeModName;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("fixme-mod-name");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
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
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("fixme-mod-name");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fixme-mod-name: Error removing patches: {ex.Message}");
        }
    }

}
