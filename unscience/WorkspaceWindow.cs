using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.Unscience;

internal sealed partial class WorkspaceWindow
{
    private readonly List<IWorkspaceFeature> _features;
    private readonly Dictionary<string, DraftState> _defaults;
    private readonly Dictionary<string, bool> _visible = new();
    private readonly WorkspaceStore _store;
    private readonly string _root = Path.Combine(KsaPaths.UserDataDir, ".unscience");
    private readonly ImInputString _featureFilter = new(256);
    private readonly ImInputString _liveFilter = new(256);
    private bool _open = true, _liveOpen, _loadOpen, _showTooltips = true;
    private string _selectedFeature, _selectedLive = "", _name = "Untitled", _message = "";
    private string _savedFingerprint = "";
    private bool _modified;
    private string _displayedFeature = "";
    private double _autosaveTime, _fingerprintTime;
    private Dictionary<string, WindowPlacement> _windows = new();
    private readonly HashSet<string> _restoreWindows = new();
    private Dictionary<string, FeatureSnapshot> _unknownFeatures = new();

    public WorkspaceWindow(List<IWorkspaceFeature> features)
    {
        _features = features;
        _selectedFeature = features[0].FeatureId;
        _store = new WorkspaceStore(Path.Combine(_root, "workspaces"));
        _defaults = features.ToDictionary(f => f.FeatureId, f => f.CaptureDraft());
        foreach (var feature in features) _visible[feature.FeatureId] = true;
        var (_, visibility) = UnscienceState.LoadSubmodState();
        foreach (var feature in features)
            if (visibility.TryGetValue(feature.Name, out bool visible)) _visible[feature.FeatureId] = visible;
        UnscienceState.LoadImGuiWindowState();
        var session = Path.Combine(_root, "session", "last-workspace.json");
        if (File.Exists(session))
            try { Restore(_store.Read(session), false); }
            catch (Exception ex) { _message = "Session could not be restored: " + ex.Message; }
        _savedFingerprint = Fingerprint();
    }
    public void Toggle() => _open = !_open;
    public void Render(double dt)
    {
        if (_open) RenderMain();
        if (_loadOpen) RenderLoad();
        if (_liveOpen) RenderLive();
        _fingerprintTime += dt;
        if (_fingerprintTime >= 1) { _fingerprintTime = 0; _modified = Fingerprint() != _savedFingerprint; }
        _autosaveTime += dt;
        if (UnscienceState.AutoSaveEnabled && _autosaveTime >= UnscienceState.SaveIntervalSeconds)
        { _autosaveTime = 0; SaveSession(); }
    }
    public void SaveSession()
    {
        if (!UnscienceState.AutoSaveEnabled) return;
        try { WorkspaceStore.Write(Path.Combine(_root, "session", "last-workspace.json"), Capture()); }
        catch (Exception ex) { _message = "Autosave failed: " + ex.Message; Console.WriteLine("unscience: " + _message); }
    }
    private WorkspaceDocument Capture()
    {
        var document = new WorkspaceDocument
        {
            Name = _name, SelectedFeature = _selectedFeature, ShowTooltips = _showTooltips,
            MainWindowVisible = _open, LoadWindowVisible = _loadOpen, LoadFilter = _loadFilter.ToString(), SelectedSave = _selectedSave,
            LiveWindowVisible = _liveOpen, SelectedLiveItem = _selectedLive,
            FeatureFilter = _featureFilter.ToString(), LiveFilter = _liveFilter.ToString(), Windows = _windows,
            Features = new(_unknownFeatures)
        };
        foreach (var feature in _features)
            document.Features[feature.FeatureId] = new FeatureSnapshot { Visible = _visible[feature.FeatureId], Draft = feature.CaptureDraft(), SelectedPreset = _presetNames.GetValueOrDefault(feature.FeatureId, ""), PresetFilter = _presetFilters.TryGetValue(feature.FeatureId, out var presetFilter) ? presetFilter.ToString() : "" };
        return document;
    }
    private string Fingerprint()
    {
        var document = Capture();
        // Identity and time do not describe editable state.
        document.Id = ""; document.Modified = default; document.Name = "";
        return JsonSerializer.Serialize(document);
    }
    private void Restore(WorkspaceDocument document, bool recover = true)
    {
        var apply = WorkspaceRestore.Prepare(document, _features.Cast<IWorkspaceParticipant>().ToArray(), _defaults);
        if (recover) WorkspaceStore.Write(Path.Combine(_root, "session", "before-load.json"), Capture());
        apply();
        foreach (var feature in _features)
        {
            feature.CancelAuthoringGesture();
            _presetNames[feature.FeatureId] = document.Features.TryGetValue(feature.FeatureId, out var view) ? view.SelectedPreset : "";
            if (!_presetFilters.TryGetValue(feature.FeatureId, out var filter)) _presetFilters[feature.FeatureId] = filter = new ImInputString(256);
            filter.Value16 = view?.PresetFilter ?? "";
            _visible[feature.FeatureId] = document.Features.TryGetValue(feature.FeatureId, out var snapshot) ? snapshot.Visible : true;
        }
        _unknownFeatures = document.Features.Where(p => !_defaults.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        _name = document.Name;
        _selectedFeature = _defaults.ContainsKey(document.SelectedFeature) ? document.SelectedFeature : _features[0].FeatureId;
        _showTooltips = document.ShowTooltips;
        _open = document.MainWindowVisible; _loadOpen = document.LoadWindowVisible; _loadFilter.Value16 = document.LoadFilter; _selectedSave = document.SelectedSave;
        if (_loadOpen) _saves = _store.List();
        _liveOpen = document.LiveWindowVisible; _selectedLive = document.SelectedLiveItem;
        _featureFilter.Value16 = document.FeatureFilter; _liveFilter.Value16 = document.LiveFilter;
        _windows = document.Windows;
        foreach (var key in _windows.Keys) _restoreWindows.Add(key);
        _savedFingerprint = Fingerprint(); _modified = false;
        _message = "Workspace loaded. Live effects continue unchanged.";
    }
    private void BeginPlacement(string id, float2 defaultSize)
    {
        if (_restoreWindows.Remove(id) && _windows.TryGetValue(id, out var placement))
        {
            var display = ImGui.GetIO().DisplaySize;
            var size = new float2(Math.Clamp(placement.Width, 320, Math.Max(320, display.X)), Math.Clamp(placement.Height, 240, Math.Max(240, display.Y)));
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            ImGui.SetNextWindowPos(new float2(Math.Clamp(placement.X, 0, Math.Max(0, display.X - size.X)), Math.Clamp(placement.Y, 0, Math.Max(0, display.Y - size.Y))), ImGuiCond.Always);
        }
        else ImGui.SetNextWindowSize(defaultSize, ImGuiCond.FirstUseEver);
    }
    private void RecordPlacement(string id)
    {
        var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        _windows[id] = new WindowPlacement { X = pos.X, Y = pos.Y, Width = size.X, Height = size.Y };
    }
    private void RenderMain()
    {
        BeginPlacement("workspace", new float2(900, 800));
        bool shown = ImGui.Begin("Unscience Workspace###Unscience Toolbox", ref _open, ImGuiWindowFlags.MenuBar);
        RecordPlacement("workspace");
        if (shown)
        {
            RenderMenu();
            ImGui.Text($"{_name}{(_modified ? " *" : "")}");
            ImGui.Spacing();
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6, 6));
            bool wide = ImGui.GetContentRegionAvail().X >= 700;
            if (wide && ImGui.BeginTable("workspace-columns", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("Features", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Authoring", ImGuiTableColumnFlags.WidthStretch, 3f);
                ImGui.TableNextColumn();
                ImGui.BeginChild("feature-navigation", new float2(0, Math.Max(160, ImGui.GetContentRegionAvail().Y - 45)));
                RenderNavigation(); ImGui.EndChild();
                ImGui.TableNextColumn(); RenderFeature();
                ImGui.EndTable();
            }
            else if (!wide)
            {
                if (ImGui.BeginCombo("Feature", _features.Find(f => f.FeatureId == _selectedFeature)?.Name ?? "Select feature"))
                { foreach (var feature in _features.Where(f => _visible[f.FeatureId])) if (ImGui.Selectable(feature.Name, _selectedFeature == feature.FeatureId)) _selectedFeature = feature.FeatureId; ImGui.EndCombo(); }
                RenderFeature();
            }
            ImGui.PopStyleVar();
            if (_message.Length > 0) ImGui.TextWrapped(_message);
            RenderSaveDialog();
        }
        ImGui.End();
    }
    private void RenderMenu()
    {
        if (!ImGui.BeginMenuBar()) return;
        if (ImGui.BeginMenu("Features"))
        {
            ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);
            if (ImGui.MenuItem("Show all")) foreach (var f in _features) _visible[f.FeatureId] = true;
            if (ImGui.MenuItem("Hide all")) foreach (var f in _features) _visible[f.FeatureId] = false;
            ImGui.Separator();
            foreach (var f in _features)
            { bool show = _visible[f.FeatureId]; if (ImGui.MenuItem(f.Name, "", ref show)) _visible[f.FeatureId] = show; }
            ImGui.PopItemFlag(); ImGui.EndMenu();
        }
        if (ImGui.MenuItem("Save")) OpenSave(null);
        if (ImGui.MenuItem("Load")) { _loadOpen = true; _saves = _store.List(); }
        if (ImGui.MenuItem("Live State")) _liveOpen = true;
        if (ImGui.BeginMenu("Preferences"))
        {
            bool autosave = UnscienceState.AutoSaveEnabled;
            if (ImGui.MenuItem("Autosave session", "", ref autosave)) { UnscienceState.AutoSaveEnabled = autosave; SavePreferences(); }
            if (ImGui.MenuItem("Tooltips", "", ref _showTooltips)) SavePreferences();
            if (ImGui.MenuItem("Recover workspace before last load"))
                try { Restore(_store.Read(Path.Combine(_root, "session", "before-load.json")), false); }
                catch (Exception ex) { _message = ex.Message; }
            ImGui.EndMenu();
        }
        ImGui.EndMenuBar();
    }
    private void SavePreferences()
    {
        UnscienceState.ShowModTooltips = _showTooltips;
        UnscienceState.SaveSubmodState(new(), _features.ToDictionary(f => f.Name, f => _visible[f.FeatureId]));
    }
    private void RenderNavigation()
    {
        ImGui.SetNextItemWidth(-1f); ImGui.InputTextWithHint("##feature-filter", "Find a feature…", _featureFilter);
        foreach (var feature in _features)
        {
            if (!_visible[feature.FeatureId] || !feature.Name.Contains(_featureFilter.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
            if (ImGui.Selectable(feature.Name, _selectedFeature == feature.FeatureId)) _selectedFeature = feature.FeatureId;
            if (_showTooltips) ImGui.SetItemTooltip(feature.Tooltip);
        }
    }
    private void RenderFeature()
    {
        var feature = _features.Find(f => f.FeatureId == _selectedFeature && _visible[f.FeatureId]);
        if (feature == null) { ImGui.TextWrapped("Show a feature from the Features menu, then select it to configure an action."); return; }
        ImGui.BeginChild("authoring-scroll", new float2(0, Math.Max(160, ImGui.GetContentRegionAvail().Y - 45)));
        if (feature.Draft.RestoreScroll || _displayedFeature != feature.FeatureId) { _displayedFeature = feature.FeatureId; ImGui.SetScrollY(feature.Draft.ScrollY); feature.Draft.RestoreScroll = false; }
        ImGui.PushID(feature.FeatureId);
        ImGui.SeparatorText(feature.Name);
        RenderFeaturePresets(feature);
        feature.Draft.RenderChoices();
        WorkspaceUi.Current = feature.Draft;
        try { feature.RenderContent(); }
        catch (Exception ex) { WorkspaceUi.Error(ex); }
        finally { feature.Draft.ReadChoices(); WorkspaceUi.Current = null; feature.Draft.ScrollY = ImGui.GetScrollY(); ImGui.PopID(); ImGui.EndChild(); }
    }
}
