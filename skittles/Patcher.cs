using System;
using Brutal.ImGuiApi;
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

// Block game hotkeys while any ImGui text input has keyboard focus.
// ImGui.GetIO().WantTextInput is a per-frame flag set automatically by ImGui;
// it resets each frame so it can never get stuck like a manual static bool.
[HarmonyPatch(typeof(GameSettings), nameof(GameSettings.OnKeyAll))]
static class PatchGameSettingsOnKeyAll
{
    static bool Prefix(ref bool __result)
    {
        if (ImGui.GetIO().WantTextInput)
        {
            __result = true;
            return false; // skip original, hotkey is blocked
        }
        return true; // run original
    }
}
