using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.IFeelSeenLib;

namespace MeowSci.IFeelSeen;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("i-feel-seen");
    private static VehicleTracker? _tracker;

    public static void Patch(VehicleTracker tracker)
    {
        try
        {
            _tracker = tracker;
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"i-feel-seen: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _tracker = null;
            _harmony?.UnpatchAll("i-feel-seen");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"i-feel-seen: Error removing patches: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Vehicle), "GetWorldMatrix")]
    [HarmonyPrefix]
    private static bool GetWorldMatrix_Prefix(Vehicle __instance, Camera camera, ref float4x4? __result)
    {
        if (_tracker == null || !_tracker.IsTracked(__instance))
            return true;

        double3 vector = camera.GetPositionEgo(__instance);
        float4x4 translation = float4x4.CreateTranslation(float3.Pack(in vector));
        float4x4 rotation = float4x4.CreateFromQuaternion(floatQuat.Pack(__instance.Body2Cce));
        __result = rotation * translation;
        return false;
    }

    [HarmonyPatch(typeof(Vehicle), "UpdateRenderData")]
    [HarmonyPrefix]
    private static bool UpdateRenderData_Prefix(Vehicle __instance, Viewport viewport, int inFrameIndex)
    {
        if (_tracker == null || !_tracker.IsTracked(__instance))
            return true;

        double4x4 matrixAsmb2Ego = __instance.GetMatrixAsmb2Ego(viewport.GetCamera());
        __instance.Parts.UpdateRenderData(in matrixAsmb2Ego, __instance.IsEditedVehicle, viewport, inFrameIndex);
        return false;
    }
}
