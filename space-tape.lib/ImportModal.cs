using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Modal popup for importing or loading a part into the editor.
/// Replaces the previous collapsible "Load / Import" section in the editor window.
/// </summary>
public sealed class ImportModal
{
    public const string PopupId = "Import Existing Part##st_import_popup";

    private readonly PartCatalog _gameParts = new();
    private List<(string partId, string fileName)> _savedParts = new();
    private int _selectedSavedPartIndex = -1;
    private int _selectedGamePartIndex = -1;
    private readonly ImInputString _loadFilter = new(128);
    private readonly ImInputString _gamePartFilter = new(128);
    private bool _clearOtherSubParts = true;

    // Set to true after a successful import so PartEditorUi resets its tracking state.
    public bool ShouldResetTracking { get; private set; }

    public void OnOpen(PartModWriter writer)
    {
        writer.RefreshFileList();
        _savedParts = writer.ListSavedParts();
        _selectedSavedPartIndex = -1;
        if (!_gameParts.IsLoaded) _gameParts.Load();
        _selectedGamePartIndex = -1;
        ShouldResetTracking = false;
    }

    public void Render(PartEditorController controller, PartEditorScene scene, PartModWriter writer)
    {
        bool open = true;
        ImGui.SetNextWindowSize(new float2(800f, 0f), ImGuiCond.Always);
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        bool clearSubParts = _clearOtherSubParts;
        if (ImGui.Checkbox(" Clear other SubParts ##st_imp_clear", ref clearSubParts))
            _clearOtherSubParts = clearSubParts;
        if (ImGui.IsItemHovered())
            ImGui.SetItemTooltip(
                "When enabled, importing replaces the current editor content.\n" +
                "When disabled, the imported SubParts are merged into the existing editor.");

        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_imp_tbl", 2,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##st_imp_lbl", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("##st_imp_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Custom Parts");
            ImGui.TableNextColumn();
            RenderCustomPartsCombo();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Stock Parts");
            ImGui.TableNextColumn();
            RenderStockPartsCombo();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        bool hasSavedSel = _selectedSavedPartIndex >= 0 && _selectedSavedPartIndex < _savedParts.Count;
        bool hasGameSel = _selectedGamePartIndex >= 0 && _selectedGamePartIndex < _gameParts.Parts.Count;
        bool canImport = hasSavedSel || hasGameSel;

        float availW = ImGui.GetContentRegionAvail().X;
        float gap = 8f;
        float btnW = (availW - gap) / 2f;

        if (!canImport) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new float4(0.1f, 0.5f, 0.7f, 1f)));
        if (ImGui.Button(" Import ##st_imp_btn", new float2(btnW, 0)) && canImport)
        {
            DoImport(controller, scene, writer);
            ImGui.CloseCurrentPopup();
        }
        ImGui.PopStyleColor();
        if (!canImport) ImGui.EndDisabled();

        ImGui.SameLine(0, gap);
        if (ImGui.Button(" Cancel ##st_imp_cancel", new float2(btnW, 0)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void RenderCustomPartsCombo()
    {
        string preview = _selectedSavedPartIndex >= 0 && _selectedSavedPartIndex < _savedParts.Count
            ? $"{_savedParts[_selectedSavedPartIndex].partId}  [{_savedParts[_selectedSavedPartIndex].fileName}]"
            : _savedParts.Count == 0 ? "(no saved parts)" : "(select a part)";

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##st_imp_load_combo", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                _loadFilter.SetValue("".AsSpan());
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.InputText("##st_imp_load_filter", _loadFilter);
            string filterText = _loadFilter.ToString().Trim();

            for (int i = 0; i < _savedParts.Count; i++)
            {
                var (partId, fileName) = _savedParts[i];
                if (!string.IsNullOrEmpty(filterText)
                    && !partId.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    && !fileName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool sel = i == _selectedSavedPartIndex;
                if (ImGui.Selectable($"{partId}  [{fileName}]##st_imp_lp{i}", sel))
                {
                    _selectedSavedPartIndex = i;
                    _selectedGamePartIndex = -1;
                }
            }

            ImGui.EndCombo();
        }
    }

    private void RenderStockPartsCombo()
    {
        string preview = _selectedGamePartIndex >= 0 && _selectedGamePartIndex < _gameParts.Parts.Count
            ? $"{_gameParts.Parts[_selectedGamePartIndex].displayName}  ({_gameParts.Parts[_selectedGamePartIndex].id})"
            : !_gameParts.IsLoaded ? "(no stock parts)" : "(select a part)";

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##st_imp_import_combo", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                _gamePartFilter.SetValue("".AsSpan());
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.InputText("##st_imp_import_filter", _gamePartFilter);
            string filterText = _gamePartFilter.ToString().Trim();

            if (_gameParts.IsLoaded)
            {
                for (int i = 0; i < _gameParts.Parts.Count; i++)
                {
                    var (id, displayName) = _gameParts.Parts[i];
                    if (!string.IsNullOrEmpty(filterText)
                        && !id.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                        && !displayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool sel = i == _selectedGamePartIndex;
                    if (ImGui.Selectable($"{displayName}  ({id})##st_imp_ip{i}", sel))
                    {
                        _selectedGamePartIndex = i;
                        _selectedSavedPartIndex = -1;
                    }
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DoImport(PartEditorController controller, PartEditorScene scene, PartModWriter writer)
    {
        bool hasSavedSel = _selectedSavedPartIndex >= 0 && _selectedSavedPartIndex < _savedParts.Count;
        bool hasGameSel = _selectedGamePartIndex >= 0 && _selectedGamePartIndex < _gameParts.Parts.Count;

        ShouldResetTracking = false;

        if (hasSavedSel)
        {
            var (partId, fileName) = _savedParts[_selectedSavedPartIndex];
            var loaded = writer.LoadPart(partId, fileName);
            if (loaded != null)
            {
                if (_clearOtherSubParts)
                {
                    controller.LoadPart(loaded);
                    writer.CurrentFileName = fileName;
                }
                else
                {
                    controller.MergeSubParts(loaded);
                }

                if (scene.IsActive) scene.SyncParts(controller.CurrentPart);
                ShouldResetTracking = true;
                Console.WriteLine($"space-tape: Loaded part '{partId}' from '{fileName}' (clear={_clearOtherSubParts})");
            }
            else
            {
                Console.WriteLine($"space-tape: LoadPart failed for '{partId}' in '{fileName}'");
            }

            return;
        }

        if (hasGameSel)
        {
            var partId = _gameParts.Parts[_selectedGamePartIndex].id;
            var imported = PartImporter.ImportFromTemplate(partId);
            if (imported != null)
            {
                if (_clearOtherSubParts)
                    controller.LoadPart(imported);
                else
                    controller.MergeSubParts(imported);

                if (scene.IsActive) scene.SyncParts(controller.CurrentPart);
                ShouldResetTracking = true;
                Console.WriteLine($"space-tape: Imported game part '{partId}' (clear={_clearOtherSubParts})");
            }
            else
            {
                Console.WriteLine($"space-tape: ImportFromTemplate failed for '{partId}'");
            }
        }
    }
}
