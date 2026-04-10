using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.FlexoLib.Runtime;
using MeowSci.KsaAbstractions;

namespace MeowSci.FlexoLib;

public sealed class FlexoSubmod : ISubmod
{
    public string Name => "Flexo";
    public string Tooltip => "Robotics — hinges, rotors, and articulated parts.";

    public static FlexoSubmod? Current { get; private set; }

    private readonly FlexoRuntime _runtime = new();
    private bool _editorOpen = false;

    public FlexoRuntime Runtime => _runtime;
    public bool EditorOpen { get => _editorOpen; set => _editorOpen = value; }

    public void Initialize()
    {
        Current = this;
        _runtime.Initialize();
        Console.WriteLine("flexo: Initialized");
    }

    public void Update(double dt)
    {
        _runtime.Update(dt);
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##flexo_panel");
        try
        {
            if (ImGui.Button("Open Editor"))
                _editorOpen = true;

            ImGui.Separator();
            FlexoRuntimeUi.Render(_runtime);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}");
        }
        SubmodUI.EndContentArea();
    }

    public void RenderFloatingWindows()
    {
        // Editor window will be wired in Phase 5
    }

    public void Dispose()
    {
        Current = null;
        Console.WriteLine("flexo: Disposed");
    }
}
