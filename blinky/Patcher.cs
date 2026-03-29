using HarmonyLib;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Blinky;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.Blinky");
        BlinkyPatches.Apply(_harmony);
        HotkeyGuard.Patch(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
        {
            BlinkyPatches.Remove(_harmony);
            HotkeyGuard.Unpatch(_harmony);
        }
        _harmony = null;
    }
}