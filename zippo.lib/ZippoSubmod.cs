using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ZippoLib;

public sealed class ZippoSubmod : ISubmod
{
    public static ZippoSubmod? Instance { get; private set; }

    public string Name => "Zippo - Lights!";
    public string Tooltip => "Controls light part intensity and colors.";

    private List<Vehicle> _vehicles = new();
    private string[] _vehicleComboItems = new[] { "(none)" };
    private int _vehicleComboIdx;

    private List<Part> _lightParts = new();
    private string[] _lightPartComboItems = new[] { "(none)" };
    private int _lightPartComboIdx;

    private float _intensity = 1.0f;
    private float _savedIntensity = 1.0f;
    private bool _lightEnabled = true;
    // 0 = Default, 1..4 = named presets (offset -1 into LightController.ColorPresetNames), 5 = (Custom)
    private int _colorComboIdx;
    private bool _colorIsCustom;
    private float4 _currentColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly Dictionary<string, float3> _originalColors = new();
    private readonly LightAnimationManager _animationManager = new();

    private ImGuiTextFilter _vehicleFilter = new();
    private ImGuiTextFilter _lightPartFilter = new();

    public void Initialize() { Instance = this; }
    public void Update(double dt) { _animationManager.Update(dt, ResolvePartById); }

    public void RenderContent()
    {
        RefreshVehicles();

        SubmodUI.BeginContentArea("##zp_content");

        // Vehicle and Light Part selectors in a 2-column table
        var tableFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##zp_selectors", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##zp_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##zp_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Vehicle row
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            int prevVehicleIdx = _vehicleComboIdx;
            if (ImGui.BeginCombo("##zp_vehicle", _vehicleComboItems[_vehicleComboIdx]))
            {
                if (ImGui.IsWindowAppearing()) { ImGui.SetKeyboardFocusHere(); _vehicleFilter.Clear(); }
                _vehicleFilter.Draw("##zp_vflt", -1f);
                for (int i = 0; i < _vehicleComboItems.Length; i++)
                {
                    if (_vehicleFilter.PassFilter(_vehicleComboItems[i]))
                    {
                        bool sel = _vehicleComboIdx == i;
                        if (ImGui.Selectable(_vehicleComboItems[i], sel)) _vehicleComboIdx = i;
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
            if (_vehicleComboIdx != prevVehicleIdx)
            {
                ClearLightParts();
                var v = SelectedVehicle;
                if (v != null) RebuildLightParts(v);
            }

            // Light Part row (only when a vehicle is selected)
            if (_vehicleComboIdx > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Light Part");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                int prevPartIdx = _lightPartComboIdx;
                if (ImGui.BeginCombo("##zp_lightpart", _lightPartComboItems[_lightPartComboIdx]))
                {
                    if (ImGui.IsWindowAppearing()) { ImGui.SetKeyboardFocusHere(); _lightPartFilter.Clear(); }
                    _lightPartFilter.Draw("##zp_pflt", -1f);
                    for (int i = 0; i < _lightPartComboItems.Length; i++)
                    {
                        if (_lightPartFilter.PassFilter(_lightPartComboItems[i]))
                        {
                            bool sel = _lightPartComboIdx == i;
                            if (ImGui.Selectable(_lightPartComboItems[i], sel)) _lightPartComboIdx = i;
                            if (sel) ImGui.SetItemDefaultFocus();
                        }
                    }
                    ImGui.EndCombo();
                }
                if (_lightPartComboIdx != prevPartIdx)
                {
                    var p = SelectedLightPart;
                    if (p != null) OnPartSelected(p);
                    else { _lightEnabled = true; _intensity = 1.0f; }
                }
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        var selectedPart = SelectedLightPart;
        if (selectedPart != null)
        {
            ImGui.SeparatorText("Light Controls");

            var ctrlFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
            if (ImGui.BeginTable("##zp_controls", 2, ctrlFlags))
            {
                ImGui.TableSetupColumn("##zp_clbl", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("##zp_cwidget", ImGuiTableColumnFlags.WidthStretch, 3f);

                // On/Off row
                var ls = selectedPart.LightSwitch ?? selectedPart.FullPart.LightSwitch;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("On / Off");
                ImGui.TableNextColumn();
                if (ImGui.Button(_lightEnabled ? " Turn Off ##zp" : " Turn On ##zp"))
                {
                    _lightEnabled = !_lightEnabled;
                    if (ls != null)
                        ls.LightIsActive = _lightEnabled;
                    else
                        LightController.ApplyIntensity(selectedPart, _lightEnabled ? _savedIntensity : 0f);
                }

                // Intensity row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Intensity");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##zp_intensity", ref _intensity, 0.005f, 0f, 1f))
                {
                    _savedIntensity = _intensity;
                    LightController.ApplyIntensity(selectedPart, _intensity);
                }

                // Color Preset row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Color Preset");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                var colorItems = BuildColorComboItems();
                if (ImGui.Combo("##zp_colorpreset", ref _colorComboIdx, colorItems, colorItems.Length))
                {
                    if (_colorComboIdx == 0)
                    {
                        // Restore original color
                        if (_originalColors.TryGetValue(selectedPart.Id, out var orig))
                        {
                            _currentColor = new float4(orig.X, orig.Y, orig.Z, 1.0f);
                            LightController.ApplyColor(selectedPart, orig);
                        }
                        _colorIsCustom = false;
                    }
                    else if (_colorComboIdx >= 1 && _colorComboIdx <= LightController.ColorPresetNames.Length)
                    {
                        var presetColor = LightController.GetPresetColor(_colorComboIdx - 1);
                        _currentColor = new float4(presetColor.X, presetColor.Y, presetColor.Z, 1.0f);
                        LightController.ApplyColor(selectedPart, presetColor);
                        _colorIsCustom = false;
                    }
                    // Selecting "(Custom)" is a no-op — keeps current color as-is
                }

                // Color picker row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding(); ImGui.Text("Color");
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.ColorEdit4("##zp_colorpicker", ref _currentColor, ImGuiColorEditFlags.NoLabel))
                {
                    var color3 = new float3(_currentColor.X, _currentColor.Y, _currentColor.Z);
                    LightController.ApplyColor(selectedPart, color3);
                    _colorIsCustom = true;
                    _colorComboIdx = LightController.ColorPresetNames.Length + 1; // "(Custom)"
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleVar(); // CellPadding
        }

        // Debug section (collapsed by default, placed after controls)
        if (_vehicleComboIdx > 0 && ImGui.CollapsingHeader("Debug##zp"))
        {
            if (ImGui.Button(" Dump Parts ##zp"))
            {
                var v = SelectedVehicle;
                if (v != null)
                {
                    Console.WriteLine("grant/zippo: === debug dump (parts with Components > 0) ===");
                    var parts = v.Parts.Parts;
                    for (int i = 0; i < parts.Length; i++)
                        LightController.DumpPartsWithComponents(parts[i]);
                }
            }
        }

        SubmodUI.EndContentArea();
    }

    /// <summary>Builds the color preset combo items, appending "(Custom)" only when a custom color is active.</summary>
    private string[] BuildColorComboItems()
    {
        var presets = LightController.ColorPresetNames;
        int count = 1 + presets.Length + (_colorIsCustom ? 1 : 0);
        var items = new string[count];
        items[0] = "Default";
        for (int i = 0; i < presets.Length; i++)
            items[i + 1] = presets[i];
        if (_colorIsCustom)
            items[count - 1] = "(Custom)";
        return items;
    }

    // ── Public API for RPC use ────────────────────────────────────────────────

    /// <summary>Returns light part info for all light parts on a vehicle, or null if vehicle not found.</summary>
    public List<LightPartInfo>? GetLightPartInfos(string vehicleId)
    {
        var vehicle = VehicleProvider.GetAllVehicles().Find(v => v.Id == vehicleId);
        if (vehicle == null) return null;

        var parts = LightController.GetLightParts(vehicle);
        var result = new List<LightPartInfo>(parts.Count);
        foreach (var part in parts)
        {
            var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
            bool isEnabled = ls == null || ls.LightIsActive;
            result.Add(new LightPartInfo(
                part.Id,
                part.DisplayName ?? part.Id,
                LightController.ReadIntensity(part.Template),
                LightController.ReadColor(part.Template),
                isEnabled,
                _animationManager.IsAnimating(part.Id),
                _animationManager.GetQueueCount(part.Id)));
        }
        return result;
    }

    /// <summary>Sets color and/or intensity on a specific light part. Returns error message or null on success.</summary>
    public string? SetLightState(string vehicleId, string partId, float3? color, float? intensity, bool? enabled)
    {
        var part = ResolvePartInVehicle(vehicleId, partId);
        if (part == null) return $"Part '{partId}' not found on vehicle '{vehicleId}'.";

        if (color.HasValue) LightController.ApplyColor(part, color.Value);
        if (intensity.HasValue) LightController.ApplyIntensity(part, intensity.Value);
        if (enabled.HasValue)
        {
            var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
            if (ls != null)
                ls.LightIsActive = enabled.Value;
            else if (!enabled.Value)
                LightController.ApplyIntensity(part, 0f);
        }
        return null;
    }

    /// <summary>Queues a light animation on a specific part. Returns error message or null on success.</summary>
    public string? QueueAnimation(string vehicleId, string partId, LightAnimation animation)
    {
        var part = ResolvePartInVehicle(vehicleId, partId);
        if (part == null) return $"Part '{partId}' not found on vehicle '{vehicleId}'.";

        if (!_animationManager.Enqueue(partId, animation))
            return $"Animation queue is full for part '{partId}' (max {LightAnimationManager.MaxQueueDepth}).";
        return null;
    }

    /// <summary>Clears the animation queue for a specific part. Returns error message or null on success.</summary>
    public string? ClearAnimationQueue(string vehicleId, string partId)
    {
        // No error if part doesn't exist — clear is idempotent
        _animationManager.CancelAll(partId);
        return null;
    }

    /// <summary>Returns true if a part has an active animation.</summary>
    public bool IsAnimating(string partId) => _animationManager.IsAnimating(partId);

    private Part? ResolvePartById(string partId)
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        foreach (var v in vehicles)
        {
            var parts = LightController.GetLightParts(v);
            foreach (var p in parts)
                if (p.Id == partId) return p;
        }
        return null;
    }

    private Part? ResolvePartInVehicle(string vehicleId, string partId)
    {
        var vehicle = VehicleProvider.GetAllVehicles().Find(v => v.Id == vehicleId);
        if (vehicle == null) return null;
        return LightController.GetLightParts(vehicle).Find(p => p.Id == partId);
    }

    public void Dispose() { Instance = null; }

    private Vehicle? SelectedVehicle =>
        _vehicleComboIdx > 0 && (_vehicleComboIdx - 1) < _vehicles.Count
            ? _vehicles[_vehicleComboIdx - 1] : null;

    private Part? SelectedLightPart =>
        _lightPartComboIdx > 0 && (_lightPartComboIdx - 1) < _lightParts.Count
            ? _lightParts[_lightPartComboIdx - 1] : null;

    private void RefreshVehicles()
    {
        var list = VehicleProvider.GetAllVehicles();
        _vehicles.Clear();
        _vehicles.AddRange(list);

        var names = new string[_vehicles.Count + 1];
        names[0] = "(none)";
        for (int i = 0; i < _vehicles.Count; i++)
            names[i + 1] = _vehicles[i].Id;
        _vehicleComboItems = names;

        if (_vehicleComboIdx > _vehicles.Count)
        {
            _vehicleComboIdx = 0;
            ClearLightParts();
        }
    }

    private void ClearLightParts()
    {
        _lightParts.Clear();
        _lightPartComboItems = new[] { "(none)" };
        _lightPartComboIdx = 0;
    }

    private void RebuildLightParts(Vehicle vehicle)
    {
        _lightParts = LightController.GetLightParts(vehicle);

        var names = new string[_lightParts.Count + 1];
        names[0] = "(none)";
        for (int i = 0; i < _lightParts.Count; i++)
            names[i + 1] = _lightParts[i].DisplayName ?? _lightParts[i].Id;
        _lightPartComboItems = names;
        _lightPartComboIdx = 0;
    }

    private void OnPartSelected(Part part)
    {
        _intensity = Math.Clamp(LightController.ReadIntensity(part.Template), 0f, 1f);
        _savedIntensity = _intensity;
        var ls = part.LightSwitch ?? part.FullPart.LightSwitch;
        _lightEnabled = ls == null || ls.LightIsActive;

        // First-time discovery: save the original color for this part
        if (!_originalColors.ContainsKey(part.Id))
            _originalColors[part.Id] = LightController.ReadColor(part.Template);

        // Default to the "Default" preset (original color)
        _colorIsCustom = false;
        _colorComboIdx = 0;
        var orig = _originalColors[part.Id];
        _currentColor = new float4(orig.X, orig.Y, orig.Z, 1.0f);
    }
}
