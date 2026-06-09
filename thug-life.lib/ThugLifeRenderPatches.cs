using System;
using Brutal.VulkanApi;
using HarmonyLib;
using KSA;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// Harmony postfix that injects per-frame thug-life quad draws into KSA's offscreen
/// main pass. Shared between the standalone thug-life mod and the unscience supermod
/// — each host calls <see cref="Apply"/> / <see cref="Remove"/> on its own Harmony
/// instance and the postfix dispatches to <see cref="ThugLifeRenderManager.Instance"/>
/// on the calling assembly's load context.
/// </summary>
public static class ThugLifeRenderPatches
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(
            typeof(SuperMeshRenderSystem),
            nameof(SuperMeshRenderSystem.RenderMainPass));
        if (original == null)
            throw new MissingMethodException(
                typeof(SuperMeshRenderSystem).FullName,
                nameof(SuperMeshRenderSystem.RenderMainPass));

        var postfixMethod = AccessTools.Method(typeof(ThugLifeRenderPatches), nameof(RenderMainPassPostfix));
        if (postfixMethod == null)
            throw new MissingMethodException(typeof(ThugLifeRenderPatches).FullName, nameof(RenderMainPassPostfix));

        harmony.Patch(original, postfix: new HarmonyMethod(postfixMethod));
    }

    public static void Remove(Harmony harmony)
    {
        var original = AccessTools.Method(
            typeof(SuperMeshRenderSystem),
            nameof(SuperMeshRenderSystem.RenderMainPass));
        var postfix = AccessTools.Method(typeof(ThugLifeRenderPatches), nameof(RenderMainPassPostfix));
        if (original != null && postfix != null)
            harmony.Unpatch(original, postfix);
    }

    private static void RenderMainPassPostfix(CommandBuffer commandBuffer)
    {
        if (!ThugLifeRenderManager.Active) return;
        try
        {
            ThugLifeRenderManager.Instance?.RecordDraws(commandBuffer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: render postfix error: {ex.Message}");
        }
    }
}
