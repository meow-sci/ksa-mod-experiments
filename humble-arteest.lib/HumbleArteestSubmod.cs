using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// Composite ISubmod that groups Vehicle Paint, Kitten Color, and Engine Emissive
/// under a single "Humble Arteest" collapsing section in the Unscience Toolbox.
/// </summary>
public sealed partial class HumbleArteestSubmod : IWorkspaceFeature
{
    public string Name => "Humble Arteest - Paint Stuff";
    public string Tooltip => "Vehicle paint, Kitten color, and engine emissive paint brushes";

    private readonly VehiclePaintSubmod _vehiclePaint = new();
    private readonly KittenColorSubmod _kittenColor = new();
    private readonly EngineEmissiveSubmod _engineEmissive = new();

    public void Initialize()
    {
        _vehiclePaint.Initialize();
        _kittenColor.Initialize();
        _engineEmissive.Initialize();
    }

    public void Update(double dt)
    {
        _vehiclePaint.Update(dt);
        _kittenColor.Update(dt);
        _engineEmissive.Update(dt);
    }

    private void RenderLivePaint()
    {
        SubmodUI.BeginContentArea("##ha_content");

        ImGui.SeparatorText("Vehicle Paint");
        ImGui.SetItemTooltip(VehiclePaintSubmod.HeaderTooltip);
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        _vehiclePaint.RenderBody();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.SeparatorText("Kitten Color");
        ImGui.SetItemTooltip(
            "Tints kitten character models by writing AlbedoColor into the\n" +
            "GPU material buffer. Only affects models using ModelPbr.frag\n" +
            "(fur, glass, eyes) — vehicle parts use a different shader path.\n\n" +
            "Alpha < 0.1 triggers discard (makes parts invisible).\n" +
            "The material list is for reference only — color applies to all.");
        ImGui.Spacing();
        ImGui.Spacing();
        _kittenColor.RenderBody();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.SeparatorText("Engine Emissive");
        ImGui.SetItemTooltip(
            "Overrides the Temperature field on dynamic engine parts to control\n" +
            "their emissive glow. Uses the game's existing per-instance Temperature\n" +
            "data path — no shader modifications needed.\n\n" +
            "Temperature drives the ENABLE_TEMPERATURE variant of MeshIndirect.frag\n" +
            "and its heat lookup table, making engines glow from cool to hot.");
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        _engineEmissive.RenderBody();

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        _vehiclePaint.Dispose();
        _kittenColor.Dispose();
        _engineEmissive.Dispose();
    }
}
