using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.Numerics;
using KSA;

namespace MeowSci.MeshDeformLib;

/// <summary>
/// Holds per-part deformation state and the compact GPU payload.
///
/// Deformation is a radial spherical displacement from the part's local-space origin
/// (the mesh assembly frame origin).  This is the simplest model that fits in the
/// 8 bytes of padding available in <see cref="PartModel.PerInstanceData"/>.
/// </summary>
public sealed class PartDeformState
{
    public float Magnitude;   // positive = bulge outward, negative = dent inward (metres)
    public float Radius;      // sphere of influence in metres

    public PartDeformState(float magnitude, float radius)
    {
        Magnitude = magnitude;
        Radius = radius;
    }
}

/// <summary>
/// Central manager for mesh deformation.  Stores CPU-side state and provides the
/// compact payload consumed by the Harmony prefix on <c>PartModel.AddInstance</c>.
///
/// Deformations are session-only (in-memory dictionary keyed by <see cref="Part"/>).
/// </summary>
public static class MeshDeformManager
{
    // ---- State ----

    private static readonly Dictionary<Part, PartDeformState> _states = new(ReferenceEqualityComparer.Instance);
    private static bool _active;
    private static float _globalMagnitude;
    private static float _globalRadius = 1.0f;

    // ---- Public API ----

    public static bool IsActive => _active;

    public static float GlobalMagnitude
    {
        get => _globalMagnitude;
        set => _globalMagnitude = value;
    }

    public static float GlobalRadius
    {
        get => _globalRadius;
        set => _globalRadius = Math.Max(0.001f, value);
    }

    public static IReadOnlyDictionary<Part, PartDeformState> States => _states;

    public static void SetActive(bool active)
    {
        _active = active;
    }

    public static bool TryGetPayload(Part part, out DeformPayload payload)
    {
        payload = default;
        if (!_active) return false;
        if (!_states.TryGetValue(part, out var state)) return false;
        if (Math.Abs(state.Magnitude) < 0.0001f) return false;

        payload = new DeformPayload
        {
            Magnitude = state.Magnitude,
            Radius = state.Radius
        };
        return true;
    }

    public static void SetDeform(Part part, float magnitude, float radius)
    {
        if (Math.Abs(magnitude) < 0.0001f)
        {
            _states.Remove(part);
            return;
        }
        _states[part] = new PartDeformState(magnitude, Math.Max(0.001f, radius));
    }

    public static void ClearPart(Part part)
    {
        _states.Remove(part);
    }

    public static void ClearAll()
    {
        _states.Clear();
    }

    public static void Cleanup()
    {
        _states.Clear();
        _active = false;
        _globalMagnitude = 0f;
        _globalRadius = 1.0f;
    }
}

/// <summary>
/// 8-byte payload injected into <see cref="PartModel.PerInstanceData"/> padding.
/// Reinterprets <c>packing1</c> and <c>packing2</c> as two floats.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DeformPayload
{
    public float Magnitude;
    public float Radius;
}

/// <summary>
/// Mirror of <see cref="PartModel.PerInstanceData"/> with deformation fields replacing padding.
/// Used with <see cref="Unsafe.As{TFrom, TTo}"> for zero-cost reinterpretation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct DeformablePerInstanceData
{
    public float4x4 ModelMatrix;   // 64 bytes
    public int      StateBitFlag;  //  4 bytes
    public uint     EmissiveColor; //  4 bytes (game-used — preserved)
    public float    DeformMagnitude; // 4 bytes ← was packing1
    public float    DeformRadius;    // 4 bytes ← was packing2, GAME-USED (Wetness) since 5018
}
