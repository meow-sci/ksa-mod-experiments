using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HumbleArteestLib.Experiments;

/// <summary>
/// Experiment 0.4: Temperature Visual Test
///
/// Forces Temperature on all dynamic vehicle parts to make them glow,
/// proving per-instance visual modification works through existing fields.
/// No shader modifications needed — Temperature is already wired from C#
/// through to the fragment shader via DynamicMeshIndirect.vert/frag.
///
/// PartModelDynamic.PerInstanceData layout:
///   float4x4 ModelMatrix   64 bytes
///   int      StateBitFlag    4 bytes
///   float    Temperature     4 bytes  ← override target
///   float    TfiThickness    4 bytes  ← override target
///   int      packing1        4 bytes
/// </summary>
public static class TemperatureTest
{
    private static bool _enabled;
    private static float _temperature = 1.0f;
    private static float _tfiThickness = 0.0f;
    private static string? _lastError;
    private static MethodInfo? _addInstanceOriginal;
    private static MethodInfo? _addInstancePrefix;

    public static string? LastError => _lastError;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Console.WriteLine($"humble-arteest: Temperature test {(value ? "ENABLED" : "DISABLED")}");
        }
    }

    public static float Temperature { get => _temperature; set => _temperature = value; }
    public static float TfiThickness { get => _tfiThickness; set => _tfiThickness = value; }

    // ---- Mirror struct matching PartModelDynamic.PerInstanceData layout ----

    [StructLayout(LayoutKind.Sequential)]
    private struct WritablePerInstanceData
    {
        public float4x4 ModelMatrix; // 64 bytes
        public int StateBitFlag;     //  4 bytes
        public float Temperature;    //  4 bytes
        public float TfiThickness;   //  4 bytes
        public int packing1;         //  4 bytes
    }

    // ---- Harmony patches ----

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            _addInstanceOriginal = AccessTools.Method(
                typeof(PartModelDynamic),
                nameof(PartModelDynamic.AddInstance));

            _addInstancePrefix = typeof(TemperatureTest).GetMethod(
                nameof(AddInstancePrefix), BindingFlags.NonPublic | BindingFlags.Static);

            if (_addInstanceOriginal == null)
            {
                Console.WriteLine("humble-arteest: WARNING — PartModelDynamic.AddInstance not found");
                _lastError = "PartModelDynamic.AddInstance not found";
                return;
            }

            harmony.Patch(_addInstanceOriginal, prefix: new HarmonyMethod(_addInstancePrefix));
            Console.WriteLine("humble-arteest: Temperature test Harmony patches applied");
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            Console.WriteLine($"humble-arteest: Error applying temperature test patches: {ex.Message}");
        }
    }

    public static void RemovePatches(Harmony harmony)
    {
        try
        {
            if (_addInstanceOriginal != null && _addInstancePrefix != null)
                harmony.Unpatch(_addInstanceOriginal, _addInstancePrefix);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error removing temperature test patches: {ex.Message}");
        }
    }

    /// <summary>
    /// Harmony prefix on PartModelDynamic.AddInstance. When enabled, overrides
    /// Temperature and TfiThickness in the PerInstanceData before it reaches the GPU.
    /// </summary>
    private static void AddInstancePrefix(ref PartModelDynamic.PerInstanceData inInstanceData)
    {
        if (!_enabled) return;

        ref var writable = ref Unsafe.As<PartModelDynamic.PerInstanceData, WritablePerInstanceData>(ref inInstanceData);
        writable.Temperature = _temperature;
        writable.TfiThickness = _tfiThickness;
    }
}
