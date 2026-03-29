using HarmonyLib;
using MeowSci.GlassLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Glass;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.Glass");
        GlassPatches.Apply(_harmony);
        HotkeyGuard.Patch(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
        {
            GlassPatches.Remove(_harmony);
            HotkeyGuard.Unpatch(_harmony);
        }
        _harmony = null;
    }
}