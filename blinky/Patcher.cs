using System;
using HarmonyLib;
using KSA;

namespace MeowSci.Blinky;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("blinky");

    /// <summary>
    /// When false, pixel engine parts (Id starting with "pixel_") are skipped in the
    /// render pipeline — their UpdateRenderData never calls AddInstance, so they cost
    /// zero GPU draw calls while remaining fully functional in the part tree.
    /// </summary>
    public static bool RenderPixelParts = false;

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            Console.WriteLine("blinky: patches applied (render-skip active)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("blinky");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error removing patches: {ex.Message}");
        }
    }

    // ── Render-skip patches ──────────────────────────────────────────────────────
    // Skip UpdateRenderData for pixel engine parts (Id starts with "pixel_").
    // Prefix returning false prevents the original method from running, so
    // AddInstance is never called and the part is not submitted to the GPU.

    [HarmonyPatch(typeof(PartModelModule), nameof(PartModelModule.UpdateRenderData))]
    static class Patch_PartModelModule_SkipRender
    {
        static bool Prefix(PartModelModule __instance)
        {
            if (RenderPixelParts) return true;
            // FullPart returns the top-level part even when module is on a sub-part
            return !__instance.Parent.FullPart.Id.StartsWith("pixel_");
        }
    }

    [HarmonyPatch(typeof(PartModelDynamicModule), nameof(PartModelDynamicModule.UpdateRenderData))]
    static class Patch_PartModelDynamicModule_SkipRender
    {
        static bool Prefix(PartModelDynamicModule __instance)
        {
            if (RenderPixelParts) return true;
            return !__instance.Parent.FullPart.Id.StartsWith("pixel_");
        }
    }

    [HarmonyPatch(typeof(PartModelGlassModule), nameof(PartModelGlassModule.UpdateRenderData))]
    static class Patch_PartModelGlassModule_SkipRender
    {
        static bool Prefix(PartModelGlassModule __instance)
        {
            if (RenderPixelParts) return true;
            return !__instance.Parent.FullPart.Id.StartsWith("pixel_");
        }
    }
}

