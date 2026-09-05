using MeowSci.KsaRings;
using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.BloominOnionLib;

/// <summary>
/// Creates and caches the painted band/control textures for ring definitions. Textures are
/// keyed by their paint-content id, so re-applying an unchanged paint reuses the upload and
/// only a real edit creates a new texture. Unreferenced textures are freed after rebuilds.
/// </summary>
public sealed class RingTextureFactory : IDisposable
{
    private readonly Dictionary<string, PaintedTextureReference> _textures = new(StringComparer.Ordinal);

    /// <summary>The painted band texture for the definition (cached), or null with an error.</summary>
    public TextureReference? GetBand(RingDefinition definition, out string? error)
    {
        return GetOrCreate(RingBandPainter.BandId(definition), () => RingBandPainter.PaintBand(definition), out error);
    }

    /// <summary>The painted control texture for the definition (cached), or null with an error.</summary>
    public TextureReference? GetControl(RingDefinition definition, out string? error)
    {
        return GetOrCreate(RingBandPainter.ControlId(definition), () => RingBandPainter.PaintControl(definition), out error);
    }

    /// <summary>
    /// Releases every painted texture whose id is not in <paramref name="keepIds"/>. Only call
    /// right after a successful renderer rebuild: the fresh ring render data references exactly
    /// the textures resolved for it, so everything else is unreferenced. Returns the count freed.
    /// </summary>
    public int PruneExcept(IReadOnlySet<string> keepIds)
    {
        int pruned = 0;
        foreach (var id in new List<string>(_textures.Keys))
        {
            if (keepIds.Contains(id)) continue;
            _textures[id].Release();
            _textures.Remove(id);
            pruned++;
        }
        if (pruned > 0) Console.WriteLine($"bloomin-onion: freed {pruned} unused painted texture(s)");
        return pruned;
    }

    /// <summary>Releases all painted textures. Only call once nothing in the renderer references them.</summary>
    public void Dispose()
    {
        foreach (var texture in _textures.Values) texture.Release();
        _textures.Clear();
    }

    private TextureReference? GetOrCreate(string id, Func<byte[]> paint, out string? error)
    {
        error = null;
        if (_textures.TryGetValue(id, out var cached)) return cached;

        var created = PaintedTextureReference.Create(id, paint(), RingBandPainter.Width, 1, out error);
        if (created == null) return null;
        _textures[id] = created;
        Console.WriteLine($"bloomin-onion: painted texture '{id}' ({RingBandPainter.Width}x1)");
        return created;
    }
}
