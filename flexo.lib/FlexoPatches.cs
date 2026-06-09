using System;
using Brutal.Numerics;
using HarmonyLib;
using KSA;
using MeowSci.FlexoLib.Editor;

namespace MeowSci.FlexoLib;

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
            FlexoSolverPatch.Apply(harmony);
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

internal static class FlexoSolverPatch
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefix = new HarmonyMethod(typeof(FlexoSolverPatch), nameof(BeforeVehicleSolvers))
        {
            priority = Priority.First
        };
        harmony.Patch(original, prefix: prefix);
    }

    private static void BeforeVehicleSolvers(double dtPlayer)
    {
        try
        {
            FlexoSubmod.Current?.UpdateBeforeVehicleSolvers(dtPlayer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"flexo: Error in solver prefix: {ex.Message}");
        }
    }
}
