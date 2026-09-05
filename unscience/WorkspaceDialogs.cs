using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.Unscience;

internal sealed partial class WorkspaceWindow
{
    private readonly ImInputString _saveName = new(512), _loadFilter = new(256);
    private IReadOnlyList<SavedWorkspace> _saves = Array.Empty<SavedWorkspace>(), _saveChoices = Array.Empty<SavedWorkspace>();
    private readonly Dictionary<string, IReadOnlyList<SavedWorkspace>> _featureSaves = new();
    private readonly Dictionary<string, string> _presetNames = new();
    private readonly Dictionary<string, ImInputString> _presetFilters = new();
    private IWorkspaceFeature? _savingFeature;
    private bool _openSave;
    private string _saveError = "", _selectedSave = "";
    private WorkspaceStore PresetStore(IWorkspaceFeature feature) => new(Path.Combine(_root, "feature-presets", feature.FeatureId));
    private void OpenSave(IWorkspaceFeature? feature)
    {
        _savingFeature = feature;
        _saveName.Value16 = feature == null && _name != "Untitled" ? _name : "";
        _saveChoices = (feature == null ? _store : PresetStore(feature)).List();
        _saveError = ""; _openSave = true;
    }
    private void RenderSaveDialog()
    {
        if (_openSave) { ImGui.OpenPopup("Save state##workspace-save"); _openSave = false; }
        ImGui.SetNextWindowSize(new float2(500, 0), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.BeginPopupModal("Save state##workspace-save", ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.Text(_savingFeature == null ? "Save the complete authoring workspace" : $"Save {_savingFeature.Name} settings");
        ImGui.TextDisabled("Existing live state is unaffected.");
        ImGui.Spacing();
        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        ImGui.SetNextItemWidth(-1f); ImGui.InputTextWithHint("##save-name", "Name…", _saveName);
        string name = WorkspaceStore.NormalizeName(_saveName.ToString());
        var collision = _saveChoices.FirstOrDefault(s => s.Document != null &&
            string.Equals(WorkspaceStore.NormalizeName(s.Document.Name), name, StringComparison.OrdinalIgnoreCase));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##existing-save", collision?.Document?.Name ?? "New save — select an existing save to overwrite"))
        {
            if (ImGui.Selectable("New save")) _saveName.Clear();
            foreach (var choice in _saveChoices.Where(s => s.Document != null))
                if (ImGui.Selectable(choice.Document!.Name, choice == collision)) _saveName.Value16 = choice.Document.Name;
            ImGui.EndCombo();
        }
        ImGui.Spacing();
        ImGui.BeginDisabled(name.Length == 0);
        if (ImGui.Button(collision == null ? " Save " : " Overwrite ", new float2((ImGui.GetContentRegionAvail().X - 8) / 2, 0)))
        {
            try
            {
                if (_savingFeature == null)
                {
                    var saved = _store.Save(Capture(), name, collision != null);
                    _name = saved.Name; _savedFingerprint = Fingerprint(); _modified = false;
                    _message = "Workspace saved."; _saves = _store.List();
                }
                else
                {
                    var draft = _savingFeature.CaptureDraft(); draft.Targets.Clear();
                    var document = new WorkspaceDocument();
                    document.Features[_savingFeature.FeatureId] = new FeatureSnapshot { Draft = draft };
                    var store = PresetStore(_savingFeature);
                    store.Save(document, name, collision != null);
                    _featureSaves[_savingFeature.FeatureId] = store.List();
                }
                ImGui.CloseCurrentPopup();
            }
            catch (Exception ex) { _saveError = ex.Message; }
        }
        ImGui.EndDisabled(); ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ", new float2(-1, 0))) ImGui.CloseCurrentPopup();
        if (_saveError.Length > 0) ImGui.TextColored(new float4(1f, .3f, .3f, 1f), _saveError);
        ImGui.EndPopup();
    }
    private void RenderLoad()
    {
        BeginPlacement("load", new float2(620, 500));
        bool shown = ImGui.Begin("Load Unscience workspace", ref _loadOpen);
        RecordPlacement("load");
        if (shown)
        {
            ImGui.TextWrapped("Loading replaces every authoring form and feature visibility. Existing live effects continue.");
            ImGui.SetNextItemWidth(-1f); ImGui.InputTextWithHint("##load-filter", "Find a saved workspace…", _loadFilter);
            string? load = null;
            if (ImGui.BeginListBox("##saved-workspaces", new float2(-1, Math.Max(100, ImGui.GetContentRegionAvail().Y - 90))))
            {
                foreach (var save in _saves)
                {
                    if (save.Document == null) { ImGui.TextDisabled($"{Path.GetFileName(save.Path)} — {save.Error ?? "Invalid save"}"); continue; }
                    if (!save.Document.Name.Contains(_loadFilter.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable($"{save.Document.Name}##{save.Path}", _selectedSave == save.Path, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        _selectedSave = save.Path;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) load = save.Path;
                    }
                    ImGui.SetItemTooltip($"{save.Document.Modified:g} · {save.Document.Features.Values.Count(f => f.Visible)} visible features");
                }
                ImGui.EndListBox();
            }
            ImGui.BeginDisabled(!_saves.Any(s => s.Path == _selectedSave && s.Document != null));
            if (ImGui.Button(" Load selected ", new float2(-1, 0))) load = _selectedSave;
            ImGui.EndDisabled();
            if (ImGui.Button(" Refresh list ")) _saves = _store.List();
            if (load != null)
                try { Restore(_store.Read(load)); _loadOpen = false; }
                catch (Exception ex) { _message = "Load failed: " + ex.Message; }
            if (_message.Length > 0) ImGui.TextWrapped(_message);
        }
        ImGui.End();
    }
    private void RenderFeaturePresets(IWorkspaceFeature feature)
    {
        if (!_featureSaves.TryGetValue(feature.FeatureId, out var presets))
            _featureSaves[feature.FeatureId] = presets = PresetStore(feature).List();
        if (!_presetFilters.TryGetValue(feature.FeatureId, out var filter)) _presetFilters[feature.FeatureId] = filter = new ImInputString(256);
        ImGui.SetNextItemWidth(-1f);
        var presetName = _presetNames.GetValueOrDefault(feature.FeatureId, "");
        if (ImGui.BeginCombo("##feature-presets", presetName.Length == 0 ? "Load saved settings into this form…" : presetName))
        {
            ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##preset-filter", "Filter presets…", filter);
            foreach (var preset in presets.Where(p => p.Document != null && p.Document.Name.Contains(filter.ToString(), StringComparison.OrdinalIgnoreCase)))
                if (ImGui.Selectable(preset.Document!.Name))
                {
                    try
                    {
                        var state = preset.Document.Features[feature.FeatureId].Draft.Clone();
                        state.Targets = feature.CaptureDraft().Targets;
                        var restore = feature.PrepareRestore(state); restore(); feature.CancelAuthoringGesture();
                        _presetNames[feature.FeatureId] = preset.Document.Name;
                    }
                    catch (Exception ex) { _message = ex.Message; }
                }
            ImGui.EndCombo();
        }
        if (ImGui.Button(" Save settings as preset ")) OpenSave(feature);
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Reset form ")) { feature.PrepareRestore(_defaults[feature.FeatureId].Clone())(); feature.CancelAuthoringGesture(); }
        ImGui.Spacing();
    }
}
