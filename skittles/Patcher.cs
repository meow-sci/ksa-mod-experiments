using System;
using HarmonyLib;
using KSA;

namespace MeowSci.Skittles;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("skittles");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("skittles");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error removing patches: {ex.Message}");
        }
    }

}

// Block game hotkeys while a Skittles text input has keyboard focus.
// Uses a per-frame flag set by Mod during rendering, scoped to only
// Skittles windows so the in-game console and other handlers are unaffected.
[HarmonyPatch(typeof(GameSettings), nameof(GameSettings.OnKeyAll))]
static class PatchGameSettingsOnKeyAll
{
    static bool Prefix(ref bool __result)
    {
        if (Mod.SkittlesHasFocusedTextInput)
        {
            __result = true;
            return false; // skip original, hotkey is blocked
        }
        return true; // run original
    }
}
