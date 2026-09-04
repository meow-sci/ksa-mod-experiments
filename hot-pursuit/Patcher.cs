using System;
using HarmonyLib;
using MeowSci.HotPursuitLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.HotPursuit;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("hot-pursuit");
            HotkeyGuard.Patch(_harmony);
            HotPursuitPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"hot-pursuit: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotPursuitPatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
            }
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"hot-pursuit: Error removing patches: {ex.Message}");
        }
    }
}
