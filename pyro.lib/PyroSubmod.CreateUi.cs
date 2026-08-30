using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.PyroLib;

/// <summary>"Create Plume" form: vehicle → part → sub-part → template, plus offsets.</summary>
public sealed partial class PyroSubmod
{
    private int _pendingVehicleIndex = -1;
    private int _pendingPartIndex = -1;
    private int _pendingSubPartIndex = 0; // 0 = "(part itself)"
    private int _pendingTemplateIndex = 0;
    private float3 _pendingPosition;
    private float3 _pendingRotation;
    private string? _createError;

    private readonly ImInputString _vehicleFilter = new(128);
    private readonly ImInputString _partFilter = new(128);
    private readonly ImInputString _subPartFilter = new(128);
    private readonly ImInputString _templateFilter = new(128);

    private Vehicle? _partsVehicle;
    private readonly List<Part> _topParts = new();
    private string[] _topPartLabels = Array.Empty<string>();
    private Part? _subPartsOwner;
    private readonly List<Part> _subParts = new();
    private string[] _subPartLabels = Array.Empty<string>();

    private void RenderCreateSection()
    {
        bool open = ImGui.CollapsingHeader("Create Plume (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Weld a standalone engine plume to a vehicle part.\nThe plume fires along the part's -X axis (the same convention the\ngame's engines use); use the rotation offset to aim it.");
        if (!open) return;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
        {
            ImGui.TextDisabled("No vehicles available.");
            return;
        }

        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++) vehicleIds[i] = vehicles[i].Id;
        if (_pendingVehicleIndex >= vehicles.Count) _pendingVehicleIndex = -1;

        RefreshPartLists(_pendingVehicleIndex >= 0 ? vehicles[_pendingVehicleIndex] : null);

        var templateIds = PlumeTemplates.GetTemplateIds();
        if (_pendingTemplateIndex >= templateIds.Length) _pendingTemplateIndex = templateIds.Length > 0 ? 0 : -1;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##pyro_form", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            FormRow("Vehicle");
            PyroUi.FilteredCombo("##pyro_vehicle", vehicleIds, ref _pendingVehicleIndex, _vehicleFilter);

            FormRow("Part");
            bool noVehicle = _pendingVehicleIndex < 0 || _topParts.Count == 0;
            if (noVehicle) ImGui.BeginDisabled();
            PyroUi.FilteredCombo("##pyro_part", _topPartLabels, ref _pendingPartIndex, _partFilter);
            if (noVehicle) ImGui.EndDisabled();

            FormRow("Sub-part");
            bool noPart = _pendingPartIndex < 0;
            if (noPart) ImGui.BeginDisabled();
            PyroUi.FilteredCombo("##pyro_subpart", _subPartLabels, ref _pendingSubPartIndex, _subPartFilter);
            if (noPart) ImGui.EndDisabled();

            FormRow("Template");
            PyroUi.FilteredCombo("##pyro_template", templateIds, ref _pendingTemplateIndex, _templateFilter);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        PyroUi.OffsetFields("##pyro_create", ref _pendingPosition, ref _pendingRotation);

        ImGui.Spacing();
        bool canCreate = _pendingVehicleIndex >= 0 && _pendingPartIndex >= 0 && _pendingTemplateIndex >= 0;
        if (!canCreate) ImGui.BeginDisabled();
        if (ImGui.Button(" Create Plume ##pyro_create_btn"))
        {
            var anchor = _pendingSubPartIndex > 0 && _pendingSubPartIndex - 1 < _subParts.Count
                ? _subParts[_pendingSubPartIndex - 1]
                : _topParts[_pendingPartIndex];
            var (_, error) = CreatePlume(vehicles[_pendingVehicleIndex], anchor, templateIds[_pendingTemplateIndex],
                _pendingPosition, _pendingRotation);
            _createError = error;
        }
        if (!canCreate) ImGui.EndDisabled();

        if (_pendingVehicleIndex >= 0 && _topParts.Count > 0 && _pendingPartIndex < 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), "Select a part to anchor the plume.");
        }
        if (!string.IsNullOrEmpty(_createError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _createError);
        }
    }

    private static void FormRow(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text(label);
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
    }

    /// <summary>Rebuilds the top-level part and sub-part lists when the selected vehicle/part changes.</summary>
    private void RefreshPartLists(Vehicle? vehicle)
    {
        if (!ReferenceEquals(vehicle, _partsVehicle))
        {
            _partsVehicle = vehicle;
            _topParts.Clear();
            _pendingPartIndex = -1;
            if (vehicle != null)
                foreach (var p in vehicle.Parts.Parts) _topParts.Add(p);
            _topPartLabels = new string[_topParts.Count];
            for (int i = 0; i < _topParts.Count; i++) _topPartLabels[i] = PyroUi.PartLabel(_topParts[i]);
        }
        if (_pendingPartIndex >= _topParts.Count) _pendingPartIndex = -1;

        var owner = _pendingPartIndex >= 0 ? _topParts[_pendingPartIndex] : null;
        if (!ReferenceEquals(owner, _subPartsOwner))
        {
            _subPartsOwner = owner;
            _subParts.Clear();
            _pendingSubPartIndex = 0;
            if (owner != null)
                foreach (var sp in owner.SubParts) _subParts.Add(sp);
            _subPartLabels = new string[_subParts.Count + 1];
            _subPartLabels[0] = "(part itself)";
            for (int i = 0; i < _subParts.Count; i++) _subPartLabels[i + 1] = PyroUi.PartLabel(_subParts[i]);
        }
        if (_pendingSubPartIndex > _subParts.Count) _pendingSubPartIndex = 0;
    }
}
