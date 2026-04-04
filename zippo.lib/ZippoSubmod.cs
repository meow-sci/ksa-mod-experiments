using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ZippoLib;

public sealed class ZippoSubmod : ISubmod
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

    private ImGuiTextFilter _vehicleFilter = new();
    private ImGuiTextFilter _lightPartFilter = new();

    public void Initialize() { }
    public void Update(double dt) { }

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

        // Debug section (collapsed by default)
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
                if (ImGui.DragFloat("##zp_intensity", ref _intensity, 0.001f, 0f, 1f))
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
                var colorItems = LightController.ColorPresetNames;
                if (ImGui.Combo("##zp_colorpreset", ref _colorComboIdx, colorItems, colorItems.Length))
                {
                    if (_colorComboIdx > 0)
                    {
                        var presetColor = LightController.GetPresetColor(_colorComboIdx);
                        _currentColor = new float4(presetColor.X, presetColor.Y, presetColor.Z, 1.0f);
                        LightController.ApplyColor(selectedPart, presetColor);
                    }
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
                    _colorComboIdx = 0;
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleVar(); // CellPadding
        }

        SubmodUI.EndContentArea();
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
