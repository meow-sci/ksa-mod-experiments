using System;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace MeowSci.SpaceTapeLib;

[HarmonyPatch(typeof(PartModelRenderer), nameof(PartModelRenderer.UpdateRenderData), new[] { typeof(Viewport), typeof(int) })]
internal static class PartModelRendererPatch
{
    [HarmonyPrefix]
    static void Prefix(Viewport viewport, int frameIndex)
    {
        var scene = PartEditorScene.Current;
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
            Console.WriteLine($"space-tape: render patch error: {ex.Message}");
        }

        // Update transform gizmo segment data so the engine's GizmoPass picks them up
        try
        {
            SpaceTapeSubmod.Current?.UpdateScene(viewport);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: gizmo update error: {ex.Message}");
        }
    }
}

internal static class PartRenderHelper
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        _harmony = new Harmony("MeowSci.SpaceTape.PartRender");
        _harmony.CreateClassProcessor(typeof(PartModelRendererPatch)).Patch();
    }

    public static void Unpatch()
    {
        if (_harmony != null)
        {
            _harmony.UnpatchAll(_harmony.Id);
            _harmony = null;
        }
    }
}
