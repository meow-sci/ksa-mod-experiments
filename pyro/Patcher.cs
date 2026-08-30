using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.PyroLib;

namespace MeowSci.Pyro;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("pyro");
            HotkeyGuard.Patch(_harmony);
            PyroPatches.Apply(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"pyro: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                PyroPatches.Remove(_harmony);
                HotkeyGuard.Unpatch(_harmony);
                _harmony = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"pyro: Error removing patches: {ex.Message}");
        }
    }
}
