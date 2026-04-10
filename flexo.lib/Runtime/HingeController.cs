using System;
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
        _currentDegrees = definition.Hinge?.RestingDegrees ?? 0;
        _targetDegrees = _currentDegrees;
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

    private void ApplyRotation()
    {
        var hinge = Definition.Hinge!;
        double angleRad = _currentDegrees * Math.PI / 180.0;
        var axis = new double3(hinge.AxisX, hinge.AxisY, hinge.AxisZ);
        var hingeRotation = doubleQuat.CreateFromAxisAngle(axis, angleRad);

        // Concatenate hinge rotation with the part's original rotation
        MovingPart.Asmb2ParentAsmb = doubleQuat.Concatenate(hingeRotation, _originalRotation);
    }
}
