using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// ISubmod implementation for the Engine Emissive feature.
/// Provides an ImGui panel to control per-engine Temperature and TFI overrides.
/// </summary>
public sealed class EngineEmissiveSubmod : ISubmod
{
    public string Name => "Engine Emissive";

    // Global controls
    private float _globalTemp = 0.8f;
    private float _globalTfi;
    private bool _globalEnabled;

    // Vehicle/engine selection
    private int _selectedVehicleIndex = -1;
    private ImGuiTextFilter _vehicleFilter = new();
    private List<(string Label, PartModelDynamic Model)> _cachedEngines = new();
    private string? _cachedVehicleId;

    // Per-engine temp overrides (mirrored from EngineEmissive for UI state)
    private readonly Dictionary<PartModelDynamic, float> _uiTemps = new();
    private readonly Dictionary<PartModelDynamic, float> _uiTfis = new();

    private string? _statusMessage;
    private bool _statusIsError;

    public void Initialize() { }
    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##ee_content");

        bool headerOpen = ImGui.CollapsingHeader("Engine Emissive (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Overrides the Temperature field on dynamic engine parts to control\n" +
            "their emissive glow. Uses the game's existing per-instance Temperature\n" +
            "data path — no shader modifications needed.\n\n" +
            "Temperature drives the DynamicMeshIndirect fragment shader's emissive\n" +
            "color lookup table, making engines glow from cool to hot.");
        if (!headerOpen)
        {
            SubmodUI.EndContentArea();
            return;
        }

        RenderGlobalControls();
        ImGui.Spacing();
        ImGui.SeparatorText("Per-Engine Control");
        RenderVehicleSelector();
        ImGui.Spacing();
        RenderEngineList();
        RenderStatusMessage();

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        EngineEmissive.Cleanup();
    }

    // ---- Global controls ----

    private void RenderGlobalControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##ee_global", 2, flags))
        {
            ImGui.TableSetupColumn("##ee_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ee_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Global enable
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Global");
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("Apply to all engines##ee_global", ref _globalEnabled))
            {
                EngineEmissive.GlobalEnabled = _globalEnabled;
            }

            // Temperature
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Temperature");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##ee_global_temp", ref _globalTemp, 0f, 1f, "%.2f"))
            {
                EngineEmissive.GlobalTemperature = _globalTemp;
            }

            // TFI
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("TFI");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderFloat("##ee_global_tfi", ref _globalTfi, 0f, 1f, "%.2f"))
            {
                EngineEmissive.GlobalTfi = _globalTfi;
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Vehicle selector ----

    private void RenderVehicleSelector()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++)
            vehicleIds[i] = vehicles[i].Id;

        if (_selectedVehicleIndex >= vehicles.Count)
            _selectedVehicleIndex = -1;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##ee_vsel", 2, flags))
        {
            ImGui.TableSetupColumn("##ee_vlbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ee_vwidget", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);

            int prevVehicle = _selectedVehicleIndex;
            string preview = _selectedVehicleIndex >= 0 && _selectedVehicleIndex < vehicleIds.Length
                ? vehicleIds[_selectedVehicleIndex] : "Select...";

            if (ImGui.BeginCombo("##ee_vehicle", preview))
            {
                if (ImGui.IsWindowAppearing())
                {
                    ImGui.SetKeyboardFocusHere();
                    _vehicleFilter.Clear();
                }
                _vehicleFilter.Draw("##ee_vfilter", -1f);

                for (int i = 0; i < vehicleIds.Length; i++)
                {
                    if (!_vehicleFilter.PassFilter(vehicleIds[i])) continue;
                    bool sel = _selectedVehicleIndex == i;
                    if (ImGui.Selectable(vehicleIds[i], sel))
                        _selectedVehicleIndex = i;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            if (_selectedVehicleIndex != prevVehicle)
            {
                RefreshEngineCache(
                    _selectedVehicleIndex >= 0 ? vehicles[_selectedVehicleIndex] : null);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Engine list with per-engine sliders ----

    private void RenderEngineList()
    {
        if (_cachedEngines.Count == 0)
        {
            ImGui.TextDisabled("No dynamic parts found. Select a vehicle above.");
            return;
        }

        ImGui.Text($"{_cachedEngines.Count} dynamic part(s):");
        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("##ee_engines", 4, flags))
        {
            ImGui.TableSetupColumn("Engine", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Temp", ImGuiTableColumnFlags.WidthFixed, 120f);
            ImGui.TableSetupColumn("TFI", ImGuiTableColumnFlags.WidthFixed, 120f);
            ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed, 60f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _cachedEngines.Count; i++)
            {
                var (label, model) = _cachedEngines[i];
                ImGui.TableNextRow();

                // Label
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                bool hasOverride = EngineEmissive.HasOverride(model);
                if (hasOverride)
                    ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), label);
                else
                    ImGui.Text(label);

                // Temp slider
                if (!_uiTemps.ContainsKey(model))
                {
                    var existing = EngineEmissive.GetSettings(model);
                    _uiTemps[model] = existing?.Temperature ?? 0.8f;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                float temp = _uiTemps[model];
                if (ImGui.SliderFloat($"##ee_t_{i}", ref temp, 0f, 1f, "%.2f"))
                {
                    _uiTemps[model] = temp;
                    float tfi = _uiTfis.GetValueOrDefault(model, 0f);
                    EngineEmissive.SetEngine(model, temp, tfi);
                }

                // TFI slider
                if (!_uiTfis.ContainsKey(model))
                {
                    var existing = EngineEmissive.GetSettings(model);
                    _uiTfis[model] = existing?.Tfi ?? 0f;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                float tfiVal = _uiTfis[model];
                if (ImGui.SliderFloat($"##ee_f_{i}", ref tfiVal, 0f, 1f, "%.2f"))
                {
                    _uiTfis[model] = tfiVal;
                    float tempVal = _uiTemps.GetValueOrDefault(model, 0.8f);
                    EngineEmissive.SetEngine(model, tempVal, tfiVal);
                }

                // Clear button
                ImGui.TableNextColumn();
                if (!hasOverride) ImGui.BeginDisabled();
                if (ImGui.Button($" Clear ##{i}"))
                {
                    EngineEmissive.ClearEngine(model);
                    SetStatus($"Cleared {label}.", false);
                }
                if (!hasOverride) ImGui.EndDisabled();
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();
        if (ImGui.Button(" Clear All Overrides "))
        {
            EngineEmissive.ClearAll();
            _globalEnabled = false;
            SetStatus("All engine overrides cleared.", false);
        }
    }

    // ---- Status ----

    private void RenderStatusMessage()
    {
        if (string.IsNullOrEmpty(_statusMessage)) return;
        ImGui.Spacing();
        if (_statusIsError)
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _statusMessage);
        else
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _statusMessage);
    }

    private void SetStatus(string msg, bool isError)
    {
        _statusMessage = msg;
        _statusIsError = isError;
    }

    // ---- Cache ----

    private void RefreshEngineCache(Vehicle? vehicle)
    {
        _cachedEngines.Clear();
        _uiTemps.Clear();
        _uiTfis.Clear();
        _cachedVehicleId = vehicle?.Id;

        if (vehicle == null) return;

        _cachedEngines = EngineEmissive.ScanDynamicParts(vehicle);
    }
}
