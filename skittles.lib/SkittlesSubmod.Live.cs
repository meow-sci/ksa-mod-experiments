using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
namespace MeowSci.SkittlesLib;

public sealed partial class SkittlesSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
yield return new LiveStateItem<ThemeManager>("theme", "ImGui style", "Global UI", "Applied", _themeManager, manager =>
        {
            if (ImGui.Button("Copy style to workspace", new float2(-1, 0))) _themeDraft = ThemeDefinition.CaptureFromImGui();
            if (ImGui.Button("Restore game style", new float2(-1, 0))) manager.RestoreDefaults();
            ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##theme-name", "Legacy theme name", _themeNameInput);
            if (ImGui.Button("Save legacy theme", new float2(-1, 0)) && !string.IsNullOrWhiteSpace(_themeNameInput.ToString())) manager.SaveCurrentAsTheme(_themeNameInput.ToString());
            ImGui.ShowStyleEditor();
        });
    }

}
