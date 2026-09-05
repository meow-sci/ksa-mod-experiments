using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.KsaAbstractions;

/// <summary>A scoped, responsive grid of label-above-input fields rendered with FormField.</summary>
public sealed class FormGrid : IDisposable
{
    private static FormGrid? _current;
    private readonly FormGrid? _previous;
    private readonly bool _table;
    public FormGrid(string id)
    {
        _previous = _current;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6, 6));
        int columns = ImGui.GetContentRegionAvail().X >= 620 ? 2 : 1;
        _table = ImGui.BeginTable(id, columns, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX);
        _current = this;
    }
    internal static void NextField()
    {
        if (_current?._table == true) ImGui.TableNextColumn();
    }
    public void Dispose()
    {
        if (_table) ImGui.EndTable();
        ImGui.PopStyleVar();
        _current = _previous;
    }
}
