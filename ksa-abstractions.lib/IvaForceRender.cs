using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Forces IVA (interior) parts to render even when not in IVA camera mode
/// by directly mutating Template.Internal on all loaded PartModel instances.
/// </summary>
public static class IvaForceRender
{
    private static bool _enabled;
    private static readonly List<PartModelModule.Template> _mutatedTemplates = new();

    private static MethodBase? _ctorOriginal;
    private static MethodInfo? _ctorPostfix;
    private static MethodBase? _addInstanceOriginal;
    private static MethodInfo? _addInstancePostfix;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
                ForceInternalVisible();
            else
                RestoreInternalHidden();
        }
    }

    /// <summary>
    /// Apply IVA force-render Harmony patches. Call from Patcher.Patch().
    /// </summary>
    public static void Patch(Harmony harmony)
    {
        _ctorOriginal = AccessTools.Constructor(typeof(PartModel), new[] { typeof(PartModelModule.Template) });
        _ctorPostfix = typeof(IvaForceRender).GetMethod(nameof(CtorPostfix), BindingFlags.NonPublic | BindingFlags.Static)!;
        harmony.Patch(_ctorOriginal, postfix: new HarmonyMethod(_ctorPostfix));

        _addInstanceOriginal = AccessTools.Method(typeof(PartModel), nameof(PartModel.AddInstance));
        _addInstancePostfix = typeof(IvaForceRender).GetMethod(nameof(AddInstancePostfix), BindingFlags.NonPublic | BindingFlags.Static)!;
        harmony.Patch(_addInstanceOriginal, postfix: new HarmonyMethod(_addInstancePostfix));

        Console.WriteLine("ksa-abstractions: IvaForceRender patches applied");
    }

    /// <summary>
    /// Remove IVA force-render Harmony patches. Call from Patcher.Unload().
    /// </summary>
    public static void Unpatch(Harmony harmony)
    {
        if (_ctorOriginal != null && _ctorPostfix != null)
            harmony.Unpatch(_ctorOriginal, _ctorPostfix);
        if (_addInstanceOriginal != null && _addInstancePostfix != null)
            harmony.Unpatch(_addInstanceOriginal, _addInstancePostfix);

        _ctorOriginal = null;
        _ctorPostfix = null;
        _addInstanceOriginal = null;
        _addInstancePostfix = null;

        Console.WriteLine("ksa-abstractions: IvaForceRender patches removed");
    }

    /// <summary>
    /// Called by the constructor patch to handle parts created after the toggle is enabled.
    /// </summary>
    public static void TrackMutated(PartModelModule.Template template)
    {
        if (!_mutatedTemplates.Contains(template))
            _mutatedTemplates.Add(template);
    }

    /// <summary>
    /// Postfix for PartModel constructor — mutates Template.Internal = false on new internal parts
    /// so they render outside IVA mode when the toggle is active.
    /// </summary>
    private static void CtorPostfix(PartModel __instance)
    {
        if (!_enabled) return;
        if (!__instance.Template.Internal) return;

        __instance.Template.Internal = false;
        TrackMutated(__instance.Template);
    }

    /// <summary>
    /// Postfix for PartModel.AddInstance — keeps internal meshes visible in the vehicle editor.
    /// KSA's stock gate hides Internal meshes unless the main viewport is in IVA mode,
    /// but editor previews are never rendered through an IVA camera.
    /// </summary>
    private static void AddInstancePostfix(PartModel __instance, PartModel.PerInstanceData __0, Viewport __1)
    {
        if (Program.Editor == null) return;
        if (!__instance.Template.Internal) return;
        if (Program.MainViewport.Mode == CameraMode.IVA) return;
        if (__instance.Template.RayTracing == PartModelModule.RaytracingMode.ShadowProxy) return;

        PartModel.ViewportData.Get(__instance, __1).InstanceList.Add(__0);
    }

    private static void ForceInternalVisible()
    {
        _mutatedTemplates.Clear();
        foreach (var pm in PartModel.Instances)
        {
            if (pm.Template.Internal)
            {
                _mutatedTemplates.Add(pm.Template);
                pm.Template.Internal = false;
            }
        }
        Console.WriteLine($"ksa-abstractions: IvaForceRender forced {_mutatedTemplates.Count} internal templates visible");
    }

    private static void RestoreInternalHidden()
    {
        foreach (var t in _mutatedTemplates)
            t.Internal = true;
        Console.WriteLine($"ksa-abstractions: IvaForceRender restored {_mutatedTemplates.Count} internal templates");
        _mutatedTemplates.Clear();
    }
}
