using System;
using KSA;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KitchenSinkLib;

/// <summary>
/// Submod for kitchen-sink: a collection of one-off hacks and fixes for KSA.
/// </summary>
public sealed class KitchenSinkSubmod : ISubmod
{
    public string Name => "Kitchen Sink";
    public string Tooltip => "Random collection of one-off hacks and fixes for KSA.";

    public void Initialize() { }

    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##ks_content");
        RenderFixInvisibleSubparts();
        SubmodUI.EndContentArea();
    }

    private void RenderFixInvisibleSubparts()
    {
        ImGui.SeparatorText("Fix Invisible Subparts");
        ImGui.TextWrapped("Workaround for a KSA bug where subparts become invisible in the editor. Click the button below to reinitialize the vehicle part tree.");
        ImGui.Spacing();

        if (ImGui.Button("Refresh Vehicle", new float2(334f, 36f)))
        {
            var editor = Program.Editor;
            if (editor?.EditingSpace?.Parts != null)
            {
                var oldStates = editor.EditingSpace.Parts.States;
                editor.EditingSpace.Parts.ReinitializeDerivedValues(oldStates);
                Console.WriteLine("kitchen-sink: ReinitializeDerivedValues called on editor parts.");
            }
            else
            {
                Console.WriteLine("kitchen-sink: Editor or parts not available — open the vehicle editor first.");
            }
        }
    }

    public void Dispose() { }
}
