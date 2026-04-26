using System;
using HarmonyLib;
using KSA;
using MeowSci.KitchenSinkLib;

namespace MeowSci.KitchenSink;

/// <summary>
/// Catches PartModel instances created after the IVA force-render toggle is enabled.
/// Mutates Template.Internal = false on new internal parts so they render outside IVA mode.
/// </summary>
[HarmonyPatch(typeof(PartModel), MethodType.Constructor,
    new[] { typeof(PartModelModule.Template) })]
internal static class IvaNewPartModelPatch
{
    static void Postfix(PartModel __instance)
    {
        if (!IvaForceRender.Enabled) return;
        if (!__instance.Template.Internal) return;

        __instance.Template.Internal = false;
        IvaForceRender.TrackMutated(__instance.Template);
    }
}

/// <summary>
/// Keeps internal meshes visible while the vehicle editor is active.
/// KSA's stock PartModel.AddInstance gate hides Internal meshes unless the main viewport is in IVA mode,
/// but editor previews are never rendered through an IVA camera.
/// </summary>
[HarmonyPatch(typeof(PartModel), nameof(PartModel.AddInstance))]
internal static class IvaEditorPartModelPatch
{
    static void Postfix(PartModel __instance, PartModel.PerInstanceData __0, Viewport __1)
    {
        if (Program.Editor == null) return;
        if (!__instance.Template.Internal) return;
        if (Program.MainViewport.Mode == CameraMode.IVA) return;
        if (__instance.Template.RayTracing == PartModelModule.RaytracingMode.ShadowProxy) return;

        PartModel.ViewportData.Get(__instance, __1).InstanceList.Add(__0);
    }
}
