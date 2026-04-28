using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Standalone floating window for managing custom Part files: browse files, list parts,
/// inspect part details, and delete files or individual parts.
/// </summary>
public sealed class FileManagerWindow
{
    public bool WindowOpen { get; set; }

    private const string FileDeletePopupId = "Delete File##st_fm_del_file_popup";
    private const string PartDeletePopupId = "Delete Part##st_fm_del_part_popup";

    private int _selectedFileIndex = -1;
    private int _prevFileIndex = -2;

    private List<string> _partsInFile = new();
    private int _selectedPartIndex = -1;
    private int _prevPartIndex = -2;

    private EditingPart? _loadedPart;

    private bool _openFileDeleteModal;
    private bool _openPartDeleteModal;

    private readonly ImInputString _fileFilter = new(128);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public void OnOpen(PartModWriter writer)
    {
        writer.RefreshFileList();
        _selectedFileIndex = -1;
        _prevFileIndex = -2;
        _partsInFile.Clear();
        _selectedPartIndex = -1;
        _prevPartIndex = -2;
        _loadedPart = null;
        _fileFilter.Clear();
        WindowOpen = true;
    }

    // -------------------------------------------------------------------------
    // Render
    // -------------------------------------------------------------------------

    public void Render(PartModWriter writer)
    {
        if (!WindowOpen) return;

        ImGui.SetNextWindowSize(new float2(1000f, 1000f), ImGuiCond.FirstUseEver);
        bool open = WindowOpen;
        if (ImGui.Begin("Part Files##st_fmgr", ref open))
        {
            HandleSelectionChanges(writer);

            RenderFileSection(writer);
            RenderPartsSection(writer);
            RenderPartDetail();

            // Must open popups before calling BeginPopupModal, within the same window context.
            if (_openFileDeleteModal) { ImGui.OpenPopup(FileDeletePopupId); _openFileDeleteModal = false; }
            if (_openPartDeleteModal) { ImGui.OpenPopup(PartDeletePopupId); _openPartDeleteModal = false; }

            RenderFileDeleteModal(writer);
            RenderPartDeleteModal(writer);
        }
        ImGui.End();
        WindowOpen = open;
    }

    // -------------------------------------------------------------------------
    // Selection change detection
    // -------------------------------------------------------------------------

    private void HandleSelectionChanges(PartModWriter writer)
    {
        if (_selectedFileIndex != _prevFileIndex)
        {
            _prevFileIndex = _selectedFileIndex;
            _partsInFile.Clear();
            _selectedPartIndex = -1;
            _prevPartIndex = -2;
            _loadedPart = null;

            if (_selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count)
                _partsInFile = writer.ListPartsInFile(writer.ExistingFiles[_selectedFileIndex]);
        }

        if (_selectedPartIndex != _prevPartIndex)
        {
            _prevPartIndex = _selectedPartIndex;
            _loadedPart = null;

            if (_selectedPartIndex >= 0 && _selectedPartIndex < _partsInFile.Count
                && _selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count)
            {
                _loadedPart = writer.LoadPart(
                    _partsInFile[_selectedPartIndex],
                    writer.ExistingFiles[_selectedFileIndex]);
            }
        }
    }

    // -------------------------------------------------------------------------
    // File section (combo + delete-file button)
    // -------------------------------------------------------------------------

    private void RenderFileSection(PartModWriter writer)
    {
        bool hasFile = _selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##fm_file_tbl", 2,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##fm_lbl", ImGuiTableColumnFlags.WidthFixed, 200f);
            ImGui.TableSetupColumn("##fm_val", ImGuiTableColumnFlags.WidthStretch, 1f);

            // File combo row
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("File");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            string preview = hasFile ? writer.ExistingFiles[_selectedFileIndex] : "(select file)";
            if (ImGui.BeginCombo("##fm_file_combo", preview))
            {
                if (ImGui.IsWindowAppearing())
                {
                    ImGui.SetKeyboardFocusHere();
                    _fileFilter.Clear();
                }
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##fm_file_filter", _fileFilter);

                string filterText = _fileFilter.ToString().Trim();

                // Deselect / clear placeholder
                bool noneSelected = _selectedFileIndex < 0;
                if (filterText.Length == 0 || "(none)".Contains(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.Selectable("(none)##fm_f_none", noneSelected))
                        _selectedFileIndex = -1;
                    if (noneSelected) ImGui.SetItemDefaultFocus();
                }

                for (int i = 0; i < writer.ExistingFiles.Count; i++)
                {
                    string name = writer.ExistingFiles[i];
                    if (filterText.Length > 0 && !name.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool sel = i == _selectedFileIndex;
                    if (ImGui.Selectable($"{name}##fm_f{i}", sel))
                        _selectedFileIndex = i;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        // Delete File button — full-width, outside the table
        if (!hasFile) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, new float4(0.65f, 0.12f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new float4(0.82f, 0.22f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new float4(0.55f, 0.08f, 0.08f, 1f));
        if (ImGui.Button(" Delete File ##fm_del_file_btn", new float2(-1, 0)))
            _openFileDeleteModal = true;
        ImGui.PopStyleColor(3);
        if (!hasFile) ImGui.EndDisabled();
    }

    // -------------------------------------------------------------------------
    // Parts section (list box + delete-part button)
    // -------------------------------------------------------------------------

    private void RenderPartsSection(PartModWriter writer)
    {
        bool hasFile = _selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count;
        if (!hasFile) return;

        string fileName = writer.ExistingFiles[_selectedFileIndex];
        bool hasPart = _selectedPartIndex >= 0 && _selectedPartIndex < _partsInFile.Count;

        ImGui.Spacing();
        ImGui.SeparatorText($"Parts in '{fileName}'");

        if (ImGui.BeginListBox("##fm_part_list", new float2(-1, 200f)))
        {
            for (int i = 0; i < _partsInFile.Count; i++)
            {
                bool sel = i == _selectedPartIndex;
                if (ImGui.Selectable(_partsInFile[i] + "##fm_p" + i, sel,
                    ImGuiSelectableFlags.AllowOverlap))
                {
                    // clicking the selected item deselects it
                    _selectedPartIndex = sel ? -1 : i;
                }
                if (i == _selectedPartIndex) ImGui.SetItemDefaultFocus();
            }
            if (_partsInFile.Count == 0)
                ImGui.TextDisabled("(no parts in file)");
            ImGui.EndListBox();
        }

        if (!hasPart) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, new float4(0.65f, 0.12f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new float4(0.82f, 0.22f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new float4(0.55f, 0.08f, 0.08f, 1f));
        if (ImGui.Button(" Delete Part ##fm_del_part_btn", new float2(-1, 0)))
            _openPartDeleteModal = true;
        ImGui.PopStyleColor(3);
        if (!hasPart) ImGui.EndDisabled();
    }

    // -------------------------------------------------------------------------
    // Part detail table
    // -------------------------------------------------------------------------

    private void RenderPartDetail()
    {
        bool hasPart = _selectedPartIndex >= 0 && _selectedPartIndex < _partsInFile.Count;
        if (!hasPart || _loadedPart == null) return;

        var part = _loadedPart;
        var gd = part.GameData;

        ImGui.Spacing();
        ImGui.SeparatorText("Part Details");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##fm_detail_tbl", 2,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("##fm_d_lbl", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableSetupColumn("##fm_d_val", ImGuiTableColumnFlags.WidthStretch, 1f);

            DetailRow("Part ID", part.PartId);
            DetailRow("Display Name", gd.DisplayName);
            DetailRow("Sub Parts", part.Placements.Count.ToString());
            DetailRow("Tags", gd.EditorTags.Count > 0
                ? string.Join(", ", gd.EditorTags)
                : "(none)");
            DetailRow("Tanks", gd.Tanks.Count.ToString());
            DetailRow("Batteries", gd.Batteries.Count.ToString());
            DetailRow("Generators", gd.Generators.Count.ToString());
            DetailRow("Power Consumers", gd.PowerConsumers.Count.ToString());
            DetailRow("Connectors", gd.Connectors.Count.ToString());
            DetailRow("Decoupler", gd.Decoupler != null ? "yes" : "no");
            DetailRow("Docking Port", gd.DockingPort != null ? "yes" : "no");
            DetailRow("EVA Door", gd.EVADoor != null ? "yes" : "no");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private static void DetailRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(value);
    }

    // -------------------------------------------------------------------------
    // Delete confirmation modals
    // -------------------------------------------------------------------------

    private void RenderFileDeleteModal(PartModWriter writer)
    {
        ImGui.SetNextWindowSize(new float2(1000f, 0f), ImGuiCond.Always);
        bool open = true;
        if (!ImGui.BeginPopupModal(FileDeletePopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        bool hasFile = _selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count;
        string fileName = hasFile ? writer.ExistingFiles[_selectedFileIndex] : "(unknown)";

        ImGui.TextWrapped($"Are you sure you want to delete custom Part file '{fileName}'?");
        ImGui.Spacing();

        float availW = ImGui.GetContentRegionAvail().X;
        const float gap = 8f;
        float btnW = (availW - gap) / 2f;

        if (ImGui.Button(" Confirm ##fm_del_file_confirm", new float2(btnW, 0)))
        {
            if (hasFile)
            {
                writer.DeleteFile(fileName);
                _selectedFileIndex = -1;
                _prevFileIndex = -2;
                _partsInFile.Clear();
                _selectedPartIndex = -1;
                _prevPartIndex = -2;
                _loadedPart = null;
            }
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine(0, gap);
        if (ImGui.Button(" Cancel ##fm_del_file_cancel", new float2(btnW, 0)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void RenderPartDeleteModal(PartModWriter writer)
    {
        ImGui.SetNextWindowSize(new float2(1000f, 0f), ImGuiCond.Always);
        bool open = true;
        if (!ImGui.BeginPopupModal(PartDeletePopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        bool hasFile = _selectedFileIndex >= 0 && _selectedFileIndex < writer.ExistingFiles.Count;
        bool hasPart = _selectedPartIndex >= 0 && _selectedPartIndex < _partsInFile.Count;
        string fileName = hasFile ? writer.ExistingFiles[_selectedFileIndex] : "(unknown)";
        string partId = hasPart ? _partsInFile[_selectedPartIndex] : "(unknown)";

        ImGui.TextWrapped($"Are you sure you want to delete Part '{partId}' from file '{fileName}'?");
        ImGui.Spacing();

        float availW = ImGui.GetContentRegionAvail().X;
        const float gap = 8f;
        float btnW = (availW - gap) / 2f;

        if (ImGui.Button(" Confirm ##fm_del_part_confirm", new float2(btnW, 0)))
        {
            if (hasFile && hasPart)
            {
                writer.DeletePartFromFile(partId, fileName);
                _partsInFile = writer.ListPartsInFile(fileName);
                _selectedPartIndex = -1;
                _prevPartIndex = -2;
                _loadedPart = null;
            }
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine(0, gap);
        if (ImGui.Button(" Cancel ##fm_del_part_cancel", new float2(btnW, 0)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }
}
