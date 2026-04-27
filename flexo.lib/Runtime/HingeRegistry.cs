using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace MeowSci.FlexoLib.Runtime;

/// <summary>
/// Static registry that maps Parts to an orbit matrix injected by the
/// MatrixParentAsmb2Ego Harmony patch.  Only non-SubPart tree descendants
/// of a hinge's moving part should be registered — SubParts automatically
/// follow through the assembly-hierarchy recursion.
/// </summary>
public static class HingeRegistry
{
    private static readonly Dictionary<Part, double4x4> _orbitMatrices = new();

    public static bool TryGetOrbitMatrix(Part part, out double4x4 orbit)
        => _orbitMatrices.TryGetValue(part, out orbit);

    public static void Register(IReadOnlyList<Part> parts, double4x4 orbit)
    {
        foreach (var p in parts)
            _orbitMatrices[p] = orbit;
    }

    public static void Update(IReadOnlyList<Part> parts, double4x4 orbit)
    {
        foreach (var p in parts)
            _orbitMatrices[p] = orbit;
    }

    public static void Unregister(IReadOnlyList<Part> parts)
    {
        foreach (var p in parts)
            _orbitMatrices.Remove(p);
    }

    public static void Clear() => _orbitMatrices.Clear();
}
