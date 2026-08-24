using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.DontStifleMeLib;

namespace MeowSci.DontStifleMe;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("dont-stifle-me");
            HotkeyGuard.Patch(_harmony);
            EditorScalePatches.Apply(_harmony);
            MenuBarPatch.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dont-stifle-me: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                MenuBarPatch.Remove(_harmony);
                EditorScalePatches.Remove(_harmony);
            }
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dont-stifle-me: Error removing patches: {ex.Message}");
        }
    }
}
