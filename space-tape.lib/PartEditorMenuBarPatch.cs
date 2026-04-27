using System;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Adds a "Part Editor" top-level menu to the game's menu bar when the editor scene is active.
/// Menu contains a single "Exit Part Editor" item to cleanly close the editor.
/// </summary>
internal static class PartEditorMenuBarPatch
{
    private static Harmony? _harmony;
    private const string HarmonyId = "MeowSci.SpaceTape.MenuBar";

    public static void Patch()
    {
        _harmony = new Harmony(HarmonyId);
        _harmony.CreateClassProcessor(typeof(PartEditorMenuBarPatch)).Patch();
        Console.WriteLine("space-tape: PartEditorMenuBarPatch applied");
    }

    public static void Unpatch()
    {
        _harmony?.UnpatchAll(HarmonyId);
        _harmony = null;
        Console.WriteLine("space-tape: PartEditorMenuBarPatch removed");
    }

    [HarmonyPatch(typeof(Program), nameof(Program.DrawProgramMenusHook))]
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (SpaceTapeSubmod.Current?.IsEditorActive != true) return;

        if (ImGui.BeginMenu("Part Editor"))
        {
            Program.MainViewport.MenuBarInUse = true;

            bool subPartsOpen = SpaceTapeSubmod.Current?.IsSubPartsWindowOpen ?? false;
            if (ImGui.MenuItem("Toggle SubParts Window", "", ref subPartsOpen))
            {
                if (SpaceTapeSubmod.Current != null)
                    SpaceTapeSubmod.Current.IsSubPartsWindowOpen = subPartsOpen;
            }

            bool editorWindowOpen = SpaceTapeSubmod.Current?.IsPartEditorWindowOpen ?? false;
            if (ImGui.MenuItem("Toggle Part Editor Window", "", ref editorWindowOpen))
            {
                if (SpaceTapeSubmod.Current != null)
                    SpaceTapeSubmod.Current.IsPartEditorWindowOpen = editorWindowOpen;
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Exit Part Editor"))
                SpaceTapeSubmod.Current?.ExitEditorFromMenu();

            ImGui.EndMenu();
        }
    }
}
