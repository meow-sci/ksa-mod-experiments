using System;
using HarmonyLib;
using MeowSci.FreeFallinLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.FreeFallin;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("free-fallin");
            HotkeyGuard.Patch(_harmony);
            FreeFallinPatches.Apply(_harmony);
        }
        catch (Exception ex) { Console.WriteLine($"free-fallin: patching failed: {ex.Message}"); }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony == null) return;
            FreeFallinPatches.Remove(_harmony);
            HotkeyGuard.Unpatch(_harmony);
            _harmony = null;
        }
        catch (Exception ex) { Console.WriteLine($"free-fallin: unpatching failed: {ex.Message}"); }
    }
}
