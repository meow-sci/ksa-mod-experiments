using System;
using Brutal.Numerics;
using HarmonyLib;
using KSA;
using MeowSci.FlexoLib.Editor;
using MeowSci.FlexoLib.Runtime;

namespace MeowSci.FlexoLib;

/// <summary>
/// Injects the hinge orbit transform into the render chain for descendant parts.
/// Non-SubPart tree descendants of a hinge's moving part get their
/// MatrixParentAsmb2Ego result replaced with orbitMatrix * vehicleAsmb2Ego.
/// SubParts of those descendants automatically pick up the transform through
/// the recursive PartParent.MatrixAsmb2Ego call.
/// </summary>
[HarmonyPatch(typeof(Part), nameof(Part.MatrixParentAsmb2Ego))]
internal static class PatchMatrixParentAsmb2EgoForHinge
{
    [HarmonyPostfix]
    static void Postfix(Part __instance, ref double4x4 __result)
    {
        // Only intercept non-SubParts — SubParts already recurse through
        // their PartParent's (patched) MatrixAsmb2Ego.
        if (!__instance.IsSubPart
            && HingeRegistry.TryGetOrbitMatrix(__instance, out var orbitMatrix))
        {
            // __result is the original matrixVehicleAsmb2Ego for non-SubParts.
            // Prepend the orbit-around-pivot transform so that:
            //   partLocal × (orbit × vehicleAsmb2Ego)
            // orbits the part's position around the hinge pivot.
            __result = orbitMatrix * __result;
        }
    }
}

[HarmonyPatch(typeof(PartModelRenderer), nameof(PartModelRenderer.UpdateRenderData), new[] { typeof(Viewport), typeof(int) })]
internal static class PatchPartModelRendererForFlexo
{
    [HarmonyPrefix]
    static void Prefix(Viewport viewport, int frameIndex)
    {
        var scene = FlexoEditorScene.Current;
        if (scene == null || !scene.IsActive) return;

        try
        {
            double4x4 matrix = scene.GetMatrixAsmb2Ego(viewport);
            foreach (var part in scene.EditorParts)
            {
                part.Tree.UpdateRenderData(in matrix, isEditedVehicle: false, viewport, frameIndex);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: render patch error: {ex.Message}");
        }

        try
        {
            scene.UpdateGizmo(viewport);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: gizmo update error: {ex.Message}");
        }
    }
}

public static class FlexoPatches
{
    public static void Apply(Harmony harmony)
    {
        try
        {
            harmony.PatchAll(typeof(FlexoPatches).Assembly);
            Console.WriteLine("flexo: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error applying patches: {ex.Message}");
        }
    }

    public static void Remove(Harmony harmony)
    {
        try
        {
            harmony.UnpatchAll(typeof(FlexoPatches).Assembly.GetName().Name);
            Console.WriteLine("flexo: Harmony patches removed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error removing patches: {ex.Message}");
        }
    }
}
