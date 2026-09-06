using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PebblesLib;

/// <summary>Detached collider workshop. GPU work runs only from the host's before-GUI Update.</summary>
public sealed partial class WorkshopEditor : IDisposable
{
    private WorkshopState _state = new();
    private readonly WorkshopHistory _history = new();
    private WorkshopPreview _preview = new();
    private Action<ObjectRecipe>? _done;
    private ClutterAssets? _assets;
    private bool _restoreInspectorScroll;
    private bool _refreshRequested, _stale = true, _restoreWindow, _releaseRequested;
    private int _width = 600, _height = 400;
    private string _message = "";
    private ObjectRecipe? _numericBefore;
    private readonly ImInputString _name = new(128);
    private string _nameId = "";

    public WorkshopState State
    {
        get => RecipeCopy.Clone(_state);
        set
        {
            CancelGesture();
            _state = RecipeCopy.Clone(value ?? new WorkshopState());
            _history.Clear(); _numericBefore = null; _refreshRequested = false;
            _stale = true; _restoreWindow = true; _restoreInspectorScroll = true; _nameId = "";
        }
    }
    private bool Header(string label)
    {
        ImGui.SetNextItemOpen(_state.Sections.GetValueOrDefault(label), ImGuiCond.Always);
        bool open = ImGui.CollapsingHeader(label); _state.Sections[label] = open; return open;
    }
    public bool IsOpen => _state.IsOpen;
    public void RebindCompletion(Action<ObjectRecipe> done) => _done = done;
    public void SetCompletion(Action<ObjectRecipe>? done) => _done = done;
    public void Open(ObjectRecipe recipe, Action<ObjectRecipe> done)
    {
        CancelGesture();
        _state.Object = RecipeCopy.Clone(recipe); _state.IsOpen = true;
        _state.PreviewLod = 0;
        _state.SelectedColliderId = recipe.Colliders.FirstOrDefault()?.Id ?? "";
        _history.Clear(); _done = done; _refreshRequested = true; _stale = true; _frameAfterRefresh = true;
        _message = ""; _nameId = "";
    }

    public void Update()
    {
        if (_releaseRequested) { _preview.Dispose(); _preview = new(); _releaseRequested = false; _stale = true; }
        if (!IsOpen || _assets == null) return;
        try
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                _preview.Refresh(_state.Object, _assets, _state.PreviewLod);
                RefreshHullSources();
                _stale = false;
                if (_frameAfterRefresh) { FrameMesh(); _frameAfterRefresh = false; }
            }
            if (!_stale) _preview.Render(_state.View, _width, _height);
        }
        catch (Exception ex) { _stale = true; _message = "Preview: " + ex.Message; Console.WriteLine("pebbles: " + _message); }
    }

    public void Draw(ClutterAssets assets)
    {
        _assets = assets;
        if (!IsOpen) return;
        if (_restoreWindow)
        {
            ImGui.SetNextWindowSize(new float2(_state.Width, _state.Height), ImGuiCond.Always);
            if (_state.WindowX >= 0) ImGui.SetNextWindowPos(new float2(_state.WindowX, _state.WindowY), ImGuiCond.Always);
            _restoreWindow = false;
        }
        else ImGui.SetNextWindowSize(new float2(1000, 720), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new float2(560, 450), new float2(float.MaxValue, float.MaxValue));
        bool open = true;
        bool shown = ImGui.Begin("Pebbles — Collider Workshop###pebbles-workshop"u8, ref open);
        try
        {
            var windowSize = ImGui.GetWindowSize(); var windowPosition = ImGui.GetWindowPos();
            _state.Width = windowSize.X; _state.Height = windowSize.Y;
            _state.WindowX = windowPosition.X; _state.WindowY = windowPosition.Y;
            if (shown)
            {
                Toolbar();
                float available = ImGui.GetContentRegionAvail().X;
                bool wide = available >= 780;
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6, 6));
                try
                {
                    if (wide && ImGui.BeginTable("##workshop-layout"u8, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
                    {
                        try
                        {
                            ImGui.TableSetupColumn("Preview"u8, ImGuiTableColumnFlags.WidthStretch, 2);
                            ImGui.TableSetupColumn("Inspector"u8, ImGuiTableColumnFlags.WidthStretch, 1);
                            ImGui.TableNextRow(); ImGui.TableNextColumn(); Canvas(Math.Max(240, ImGui.GetContentRegionAvail().Y - 95));
                            ImGui.TableNextColumn(); Inspector();
                        }
                        finally { ImGui.EndTable(); }
                    }
                    else { Canvas(Math.Clamp(ImGui.GetContentRegionAvail().Y * .55f, 180, 450)); Inspector(); }
                }
                finally { ImGui.PopStyleVar(); }
                Footer();
            }
        }
        finally { ImGui.End(); }
        if (!open) Close(false);
    }

    private void Toolbar()
    {
        foreach (var tool in Enum.GetValues<WorkshopTool>())
        {
            if (tool != WorkshopTool.Move) ImGui.SameLine(0, 8);
            if (ImGui.RadioButton(tool.ToString(), _state.Tool == tool)) { CancelGesture(); _state.Tool = tool; }
        }
        ImGui.SameLine(0, 16); bool local = _state.LocalAxes;
        if (ImGui.Checkbox("Local axes"u8, ref local)) _state.LocalAxes = local;
        ImGui.SameLine(0, 8); bool snap = _state.Snap;
        if (ImGui.Checkbox("Snap"u8, ref snap)) _state.Snap = snap;
        if (ImGui.Button(" Frame mesh "u8)) FrameMesh();
        ImGui.SameLine(0, 8); if (ImGui.Button(" Refresh preview "u8)) _refreshRequested = true;
        ImGui.SameLine(0, 8); ImGui.BeginDisabled(!_history.CanUndo);
        if (ImGui.Button(" Undo "u8)) { CancelGesture(); _state.Object = _history.Undo(_state.Object); _refreshRequested = true; _nameId = ""; }
        ImGui.EndDisabled(); ImGui.SameLine(0, 8); ImGui.BeginDisabled(!_history.CanRedo);
        if (ImGui.Button(" Redo "u8)) { CancelGesture(); _state.Object = _history.Redo(_state.Object); _refreshRequested = true; _nameId = ""; }
        ImGui.EndDisabled();
    }

    private void Footer()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Left: select / drag handles · right: orbit · middle: pan · wheel: zoom"u8);
        if (_stale) ImGui.TextDisabled("Preview needs refreshing."u8);
        else if (!_preview.IsReady) ImGui.TextWrapped(_preview.Status);
        if (_message.Length > 0) ImGui.TextWrapped(_message);
        if (ImGui.Button(" Done — keep in authoring form "u8)) Close(true);
        ImGui.SameLine(0, 8); if (ImGui.Button(" Cancel "u8)) Close(false);
        ImGui.SameLine(0, 12); ImGui.TextDisabled("Apply the main form to change the planet."u8);
    }

    private void Close(bool accept)
    {
        CancelGesture();
        if (accept)
        {
            if (_done == null) { _message = "Select the original destination before keeping this restored workshop."; return; }
            try { RecipeValidation.Object(_state.Object); _done(RecipeCopy.Clone(_state.Object)); }
            catch (Exception ex) { _message = ex.Message; return; }
        }
        _state.IsOpen = false;
    }

    private ColliderRecipe? Selected => _state.Object.Colliders.FirstOrDefault(c => c.Id == _state.SelectedColliderId);
    private void Edit(Action action, bool refresh = false)
    {
        CancelGesture(); _history.Record(_state.Object); action();
        if (refresh) { _refreshRequested = true; _stale = true; }
    }
    private void NumericChanged(ObjectRecipe before, bool refresh)
    {
        _numericBefore ??= before;
        if (refresh) { _refreshRequested = true; _stale = true; }
    }
    private void FinishNumeric()
    {
        if (_numericBefore != null && !ImGui.IsAnyItemActive()) { _history.Record(_numericBefore); _numericBefore = null; }
    }
    public void CancelGesture()
    {
        if (_dragBefore != null) _state.Object = _dragBefore;
        _dragBefore = null; _activeAxis = -1; _cameraDrag = 0;
    }
    public void Release() { CancelGesture(); _refreshRequested = false; _releaseRequested = true; _stale = true; _assets = null; }
    public void Dispose() { CancelGesture(); _preview.Dispose(); _stale = true; _assets = null; }
}
