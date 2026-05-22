using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.ThugLifeLib;

namespace MeowSci.ThugLife;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("thug-life");
            HotkeyGuard.Patch(_harmony);
            ThugLifeRenderPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                ThugLifeRenderPatches.Remove(_harmony);
            }
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error removing patches: {ex.Message}");
        }
    }
}
