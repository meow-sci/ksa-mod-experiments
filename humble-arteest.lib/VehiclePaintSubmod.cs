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
    public string Tooltip => "Paints vehicle parts with custom colors via shader injection.";

    // UI state
    private float3 _pickerColor = new float3(1f, 0.3f, 0.3f);
    private bool _applyToAll = true;
    private string? _statusMessage;
    private bool _statusIsError;

    // Vehicle selection (when not applying to all)
    private int _selectedVehicleIndex = -1;
    private readonly ImInputString _vehicleFilter = new(128);

    // Cached part entries for the selected vehicle
    private List<PartEntry> _cachedParts = new();
    private string? _cachedVehicleId;
    private ImGuiTextFilter _partTableFilter = new();

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

        RenderBody();

        SubmodUI.EndContentArea();
    }

    internal void RenderBody()
    {
        if (!VehiclePaint.IsSupported)
        {
            RenderUnsupportedNotice();
            return;
        }

        RenderShaderButtonRow();
        RenderStatusMessage();
        if (VehiclePaint.ShadersActive)
        {
            ImGui.Spacing();
            RenderControls();
        }
    }

    private static void RenderUnsupportedNotice()
    {
        ImGui.TextColored(new float4(1f, 0.6f, 0.2f, 1f), "Unavailable on this KSA build");
        ImGui.TextWrapped(VehiclePaint.UnsupportedReason
            ?? "Vehicle Paint is not supported on this game version.");
    }

    public void Dispose()
    {
        VehiclePaint.Cleanup();
    }

    // ---- Shader status + action buttons ----

    private void RenderShaderButtonRow()
    {
        if (VehiclePaint.ShadersActive)
            ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), "Shaders: Active");
        else
            ImGui.TextColored(new float4(1f, 1f, 0.4f, 1f), "Shaders: Inactive");

        if (!VehiclePaint.ShadersActive)
        {
            ImGui.SameLine(0, 12);
            if (ImGui.Button(" Activate "))
            {
                if (VehiclePaint.ActivateShaders())
                    SetStatus("Paint shaders activated.", false);
                else
                    SetStatus(VehiclePaint.LastError ?? "Shader activation failed.", true);
            }
        }

        bool hasPaint = VehiclePaint.ShadersActive;
        if (!hasPaint) ImGui.BeginDisabled();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Deactivate "))
        {
            if (VehiclePaint.DeactivateShaders())
                SetStatus("Shaders deactivated.", false);
            else
                SetStatus(VehiclePaint.LastError ?? "Shader deactivation failed.", true);
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Clear All "))
        {
            VehiclePaint.ClearAllPaint();
            foreach (var entry in _cachedParts)
                entry.Enabled = false;
            SetStatus("All paint cleared.", false);
        }
        if (!hasPaint) ImGui.EndDisabled();
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

            // Color picker (always visible)
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Color");
            ImGui.TableNextColumn();
            if (ImGui.ColorEdit3("##vp_color", ref _pickerColor, ImGuiColorEditFlags.NoInputs))
                OnGlobalColorChanged();

            // Apply-to-all: auto-apply when color changes and shaders are active
            if (_applyToAll && VehiclePaint.ShadersActive)
            {
                VehiclePaint.PaintAllEnabled = true;
                VehiclePaint.DefaultColor = _pickerColor;
            }

            // Vehicle selector (only when not applying to all)
            if (!_applyToAll)
            {
                var vehicles = VehicleProvider.GetAllVehicles();
                var vehicleIds = new string[vehicles.Count];
                for (int i = 0; i < vehicles.Count; i++)
                    vehicleIds[i] = vehicles[i].Id;

                if (_selectedVehicleIndex >= vehicles.Count)
                    _selectedVehicleIndex = -1;

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                int prevVehicle = _selectedVehicleIndex;
                RenderFilteredCombo("##vp_vehicle", vehicleIds, ref _selectedVehicleIndex, _vehicleFilter);

                if (_selectedVehicleIndex != prevVehicle)
                    RefreshPartCache(_selectedVehicleIndex >= 0 ? vehicles[_selectedVehicleIndex] : null);
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Per-part table (visible when not applying to all)
        if (!_applyToAll)
        {
            ImGui.Spacing();
            RenderPartTable();
        }
    }

    // ---- Per-part table ----

    private void RenderPartTable()
    {
        if (_cachedParts.Count == 0)
        {
            ImGui.Text("No parts. Select a vehicle above.");
            return;
        }

        // Toolbar: All / None / filter
        if (ImGui.Button(" All ##vp"))
        {
            foreach (var entry in _cachedParts)
            {
                if (!_partTableFilter.PassFilter(entry.Label)) continue;
                entry.Enabled = true;
                ApplyPartPaint(entry);
            }
        }
        ImGui.SameLine(0, 4);
        if (ImGui.Button(" None ##vp"))
        {
            foreach (var entry in _cachedParts)
            {
                if (!_partTableFilter.PassFilter(entry.Label)) continue;
                entry.Enabled = false;
                ApplyPartPaint(entry);
            }
        }
        ImGui.SameLine(0, 12);
        ImGui.SetNextItemWidth(-1f);
        _partTableFilter.Draw("##vp_ptfilter");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
                  | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                  | ImGuiTableFlags.ScrollY;

        float maxHeight = ImGui.GetTextLineHeightWithSpacing() * 12;
        if (ImGui.BeginTable("##vp_parts", 3, flags, new float2(0, maxHeight)))
        {
            ImGui.TableSetupColumn("##chk", ImGuiTableColumnFlags.WidthFixed, 38f);
            ImGui.TableSetupColumn("##clr", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _cachedParts.Count; i++)
            {
                var entry = _cachedParts[i];
                if (!_partTableFilter.PassFilter(entry.Label)) continue;
                ImGui.PushID(i);

                ImGui.TableNextRow();

                // Checkbox column
                ImGui.TableNextColumn();
                bool enabled = entry.Enabled;
                if (ImGui.Checkbox("##en", ref enabled))
                {
                    entry.Enabled = enabled;
                    ApplyPartPaint(entry);
                }

                // Color picker column
                ImGui.TableNextColumn();
                var color = entry.Color;
                if (ImGui.ColorEdit3("##clr", ref color,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                {
                    entry.Color = color;
                    if (entry.Enabled)
                        ApplyPartPaint(entry);
                }

                // Part name column
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(entry.Label);

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
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

    // ---- Paint helpers ----

    private void OnGlobalColorChanged()
    {
        if (_applyToAll)
            return; // apply-to-all is handled in RenderControls continuously

        // Propagate global color to all per-part entries
        foreach (var entry in _cachedParts)
            entry.Color = _pickerColor;

        if (VehiclePaint.ShadersActive)
        {
            foreach (var entry in _cachedParts)
            {
                if (entry.Enabled)
                    VehiclePaint.SetPaintColor(entry.Model, entry.Color);
            }
        }
    }

    private void ApplyPartPaint(PartEntry entry)
    {
        if (!VehiclePaint.ShadersActive) return;

        if (entry.Enabled)
            VehiclePaint.SetPaintColor(entry.Model, entry.Color);
        else
            VehiclePaint.ClearPaint(entry.Model);
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
            // Track label occurrences to disambiguate duplicates
            var labelCounts = new Dictionary<string, int>();

            foreach (var part in parts)
            {
                var modules = part.Modules.Get<PartModelModule>();
                for (int i = 0; i < modules.Length; i++)
                {
                    var baseName = modules.Length > 1
                        ? $"{part.Id} [{i}]"
                        : part.Id;

                    if (!labelCounts.TryGetValue(baseName, out int count))
                        count = 0;
                    labelCounts[baseName] = count + 1;

                    // Label will be disambiguated in a second pass
                    _cachedParts.Add(new PartEntry(baseName, modules[i].PartModel, _pickerColor));
                }
            }

            // Second pass: append occurrence number for any duplicated labels
            var seen = new Dictionary<string, int>();
            foreach (var entry in _cachedParts)
            {
                if (labelCounts[entry.BaseName] > 1)
                {
                    if (!seen.TryGetValue(entry.BaseName, out int idx))
                        idx = 0;
                    seen[entry.BaseName] = idx + 1;
                    entry.Label = $"{entry.BaseName} #{idx + 1}";
                }
                else
                {
                    entry.Label = entry.BaseName;
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
        ImInputString filter)
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
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint($"{id}_filter", "filter..."u8, filter);
        string filterText = filter.ToString().Trim();

        for (int i = 0; i < items.Length; i++)
        {
            if (filterText.Length > 0 &&
                !items[i].Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;
            bool sel = selectedIndex == i;
            if (ImGui.Selectable(items[i], sel))
                selectedIndex = i;
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    // ---- Part entry ----

    private sealed class PartEntry
    {
        public string BaseName;
        public string Label;
        public PartModel Model;
        public float3 Color;
        public bool Enabled;

        public PartEntry(string baseName, PartModel model, float3 defaultColor)
        {
            BaseName = baseName;
            Label = baseName;
            Model = model;
            Color = defaultColor;
            Enabled = false;
        }
    }
}
