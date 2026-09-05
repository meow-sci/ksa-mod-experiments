using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HumbleArteestLib;

public static partial class VehiclePaint
{
    // Mesh identity comes from the resolved render MeshReference, never a material or template id.
    public readonly record struct MeshInstance(Part Part, string MeshId);
    private static readonly Dictionary<MeshInstance, PaintEntry> ByMeshInstance = new();
    private static readonly Dictionary<string, PaintEntry> ByMesh = new(StringComparer.Ordinal);
    public static IEnumerable<MeshInstance> PaintedMeshInstances => ByMeshInstance.Keys;
    public static IEnumerable<string> PaintedMeshes => ByMesh.Keys;
    public static void SetMeshInstance(MeshInstance instance, float3 color) => ByMeshInstance[instance] = PaintEntry.From(color);
    public static void ClearMeshInstance(MeshInstance instance) => ByMeshInstance.Remove(instance);
    public static void SetMesh(string meshId, float3 color) => ByMesh[meshId] = PaintEntry.From(color);
    public static void ClearMesh(string meshId) => ByMesh.Remove(meshId);
    public static bool TryGetMeshInstanceColor(MeshInstance instance, out float3 color)
    {
        bool found = ByMeshInstance.TryGetValue(instance, out var entry); color = entry.Color; return found;
    }
    public static bool TryGetMeshColor(string meshId, out float3 color)
    {
        bool found = ByMesh.TryGetValue(meshId, out var entry); color = entry.Color; return found;
    }
    private static void PruneMeshInstances(ICollection<Part> living)
    {
        if (living.Count == 0) return;
        foreach (var key in ByMeshInstance.Keys.Where(k => !living.Contains(k.Part)).ToArray()) ByMeshInstance.Remove(key);
    }
}
