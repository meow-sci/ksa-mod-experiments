using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// Harmony patches for the vehicle paint system.
///
/// Prefixes PartModel.AddInstance to inject paint RGB values into the PerInstanceData
/// padding bytes before the data is written to the GPU. The prefix uses __instance
/// (the PartModel) to look up the paint color via <see cref="VehiclePaint"/>.
/// </summary>
public static class VehiclePaintPatches
{
    private static MethodInfo? _addInstanceOriginal;
    private static MethodInfo? _addInstancePrefix;

    /// <summary>Mirror struct with same layout but float fields at padding offsets.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PaintablePerInstanceData
    {
        public float4x4 ModelMatrix; // 64 bytes
        public int StateBitFlag;     //  4 bytes
        public float PaintR;         //  4 bytes (was packing1)
        public float PaintG;         //  4 bytes (was packing2)
        public float PaintB;         //  4 bytes (was packing3)
    }

    public static void Apply(Harmony harmony)
    {
        _addInstanceOriginal = AccessTools.Method(typeof(PartModel), nameof(PartModel.AddInstance));
        _addInstancePrefix = typeof(VehiclePaintPatches).GetMethod(
            nameof(AddInstancePrefix), BindingFlags.NonPublic | BindingFlags.Static);

        if (_addInstanceOriginal == null)
        {
            Console.WriteLine("humble-arteest: WARNING — PartModel.AddInstance not found");
            return;
        }

        harmony.Patch(_addInstanceOriginal, prefix: new HarmonyMethod(_addInstancePrefix));
        Console.WriteLine("humble-arteest: VehiclePaint patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        if (_addInstanceOriginal != null && _addInstancePrefix != null)
            harmony.Unpatch(_addInstanceOriginal, _addInstancePrefix);

        _addInstanceOriginal = null;
        _addInstancePrefix = null;

        Console.WriteLine("humble-arteest: VehiclePaint patches removed");
    }

    /// <summary>
    /// Harmony prefix on PartModel.AddInstance. Writes paint color into the
    /// PerInstanceData padding bytes when paint is active for this PartModel.
    /// </summary>
    private static void AddInstancePrefix(PartModel __instance, ref PartModel.PerInstanceData instanceData)
    {
        // Never touch the per-instance bytes unless paint shaders are genuinely active.
        // On KSA 4693+ they can never activate (see VehiclePaint.IsSupported), so this
        // guarantees the prefix is a no-op and never clobbers PerInstanceData.EmissiveColor
        // (offset 68 — game-used in the current build).
        if (!VehiclePaint.ShadersActive)
            return;

        if (!VehiclePaint.TryGetEffectiveColor(__instance, out var color))
            return;

        ref var paintable = ref Unsafe.As<PartModel.PerInstanceData, PaintablePerInstanceData>(ref instanceData);
        paintable.PaintR = color.X;
        paintable.PaintG = color.Y;
        paintable.PaintB = color.Z;
    }
}
