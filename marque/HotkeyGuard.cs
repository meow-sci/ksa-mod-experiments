using System;
using System.Reflection;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace MeowSci.Marque;

/// <summary>
/// Blocks game hotkeys (GameSettings.OnKeyAll) whenever an ImGui text input has keyboard focus.
/// Uses the global ImGui WantTextInput flag so every InputText / combo filter is covered automatically.
/// </summary>
public static class HotkeyGuard
{
    private static MethodInfo? _original;
    private static MethodInfo? _prefix;

    public static void Patch(Harmony harmony)
    {
        _original = AccessTools.Method(typeof(GameSettings), nameof(GameSettings.OnKeyAll));
        _prefix = typeof(HotkeyGuard).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static)!;
        harmony.Patch(_original, prefix: new HarmonyMethod(_prefix));
        Console.WriteLine("ksa-abstractions: HotkeyGuard patch applied");
    }

    public static void Unpatch(Harmony harmony)
    {
        if (_original != null && _prefix != null)
            harmony.Unpatch(_original, _prefix);
        _original = null;
        _prefix = null;
        Console.WriteLine("ksa-abstractions: HotkeyGuard patch removed");
    }

    private static bool Prefix(ref bool __result)
    {
        if (ImGui.GetIO().WantTextInput)
        {
            __result = true;
            return false;
        }
        return true;
    }
}
