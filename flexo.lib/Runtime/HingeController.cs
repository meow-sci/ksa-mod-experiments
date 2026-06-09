using System;
using System.Collections.Generic;
using Brutal.Numerics;
using HarmonyLib;
using KSA;
using MeowSci.FlexoLib.Data;

namespace MeowSci.FlexoLib.Runtime;

public sealed class HingeController
{
    public FlexoPartDefinition Definition { get; }
    public Part FixedPart { get; }
    public Part MovingPart { get; }
    public Vehicle? Vehicle { get; set; }

    private readonly doubleQuat _originalRotation;
    private readonly double3 _pivotPosition;
    private readonly List<DescendantSnapshot> _descendants = new();
    private double _currentDegrees;
    private double _targetDegrees;
    private bool _isAnimating;
    private bool _rotationDirty;

    public double CurrentDegrees => _currentDegrees;
    public double TargetDegrees => _targetDegrees;
    public bool IsAnimating => _isAnimating;

    public HingeController(FlexoPartDefinition definition, Part fixedPart, Part movingPart)
    {
        Definition = definition;
        FixedPart = fixedPart;
        MovingPart = movingPart;
        _originalRotation = movingPart.Asmb2ParentAsmb;
        _pivotPosition = movingPart.PositionParentAsmb;
        _currentDegrees = definition.Hinge?.RestingDegrees ?? 0;
        _targetDegrees = _currentDegrees;

        // Snapshot original transforms of all tree descendants.
        // TreeChildren in KSA have independent positions in vehicle-assembly
        // space — we must update their stored transforms so that BOTH
        // rendering and physics (CoM, thrust vectors, bounding boxes) see
        // the correct hinged positions.  SubParts are NOT collected — they
        // follow their parent automatically via the assembly hierarchy.
        CollectTreeDescendants(movingPart);
    }

    public void SetTarget(double degrees)
    {
        var hinge = Definition.Hinge!;
        _targetDegrees = Math.Clamp(degrees, hinge.MinDegrees, hinge.MaxDegrees);
        _isAnimating = Math.Abs(_targetDegrees - _currentDegrees) > 0.01;
    }

    public void Open() => SetTarget(Definition.Hinge!.MaxDegrees);

    public void Close() => SetTarget(Definition.Hinge!.MinDegrees);

    public void Reset() => SetTarget(Definition.Hinge!.RestingDegrees);

    public void SetImmediate(double degrees)
    {
        var hinge = Definition.Hinge!;
        _currentDegrees = Math.Clamp(degrees, hinge.MinDegrees, hinge.MaxDegrees);
        _targetDegrees = _currentDegrees;
        _isAnimating = false;
        _rotationDirty = true;  // deferred to UpdateBeforeVehicleSolvers
    }

    public void Update(double dt)
    {
        if (_isAnimating)
        {
            var hinge = Definition.Hinge!;
            double speed = hinge.SpeedDegreesPerSecond;
            double delta = speed * dt;

            if (_currentDegrees < _targetDegrees)
                _currentDegrees = Math.Min(_currentDegrees + delta, _targetDegrees);
            else
                _currentDegrees = Math.Max(_currentDegrees - delta, _targetDegrees);

            if (Math.Abs(_currentDegrees - _targetDegrees) < 0.01)
            {
                _currentDegrees = _targetDegrees;
                _isAnimating = false;
            }

            _rotationDirty = true;
        }

        if (!_rotationDirty) return;
        _rotationDirty = false;
        ApplyRotation();
    }

    /// <summary>
    /// Restores original transforms on all descendants.
    /// Call when vehicle is unloaded or the hinge controller is discarded.
    /// </summary>
    public void Dispose()
    {
        MovingPart.Asmb2ParentAsmb = _originalRotation;
        foreach (var snap in _descendants)
        {
            snap.Part.PositionParentAsmb = snap.OriginalPosition;
            snap.Part.Asmb2ParentAsmb = snap.OriginalRotation;
        }
    }

    private void ApplyRotation()
    {
        var hinge = Definition.Hinge!;
        double angleRad = _currentDegrees * Math.PI / 180.0;
        var axis = new double3(hinge.AxisX, hinge.AxisY, hinge.AxisZ);
        var hingeRotation = doubleQuat.CreateFromAxisAngle(axis, angleRad);

        // 1) Rotate the moving part — the hinge axis is defined in the
        //    part's local assembly space, so hinge rotation is applied
        //    first (in local space), then the original orientation
        //    converts to vehicle-assembly space.
        MovingPart.Asmb2ParentAsmb = doubleQuat.Concatenate(hingeRotation, _originalRotation);
        InvalidateSubPartCaches(MovingPart);
        MovingPart.BoundingBoxVehicleAsmb = MovingPart.ComputeBoundingBoxVehicleAsmb();

        // 2) Update stored transforms on tree descendants.
        //    Tree descendants have positions/rotations in vehicle-assembly
        //    space.  The orbit rotation must be applied in vehicle space
        //    (AFTER the part's original local orientation), so we use
        //    Concatenate(origRot, hingeRot) — original first, then hinge.
        //    This keeps all descendants rotating around the same vehicle-
        //    space axis, producing coherent rigid-body motion.
        var rotMatrix = double4x4.CreateFromQuaternion(hingeRotation);
        foreach (var snap in _descendants)
        {
            // Orbit position around the hinge pivot in vehicle-assembly space
            double3 relative = snap.OriginalPosition - _pivotPosition;
            double3 rotated = double3.Transform(relative, rotMatrix);
            snap.Part.PositionParentAsmb = _pivotPosition + rotated;

            // Apply original orientation first, then hinge rotation
            // (both operating in vehicle-assembly space for descendants)
            snap.Part.Asmb2ParentAsmb = doubleQuat.Concatenate(
                snap.OriginalRotation, hingeRotation);

            // SubParts of this descendant have their own cached vehicle-space
            // transforms that are NOT invalidated when the parent's stored
            // values change.  Touch them to force recompute on next access.
            InvalidateSubPartCaches(snap.Part);

            // Recompute cached bounding box from new transforms
            snap.Part.BoundingBoxVehicleAsmb = snap.Part.ComputeBoundingBoxVehicleAsmb();
        }

        // 3) Update vehicle-level physics: bounding box, CoM, aero, etc.
        UpdateVehiclePhysics();
    }

    /// <summary>
    /// SubParts cache _positionVehicleAsmb and _asmb2VehicleAsmb based on
    /// their PartParent's rotation.  These caches are only invalidated by
    /// the SubPart's OWN property setter — not by changing the parent.
    /// We touch them to force cache invalidation so thrust vectors,
    /// connector positions, etc. pick up the parent's new rotation.
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
        if (Vehicle == null) return;

        try
        {
            // Force PartTree to recompute static (inert) mass properties
            // from the updated part positions.  RecomputeStaticMass is
            // private, so we use Traverse to invoke it.
            Traverse.Create(Vehicle.Parts).Method("RecomputeStaticMass").GetValue();

            // UpdateAfterPartTreeModification recomputes bounding box,
            // mass properties (including propellant), aero, and flight
            // computer config from the newly updated part transforms.
            Vehicle.UpdateAfterPartTreeModification();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Vehicle physics update error: {ex.Message}");
        }
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
}
