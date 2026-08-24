using Brutal.ImGuiApi;

namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Top-level "Don't Stifle Me" menu for the game's main menu bar. Call from inside the menu bar
/// (e.g. a postfix on <c>Program.DrawProgramMenusHook</c>).
/// </summary>
public static class DontStifleMeMenu
{
    public static void Draw()
    {
        if (!ImGui.BeginMenu("Don't Stifle Me")) return;

        ImGui.MenuItem("Enabled", "", ref EditorScaleSettings.Enabled);
        ImGui.SetItemTooltip("Lift the 0.5x-2x part scale clamp and scale per axis (X/Y/Z) with the gizmo.\nOff = stock editor.");

        ImGui.BeginDisabled(!EditorScaleSettings.Enabled);
        ImGui.MenuItem("Snap scaling", "", ref EditorScaleSettings.Snap);
        ImGui.SetItemTooltip("Snap scale drags to 0.25 m diameter increments (game default).\nOff = free, continuous scaling.");
        ImGui.EndDisabled();

        if (!EditorScalePatches.IsApplied)
        {
            ImGui.Separator();
            ImGui.TextColored(new Brutal.Numerics.float4(1f, 0.3f, 0.3f, 1f), "Patches not applied - check log");
        }

        ImGui.EndMenu();
    }
}
