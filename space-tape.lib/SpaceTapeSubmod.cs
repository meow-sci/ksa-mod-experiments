using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

public sealed class SpaceTapeSubmod : ISubmod
{
    public string Name => "Space Tape";
    public string Tooltip => "In-game Part editor. Compose new Parts from existing SubParts.";

    public void Initialize() { }

    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##space_tape_content");
        try
        {
            ImGui.Text("Space Tape Part Editor - coming soon");
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Render error: {ex.Message}");
        }
        SubmodUI.EndContentArea();
    }

    public void Dispose() { }
}
