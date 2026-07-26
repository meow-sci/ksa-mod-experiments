using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// The two paint target tables: individual part instances, and part types (templates).
/// Split from <see cref="VehiclePaintSubmod"/> to keep each file readable.
/// </summary>
public sealed partial class VehiclePaintSubmod
{
    private SortedDictionary<string, int> _typeCounts = new();

    // ---- Parts tab: one row per part instance ----

    private void RenderPartsTab()
    {
        RenderGroupSelector();

        var group = CurrentGroup;
        if (group == null) return;

        ImGui.Spacing();
        if (ImGui.Button(" Paint shown ##vp_parts"))
        {
            ForEachShownPart(group, part => VehiclePaint.SetPart(part, _brush));
            SetStatus("Painted every listed part.", false);
        }
        ImGui.SameLine(0, 4);
        if (ImGui.Button(" Clear shown ##vp_parts"))
        {
            ForEachShownPart(group, VehiclePaint.ClearPart);
            SetStatus("Cleared paint on every listed part.", false);
        }
        ImGui.SameLine(0, 12);
        ImGui.SetNextItemWidth(-1f);
        _partFilter.Draw("##vp_part_filter");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
                  | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                  | ImGuiTableFlags.ScrollY;
        float height = ImGui.GetTextLineHeightWithSpacing() * 12;

        if (ImGui.BeginTable("##vp_parts", 3, flags, new float2(0, height)))
        {
            ImGui.TableSetupColumn("##vp_part_on", ImGuiTableColumnFlags.WidthFixed, 38f);
            ImGui.TableSetupColumn("##vp_part_col", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (int i = 0; i < group.Parts.Count; i++)
            {
                var label = group.PartLabels[i];
                if (!_partFilter.PassFilter(label)) continue;

                ImGui.PushID(i);
                ImGui.TableNextRow();
                RenderPartRow(group.Parts[i], label);
                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private void RenderPartRow(Part part, string label)
    {
        bool painted = VehiclePaint.TryGetPartColor(part, out var color);

        ImGui.TableNextColumn();
        bool toggled = painted;
        if (ImGui.Checkbox("##vp_part_on", ref toggled))
        {
            if (toggled)
                VehiclePaint.SetPart(part, painted ? color : _brush);
            else
                VehiclePaint.ClearPart(part);
        }

        ImGui.TableNextColumn();
        var swatch = painted ? color : _brush;
        if (ImGui.ColorEdit3("##vp_part_col", ref swatch,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            VehiclePaint.SetPart(part, swatch);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
    }

    private void ForEachShownPart(PaintTargets.Group group, System.Action<Part> action)
    {
        for (int i = 0; i < group.Parts.Count; i++)
        {
            if (!_partFilter.PassFilter(group.PartLabels[i])) continue;
            action(group.Parts[i]);
        }
    }

    // ---- Part types tab: one row per part template, applies to every instance ----

    private void RenderTypesTab()
    {
        if (_typeCounts.Count == 0)
        {
            ImGui.TextDisabled("No parts in range. Load a vehicle or open the editor.");
            return;
        }

        if (ImGui.Button(" Paint shown ##vp_types"))
        {
            ForEachShownType(id => VehiclePaint.SetTemplate(id, _brush));
            SetStatus("Painted every listed part type.", false);
        }
        ImGui.SameLine(0, 4);
        if (ImGui.Button(" Clear shown ##vp_types"))
        {
            ForEachShownType(VehiclePaint.ClearTemplate);
            SetStatus("Cleared paint on every listed part type.", false);
        }
        ImGui.SameLine(0, 12);
        ImGui.SetNextItemWidth(-1f);
        _typeFilter.Draw("##vp_type_filter");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
                  | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                  | ImGuiTableFlags.ScrollY;
        float height = ImGui.GetTextLineHeightWithSpacing() * 12;

        if (ImGui.BeginTable("##vp_types", 4, flags, new float2(0, height)))
        {
            ImGui.TableSetupColumn("##vp_type_on", ImGuiTableColumnFlags.WidthFixed, 38f);
            ImGui.TableSetupColumn("##vp_type_col", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Part type", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 34f);
            ImGui.TableHeadersRow();

            int row = 0;
            foreach (var pair in _typeCounts)
            {
                if (!_typeFilter.PassFilter(pair.Key)) continue;

                ImGui.PushID(row++);
                ImGui.TableNextRow();
                RenderTypeRow(pair.Key, pair.Value);
                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private void RenderTypeRow(string templateId, int count)
    {
        bool painted = VehiclePaint.TryGetTemplateColor(templateId, out var color);

        ImGui.TableNextColumn();
        bool toggled = painted;
        if (ImGui.Checkbox("##vp_type_on", ref toggled))
        {
            if (toggled)
                VehiclePaint.SetTemplate(templateId, painted ? color : _brush);
            else
                VehiclePaint.ClearTemplate(templateId);
        }

        ImGui.TableNextColumn();
        var swatch = painted ? color : _brush;
        if (ImGui.ColorEdit3("##vp_type_col", ref swatch,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            VehiclePaint.SetTemplate(templateId, swatch);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(templateId);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(count.ToString());
    }

    private void ForEachShownType(System.Action<string> action)
    {
        foreach (var pair in _typeCounts)
        {
            if (!_typeFilter.PassFilter(pair.Key)) continue;
            action(pair.Key);
        }
    }
}
