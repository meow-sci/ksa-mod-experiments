using HarmonyLib;
using MeowSci.GlassLib;

namespace MeowSci.Glass;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.Glass");
        GlassPatches.Apply(_harmony);
    }

    public static void Unload()
    {
        if (_harmony != null)
            GlassPatches.Remove(_harmony);
        _harmony = null;
    }
}