using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using KSA;

namespace MeowSci.ConManLib;

public sealed class GaugeStateAccessor
{
    private readonly FieldInfo? _canvasesField;
    private readonly FieldInfo? _enabledField;
    private readonly FieldInfo? _offsetField;
    private readonly FieldInfo? _scaleField;
    private readonly FieldInfo? _windowPositionField;
    private readonly FieldInfo? _windowSizeField;
    private readonly FieldInfo? _windowTitleField;

    public bool IsValid { get; }

    public GaugeStateAccessor()
    {
        try
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            _canvasesField = typeof(GaugeCanvas).GetField("_canvases", flags)!;
            _enabledField = typeof(GaugeCanvas).GetField("_enabled", instanceFlags)!;
            _offsetField = typeof(GaugeCanvas).GetField("_customOffset", instanceFlags)!;
            _scaleField = typeof(GaugeCanvas).GetField("_customScale", instanceFlags)!;
            _windowPositionField = typeof(GaugeCanvas).GetField("_windowPosition", instanceFlags);
            _windowSizeField = typeof(GaugeCanvas).GetField("_windowSize", instanceFlags);
            _windowTitleField = typeof(GaugeCanvas).GetField("_windowTitle", instanceFlags);

            IsValid = _canvasesField != null && _enabledField != null
                   && _offsetField != null && _scaleField != null;

            if (!IsValid)
                Console.WriteLine("[con-man] GaugeStateAccessor: One or more fields not found via reflection");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] GaugeStateAccessor: Reflection init failed: {ex.Message}");
            IsValid = false;
            _canvasesField = null;
            _enabledField = null;
            _offsetField = null;
            _scaleField = null;
            _windowPositionField = null;
            _windowSizeField = null;
            _windowTitleField = null;
        }
    }

    public List<GaugeCanvas>? GetCanvases()
    {
        return _canvasesField?.GetValue(null) as List<GaugeCanvas>;
    }

    public bool GetEnabled(GaugeCanvas canvas)
    {
        return (bool)(_enabledField?.GetValue(canvas) ?? true);
    }

    public void SetEnabled(GaugeCanvas canvas, bool value)
    {
        _enabledField?.SetValue(canvas, value);
    }

    public float2 GetCustomOffset(GaugeCanvas canvas)
    {
        return (float2)(_offsetField?.GetValue(canvas) ?? float2.Zero);
    }

    public void SetCustomOffset(GaugeCanvas canvas, float2 value)
    {
        _offsetField?.SetValue(canvas, value);
    }

    public float2 GetCustomScale(GaugeCanvas canvas)
    {
        return (float2)(_scaleField?.GetValue(canvas) ?? new float2(1f, 1f));
    }

    public void SetCustomScale(GaugeCanvas canvas, float2 value)
    {
        _scaleField?.SetValue(canvas, value);
    }

    public float2 GetWindowPosition(GaugeCanvas canvas)
    {
        return (float2)(_windowPositionField?.GetValue(canvas) ?? float2.Zero);
    }

    public float2 GetWindowSize(GaugeCanvas canvas)
    {
        return (float2)(_windowSizeField?.GetValue(canvas) ?? new float2(100f, 100f));
    }

    public string? GetWindowTitle(GaugeCanvas canvas)
    {
        return _windowTitleField?.GetValue(canvas) as string;
    }
}
