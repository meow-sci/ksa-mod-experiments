using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using MeowSci.FlexoLib.Data;

namespace MeowSci.FlexoLib.Runtime;

public sealed class HingeController
{
    public FlexoPartDefinition Definition { get; }
    public Part FixedPart { get; }
    public Part MovingPart { get; }

    private readonly doubleQuat _originalRotation;
    private readonly double3 _pivotPosition;
    private readonly List<DescendantSnapshot> _descendants = new();
    private double _currentDegrees;
    private double _targetDegrees;
    private bool _isAnimating;

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
        ApplyRotation();
    }

    public void Update(double dt)
    {
        if (!_isAnimating) return;

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
        //    SubParts follow automatically via assembly-hierarchy recursion.
        MovingPart.Asmb2ParentAsmb = doubleQuat.Concatenate(hingeRotation, _originalRotation);
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

            // Recompute cached bounding box from new transforms
            snap.Part.BoundingBoxVehicleAsmb = snap.Part.ComputeBoundingBoxVehicleAsmb();
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
