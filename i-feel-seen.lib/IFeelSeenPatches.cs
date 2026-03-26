using System;
using System.Reflection;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.IFeelSeenLib;

/// <summary>Manual Harmony patch helpers for i-feel-seen vehicle render distance override.</summary>
public static class IFeelSeenPatches
{
    private static VehicleTracker? _tracker;

    private static MethodInfo? _vehicleGetWorldMatrix;
    private static MethodInfo? _vehicleUpdateRenderData;

    private static MethodInfo? _getWorldMatrixPrefix;
    private static MethodInfo? _updateRenderDataPrefix;

    public static void Apply(Harmony harmony, VehicleTracker tracker)
    {
        _tracker = tracker;

        _getWorldMatrixPrefix = typeof(IFeelSeenPatches).GetMethod(nameof(GetWorldMatrixPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;
        _updateRenderDataPrefix = typeof(IFeelSeenPatches).GetMethod(nameof(UpdateRenderDataPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;

        _vehicleGetWorldMatrix = AccessTools.Method(typeof(Vehicle), "GetWorldMatrix");
        _vehicleUpdateRenderData = AccessTools.Method(typeof(Vehicle), "UpdateRenderData");

        harmony.Patch(_vehicleGetWorldMatrix, prefix: new HarmonyMethod(_getWorldMatrixPrefix));
        harmony.Patch(_vehicleUpdateRenderData, prefix: new HarmonyMethod(_updateRenderDataPrefix));

        Console.WriteLine("i-feel-seen.lib: patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        if (_vehicleGetWorldMatrix != null && _getWorldMatrixPrefix != null)
            harmony.Unpatch(_vehicleGetWorldMatrix, _getWorldMatrixPrefix);
        if (_vehicleUpdateRenderData != null && _updateRenderDataPrefix != null)
            harmony.Unpatch(_vehicleUpdateRenderData, _updateRenderDataPrefix);

        _tracker = null;
        _vehicleGetWorldMatrix = null;
        _vehicleUpdateRenderData = null;
        _getWorldMatrixPrefix = null;
        _updateRenderDataPrefix = null;

        Console.WriteLine("i-feel-seen.lib: patches removed");
    }

    private static bool GetWorldMatrixPrefix(Vehicle __instance, Camera camera, ref float4x4? __result)
    {
        if (_tracker == null || !_tracker.IsTracked(__instance))
            return true;

        double3 vector = camera.GetPositionEgo(__instance);
        float4x4 translation = float4x4.CreateTranslation(float3.Pack(in vector));
        float4x4 rotation = float4x4.CreateFromQuaternion(floatQuat.Pack(__instance.Body2Cce));
        __result = rotation * translation;
        return false;
    }

    private static bool UpdateRenderDataPrefix(Vehicle __instance, Viewport viewport, int inFrameIndex)
    {
        if (_tracker == null || !_tracker.IsTracked(__instance))
            return true;

        double4x4 matrixAsmb2Ego = __instance.GetMatrixAsmb2Ego(viewport.GetCamera());
        __instance.Parts.UpdateRenderData(in matrixAsmb2Ego, __instance.IsEditedVehicle, viewport, inFrameIndex);
        return false;
    }
}
