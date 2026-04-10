using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.FlexoLib.Runtime;

public static class FlexoRuntimeUi
{
    public static void Render(FlexoRuntime runtime)
    {
        // Header buttons
        if (ImGui.Button("Scan Vehicle"))
            runtime.ScanVehicle();

        ImGui.SameLine();

        if (ImGui.Button("Reload Definitions"))
            runtime.ReloadDefinitions();

        ImGui.SameLine();

        if (runtime.HasScanned)
        {
            if (ImGui.Button("Clear"))
                runtime.ClearScan();
        }

        // Status
        ImGui.Spacing();
        ImGui.TextDisabled($"Definitions: {runtime.Definitions.Count}");
        if (runtime.HasScanned)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"| Active hinges: {runtime.ActiveHinges.Count}");
        }

        if (runtime.ScanStatusMessage != null)
        {
            ImGui.TextColored(
                runtime.ActiveHinges.Count > 0
                    ? new float4(0.3f, 1f, 0.3f, 1f)
                    : new float4(1f, 0.8f, 0.3f, 1f),
                runtime.ScanStatusMessage);
        }

        if (runtime.Definitions.Count == 0 && !runtime.HasScanned)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No definitions found. Use the editor to create one.");
            return;
        }

        // Per-hinge controls
        ImGui.Spacing();
        for (int i = 0; i < runtime.ActiveHinges.Count; i++)
        {
            var controller = runtime.ActiveHinges[i];
            RenderHingeControls(controller, i);
        }
    }

    private static void RenderHingeControls(HingeController controller, int index)
    {
        var def = controller.Definition;
        var hinge = def.Hinge!;
        string headerLabel = $"{def.DisplayName}##hinge_{index}";

        if (!ImGui.CollapsingHeader(headerLabel, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Indent();

        // Part info
        ImGui.TextDisabled($"Fixed: {controller.FixedPart.Template.Id}");
        ImGui.TextDisabled($"Moving: {controller.MovingPart.Template.Id}");

        // Angle slider
        float angle = (float)controller.CurrentDegrees;
        float minDeg = (float)hinge.MinDegrees;
        float maxDeg = (float)hinge.MaxDegrees;
        if (ImGui.DragFloat($"Angle##hinge_angle_{index}", ref angle, 1.0f, minDeg, maxDeg))
            controller.SetImmediate(angle);

        // Speed display
        ImGui.TextDisabled($"Speed: {hinge.SpeedDegreesPerSecond:F0} °/s");

        // Status
        if (controller.IsAnimating)
        {
            ImGui.SameLine();
            ImGui.TextColored(new float4(0.3f, 0.8f, 1f, 1f),
                $" → {controller.TargetDegrees:F1}°");
        }

        // Control buttons
        if (ImGui.Button($"Open##hinge_open_{index}"))
            controller.Open();
        ImGui.SameLine();
        if (ImGui.Button($"Close##hinge_close_{index}"))
            controller.Close();
        ImGui.SameLine();
        if (ImGui.Button($"Reset##hinge_reset_{index}"))
            controller.Reset();

        ImGui.Unindent();
        ImGui.Separator();
    }
}
