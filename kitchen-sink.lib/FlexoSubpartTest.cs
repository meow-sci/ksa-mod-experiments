using System;
using System.Collections.Generic;
using HarmonyLib;
using KSA;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KitchenSinkLib;

/// <summary>
/// Hacky subpart position/rotation test panel.
/// Lets users select a vehicle, part, and subpart to interactively nudge transforms.
/// </summary>
internal sealed class FlexoSubpartTest
{
    private List<Vehicle> _vehicles = new();
    private int _vehicleIdx = -1;

    private List<Part> _parts = new();
    private int _partIdx = -1;

    private List<Part> _subParts = new();
    private int _subPartIdx = -1;

    private double3 _originalPosition;
    private doubleQuat _originalRotation;

    private float _posX, _posY, _posZ;
    private float _rotX, _rotY, _rotZ;

    // Physics update is deferred to a Harmony prefix on
    // Universe.ExecuteNextVehicleSolvers — the only safe phase to call
    // UpdateAfterPartTreeModification() without producing
    // "outdated kinematic states" errors from the solver pipeline.
    private bool _physicsUpdatePending;

    public void UpdateBeforeVehicleSolvers(double dt)
    {
        if (!_physicsUpdatePending) return;
        _physicsUpdatePending = false;
        UpdateVehiclePhysics();
    }

    public void RenderContent()
    {
        ImGui.SeparatorText("Flexo Subpart Test"u8);

        if (ImGui.Button("Refresh##ks_fst_refresh"u8))
            RefreshVehicles();

        ImGui.Spacing();

        // 2-col table: label 1/4, widget 3/4
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ks_fst_sel", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##ks_fst_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ks_fst_wgt", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Vehicle selector
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle"u8);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);

                ImString vehiclePreview = _vehicleIdx >= 0 && _vehicleIdx < _vehicles.Count
                    ? _vehicles[_vehicleIdx].Id
                    : "(none)";
                if (ImGui.BeginCombo("##ks_fst_veh", vehiclePreview))
                {
                    for (int i = 0; i < _vehicles.Count; i++)
                    {
                        bool sel = _vehicleIdx == i;
                        ImString label = _vehicles[i].Id;
                        if (ImGui.Selectable(label, sel))
                            SelectVehicle(i);
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }

            // Part selector (only when a vehicle is chosen)
            if (_vehicleIdx >= 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Part"u8);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);

                ImString partPreview = _partIdx >= 0 && _partIdx < _parts.Count
                    ? (ImString)$"[{_partIdx}] {_parts[_partIdx].Template.Id}"
                    : "(none)";
                if (ImGui.BeginCombo("##ks_fst_part", partPreview))
                {
                    for (int i = 0; i < _parts.Count; i++)
                    {
                        bool sel = _partIdx == i;
                        ImString label = $"[{i}] {_parts[i].Template.Id}";
                        if (ImGui.Selectable(label, sel))
                            SelectPart(i);
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }

            // SubPart selector (only when a part is chosen)
            if (_partIdx >= 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("SubPart"u8);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);

                ImString subPreview = _subPartIdx >= 0 && _subPartIdx < _subParts.Count
                    ? (ImString)$"[{_subPartIdx}] {_subParts[_subPartIdx].Template.Id}"
                    : "(none)";
                if (ImGui.BeginCombo("##ks_fst_sub", subPreview))
                {
                    for (int i = 0; i < _subParts.Count; i++)
                    {
                        bool sel = _subPartIdx == i;
                        ImString label = $"[{i}] {_subParts[i].Template.Id}";
                        if (ImGui.Selectable(label, sel))
                            SelectSubPart(i);
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        // Only show offset controls when a subpart is selected
        if (_subPartIdx < 0 || _subPartIdx >= _subParts.Count)
            return;

        ImGui.Spacing();

        // 4-column equal-width table for position and rotation offsets
        bool anyChanged = false;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ks_fst_xform", 4, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
        {
            // Pos X | Pos Y
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos X"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_px", ref _posX, 0.01f)) anyChanged = true;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos Y"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_py", ref _posY, 0.01f)) anyChanged = true;

            // Pos Z | (empty)
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos Z"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_pz", ref _posZ, 0.01f)) anyChanged = true;
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            // Rot X | Rot Y
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot X"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_rx", ref _rotX, 0.1f)) anyChanged = true;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot Y"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_ry", ref _rotY, 0.1f)) anyChanged = true;

            // Rot Z | (empty)
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot Z"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fst_rz", ref _rotZ, 0.1f)) anyChanged = true;
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        if (anyChanged)
            ApplyTransform();

        ImGui.Spacing();

        if (ImGui.Button("Reset##ks_fst_reset"u8))
            Reset();
        ImGui.SameLine();
        if (ImGui.Button("Update Physics##ks_fst_phys"u8))
            _physicsUpdatePending = true;
    }

    private void RefreshVehicles()
    {
        _vehicles = VehicleProvider.GetAllVehicles();
        _vehicleIdx = -1;
        _parts.Clear();
        _partIdx = -1;
        _subParts.Clear();
        _subPartIdx = -1;
        Console.WriteLine("kitchen-sink: FlexoSubpartTest — vehicles refreshed");
    }

    private void SelectVehicle(int idx)
    {
        _vehicleIdx = idx;
        _partIdx = -1;
        _subParts.Clear();
        _subPartIdx = -1;
        _parts.Clear();

        if (_vehicleIdx < 0 || _vehicleIdx >= _vehicles.Count)
            return;

        var vehicle = _vehicles[_vehicleIdx];
        foreach (var part in vehicle.Parts.Parts)
            _parts.Add(part);

        Console.WriteLine($"kitchen-sink: FlexoSubpartTest — selected vehicle '{vehicle.Id}' with {_parts.Count} root part(s)");
    }

    private void SelectPart(int idx)
    {
        _partIdx = idx;
        _subPartIdx = -1;
        _subParts.Clear();

        if (_partIdx < 0 || _partIdx >= _parts.Count)
            return;

        var part = _parts[_partIdx];
        foreach (var sub in part.SubParts)
            _subParts.Add(sub);

        Console.WriteLine($"kitchen-sink: FlexoSubpartTest — selected part '{part.Template.Id}' with {_subParts.Count} subpart(s)");
    }

    private void SelectSubPart(int idx)
    {
        _subPartIdx = idx;
        if (_subPartIdx < 0 || _subPartIdx >= _subParts.Count)
            return;

        var sub = _subParts[_subPartIdx];
        _originalPosition = sub.PositionParentAsmb;
        _originalRotation = sub.Asmb2ParentAsmb;
        _posX = _posY = _posZ = 0f;
        _rotX = _rotY = _rotZ = 0f;

        Console.WriteLine($"kitchen-sink: FlexoSubpartTest — selected subpart '{sub.Template.Id}' original pos={_originalPosition}");
    }

    private void ApplyTransform()
    {
        if (_subPartIdx < 0 || _subPartIdx >= _subParts.Count)
            return;

        var sub = _subParts[_subPartIdx];
        sub.PositionParentAsmb = _originalPosition + new double3(_posX, _posY, _posZ);

        var rotX = doubleQuat.CreateFromAxisAngle(new double3(1, 0, 0), _rotX * Math.PI / 180.0);
        var rotY = doubleQuat.CreateFromAxisAngle(new double3(0, 1, 0), _rotY * Math.PI / 180.0);
        var rotZ = doubleQuat.CreateFromAxisAngle(new double3(0, 0, 1), _rotZ * Math.PI / 180.0);
        sub.Asmb2ParentAsmb = doubleQuat.Concatenate(
            doubleQuat.Concatenate(
                doubleQuat.Concatenate(_originalRotation, rotX),
                rotY),
            rotZ);
    }

    private void Reset()
    {
        if (_subPartIdx < 0 || _subPartIdx >= _subParts.Count)
            return;

        var sub = _subParts[_subPartIdx];
        sub.PositionParentAsmb = _originalPosition;
        sub.Asmb2ParentAsmb = _originalRotation;
        _posX = _posY = _posZ = 0f;
        _rotX = _rotY = _rotZ = 0f;
        Console.WriteLine("kitchen-sink: FlexoSubpartTest — reset subpart transform");
    }

    private void UpdateVehiclePhysics()
    {
        if (_vehicleIdx < 0 || _vehicleIdx >= _vehicles.Count)
            return;

        var vehicle = _vehicles[_vehicleIdx];
        try
        {
            Traverse.Create(vehicle.Parts).Method("RecomputeStaticMass").GetValue();
            vehicle.UpdateAfterPartTreeModification();
            Console.WriteLine("kitchen-sink: FlexoSubpartTest — vehicle physics updated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitchen-sink: FlexoSubpartTest — physics update error: {ex.Message}");
        }
    }
}
