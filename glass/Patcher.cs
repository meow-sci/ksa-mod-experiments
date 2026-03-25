using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.GlassLib;

namespace MeowSci.Glass;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("glass");

    private static System.Reflection.FieldInfo? _fovRadiansField;

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            _fovRadiansField = AccessTools.Field(typeof(Camera), "_fovRadians");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("glass");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error removing patches: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Camera), "ChangeFieldOfView")]
    [HarmonyPrefix]
    private static bool ChangeFieldOfView_Prefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive) return true; // let game handle it
        // Block game's FOV input — we control FOV
        return false;
    }

    [HarmonyPatch(typeof(Camera), "UpdateProjection")]
    [HarmonyPrefix]
    private static void UpdateProjection_Prefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive) return;
        if (_fovRadiansField == null) return;
        float targetRadians = (float)(FovController.OverrideFovDegrees * (Math.PI / 180.0));
        _fovRadiansField.SetValue(__instance, targetRadians);
    }
}
