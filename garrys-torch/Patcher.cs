using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorch;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("garrys-torch");
            HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"garrys-torch: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("garrys-torch");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"garrys-torch: Error removing patches: {ex.Message}");
        }
    }
}
