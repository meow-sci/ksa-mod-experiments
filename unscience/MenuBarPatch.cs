using System;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace MeowSci.Unscience;

[HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
internal static class MenuBarPatch
{
    public static Action? ToggleWindow { get; set; }

    public static void Apply(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(MenuBarPatch)).Patch();
        Console.WriteLine("unscience: MenuBarPatch applied");
    }

    public static void Remove(Harmony harmony)
    {
        harmony.Unpatch(
            AccessTools.Method(typeof(Program), nameof(Program.DrawProgramMenusHook)),
            HarmonyPatchType.Postfix,
            harmony.Id);
    }

    [HarmonyPostfix]
    static void Postfix()
    {
        if (ImGui.MenuItem("Unscience"))
            ToggleWindow?.Invoke();
    }
}
