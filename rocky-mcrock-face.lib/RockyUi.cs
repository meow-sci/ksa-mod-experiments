using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>Small shared ImGui widgets for the rocky-mcrock-face panels (the repo's standard shapes).</summary>
public static class RockyUi
{
    /// <summary>
    /// Filtered combo bound to an asset id string. An empty id means the default entry
    /// (labelled <paramref name="defaultLabel"/>), which is always first. Returns true when the selection changed.
    /// </summary>
    public static bool IdCombo(string id, string[] ids, ref string selectedId, ImInputString filter,
        string defaultLabel = "(game default)")
    {
        bool changed = false;
        string preview = selectedId.Length > 0 ? selectedId : defaultLabel;
        if (!ImGui.BeginCombo(id, preview)) return false;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            filter.Clear();
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint($"{id}_filter", "filter..."u8, filter);
        string filterText = filter.ToString().Trim();

        bool isDefault = selectedId.Length == 0;
        if (ImGui.Selectable(defaultLabel, isDefault))
        {
            selectedId = "";
            changed = true;
        }
        if (isDefault) ImGui.SetItemDefaultFocus();

        for (int i = 0; i < ids.Length; i++)
        {
            if (filterText.Length > 0 && !ids[i].Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;
            bool selected = string.Equals(ids[i], selectedId, StringComparison.OrdinalIgnoreCase);
            ImGui.PushID(i);
            if (ImGui.Selectable(ids[i], selected))
            {
                selectedId = ids[i];
                changed = true;
            }
            ImGui.PopID();
            if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
        return changed;
    }

    /// <summary>Begins a 2-column label/widget table (1fr label : 3fr widget) with standard padding.</summary>
    public static bool BeginFormTable(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn($"{id}_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn($"{id}_widget", ImGuiTableColumnFlags.WidthStretch, 3f);
            return true;
        }
        ImGui.PopStyleVar();
        return false;
    }

    public static void EndFormTable()
    {
        ImGui.EndTable();
        ImGui.PopStyleVar();
    }

    public static void FormLabel(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1f);
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

    /// <summary>Drag widget in a param grid cell backed by a double value.</summary>
    public static bool GridDrag(string label, string id, ref double value, float speed, float min, float max,
        string format = "%.2f")
    {
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
        float floatValue = (float)value;
        if (!ImGui.DragFloat(id, ref floatValue, speed, min, max, format)) return false;
        value = floatValue;
        return true;
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
