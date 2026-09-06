using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.PebblesLib;

/// <summary>Owns lazy native uploads separately from detached glTF material conversion.</summary>
public sealed class GlbMaterials : IDisposable
{
    private readonly GlbDocument _document;
    private readonly string _sourceKey;
    private GlbMaterialReader _reader;
    private readonly Dictionary<string, GlbTexture> _textures = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Warnings => _reader.Warnings;
    public IReadOnlyList<string> TextureIds => _reader.TextureIds;
    public GlbMaterials(GlbDocument document, string sourceKey)
    { _document = document; _sourceKey = sourceKey; _reader = new(document, sourceKey, GlbPixels.Decode); }
    public MaterialRecipe GetMaterial(int index) => _reader.GetMaterial(index);

    /// <summary>Call only from explicit import/preview/apply processing outside GUI rendering.</summary>
    public TextureReference ResolveTexture(string id)
    {
        if (_textures.TryGetValue(id, out var existing)) return existing;
        if (!_reader.Pixels.TryGetValue(id, out var pixels))
        {
            var prefix = _sourceKey + "/material/";
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Texture belongs to another GLB source.");
            var remainder = id[prefix.Length..]; var slash = remainder.IndexOf('/');
            if (slash < 1 || !int.TryParse(remainder[..slash], out var index)) throw new InvalidOperationException("Invalid GLB texture identity.");
            GetMaterial(index);
            if (!_reader.Pixels.TryGetValue(id, out pixels)) throw new InvalidOperationException("GLB texture channel does not exist.");
        }
        var texture = GlbTexture.Upload(id, pixels);
        _textures.Add(id, texture);
        return texture;
    }

    /// <summary>
    /// Owner must first retire all live graphs/previews. Wait for each actual owning device
    /// before recycling descriptors/images. CPU-only sources never query or touch the renderer.
    /// </summary>
    public void Dispose()
    {
        // Wait before mutating any cache state, so a failed wait preserves every owned handle.
        // Remember the renderer that allocated each image rather than looking up a replacement.
        foreach (var renderer in _textures.Values.Select(t => t.Owner).Distinct()) renderer.Device.WaitIdle();
        foreach (var texture in _textures.Values) texture.Release();
        _textures.Clear();
        _reader = new(_document, _sourceKey, GlbPixels.Decode);
    }
}
