using System;
using System.Reflection;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Harmony patches against <see cref="VehicleEditor"/> that (a) lift the 0.5x–2.0x part-scale clamp
/// and (b) restore per-axis scaling on the scale gizmo. Behavior is gated at runtime by
/// <see cref="EditorScaleSettings"/>.
/// </summary>
public static class EditorScalePatches
{
    private const string ScaleBoundsForName = "ScaleBoundsFor";
    private const string UpdateSelectedScaleName = "UpdateSelectedScale";
    private const string UpdateScaleGizmoName = "UpdateScaleGizmo";
    private const string QuantizeScaleName = "QuantizeScale";
    private const string ForEachPartWithSymmetryName = "ForEachPartWithSymmetry";

    private const double MinimumPositiveScale = 1e-6;

    private static MethodInfo? _scaleBoundsFor;
    private static MethodInfo? _updateSelectedScale;
    private static MethodInfo? _updateScaleGizmo;

    // Private static game helpers reused so per-axis drags keep the stock 0.25 m diameter snapping
    // and symmetry propagation.
    private static Func<Part, double, double>? _quantizeScale;
    private static Action<Part, Action<Part>>? _forEachPartWithSymmetry;

    public static bool IsApplied { get; private set; }

    public static void Apply(Harmony harmony)
    {
        if (IsApplied) return;

        _scaleBoundsFor = AccessTools.Method(typeof(VehicleEditor), ScaleBoundsForName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), ScaleBoundsForName);
        _updateSelectedScale = AccessTools.Method(typeof(VehicleEditor), UpdateSelectedScaleName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), UpdateSelectedScaleName);
        _updateScaleGizmo = AccessTools.Method(typeof(VehicleEditor), UpdateScaleGizmoName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), UpdateScaleGizmoName);

        var quantize = AccessTools.Method(typeof(VehicleEditor), QuantizeScaleName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), QuantizeScaleName);
        var forEach = AccessTools.Method(typeof(VehicleEditor), ForEachPartWithSymmetryName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), ForEachPartWithSymmetryName);
        _quantizeScale = AccessTools.MethodDelegate<Func<Part, double, double>>(quantize);
        _forEachPartWithSymmetry = AccessTools.MethodDelegate<Action<Part, Action<Part>>>(forEach);

        harmony.Patch(_scaleBoundsFor,
            postfix: new HarmonyMethod(typeof(EditorScalePatches), nameof(ScaleBoundsForPostfix)));
        harmony.Patch(_updateSelectedScale,
            prefix: new HarmonyMethod(typeof(EditorScalePatches), nameof(UpdateSelectedScalePrefix)));
        harmony.Patch(_updateScaleGizmo,
            postfix: new HarmonyMethod(typeof(EditorScalePatches), nameof(UpdateScaleGizmoPostfix)));

        IsApplied = true;
        Console.WriteLine("dont-stifle-me: editor scale patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        if (!IsApplied) return;
        if (_scaleBoundsFor != null) harmony.Unpatch(_scaleBoundsFor, HarmonyPatchType.Postfix, harmony.Id);
        if (_updateSelectedScale != null) harmony.Unpatch(_updateSelectedScale, HarmonyPatchType.Prefix, harmony.Id);
        if (_updateScaleGizmo != null) harmony.Unpatch(_updateScaleGizmo, HarmonyPatchType.Postfix, harmony.Id);
        _scaleBoundsFor = _updateSelectedScale = _updateScaleGizmo = null;
        _quantizeScale = null;
        _forEachPartWithSymmetry = null;
        IsApplied = false;
    }

    // ---- clamp removal ----

    /// <summary>
    /// <c>VehicleEditor.ScaleBoundsFor(Part)</c> returns (0.5, 2.0) for top-level parts. Both the
    /// drag accumulator and <c>QuantizeScale</c> consult it, so widening it here lifts the clamp everywhere.
    /// </summary>
    private static void ScaleBoundsForPostfix(ref (double Min, double Max) __result)
    {
        if (!EditorScaleSettings.ClampRemovalActive) return;
        __result = (MinimumPositiveScale, double.PositiveInfinity);
    }

    // ---- per-axis scaling ----

    private static void UpdateScaleGizmoPostfix(VehicleEditor __instance)
    {
        // Runs every editor frame; a released gizmo ends the current drag session so the next
        // grab re-seeds its raw value from the part's actual scale.
        if (!__instance.GizmoGrabbed) PerAxisScaleDrag.End();
    }

    /// <summary>
    /// Replaces <c>VehicleEditor.UpdateSelectedScale</c> when per-axis scaling is active. Mirrors the
    /// stock cursor-delta → scale-delta math but applies it to the dragged axis only.
    /// </summary>
    private static bool UpdateSelectedScalePrefix(VehicleEditor __instance, ref double4x4 matrixVehicleAsmb2Ego, Viewport inViewport)
    {
        if (!EditorScaleSettings.PerAxisScalingActive) return true;
        if (_quantizeScale == null || _forEachPartWithSymmetry == null) return true;

        try
        {
            PerAxisScaleDrag.Step(__instance, in matrixVehicleAsmb2Ego, inViewport, _quantizeScale, _forEachPartWithSymmetry);
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"dont-stifle-me: per-axis scale drag failed, falling back to stock: {ex.Message}");
            return true;
        }
    }
}
