using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.DontStifleMeLib;

/// <summary>
/// ImGui surface for dont-stifle-me's scale and editor value-limit controls.
/// </summary>
public sealed partial class DontStifleMeSubmod : IWorkspaceFeature
{
    public string Name => "Don't Stifle Me - Editor Limits";
    public string Tooltip => "Removes restrictive vehicle-editor scale and configurable-value limits.";

    private bool _enabled = true, _snap = true, _expandedLimits;
    private static void ApplyPolicy(bool enabled, bool snap, bool expanded)
    {
        EditorScaleSettings.Enabled = enabled; EditorScaleSettings.Snap = snap;
        EditorLimitSettings.JplSaidNoClamps = expanded;
        if (!expanded) EditorValueLimitPatches.RestoreTrackedBounds();
    }
    public void Initialize() { }
    public void Update(double dt) { }
    public void Dispose()
    {
        ReleaseLiveState();
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##dsm_content");

        ImGui.Checkbox("Enabled", ref _enabled);
        ImGui.Checkbox("Snap scaling", ref _snap);
        ImGui.Checkbox("jpl said no clamps", ref _expandedLimits);
        if (ImGui.Button(" Apply editor policy ")) ApplyPolicy(_enabled, _snap, _expandedLimits);

        if (EditorScaleSettings.Enabled && !EditorScalePatches.IsApplied || EditorLimitSettings.JplSaidNoClamps && !EditorValueLimitPatches.IsApplied)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Brutal.Numerics.float4(1f, 0.3f, 0.3f, 1f), "Editor patches are not applied - check the log.");
        }

        SubmodUI.EndContentArea();
    }
}
