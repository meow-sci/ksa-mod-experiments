using System;
using System.Collections.Generic;
using System.Linq;

namespace MeowSci.PebblesLib;

/// <summary>Preserves stock slot semantics and separates imported files with stable source identities.</summary>
public sealed class GlbMaterialSlots
{
    private readonly Dictionary<(string Source, int Material), int> _slots;
    public int Count => _slots.Count;
    public GlbMaterialSlots(IEnumerable<(string Id, int[] Materials)> meshes)
    {
        _slots = meshes.SelectMany(m => m.Materials.Select(i => (Source: Source(m.Id), Material: i)))
            .Distinct().OrderBy(k => k.Source, StringComparer.Ordinal).ThenBy(k => k.Material)
            .Select((key, slot) => (key, slot)).ToDictionary(p => p.key, p => p.slot);
    }
    public int Slot(string meshId, int material) => _slots[(Source(meshId), material)];
    public Dictionary<int, int> Mapping(string meshId, IEnumerable<int> materials) => materials.Distinct().ToDictionary(m => m, m => Slot(meshId, m));
    private static string Source(string id) => id.StartsWith(GlbIdentity.Prefix, StringComparison.Ordinal) ? GlbIdentity.Parse(id).SourceKey : "";
}
