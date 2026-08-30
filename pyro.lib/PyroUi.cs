using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.PyroLib;

/// <summary>Small shared ImGui widgets used by the pyro panels.</summary>
internal static class PyroUi
{
    public static string PartLabel(Part part) => $"{part.Template.Id}  [{part.Id}]";

    public static void FilteredCombo(string id, string[] items, ref int selectedIndex, ImInputString filter)
    {
        string preview = selectedIndex >= 0 && selectedIndex < items.Length ? items[selectedIndex] : "Select...";
        if (!ImGui.BeginCombo(id, preview)) return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            filter.Clear();
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint($"{id}_filter", "filter..."u8, filter);
        string filterText = filter.ToString().Trim();

        for (int i = 0; i < items.Length; i++)
        {
            if (filterText.Length > 0 && !items[i].Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;
            bool sel = selectedIndex == i;
            ImGui.PushID(i);
            if (ImGui.Selectable(items[i], sel)) selectedIndex = i;
            ImGui.PopID();
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    public static void OffsetFields(string idPrefix, ref float3 position, ref float3 rotation)
    {
        ImGui.Text("Position offset (x, y, z) in meters");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_pos", ref position, 0.001f, 0f, 0f);

        ImGui.Spacing();
        ImGui.Text("Rotation offset (x, y, z) in degrees");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_rot", ref rotation, 0.1f, -180f, 180f);
    }

    /// <summary>Begins a 4-column label/widget parameter grid with the repo's standard padding.</summary>
    public static bool BeginParamGrid(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable(id, 4, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
            return true;
        ImGui.PopStyleVar();
        return false;
    }

    public static void EndParamGrid()
    {
        ImGui.EndTable();
        ImGui.PopStyleVar();
    }

    public static bool GridDrag(string label, string id, ref float value, float speed, float min, float max,
        string format = "%.3f")
    {
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
        return ImGui.DragFloat(id, ref value, speed, min, max, format);
    }

    public static bool GridDragDouble(string label, string id, ref double value, float speed, float min, float max,
        string format = "%.3f")
    {
        float f = (float)value;
        bool changed = GridDrag(label, id, ref f, speed, min, max, format);
        if (changed) value = f;
        return changed;
    }

    public static bool GridColor(string label, string id, ref float3 color)
    {
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
        return ImGui.ColorEdit3(id, ref color, ImGuiColorEditFlags.NoLabel);
    }

    public static void DangerButtonBegin()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
    }

    public static void DangerButtonEnd()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }
}
