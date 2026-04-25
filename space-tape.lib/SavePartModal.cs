using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

public sealed class SavePartModal
{
    public const string PopupId = "Save Part##st_save_popup";

    private readonly ImInputString _newFileNameInput = new(128);
    private readonly ImInputString _filter = new(128);
    private int _selectedFileIndex = -1; // -1 => (new file)
    private string? _lastStatusMessage;
    private float4 _lastStatusColor;

    public void OnOpen(PartModWriter writer)
    {
        _selectedFileIndex = -1;
        for (int i = 0; i < writer.ExistingFiles.Count; i++)
        {
            if (string.Equals(writer.ExistingFiles[i], writer.CurrentFileName, StringComparison.OrdinalIgnoreCase))
            {
                _selectedFileIndex = i;
                break;
            }
        }

        _newFileNameInput.SetValue(writer.CurrentFileName.AsSpan());
        _filter.Clear();
        _lastStatusMessage = null;
    }

    public void Render(PartEditorController controller, PartModWriter writer, Action? onSaveSuccess = null)
    {
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        RenderFileCombo(writer);

        bool isNewFile = _selectedFileIndex < 0;
        if (isNewFile)
        {
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("File:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##st_save_newname", _newFileNameInput);
        }

        ImGui.Spacing();

        string fileName = isNewFile
            ? _newFileNameInput.ToString().Trim()
            : writer.ExistingFiles[_selectedFileIndex];
        bool canSave = !string.IsNullOrWhiteSpace(fileName);

        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button(" Save ##st_save_confirm"))
        {
            writer.CurrentFileName = fileName;
            bool ok = writer.SavePart(controller.CurrentPart);
            if (ok)
            {
                _lastStatusMessage = $"Saved to {fileName}.xml";
                _lastStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
                onSaveSuccess?.Invoke();
                ImGui.CloseCurrentPopup();
            }
            else
            {
                _lastStatusMessage = $"Save failed: {writer.LastError}";
                _lastStatusColor = new float4(1f, 0.3f, 0.3f, 1f);
            }
        }
        if (!canSave) ImGui.EndDisabled();

        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##st_save_cancel"))
            ImGui.CloseCurrentPopup();

        if (_lastStatusMessage != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(_lastStatusColor, _lastStatusMessage);
        }

        ImGui.EndPopup();
    }

    private void RenderFileCombo(PartModWriter writer)
    {
        if (_selectedFileIndex >= writer.ExistingFiles.Count)
            _selectedFileIndex = -1;

        string preview = _selectedFileIndex < 0
            ? "(new file)"
            : writer.ExistingFiles[_selectedFileIndex];

        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("##st_save_combo", preview))
            return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            _filter.Clear();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##st_save_combo_filter", _filter);

        string filterText = _filter.ToString().Trim();
        if (filterText.Length == 0
            || "(new file)".Contains(filterText, StringComparison.OrdinalIgnoreCase)
            || "new".Contains(filterText, StringComparison.OrdinalIgnoreCase))
        {
            bool sel = _selectedFileIndex < 0;
            if (ImGui.Selectable("(new file)##st_save_newf", sel))
                _selectedFileIndex = -1;
            if (sel) ImGui.SetItemDefaultFocus();
        }

        for (int i = 0; i < writer.ExistingFiles.Count; i++)
        {
            string name = writer.ExistingFiles[i];
            if (filterText.Length > 0
                && !name.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;

            bool sel = i == _selectedFileIndex;
            if (ImGui.Selectable($"{name}##st_save_f{i}", sel))
                _selectedFileIndex = i;
            if (sel) ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
    }
}