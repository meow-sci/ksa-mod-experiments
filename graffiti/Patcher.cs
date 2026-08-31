using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.GraffitiLib;

namespace MeowSci.Graffiti;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("graffiti");
            HotkeyGuard.Patch(_harmony);
            GraffitiPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                GraffitiPatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
                _harmony = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: Error removing patches: {ex.Message}");
        }
    }
}
