using System;
using HarmonyLib;
using MeowSci.KsaAbstractions;
using MeowSci.FlexoLib;

namespace MeowSci.Flexo;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("flexo");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null)
            {
                HotkeyGuard.Patch(_harmony);
                FlexoPatches.Apply(_harmony);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                FlexoPatches.Remove(_harmony);
            }
            _harmony?.UnpatchAll("flexo");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error removing patches: {ex.Message}");
        }
    }
}
