using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.MeshDeformLib;

/// <summary>
/// ISubmod implementation for MeshDeform.
/// Provides an ImGui panel to manually apply radial deformation (dents / bulges)
/// to individual vehicle parts.  Deformations are session-only and visual-only.
/// </summary>
public sealed class MeshDeformSubmod : ISubmod
{
    public string Name => "Mesh Deform";
    public string Tooltip => "Per-part GPU shader deformation — manual dents and bulges via vertex displacement.";

    // UI state
    private bool _active;
    private int _selectedVehicleIndex = -1;
    private readonly List<PartEntry> _partEntries = new();
    private ImGuiTextFilter _partFilter = new();
    private string? _statusMessage;
    private bool _statusIsError;

    // Controls for the currently selected part(s)
    private float _editMagnitude = -0.15f;
    private float _editRadius = 1.0f;
    private bool _applyToAll;

    public void Initialize() { }
    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##md_content");

        bool headerOpen = ImGui.CollapsingHeader("Mesh Deform (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip(
            "Applies per-part radial deformation by displacing vertices in the GPU\n" +
            "vertex shader. Deformation is session-only and purely visual.\n\n" +
            "Positive magnitude = bulge outward. Negative magnitude = dent inward.");
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
        bool prevActive = _active;
        ImGui.Checkbox("Active##md_active", ref _active);
        if (_active && !prevActive)
        {
            if (MeshDeformShaders.Activate())
                SetStatus("Shaders activated.", false);
            else
                SetStatus($"Shader activation failed: {MeshDeformShaders.LastError}", true);
        }
        else if (!_active && prevActive)
        {
            MeshDeformShaders.Deactivate();
            MeshDeformManager.ClearAll();
            SetStatus("Shaders deactivated and deformations cleared.", false);
        }

        if (!_active)
            return;

        ImGui.Spacing();
        RenderControls();
        RenderStatusMessage();
        ImGui.Spacing();
        RenderPartTable();
    }

    public void Dispose()
    {
        MeshDeformManager.Cleanup();
        MeshDeformShaders.Cleanup();
    }

    // ---- Global Controls ----

    private void RenderControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##md_ctrl", 2, flags))
        {
            ImGui.TableSetupColumn("##md_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##md_widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Vehicle selector
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderVehicleCombo();

            // Apply mode
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Mode");
            ImGui.TableNextColumn();
            ImGui.Checkbox("Apply to all parts in vehicle##md", ref _applyToAll);

            // Magnitude
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Magnitude");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##md_mag", ref _editMagnitude, -1.0f, 1.0f, "%.2f m");
            ImGui.SetItemTooltip("Negative = dent inward. Positive = bulge outward.");

            // Radius
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Radius");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("##md_rad", ref _editRadius, 0.01f, 5.0f, "%.2f m");
            ImGui.SetItemTooltip("Sphere of influence in local metres.");

            // Actions
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            if (ImGui.Button(" Apply "))
                DoApply();
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Clear All "))
                DoClearAll();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private void RenderVehicleCombo()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        var names = vehicles.Select(v => v.Id).ToArray();

        if (_selectedVehicleIndex >= names.Length)
            _selectedVehicleIndex = -1;

        string preview = _selectedVehicleIndex >= 0 ? names[_selectedVehicleIndex] : "Select vehicle...";
        if (ImGui.BeginCombo("##md_vehicle", preview))
        {
            for (int i = 0; i < names.Length; i++)
            {
                bool sel = _selectedVehicleIndex == i;
                if (ImGui.Selectable(names[i] + "##md_v", sel))
                    _selectedVehicleIndex = i;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    // ---- Part Table ----

    private void RenderPartTable()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        if (_selectedVehicleIndex < 0 || _selectedVehicleIndex >= vehicles.Count)
        {
            ImGui.TextDisabled("Select a vehicle to list parts.");
            return;
        }

        var vehicle = vehicles[_selectedVehicleIndex];

        // Toolbar
        if (ImGui.Button(" Scan Parts "))
        {
            ScanParts(vehicle);
            SetStatus($"Found {_partEntries.Count} part(s).", false);
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Clear Vehicle "))
        {
            foreach (var p in vehicle.Parts.Parts)
                MeshDeformManager.ClearPart(p);
            foreach (var e in _partEntries) e.HasDeform = false;
            SetStatus("Cleared all deformations for this vehicle.", false);
        }
        ImGui.SameLine(0, 12);
        ImGui.SetNextItemWidth(-1f);
        _partFilter.Draw("##md_filter");

        ImGui.Spacing();

        if (_partEntries.Count == 0)
        {
            ImGui.TextDisabled("No parts scanned. Press Scan Parts to discover.");
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
                       | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                       | ImGuiTableFlags.ScrollY;

        float maxHeight = ImGui.GetTextLineHeightWithSpacing() * 14;
        if (ImGui.BeginTable("##md_parts", 4, tableFlags, new float2(0, maxHeight)))
        {
            ImGui.TableSetupColumn("##chk", ImGuiTableColumnFlags.WidthFixed, 38f);
            ImGui.TableSetupColumn("Part", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Mag", ImGuiTableColumnFlags.WidthFixed, 70f);
            ImGui.TableSetupColumn("Rad", ImGuiTableColumnFlags.WidthFixed, 70f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _partEntries.Count; i++)
            {
                var entry = _partEntries[i];
                if (!_partFilter.PassFilter(entry.Label)) continue;
                ImGui.PushID(i);

                ImGui.TableNextRow();

                // Checkbox / status
                ImGui.TableNextColumn();
                bool hasDeform = entry.HasDeform;
                if (ImGui.Checkbox("##en", ref hasDeform))
                {
                    entry.HasDeform = hasDeform;
                    if (hasDeform)
                        MeshDeformManager.SetDeform(entry.Part, _editMagnitude, _editRadius);
                    else
                        MeshDeformManager.ClearPart(entry.Part);
                }

                // Label
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(entry.Label);

                // Magnitude readout
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (MeshDeformManager.TryGetPayload(entry.Part, out var payload))
                    ImGui.Text($"{payload.Magnitude:F2}");
                else
                    ImGui.TextDisabled("—");

                // Radius readout
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (MeshDeformManager.TryGetPayload(entry.Part, out var payload2))
                    ImGui.Text($"{payload2.Radius:F2}");
                else
                    ImGui.TextDisabled("—");

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    // ---- Actions ----

    private void DoApply()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        if (_selectedVehicleIndex < 0 || _selectedVehicleIndex >= vehicles.Count)
        {
            SetStatus("No vehicle selected.", true);
            return;
        }

        var vehicle = vehicles[_selectedVehicleIndex];

        if (_applyToAll)
        {
            int count = 0;
            foreach (var part in vehicle.Parts.Parts)
            {
                MeshDeformManager.SetDeform(part, _editMagnitude, _editRadius);
                count++;
            }
            ScanParts(vehicle); // refresh table state
            SetStatus($"Applied to {count} part(s).", false);
        }
        else
        {
            int count = 0;
            foreach (var entry in _partEntries)
            {
                if (!entry.HasDeform) continue;
                MeshDeformManager.SetDeform(entry.Part, _editMagnitude, _editRadius);
                count++;
            }
            SetStatus($"Applied to {count} selected part(s).", false);
        }
    }

    private void DoClearAll()
    {
        MeshDeformManager.ClearAll();
        foreach (var e in _partEntries) e.HasDeform = false;
        SetStatus("All deformations cleared.", false);
    }

    private void ScanParts(Vehicle vehicle)
    {
        var existing = new Dictionary<Part, PartEntry>(ReferenceEqualityComparer.Instance);
        foreach (var e in _partEntries)
            existing[e.Part] = e;

        var updated = new List<PartEntry>();
        foreach (var part in vehicle.Parts.Parts)
        {
            string label = $"{part.DisplayName} ({part.Id})";
            if (existing.TryGetValue(part, out var prev))
            {
                prev.Label = label;
                prev.HasDeform = MeshDeformManager.States.ContainsKey(part);
                updated.Add(prev);
            }
            else
            {
                updated.Add(new PartEntry(label, part)
                {
                    HasDeform = MeshDeformManager.States.ContainsKey(part)
                });
            }
        }
        _partEntries.Clear();
        _partEntries.AddRange(updated);
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

    // ---- Entry ----

    private sealed class PartEntry
    {
        public string Label;
        public Part Part;
        public bool HasDeform;

        public PartEntry(string label, Part part)
        {
            Label = label;
            Part = part;
        }
    }
}
