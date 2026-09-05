using System.Text.RegularExpressions;
using Brutal.ImGuiApi;

namespace MeowSci.KsaAbstractions;

/// <summary>Label above a full-width input; avoids reserving invisible space for a trailing label.</summary>
public static class FormField
{
    public static string Label(string label)
    {
        FormGrid.NextField();
        string visible = label.Split("##")[0];
        ImGui.Spacing();
        ImGui.TextWrapped(Regex.Replace(visible, "([a-z])([A-Z])", "$1 $2"));
        ImGui.SetNextItemWidth(-1f);
        return "##" + label;
    }
}
