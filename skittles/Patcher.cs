using System;
using HarmonyLib;
using MeowSci.SkittlesLib;

namespace MeowSci.Skittles;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch(Func<bool> hasFocusedTextInput)
    {
        _harmony = new Harmony("MeowSci.Skittles");
        SkittlesPatches.Apply(_harmony, hasFocusedTextInput);
    }

    public static void Unload()
    {
        if (_harmony != null)
            SkittlesPatches.Remove(_harmony);
        _harmony = null;
    }
}