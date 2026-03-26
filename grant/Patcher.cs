using System;
using System.Reflection;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.GlassLib;
using MeowSci.IFeelSeenLib;

namespace MeowSci.Grant;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony;

    // ── Blinky ──
    /// <summary>When false, pixel-engine meshes are hidden for better performance.</summary>
    public static bool RenderPixelParts = false;

    // ── Glass ──
    private static FieldInfo? _fovRadiansField;

    // ── I Feel Seen ──
    public static VehicleTracker? IFeelSeenTracker;

    // ── Skittles ──
    /// <summary>Delegate checked per frame to block game hotkeys when Skittles text inputs are focused.</summary>
    public static Func<bool>? SkittlesHasFocusedTextInput;

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("MeowSci.Grant");
            _harmony.PatchAll(typeof(Patcher).Assembly);
            _fovRadiansField = AccessTools.Field(typeof(Camera), "_fovRadians");
            Console.WriteLine("grant: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("MeowSci.Grant");
            _harmony = null;
            IFeelSeenTracker = null;
            SkittlesHasFocusedTextInput = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"grant: Error removing patches: {ex.Message}");
        }
    }

    // ── Blinky: Render-skip patches ─────────────────────────────────────────────
    // Skip UpdateRenderData for pixel engine parts (Id starts with "pixel_").

    [HarmonyPatch(typeof(PartModelModule), nameof(PartModelModule.UpdateRenderData))]
    static class Patch_PartModelModule_SkipRender
    {
        static bool Prefix(PartModelModule __instance)
        {
            if (RenderPixelParts) return true;
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

    // ── Glass: FOV override patches ─────────────────────────────────────────────

    [HarmonyPatch(typeof(Camera), "ChangeFieldOfView")]
    static class Patch_Camera_ChangeFieldOfView
    {
        static bool Prefix()
        {
            if (!FovController.IsOverrideActive) return true;
            return false; // block game FOV input
        }
    }

    [HarmonyPatch(typeof(Camera), "UpdateProjection")]
    static class Patch_Camera_UpdateProjection
    {
        static void Prefix(Camera __instance)
        {
            if (!FovController.IsOverrideActive) return;
            if (_fovRadiansField == null) return;
            float targetRadians = (float)(FovController.OverrideFovDegrees * (Math.PI / 180.0));
            _fovRadiansField.SetValue(__instance, targetRadians);
        }
    }

    // ── I Feel Seen: Vehicle render distance patches ────────────────────────────

    [HarmonyPatch(typeof(Vehicle), "GetWorldMatrix")]
    static class Patch_Vehicle_GetWorldMatrix
    {
        static bool Prefix(Vehicle __instance, Camera camera, ref float4x4? __result)
        {
            if (IFeelSeenTracker == null || !IFeelSeenTracker.IsTracked(__instance))
                return true;

            double3 vector = camera.GetPositionEgo(__instance);
            float4x4 translation = float4x4.CreateTranslation(float3.Pack(in vector));
            float4x4 rotation = float4x4.CreateFromQuaternion(floatQuat.Pack(__instance.Body2Cce));
            __result = rotation * translation;
            return false;
        }
    }

    [HarmonyPatch(typeof(Vehicle), "UpdateRenderData")]
    static class Patch_Vehicle_UpdateRenderData
    {
        static bool Prefix(Vehicle __instance, Viewport viewport, int inFrameIndex)
        {
            if (IFeelSeenTracker == null || !IFeelSeenTracker.IsTracked(__instance))
                return true;

            double4x4 matrixAsmb2Ego = __instance.GetMatrixAsmb2Ego(viewport.GetCamera());
            __instance.Parts.UpdateRenderData(in matrixAsmb2Ego, __instance.IsEditedVehicle, viewport, inFrameIndex);
            return false;
        }
    }

    // ── Skittles: Hotkey blocking patch ─────────────────────────────────────────

    [HarmonyPatch(typeof(GameSettings), nameof(GameSettings.OnKeyAll))]
    static class Patch_GameSettings_OnKeyAll
    {
        static bool Prefix(ref bool __result)
        {
            if (SkittlesHasFocusedTextInput?.Invoke() == true)
            {
                __result = true;
                return false; // skip original, hotkey blocked
            }
            return true;
        }
    }
}
