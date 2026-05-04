using HarmonyLib;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.ItsSoShiny;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.ItsSoShiny");
        HotkeyGuard.Patch(_harmony);
        ShinyPatches.Apply(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
        {
            ShinyPatches.Remove(_harmony);
            HotkeyGuard.Unpatch(_harmony);
        }
        _harmony = null;
    }
}