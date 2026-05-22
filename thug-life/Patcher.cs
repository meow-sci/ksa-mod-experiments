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
            Console.WriteLine("thug-life: standalone Patcher.Patch() entered");
            _harmony = new Harmony("thug-life");
            HotkeyGuard.Patch(_harmony);
            Console.WriteLine("thug-life: HotkeyGuard patched, about to apply render patches");
            ThugLifeRenderPatches.Apply(_harmony);
            Console.WriteLine("thug-life: standalone Patcher.Patch() completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: Error applying patches: {ex.GetType().Name}: {ex.Message}\n{ex}");
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
