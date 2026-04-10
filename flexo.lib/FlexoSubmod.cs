using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.FlexoLib;

public sealed class FlexoSubmod : ISubmod
{
    public string Name => "Flexo";
    public string Tooltip => "Robotics — hinges, rotors, and articulated parts.";

    public static FlexoSubmod? Current { get; private set; }

    public void Initialize()
    {
        Current = this;
        Console.WriteLine("flexo: Initialized");
    }

    public void Update(double dt)
    {
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##flexo_panel");
        try
        {
            ImGui.TextDisabled("Flexo — Robotics");
            ImGui.Separator();
            ImGui.Text("No definitions loaded.");
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}");
        }
        SubmodUI.EndContentArea();
    }

    public void RenderFloatingWindows()
    {
    }

    public void Dispose()
    {
        Current = null;
        Console.WriteLine("flexo: Disposed");
    }
}
