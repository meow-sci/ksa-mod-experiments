using HarmonyLib;
using MeowSci.IFeelSeenLib;

namespace MeowSci.IFeelSeen;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch(VehicleTracker tracker)
    {
        _harmony = new Harmony("MeowSci.IFeelSeen");
        IFeelSeenPatches.Apply(_harmony, tracker);
    }

    public static void Unload()
    {
        if (_harmony != null)
            IFeelSeenPatches.Remove(_harmony);
        _harmony = null;
    }
}