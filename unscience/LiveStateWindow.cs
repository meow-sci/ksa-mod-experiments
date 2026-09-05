using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.Unscience;

internal sealed partial class WorkspaceWindow
{
    private sealed record LiveRow(IWorkspaceFeature Feature, ILiveStateItem Item)
    {
        public string Id => Feature.FeatureId + "/" + Item.Id;
    }
    private void RenderLive()
    {
        var rows = new List<LiveRow>();
        foreach (var feature in _features)
            try { foreach (var item in feature.GetLiveItems()) rows.Add(new LiveRow(feature, item)); }
            catch (Exception ex) { Console.WriteLine($"unscience/{feature.FeatureId}: live enumeration failed: {ex.Message}"); }
        if (!rows.Exists(r => r.Id == _selectedLive)) _selectedLive = "";
        BeginPlacement("live", new float2(1000, 700));
        bool shown = ImGui.Begin("Unscience Live State", ref _liveOpen);
        RecordPlacement("live");
        if (shown)
        {
            ImGui.Text($"{rows.Count} managed items");
            ImGui.SetNextItemWidth(-1f); ImGui.InputTextWithHint("##live-filter", "Filter by feature, target, type or status…", _liveFilter);
            ImGui.Spacing();
            bool wide = ImGui.GetContentRegionAvail().X >= 750;
            bool tableOpen = false;
            if (wide)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6, 6));
                tableOpen = ImGui.BeginTable("live-columns", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX);
                ImGui.PopStyleVar();
            }
            if (tableOpen)
            {
                ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Inspector", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableNextColumn(); RenderLiveList(rows, 0);
                ImGui.TableNextColumn(); RenderLiveInspector(rows);
                ImGui.EndTable();
            }
            else if (!wide) { RenderLiveList(rows, 180); RenderLiveInspector(rows); }
        }
        ImGui.End();
    }
    private void RenderLiveList(List<LiveRow> rows, float height)
    {
        if (ImGui.BeginChild("live-list", new float2(0, height)))
        {
            string filter = _liveFilter.ToString();
            foreach (var row in rows)
            {
                if (!($"{row.Feature.Name} {row.Item.Label} {row.Item.Target} {row.Item.Status}").Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable($"{row.Item.Label}##{row.Id}", _selectedLive == row.Id)) _selectedLive = row.Id;
                ImGui.TextDisabled($"{row.Item.Target} · {row.Item.Status}");
                ImGui.SetItemTooltip(row.Feature.Name);
                ImGui.Spacing();
            }
            if (rows.Count == 0) ImGui.TextWrapped("Apply an effect from the workspace to manage it here.");
        }
        ImGui.EndChild();
    }
    private void RenderLiveInspector(List<LiveRow> rows)
    {
        var row = rows.Find(r => r.Id == _selectedLive);
        if (row == null) { ImGui.TextDisabled("Select a live item to inspect it."); return; }
        ImGui.PushID(row.Id);
        ImGui.BeginChild("inspector-scroll", new float2(0, 0));
        SubmodUI.BeginContentArea("live-inspector");
        try
        {
            ImGui.SeparatorText(row.Item.Label);
            ImGui.TextDisabled(row.Item.Target);
            if (FeatureRuntime.For(row.Feature).Error is { } error) ImGui.TextWrapped(error);
            FeatureUi.Render(row.Item.RenderInspector);
        }
        catch (Exception ex) { WorkspaceUi.Error(ex); }
        finally { SubmodUI.EndContentArea(); ImGui.EndChild(); ImGui.PopID(); }
    }
}
