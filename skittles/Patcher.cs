using HarmonyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Skittles;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.Skittles");
        HotkeyGuard.Patch(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
            HotkeyGuard.Unpatch(_harmony);
        _harmony = null;
    }
}