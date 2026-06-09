using System;
using System.Collections.Generic;
using HarmonyLib;
using KSA;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KitchenSinkLib;

/// <summary>
/// Part position/rotation test panel.
/// Lets users select a vehicle and part to interactively nudge transforms.
///
/// Unlike SubParts, KSA Part tree-children have independent positions in
/// vehicle-assembly space — they do NOT follow their parent automatically.
/// We snapshot all TreeChildren descendants and propagate every transform
/// manually, matching the strategy used by flexo's HingeController.
/// </summary>
internal sealed class FlexoPartTest
{
    private List<Vehicle> _vehicles = new();
    private int _vehicleIdx = -1;

    private List<Part> _parts = new();
    private int _partIdx = -1;

    private double3 _originalPosition;
    private doubleQuat _originalRotation;

    private float _posX, _posY, _posZ;
    private float _rotX, _rotY, _rotZ;

    private bool _physicsUpdatePending;

    // Snapshots of all TreeChildren descendants taken at part selection time.
    // Required because tree-children positions are in vehicle-assembly space
    // and must be updated explicitly when the selected part moves.
    private readonly List<DescendantSnapshot> _descendants = new();

    private sealed class DescendantSnapshot
    {
        public readonly Part Part;
        public readonly double3 OriginalPosition;
        public readonly doubleQuat OriginalRotation;

        public DescendantSnapshot(Part part, double3 position, doubleQuat rotation)
        {
            Part = part;
            OriginalPosition = position;
            OriginalRotation = rotation;
        }
    }

    public void UpdateBeforeVehicleSolvers(double dt)
    {
        if (!_physicsUpdatePending) return;
        _physicsUpdatePending = false;
        UpdateVehiclePhysics();
    }

    public void RenderContent()
    {
        ImGui.SeparatorText("Flexo Part Test"u8);

        if (ImGui.Button("Refresh##ks_fpt_refresh"u8))
            RefreshVehicles();

        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ks_fpt_sel", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##ks_fpt_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##ks_fpt_wgt", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Vehicle selector
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle"u8);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);

                ImString vehiclePreview = _vehicleIdx >= 0 && _vehicleIdx < _vehicles.Count
                    ? _vehicles[_vehicleIdx].Id
                    : "(none)";
                if (ImGui.BeginCombo("##ks_fpt_veh", vehiclePreview))
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
                if (ImGui.BeginCombo("##ks_fpt_part", partPreview))
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

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        // Only show offset controls when a part is selected
        if (_partIdx < 0 || _partIdx >= _parts.Count)
            return;

        ImGui.Spacing();

        bool anyChanged = false;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##ks_fpt_xform", 4, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX))
        {
            // Pos X | Pos Y
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos X"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_px", ref _posX, 0.01f)) anyChanged = true;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos Y"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_py", ref _posY, 0.01f)) anyChanged = true;

            // Pos Z | (empty)
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Pos Z"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_pz", ref _posZ, 0.01f)) anyChanged = true;
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            // Rot X | Rot Y
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot X"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_rx", ref _rotX, 0.1f)) anyChanged = true;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot Y"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_ry", ref _rotY, 0.1f)) anyChanged = true;

            // Rot Z | (empty)
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Rot Z"u8);
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); if (ImGui.DragFloat("##ks_fpt_rz", ref _rotZ, 0.1f)) anyChanged = true;
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        if (anyChanged)
            ApplyTransform();

        ImGui.Spacing();

        if (ImGui.Button("Reset##ks_fpt_reset"u8))
            Reset();
        ImGui.SameLine();
        if (ImGui.Button("Update Physics##ks_fpt_phys"u8))
            _physicsUpdatePending = true;
    }

    private void RefreshVehicles()
    {
        _vehicles = VehicleProvider.GetAllVehicles();
        _vehicleIdx = -1;
        _parts.Clear();
        _partIdx = -1;
        Console.WriteLine("kitchen-sink: FlexoPartTest — vehicles refreshed");
    }

    private void SelectVehicle(int idx)
    {
        _vehicleIdx = idx;
        _partIdx = -1;
        _parts.Clear();

        if (_vehicleIdx < 0 || _vehicleIdx >= _vehicles.Count)
            return;

        var vehicle = _vehicles[_vehicleIdx];
        foreach (var part in vehicle.Parts.Parts)
            _parts.Add(part);

        Console.WriteLine($"kitchen-sink: FlexoPartTest — selected vehicle '{vehicle.Id}' with {_parts.Count} part(s)");
    }

    private void SelectPart(int idx)
    {
        _partIdx = idx;
        _descendants.Clear();

        if (_partIdx < 0 || _partIdx >= _parts.Count)
            return;

        var part = _parts[_partIdx];
        _originalPosition = part.PositionParentAsmb;
        _originalRotation = part.Asmb2ParentAsmb;
        _posX = _posY = _posZ = 0f;
        _rotX = _rotY = _rotZ = 0f;

        CollectTreeDescendants(part);
        Console.WriteLine($"kitchen-sink: FlexoPartTest — selected part '{part.Template.Id}' original pos={_originalPosition}, {_descendants.Count} tree descendant(s)");
    }

    private void CollectTreeDescendants(Part parent)
    {
        foreach (var child in parent.TreeChildren)
        {
            _descendants.Add(new DescendantSnapshot(
                child, child.PositionParentAsmb, child.Asmb2ParentAsmb));
            CollectTreeDescendants(child);
        }
    }

    private void ApplyTransform()
    {
        if (_partIdx < 0 || _partIdx >= _parts.Count)
            return;

        var part = _parts[_partIdx];
        var posOffset = new double3(_posX, _posY, _posZ);

        // Build the incremental rotation delta (axes in vehicle-assembly space)
        var rotX = doubleQuat.CreateFromAxisAngle(new double3(1, 0, 0), _rotX * Math.PI / 180.0);
        var rotY = doubleQuat.CreateFromAxisAngle(new double3(0, 1, 0), _rotY * Math.PI / 180.0);
        var rotZ = doubleQuat.CreateFromAxisAngle(new double3(0, 0, 1), _rotZ * Math.PI / 180.0);
        var deltaRot = doubleQuat.Concatenate(doubleQuat.Concatenate(rotX, rotY), rotZ);

        // 1) Apply to the selected part itself
        part.PositionParentAsmb = _originalPosition + posOffset;
        part.Asmb2ParentAsmb = doubleQuat.Concatenate(_originalRotation, deltaRot);
        InvalidateSubPartCaches(part);
        part.BoundingBoxVehicleAsmb = part.ComputeBoundingBoxVehicleAsmb();

        // 2) Propagate to tree descendants.
        //    Descendants have independent positions in vehicle-assembly space;
        //    orbit each one around the selected part's pivot, then translate.
        var rotMatrix = double4x4.CreateFromQuaternion(deltaRot);
        foreach (var snap in _descendants)
        {
            double3 relative = snap.OriginalPosition - _originalPosition;
            double3 rotated = double3.Transform(relative, rotMatrix);
            snap.Part.PositionParentAsmb = _originalPosition + posOffset + rotated;
            snap.Part.Asmb2ParentAsmb = doubleQuat.Concatenate(snap.OriginalRotation, deltaRot);
            InvalidateSubPartCaches(snap.Part);
            snap.Part.BoundingBoxVehicleAsmb = snap.Part.ComputeBoundingBoxVehicleAsmb();
        }
    }

    private void Reset()
    {
        if (_partIdx < 0 || _partIdx >= _parts.Count)
            return;

        var part = _parts[_partIdx];
        part.PositionParentAsmb = _originalPosition;
        part.Asmb2ParentAsmb = _originalRotation;
        InvalidateSubPartCaches(part);
        part.BoundingBoxVehicleAsmb = part.ComputeBoundingBoxVehicleAsmb();

        foreach (var snap in _descendants)
        {
            snap.Part.PositionParentAsmb = snap.OriginalPosition;
            snap.Part.Asmb2ParentAsmb = snap.OriginalRotation;
            InvalidateSubPartCaches(snap.Part);
            snap.Part.BoundingBoxVehicleAsmb = snap.Part.ComputeBoundingBoxVehicleAsmb();
        }

        _posX = _posY = _posZ = 0f;
        _rotX = _rotY = _rotZ = 0f;
        Console.WriteLine("kitchen-sink: FlexoPartTest — reset part transform");
    }

    /// <summary>
    /// SubParts cache vehicle-space transforms based on their parent's rotation.
    /// These caches are only invalidated by the SubPart's own property setter —
    /// not by changing the parent.  Touching them forces cache invalidation so
    /// thrust vectors, connector positions, etc. pick up the new parent rotation.
    /// </summary>
    private static void InvalidateSubPartCaches(Part part)
    {
        foreach (var sub in part.SubParts)
        {
            sub.PositionParentAsmb = sub.PositionParentAsmb;
            sub.Asmb2ParentAsmb = sub.Asmb2ParentAsmb;
            sub.BoundingBoxVehicleAsmb = sub.ComputeBoundingBoxVehicleAsmb();
            InvalidateSubPartCaches(sub);
        }
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
            Console.WriteLine("kitchen-sink: FlexoPartTest — vehicle physics updated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitchen-sink: FlexoPartTest — physics update error: {ex.Message}");
        }
    }
}
