using System;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.GlassLib;

/// <summary>Manual Harmony patch helpers for glass camera FOV override.</summary>
public static class GlassPatches
{
    private static MethodInfo? _cameraChangeFieldOfView;
    private static MethodInfo? _cameraUpdateProjection;
    private static FieldInfo? _fovRadiansField;

    private static MethodInfo? _changeFovPrefix;
    private static MethodInfo? _updateProjectionPrefix;

    public static void Apply(Harmony harmony)
    {
        _fovRadiansField = AccessTools.Field(typeof(Camera), "_fovRadians");

        _changeFovPrefix = typeof(GlassPatches).GetMethod(nameof(ChangeFieldOfViewPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;
        _updateProjectionPrefix = typeof(GlassPatches).GetMethod(nameof(UpdateProjectionPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;

        _cameraChangeFieldOfView = AccessTools.Method(typeof(Camera), "ChangeFieldOfView");
        _cameraUpdateProjection = AccessTools.Method(typeof(Camera), "UpdateProjection");

        harmony.Patch(_cameraChangeFieldOfView, prefix: new HarmonyMethod(_changeFovPrefix));
        harmony.Patch(_cameraUpdateProjection, prefix: new HarmonyMethod(_updateProjectionPrefix));

        Console.WriteLine("glass.lib: patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        if (_cameraChangeFieldOfView != null && _changeFovPrefix != null)
            harmony.Unpatch(_cameraChangeFieldOfView, _changeFovPrefix);
        if (_cameraUpdateProjection != null && _updateProjectionPrefix != null)
            harmony.Unpatch(_cameraUpdateProjection, _updateProjectionPrefix);

        _cameraChangeFieldOfView = null;
        _cameraUpdateProjection = null;
        _changeFovPrefix = null;
        _updateProjectionPrefix = null;

        Console.WriteLine("glass.lib: patches removed");
    }

    // Block the game's FOV change input — we control FOV when override is active.
    private static bool ChangeFieldOfViewPrefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive || !IsMainCamera(__instance)) return true;
        return false;
    }

    // Inject our target FOV value into the camera before UpdateProjection runs.
    private static void UpdateProjectionPrefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive) return;
        // Glass is the player's lens for the main camera. Do not stomp the independent
        // projections of stock secondary viewports (for example Hot Pursuit cameras), which
        // explicitly own their per-camera FOV.
        if (!IsMainCamera(__instance)) return;
        if (_fovRadiansField == null) return;
        float targetRadians = (float)(FovController.OverrideFovDegrees * (Math.PI / 180.0));
        _fovRadiansField.SetValue(__instance, targetRadians);
    }

    private static bool IsMainCamera(Camera camera)
        => ViewportRegistry.IsMainCamera(camera);
}
