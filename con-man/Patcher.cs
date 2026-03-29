using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.ConMan;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.ConMan");
        HotkeyGuard.Patch(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
            HotkeyGuard.Unpatch(_harmony);
        _harmony = null;
    }
}
