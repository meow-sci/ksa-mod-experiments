using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using KSA;

namespace MeowSci.MeshDeformLib;

/// <summary>
/// Harmony patches that inject deformation data into the GPU rendering pipeline.
///
/// Two patches work together:
/// 1. <see cref="CapturePartPatch"/> — a prefix on <c>PartModelModule.UpdateRenderData</c>
///    that stashes the current <see cref="Part"/> in a static thread-local slot.
/// 2. <see cref="AddInstanceDeformPatch"/> — a prefix on <c>PartModel.AddInstance</c>
///    that reads the stashed Part, looks up its deformation state, and writes the
///    compact payload into the <see cref="PartModel.PerInstanceData"/> padding bytes.
///
/// This mirrors the pattern used by <c>humble-arteest.lib/VehiclePaintPatches.cs</c>.
/// </summary>
public static class MeshDeformPatches
{
    private static MethodInfo? _updateRenderDataOriginal;
    private static MethodInfo? _updateRenderDataPrefix;

    private static MethodInfo? _addInstanceOriginal;
    private static MethodInfo? _addInstancePrefix;

    public static void Apply(Harmony harmony)
    {
        // Patch 1: capture the Part in UpdateRenderData
        _updateRenderDataOriginal = AccessTools.Method(
            typeof(PartModelModule), nameof(PartModelModule.UpdateRenderData));
        _updateRenderDataPrefix = typeof(CapturePartPatch).GetMethod(
            nameof(CapturePartPatch.Prefix),
            BindingFlags.Public | BindingFlags.Static);

        if (_updateRenderDataOriginal != null && _updateRenderDataPrefix != null)
        {
            harmony.Patch(_updateRenderDataOriginal, prefix: new HarmonyMethod(_updateRenderDataPrefix));
            Console.WriteLine("mesh-deform: PartModelModule.UpdateRenderData capture patch applied");
        }
        else
        {
            Console.WriteLine("mesh-deform: WARNING — PartModelModule.UpdateRenderData not found");
        }

        // Patch 2: inject deformation payload in AddInstance
        _addInstanceOriginal = AccessTools.Method(
            typeof(PartModel), nameof(PartModel.AddInstance));
        _addInstancePrefix = typeof(AddInstanceDeformPatch).GetMethod(
            nameof(AddInstanceDeformPatch.Prefix),
            BindingFlags.Public | BindingFlags.Static);

        if (_addInstanceOriginal != null && _addInstancePrefix != null)
        {
            harmony.Patch(_addInstanceOriginal, prefix: new HarmonyMethod(_addInstancePrefix));
            Console.WriteLine("mesh-deform: PartModel.AddInstance deform patch applied");
        }
        else
        {
            Console.WriteLine("mesh-deform: WARNING — PartModel.AddInstance not found");
        }
    }

    public static void Remove(Harmony harmony)
    {
        if (_updateRenderDataOriginal != null && _updateRenderDataPrefix != null)
            harmony.Unpatch(_updateRenderDataOriginal, _updateRenderDataPrefix);

        if (_addInstanceOriginal != null && _addInstancePrefix != null)
            harmony.Unpatch(_addInstanceOriginal, _addInstancePrefix);

        _updateRenderDataOriginal = null;
        _updateRenderDataPrefix = null;
        _addInstanceOriginal = null;
        _addInstancePrefix = null;

        Console.WriteLine("mesh-deform: Harmony patches removed");
    }
}

/// <summary>
/// Harmony prefix on <c>PartModelModule.UpdateRenderData</c>.
/// Stores the <see cref="PartModelModule.Parent"/> (the <see cref="Part"/>) in a
/// thread-local slot so the downstream <c>PartModel.AddInstance</c> prefix knows
/// which Part is being rendered.
/// </summary>
public static class CapturePartPatch
{
    /// <summary>
    /// Thread-local slot holding the Part currently being rendered.
    /// Safe because KSA renders on a single thread.
    /// </summary>
    public static readonly ThreadLocal<Part?> CurrentPart = new();

    public static void Prefix(PartModelModule __instance)
    {
        CurrentPart.Value = __instance.Parent;
    }
}

/// <summary>
/// Harmony prefix on <c>PartModel.AddInstance</c>.
/// Reinterprets the <see cref="PartModel.PerInstanceData"/> padding as
/// <see cref="DeformablePerInstanceData"/> and writes deformation values.
/// </summary>
public static class AddInstanceDeformPatch
{
    public static void Prefix(ref PartModel.PerInstanceData instanceData)
    {
        var part = CapturePartPatch.CurrentPart.Value;
        if (part == null) return;
        if (!MeshDeformManager.TryGetPayload(part, out var payload)) return;

        ref var deformable = ref Unsafe.As<PartModel.PerInstanceData, DeformablePerInstanceData>(
            ref instanceData);

        deformable.DeformMagnitude = payload.Magnitude;
        deformable.DeformRadius = payload.Radius;
    }
}
