using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// ISubmod implementation for the Vehicle Paint feature.
/// Provides an ImGui panel for activating paint shaders and picking colors
/// to apply to vehicle parts.
/// </summary>
public sealed class VehiclePaintSubmod : ISubmod
{
    public string Name => "Vehicle Paint";

    // UI state
    private float3 _pickerColor = new float3(1f, 0.3f, 0.3f);
    private bool _applyToAll = true;
    private string? _statusMessage;
    private bool _statusIsError;

    // Vehicle/part selection (when not applying to all)
    private int _selectedVehicleIndex = -1;
    private int _selectedPartIndex = -1;
    private ImGuiTextFilter _vehicleFilter = new();
    private ImGuiTextFilter _partFilter = new();

    // Cached part model refs for the selected vehicle
    private List<(string Label, PartModel Model)> _cachedParts = new();
    private string? _cachedVehicleId;

    public void Initialize() { }

    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##vp_content");

        bool headerOpen = ImGui.CollapsingHeader("Vehicle Paint (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Paints vehicle parts by injecting custom shaders at runtime.\n" +
            "Writes RGB color into the PerInstanceData padding bytes\n" +
            "and applies a multiplicative tint in the fragment shader.");
        if (!headerOpen)
        {
            SubmodUI.EndContentArea();
            return;
        }

        RenderShaderStatus();
        ImGui.Spacing();
        RenderControls();
        ImGui.Spacing();
        RenderButtonRow();
        RenderStatusMessage();

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        VehiclePaint.Cleanup();
    }

    // ---- Shader status ----

    private void RenderShaderStatus()
    {
        if (VehiclePaint.ShadersActive)
        {
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), "Shaders: Active");
        }
        else
        {
            ImGui.TextColored(new float4(1f, 1f, 0.4f, 1f), "Shaders: Inactive");
            ImGui.SameLine(0, 12);
            if (ImGui.Button(" Activate "))
            {
                if (VehiclePaint.ActivateShaders())
                    SetStatus("Paint shaders activated.", false);
                else
                    SetStatus(VehiclePaint.LastError ?? "Shader activation failed.", true);
            }
        }
    }

    // ---- Main controls ----

    private void RenderControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##vp_controls", 2, flags))
        {
            ImGui.TableSetupColumn("##vp_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##vp_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Mode
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Mode");
            ImGui.TableNextColumn();
            ImGui.Checkbox("Apply to All##vp", ref _applyToAll);

            // Color
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Color");
            ImGui.TableNextColumn();
            ImGui.ColorEdit3("##vp_color", ref _pickerColor, ImGuiColorEditFlags.NoInputs);
            ImGui.SameLine(0, 8);

            bool canApply = VehiclePaint.ShadersActive;
            if (!canApply) ImGui.BeginDisabled();
            if (ImGui.Button(" Apply ##vp_apply"))
                ApplyPaint();
            if (!canApply) ImGui.EndDisabled();

            // Vehicle / Part combos (only when not applying to all)
            if (!_applyToAll)
            {
                var vehicles = VehicleProvider.GetAllVehicles();
                var vehicleIds = new string[vehicles.Count];
                for (int i = 0; i < vehicles.Count; i++)
                    vehicleIds[i] = vehicles[i].Id;

                if (_selectedVehicleIndex >= vehicles.Count)
                    _selectedVehicleIndex = -1;

                // Vehicle
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                int prevVehicle = _selectedVehicleIndex;
                RenderFilteredCombo("##vp_vehicle", vehicleIds, ref _selectedVehicleIndex, _vehicleFilter);

                // Refresh part cache when vehicle selection changes
                if (_selectedVehicleIndex != prevVehicle)
                {
                    _selectedPartIndex = -1;
                    RefreshPartCache(
                        _selectedVehicleIndex >= 0 ? vehicles[_selectedVehicleIndex] : null);
                }

                // Part
                var partLabels = new string[_cachedParts.Count];
                for (int i = 0; i < _cachedParts.Count; i++)
                    partLabels[i] = _cachedParts[i].Label;

                if (_selectedPartIndex >= _cachedParts.Count)
                    _selectedPartIndex = -1;

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Part");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                RenderFilteredCombo("##vp_part", partLabels, ref _selectedPartIndex, _partFilter);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Button row ----

    private void RenderButtonRow()
    {
        bool hasPaint = VehiclePaint.ShadersActive;
        if (!hasPaint) ImGui.BeginDisabled();

        if (ImGui.Button(" Clear All Paint "))
        {
            VehiclePaint.ClearAllPaint();
            SetStatus("All paint cleared.", false);
        }

        ImGui.SameLine(0, 8);

        if (ImGui.Button(" Deactivate Shaders "))
        {
            if (VehiclePaint.DeactivateShaders())
                SetStatus("Shaders deactivated.", false);
            else
                SetStatus(VehiclePaint.LastError ?? "Shader deactivation failed.", true);
        }

        if (!hasPaint) ImGui.EndDisabled();
    }

    // ---- Status messages ----

    private void RenderStatusMessage()
    {
        if (string.IsNullOrEmpty(_statusMessage)) return;
        ImGui.Spacing();
        if (_statusIsError)
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _statusMessage);
        else
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _statusMessage);
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }

    // ---- Paint application ----

    private void ApplyPaint()
    {
        if (_applyToAll)
        {
            VehiclePaint.PaintAllEnabled = true;
            VehiclePaint.DefaultColor = _pickerColor;
            SetStatus("Paint applied to all parts.", false);
            return;
        }

        // Single part mode
        if (_selectedPartIndex < 0 || _selectedPartIndex >= _cachedParts.Count)
        {
            SetStatus("Select a vehicle and part first.", true);
            return;
        }

        var partModel = _cachedParts[_selectedPartIndex].Model;
        VehiclePaint.SetPaintColor(partModel, _pickerColor);
        SetStatus($"Paint applied to {_cachedParts[_selectedPartIndex].Label}.", false);
    }

    // ---- Vehicle/part cache ----

    private void RefreshPartCache(Vehicle? vehicle)
    {
        _cachedParts.Clear();
        _cachedVehicleId = vehicle?.Id;

        if (vehicle == null) return;

        try
        {
            var parts = PartHelpers.GetAllParts(vehicle);
            foreach (var part in parts)
            {
                var modules = part.Modules.Get<PartModelModule>();
                for (int i = 0; i < modules.Length; i++)
                {
                    var label = modules.Length > 1
                        ? $"{part.Id} [{i}]"
                        : part.Id;
                    _cachedParts.Add((label, modules[i].PartModel));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error scanning parts: {ex.Message}");
        }
    }

    // ---- Filtered combo helper ----

    private static void RenderFilteredCombo(string id, string[] items, ref int selectedIndex,
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
}
