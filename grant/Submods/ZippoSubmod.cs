using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.ZippoLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Grant.Submods;

internal sealed class ZippoSubmod : IGrantSubmod
{
    public string Name => "Zippo \u2014 Light Control";

    private List<Vehicle> _vehicles = new();
    private string[] _vehicleComboItems = new[] { "(none)" };
    private int _vehicleComboIdx;

    private List<Part> _lightParts = new();
    private string[] _lightPartComboItems = new[] { "(none)" };
    private int _lightPartComboIdx;

    private float _intensity = 1.0f;
    private float _savedIntensity = 1.0f;
    private bool _lightEnabled = true;
    private int _colorComboIdx;
    private float4 _currentColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    public void Initialize() { }
    public void Update(double dt) { }

    public void RenderContent()
    {
        RefreshVehicles();

        ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "Zippo  Light Control");
        ImGui.Separator();
        ImGui.Spacing();

        // Vehicle combobox
        int prevVehicleIdx = _vehicleComboIdx;
        if (ImGui.Combo("Vehicle##zp", ref _vehicleComboIdx, _vehicleComboItems, _vehicleComboItems.Length))
        {
            if (_vehicleComboIdx != prevVehicleIdx)
            {
                ClearLightParts();
                var v = SelectedVehicle;
                if (v != null) RebuildLightParts(v);
            }
        }

        if (_vehicleComboIdx > 0)
        {
            ImGui.Spacing();

            // Light part combobox
            int prevPartIdx = _lightPartComboIdx;
            if (ImGui.Combo("Light Part##zp", ref _lightPartComboIdx, _lightPartComboItems, _lightPartComboItems.Length))
            {
                if (_lightPartComboIdx != prevPartIdx)
                {
                    var p = SelectedLightPart;
                    if (p != null) OnPartSelected(p);
                    else { _lightEnabled = true; _intensity = 1.0f; }
                }
            }

            // Debug dump button
            ImGui.SameLine();
            if (ImGui.Button("Dbg##zp"))
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

        var selectedPart = SelectedLightPart;
        if (selectedPart != null)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // On/Off toggle
            var ls = selectedPart.LightSwitch ?? selectedPart.FullPart.LightSwitch;
            if (ImGui.Button(_lightEnabled ? "Turn Off##zp" : "Turn On##zp"))
            {
                _lightEnabled = !_lightEnabled;
                if (ls != null)
                    ls.LightIsActive = _lightEnabled;
                else
                    LightController.ApplyIntensity(selectedPart, _lightEnabled ? _savedIntensity : 0f);
            }

            ImGui.Spacing();

            // Intensity drag slider
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.DragFloat("##intensity_zp", ref _intensity, 0.001f, 0f, 1f))
            {
                _savedIntensity = _intensity;
                LightController.ApplyIntensity(selectedPart, _intensity);
            }
            ImGui.Text("Emissive Intensity");

            ImGui.Spacing();

            // Color preset combo
            var colorItems = LightController.ColorPresetNames;
            if (ImGui.Combo("Color##zp", ref _colorComboIdx, colorItems, colorItems.Length))
            {
                if (_colorComboIdx > 0)
                {
                    var presetColor = LightController.GetPresetColor(_colorComboIdx);
                    _currentColor = new float4(presetColor.X, presetColor.Y, presetColor.Z, 1.0f);
                    LightController.ApplyColor(selectedPart, presetColor);
                }
            }

            // Manual color picker
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.ColorEdit4("##color_picker_zp", ref _currentColor, ImGuiColorEditFlags.NoLabel))
            {
                var color3 = new float3(_currentColor.X, _currentColor.Y, _currentColor.Z);
                LightController.ApplyColor(selectedPart, color3);
                _colorComboIdx = 0;
            }
        }
    }

    public void Dispose() { }

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
        _colorComboIdx = 0;
        var color3 = LightController.ReadColor(part.Template);
        _currentColor = new float4(color3.X, color3.Y, color3.Z, 1.0f);
    }
}
