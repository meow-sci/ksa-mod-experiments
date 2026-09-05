using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorchLib;

/// <summary>Original runtime scales, owned by one weld. Factors never compound.</summary>
internal sealed class WeldScaleState
{
    private readonly Dictionary<Part, double3> _parts = new();
    private readonly Vehicle _vehicle;
    private object? _avatar;
    private FieldInfo? _coreField;
    private FieldInfo? _scaleField;
    private PropertyInfo? _scaleProperty;
    private float _avatarScale;

    public WeldScaleState(Vehicle vehicle)
    {
        _vehicle = vehicle;
        CaptureParts();
        if (vehicle is not KittenEva) return;
        var renderable = ReflectionHelpers.GetFieldValue(vehicle, "_renderable");
        _avatar = ReflectionHelpers.GetFieldValue(renderable, "_characterAvatar");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        _coreField = _avatar?.GetType().GetField("Core", flags);
        var core = _coreField?.GetValue(_avatar);
        _scaleField = core?.GetType().GetField("Scale", flags);
        _scaleProperty = core?.GetType().GetProperty("Scale", flags);
        if ((_scaleField?.GetValue(core) ?? _scaleProperty?.GetValue(core)) is float scale)
            _avatarScale = scale;
        else if (_avatar != null)
            throw new MissingMemberException("Kitten CharacterAvatar.Core.Scale is unavailable");
    }

    private void CaptureParts()
    {
        void Capture(Part part)
        {
            _parts.TryAdd(part, part.Scale);
            foreach (var child in part.SubParts) Capture(child);
        }
        foreach (var part in _vehicle.Parts.Parts) Capture(part);
    }

    public void Apply(float factor)
    {
        CaptureParts();
        foreach (var (part, original) in _parts) part.Scale = original * (double)factor;
        if (_avatar == null || _coreField == null) return;
        var core = _coreField.GetValue(_avatar)!;
        if (_scaleField != null) _scaleField.SetValue(core, _avatarScale * factor);
        else _scaleProperty!.SetValue(core, _avatarScale * factor);
        _coreField.SetValue(_avatar, core);
    }

    public void Restore() => Apply(1f);
}
