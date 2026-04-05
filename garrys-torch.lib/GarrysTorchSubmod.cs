using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorchLib;

public sealed class GarrysTorchSubmod : ISubmod
{
    public string Name => "Garry's Torch";
    public string Tooltip => "Welds vehicle parts together with adjustable position, rotation, and scale.";

    public static GarrysTorchSubmod? Instance { get; private set; }

    private readonly List<WeldEntry> _welds = new();
    public IReadOnlyList<WeldEntry> Welds => _welds;
    private readonly PresetManager _presetManager = new();

    // Create weld form state
    private int _pendingSourceIndex = -1;
    private int _pendingTargetIndex = -1;
    private int _selectedPresetIndex = -1;
    private float3 _pendingPosition = new float3(0f, 0f, 0f);
    private float3 _pendingRotation = new float3(0f, 0f, 0f);
    private float _pendingScale = 1f;
    private bool _pendingLockRotation = true;
    private string? _weldError;

    // Combo filters
    private ImGuiTextFilter _sourceFilter = new();
    private ImGuiTextFilter _targetFilter = new();
    private ImGuiTextFilter _presetFilter = new();

    // Deferred modal open flags (popups must be opened at matching ID scope)
    private bool _openDeleteModal;
    private bool _openSaveModal;

    // Delete preset modal state
    private string? _deleteConfirmName;

    // Save preset modal state
    private readonly ImInputString _savePresetName = new ImInputString(128);
    private string? _savePresetError;
    private WeldPreset _pendingSavePreset;

    public void Initialize()
    {
        Instance = this;
        _presetManager.Initialize();
    }

    public void Update(double dt)
    {
        var toRemove = new List<WeldEntry>();
        foreach (var weld in _welds)
            if (!WeldEngine.UpdateWeld(weld)) toRemove.Add(weld);
        foreach (var weld in toRemove)
            RemoveWeld(weld);
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##gt_content");

        RenderCreateSection();

        if (_welds.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Active Welds ( {_welds.Count} )");

            WeldEntry? toRemove = null;
            for (int i = 0; i < _welds.Count; i++)
                RenderWeldSection(_welds[i], i, ref toRemove);
            if (toRemove != null)
                RemoveWeld(toRemove);
        }

        // Deferred popup opens at content area scope
        if (_openDeleteModal)
        {
            ImGui.OpenPopup("Delete preset##gt");
            _openDeleteModal = false;
        }
        if (_openSaveModal)
        {
            ImGui.OpenPopup("Save as preset##gt");
            _openSaveModal = false;
        }
        RenderDeletePresetModal();
        RenderSavePresetModal();

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        foreach (var weld in _welds)
            WeldEngine.ApplyVehicleScale(weld.Source, 1.0f);
        _welds.Clear();
        Instance = null;
    }

    // ---- Create Section ----

    private void RenderCreateSection()
    {
        bool headerOpen = ImGui.CollapsingHeader("Create Weld (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Weld two vehicles together.\nThe source vehicle is positioned relative to\nthe target at the specified offset, rotation, and scale.");
        if (!headerOpen)
            return;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
        {
            ImGui.Text("No vehicles available.");
            return;
        }

        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++)
            vehicleIds[i] = vehicles[i].Id;

        if (_pendingSourceIndex >= vehicles.Count) _pendingSourceIndex = -1;
        if (_pendingTargetIndex >= vehicles.Count) _pendingTargetIndex = -1;

        var presetNames = _presetManager.GetPresetNames();

        // Source / Target / Preset table
        var style = ImGui.GetStyle();
        float labelW = ImGui.CalcTextSize("Preset").X + style.ItemSpacing.X;
        float delW = ImGui.CalcTextSize(" del ").X + style.FramePadding.X * 2f;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var formFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##gt_form", 3, formFlags))
        {
            ImGui.TableSetupColumn("##gt_lbl", ImGuiTableColumnFlags.WidthFixed, labelW);
            ImGui.TableSetupColumn("##gt_widget", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##gt_btns", ImGuiTableColumnFlags.WidthFixed, delW);

            // Source
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Source");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderFilteredCombo("##gt_src", vehicleIds, ref _pendingSourceIndex, _sourceFilter);
            ImGui.TableNextColumn();

            // Target
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Target");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderFilteredCombo("##gt_tgt", vehicleIds, ref _pendingTargetIndex, _targetFilter);
            ImGui.TableNextColumn();

            // Preset
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Preset");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderPresetCombo(presetNames);
            ImGui.TableNextColumn();
            bool hasPresetSelection = _selectedPresetIndex >= 0 && _selectedPresetIndex < presetNames.Length;
            if (!hasPresetSelection) ImGui.BeginDisabled();
            if (ImGui.Button(" del ##gt_del"))
            {
                _deleteConfirmName = presetNames[_selectedPresetIndex];
                _openDeleteModal = true;
            }
            if (!hasPresetSelection) ImGui.EndDisabled();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Position / Rotation / Scale + Lock Rotation
        RenderDataFields("##gt_create", ref _pendingPosition, ref _pendingRotation,
            ref _pendingScale, ref _pendingLockRotation);

        // Create button
        ImGui.Spacing();
        bool canCreate = _pendingSourceIndex >= 0 && _pendingTargetIndex >= 0
            && _pendingSourceIndex != _pendingTargetIndex;
        if (!canCreate) ImGui.BeginDisabled();
        if (ImGui.Button(" Create Weld ##gt_addweld"))
        {
            InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex],
                _pendingPosition, _pendingRotation, _pendingScale, _pendingLockRotation);
        }
        if (!canCreate) ImGui.EndDisabled();

        // Validation / error messages
        if (_pendingSourceIndex >= 0 && _pendingTargetIndex >= 0
            && _pendingSourceIndex == _pendingTargetIndex)
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), "Source and target must differ.");
        }
        if (!string.IsNullOrEmpty(_weldError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _weldError);
        }
    }

    // ---- Weld Section ----

    private void RenderWeldSection(WeldEntry weld, int index, ref WeldEntry? toRemove)
    {
        if (!ImGui.CollapsingHeader($"Weld: {weld.Source.Id} -> {weld.Target.Id}##gt_weld_{index}",
            ImGuiTreeNodeFlags.DefaultOpen))
            return;

        // Bordered child window flush under the header
        var wpadX = ImGui.GetStyle().WindowPadding.X;
        float childW = ImGui.GetContentRegionAvail().X + wpadX * 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - wpadX);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 10f));
        ImGui.BeginChild($"gt_child_{index}", new float2(childW, 0),
            ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleVar();

        ImGui.Text($"{weld.Source.Id} welded to {weld.Target.Id}");

        float prevScale = weld.Scale;
        RenderDataFields($"##gt_w{index}", ref weld.Position, ref weld.Rotation,
            ref weld.Scale, ref weld.LockRotation);
        if (weld.Scale != prevScale)
            WeldEngine.ApplyVehicleScale(weld.Source, weld.Scale);

        ImGui.Spacing();
        if (ImGui.Button($" Save settings as preset... ##gt_save_{index}"))
        {
            _pendingSavePreset = new WeldPreset
            {
                Position = weld.Position,
                Rotation = weld.Rotation,
                Scale = weld.Scale,
                LockRotation = weld.LockRotation,
            };
            _savePresetName.Clear();
            _savePresetError = null;
            _openSaveModal = true;
        }
        ImGui.SameLine(0, 8);
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
        if (ImGui.Button($" Unweld ##gt_unweld_{index}"))
            toRemove = weld;
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.EndChild();
    }

    // ---- Shared Data Fields ----

    private void RenderDataFields(string idPrefix, ref float3 position, ref float3 rotation,
        ref float scale, ref bool lockRotation)
    {
        ImGui.Text("Position (x, y, z) in meters");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_pos", ref position, 0.001f, 0f, 0f);

        ImGui.Spacing();
        ImGui.Text("Rotation (pitch, yaw, roll) in degrees");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_rot", ref rotation, 0.025f, -180f, 180f);

        ImGui.Spacing();
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable($"{idPrefix}_scaletbl", 3, flags))
        {
            ImGui.TableSetupColumn("##s_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##s_val", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##s_lock", ImGuiTableColumnFlags.WidthStretch, 2f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Scale");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.DragFloat($"{idPrefix}_scaleval", ref scale, 0.001f, 0.05f, 20f);
            ImGui.TableNextColumn();
            ImGui.Checkbox($"Lock Rotation{idPrefix}_lockrot", ref lockRotation);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Filterable Combos ----

    private void RenderFilteredCombo(string id, string[] items, ref int selectedIndex,
        ImGuiTextFilter filter)
    {
        string preview = selectedIndex >= 0 && selectedIndex < items.Length
            ? items[selectedIndex] : "Select...";

        if (!ImGui.BeginCombo(id, preview))
            return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            filter.Clear();
        }
        filter.Draw($"{id}_filter", -1f);

        for (int i = 0; i < items.Length; i++)
        {
            if (!filter.PassFilter(items[i])) continue;
            bool sel = selectedIndex == i;
            if (ImGui.Selectable(items[i], sel))
                selectedIndex = i;
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    private void RenderPresetCombo(string[] presetNames)
    {
        string preview = _selectedPresetIndex >= 0 && _selectedPresetIndex < presetNames.Length
            ? presetNames[_selectedPresetIndex] : "Select...";

        if (!ImGui.BeginCombo("##gt_preset", preview))
            return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            _presetFilter.Clear();
        }
        _presetFilter.Draw("##gt_preset_filter", -1f);

        for (int i = 0; i < presetNames.Length; i++)
        {
            if (!_presetFilter.PassFilter(presetNames[i])) continue;
            bool sel = _selectedPresetIndex == i;
            if (ImGui.Selectable(presetNames[i], sel))
            {
                _selectedPresetIndex = i;
                var preset = _presetManager.GetPreset(presetNames[i]);
                if (preset != null)
                {
                    _pendingPosition = preset.Value.Position;
                    _pendingRotation = preset.Value.Rotation;
                    _pendingScale = preset.Value.Scale;
                    _pendingLockRotation = preset.Value.LockRotation;
                }
            }
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    // ---- Modals ----

    private void RenderDeletePresetModal()
    {
        bool open = true;
        if (!ImGui.BeginPopupModal("Delete preset##gt", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.Text($"Are you sure you want to delete\npreset '{_deleteConfirmName}'?");
        ImGui.Spacing();
        if (ImGui.Button(" You bet ##gt_delyes"))
        {
            if (_deleteConfirmName != null)
                _presetManager.DeletePreset(_deleteConfirmName);
            _selectedPresetIndex = -1;
            _deleteConfirmName = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##gt_delno"))
        {
            _deleteConfirmName = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void RenderSavePresetModal()
    {
        bool open = true;
        if (!ImGui.BeginPopupModal("Save as preset##gt", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.InputText("##gt_savename", _savePresetName);
        ImGui.Spacing();
        if (ImGui.Button(" Save ##gt_savebtn"))
        {
            var name = _savePresetName.ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _savePresetError = "Name is required";
            }
            else if (_presetManager.PresetExists(name))
            {
                _savePresetError = "A preset with this name already exists";
            }
            else
            {
                _presetManager.SavePreset(name, _pendingSavePreset);
                _savePresetError = null;
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##gt_savecancel"))
        {
            _savePresetError = null;
            ImGui.CloseCurrentPopup();
        }
        if (!string.IsNullOrEmpty(_savePresetError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _savePresetError);
        }
        ImGui.EndPopup();
    }

    // ---- Weld Logic (Public API) ----

    /// <summary>Creates a weld between two vehicles by their IDs.</summary>
    public (WeldEntry? Weld, string? Error) CreateWeld(
        string sourceVehicleId, string targetVehicleId,
        float3 position, float3 rotation, float scale, bool lockRotation)
    {
        if (sourceVehicleId == targetVehicleId)
            return (null, "Source and target must be different vehicles.");

        var vehicles = VehicleProvider.GetAllVehicles();
        var source = vehicles.FirstOrDefault(v => v.Id == sourceVehicleId);
        if (source == null)
            return (null, $"Source vehicle '{sourceVehicleId}' not found.");

        var target = vehicles.FirstOrDefault(v => v.Id == targetVehicleId);
        if (target == null)
            return (null, $"Target vehicle '{targetVehicleId}' not found.");

        foreach (var weld in _welds)
        {
            if (weld.Source == source)
                return (null, $"Vehicle {source.Id} is already welded as a source.");
        }

        var entry = new WeldEntry
        {
            Source = source,
            Target = target,
            Position = position,
            Rotation = rotation,
            Scale = scale,
            LockRotation = lockRotation,
        };
        _welds.Add(entry);

        if (scale != 1f)
            WeldEngine.ApplyVehicleScale(source, scale);

        SortWelds();
        Console.WriteLine($"garrys-torch: Welded {source.Id} to {target.Id}");
        return (entry, null);
    }

    /// <summary>Finds a weld by its source vehicle ID.</summary>
    public WeldEntry? FindWeld(string sourceVehicleId)
    {
        for (int i = 0; i < _welds.Count; i++)
            if (_welds[i].Source.Id == sourceVehicleId)
                return _welds[i];
        return null;
    }

    /// <summary>Modifies an existing weld. Only non-null fields are updated.</summary>
    public (WeldEntry? Weld, string? Error) ModifyWeld(
        string sourceVehicleId, float3? position, float3? rotation, float? scale, bool? lockRotation)
    {
        var weld = FindWeld(sourceVehicleId);
        if (weld == null)
            return (null, $"No weld found with source vehicle '{sourceVehicleId}'.");

        if (position.HasValue) weld.Position = position.Value;
        if (rotation.HasValue) weld.Rotation = rotation.Value;
        if (lockRotation.HasValue) weld.LockRotation = lockRotation.Value;

        if (scale.HasValue && scale.Value != weld.Scale)
        {
            weld.Scale = scale.Value;
            WeldEngine.ApplyVehicleScale(weld.Source, weld.Scale);
        }

        return (weld, null);
    }

    /// <summary>Removes a weld by its source vehicle ID.</summary>
    public bool RemoveWeld(string sourceVehicleId)
    {
        var weld = FindWeld(sourceVehicleId);
        if (weld == null) return false;
        RemoveWeld(weld);
        return true;
    }

    // ---- Preset API ----

    public string[] GetPresetNames() => _presetManager.GetPresetNames();
    public WeldPreset? GetPreset(string name) => _presetManager.GetPreset(name);
    public bool PresetExists(string name) => _presetManager.PresetExists(name);
    public bool SavePreset(string name, WeldPreset preset) => _presetManager.SavePreset(name, preset);
    public bool DeletePreset(string name) => _presetManager.DeletePreset(name);

    // ---- Weld Logic (Internal) ----

    private void InitiateWeld(Vehicle source, Vehicle target, float3 position, float3 rotation,
        float scale, bool lockRotation)
    {
        var (_, error) = CreateWeld(source.Id, target.Id, position, rotation, scale, lockRotation);
        if (error != null)
        {
            _weldError = error;
            return;
        }

        _weldError = null;
        _pendingPosition = new float3(0f, 0f, 0f);
        _pendingRotation = new float3(0f, 0f, 0f);
        _pendingScale = 1f;
        _pendingLockRotation = true;
    }

    private void RemoveWeld(WeldEntry entry)
    {
        WeldEngine.ApplyVehicleScale(entry.Source, 1.0f);
        Console.WriteLine($"garrys-torch: Unwelded {entry.Source.Id} from {entry.Target.Id}");
        _welds.Remove(entry);
    }

    private void SortWelds()
    {
        var sorted = WeldEngine.TopologicalSort(_welds);
        _welds.Clear();
        foreach (var w in sorted)
            _welds.Add(w);
    }
}
