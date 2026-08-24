using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.DontStifleMeLib;

/// <summary>
/// ImGui surface for dont-stifle-me: one master toggle plus two sub-options.
/// </summary>
public sealed class DontStifleMeSubmod : ISubmod
{
    public string Name => "Don't Stifle Me - Editor Scale Limits";
    public string Tooltip => "Removes the vehicle editor's 0.5x-2x part scale clamp and restores per-axis (non-uniform) part scaling.";

    public void Initialize() { }
    public void Update(double dt) { }
    public void Dispose() { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##dsm_content");

        ImGui.Checkbox("Don't stifle me", ref EditorScaleSettings.Enabled);
        ImGui.SetItemTooltip("Master switch. Off = stock editor behavior (0.5x-2x clamp, uniform scaling).");

        ImGui.BeginDisabled(!EditorScaleSettings.Enabled);
        ImGui.Indent();
        ImGui.Checkbox("Remove 0.5x-2x scale clamp", ref EditorScaleSettings.RemoveClamp);
        ImGui.SetItemTooltip("Top-level parts can be scaled to any positive size. 0.25 m diameter snapping still applies.");
        ImGui.Checkbox("Per-axis (non-uniform) scaling", ref EditorScaleSettings.PerAxisScaling);
        ImGui.SetItemTooltip("Dragging a scale gizmo arrow changes only that axis (X/Y/Z) instead of all three.\nConnectors and mass follow the largest axis (game limitation).");
        ImGui.Unindent();
        ImGui.EndDisabled();

        if (!EditorScalePatches.IsApplied)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Brutal.Numerics.float4(1f, 0.3f, 0.3f, 1f), "Editor patches are not applied - check the log.");
        }

        SubmodUI.EndContentArea();
    }
}
