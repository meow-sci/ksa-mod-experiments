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
