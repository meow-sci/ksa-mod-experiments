using System;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.SkittlesLib;

/// <summary>Manual Harmony patch helpers for skittles hotkey blocking.</summary>
public static class SkittlesPatches
{
    private static Func<bool>? _hasFocusedTextInput;

    private static MethodInfo? _gameSettingsOnKeyAll;
    private static MethodInfo? _onKeyAllPrefix;

    public static void Apply(Harmony harmony, Func<bool> hasFocusedTextInput)
    {
        _hasFocusedTextInput = hasFocusedTextInput;

        _onKeyAllPrefix = typeof(SkittlesPatches).GetMethod(nameof(OnKeyAllPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;
        _gameSettingsOnKeyAll = AccessTools.Method(typeof(GameSettings), nameof(GameSettings.OnKeyAll));

        harmony.Patch(_gameSettingsOnKeyAll, prefix: new HarmonyMethod(_onKeyAllPrefix));

        Console.WriteLine("skittles.lib: patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        if (_gameSettingsOnKeyAll != null && _onKeyAllPrefix != null)
            harmony.Unpatch(_gameSettingsOnKeyAll, _onKeyAllPrefix);

        _hasFocusedTextInput = null;
        _gameSettingsOnKeyAll = null;
        _onKeyAllPrefix = null;

        Console.WriteLine("skittles.lib: patches removed");
    }

    // Block game hotkeys while a Skittles text input has keyboard focus.
    private static bool OnKeyAllPrefix(ref bool __result)
    {
        if (_hasFocusedTextInput?.Invoke() == true)
        {
            __result = true;
            return false; // skip original, hotkey blocked
        }
        return true;
    }
}
