using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.RockyMcRockFace;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("rocky-mcrock-face");
            HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                _harmony = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: Error removing patches: {ex.Message}");
        }
    }
}
