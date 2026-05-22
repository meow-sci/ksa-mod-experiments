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
        Console.WriteLine("thug-life: ThugLifeRenderPatches.Apply() entered");
        var original = AccessTools.Method(
            typeof(SuperMeshRenderSystem),
            nameof(SuperMeshRenderSystem.RenderMainPass));
        if (original == null)
        {
            Console.WriteLine("thug-life: AccessTools.Method returned null for SuperMeshRenderSystem.RenderMainPass — aborting patch");
            throw new MissingMethodException(
                typeof(SuperMeshRenderSystem).FullName,
                nameof(SuperMeshRenderSystem.RenderMainPass));
        }
        Console.WriteLine($"thug-life: original resolved: {original.DeclaringType?.FullName}.{original.Name}");

        var postfixMethod = AccessTools.Method(typeof(ThugLifeRenderPatches), nameof(RenderMainPassPostfix));
        if (postfixMethod == null)
        {
            Console.WriteLine("thug-life: AccessTools.Method returned null for ThugLifeRenderPatches.RenderMainPassPostfix — aborting patch");
            throw new MissingMethodException(typeof(ThugLifeRenderPatches).FullName, nameof(RenderMainPassPostfix));
        }

        try
        {
            var replacement = harmony.Patch(original, postfix: new HarmonyMethod(postfixMethod));
            Console.WriteLine($"thug-life: harmony.Patch returned {(replacement != null ? "non-null" : "null")} replacement");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"thug-life: harmony.Patch threw: {ex.GetType().Name}: {ex.Message}\n{ex}");
            throw;
        }
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

    private static int _postfixCalls;

    private static void RenderMainPassPostfix(CommandBuffer commandBuffer)
    {
        if (_postfixCalls == 0)
            Console.WriteLine("thug-life: RenderMainPass postfix invoked (frame 1)");
        _postfixCalls++;

        if (!ThugLifeRenderManager.Active)
        {
            if (_postfixCalls == 1)
                Console.WriteLine($"thug-life: postfix fired but Active={ThugLifeRenderManager.Active} Instance={(ThugLifeRenderManager.Instance != null ? "set" : "null")} — skipping");
            return;
        }
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
