using System;
using HarmonyLib;
using KSA;
using MeowSci.DontStifleMeLib;

namespace MeowSci.DontStifleMe;

/// <summary>
/// Adds the "Don't Stifle Me" top-level menu to the game's main menu bar (standalone mod only;
/// inside unscience the same controls live in the submod section).
/// </summary>
internal static class MenuBarPatch
{
    private static readonly System.Reflection.MethodInfo? Target =
        AccessTools.Method(typeof(Program), nameof(Program.DrawProgramMenusHook));

    public static void Apply(Harmony harmony)
    {
        if (Target == null) throw new MissingMethodException(nameof(Program), nameof(Program.DrawProgramMenusHook));
        harmony.Patch(Target, postfix: new HarmonyMethod(typeof(MenuBarPatch), nameof(Postfix)));
    }

    public static void Remove(Harmony harmony)
    {
        if (Target != null) harmony.Unpatch(Target, HarmonyPatchType.Postfix, harmony.Id);
    }

    private static void Postfix()
    {
        try { DontStifleMeMenu.Draw(); }
        catch (Exception ex) { Console.WriteLine($"dont-stifle-me: menu draw failed: {ex.Message}"); }
    }
}
