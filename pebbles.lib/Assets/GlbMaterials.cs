using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.PebblesLib;

/// <summary>Private glTF metallic/roughness adaptation. Construction never uploads GPU data.</summary>
public sealed class GlbMaterials : IDisposable
{
    private readonly GlbDocument _document;
    private readonly string _sourceKey;
    private readonly Dictionary<int, MaterialRecipe> _materials = new();
    private readonly Dictionary<int, GlbPixels> _images = new();
    private readonly Dictionary<string, GlbPixels> _pixels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GlbTexture> _textures = new(StringComparer.Ordinal);
    private long _pixelBytes;
    public IReadOnlyList<string> TextureIds => _pixels.Keys.ToArray();

    public GlbMaterials(GlbDocument document, string sourceKey) { _document = document; _sourceKey = sourceKey; }

    public MaterialRecipe GetMaterial(int index)
    {
        if (index < -1) throw new InvalidOperationException("Invalid GLB default material index.");
        if (_materials.TryGetValue(index, out var existing)) return RecipeCopy.Clone(existing);
        var material = index < 0 ? default : At(_document.Root, "materials", index);
        RejectExtensions(material);
        if (material.ValueKind == JsonValueKind.Object && material.TryGetProperty("emissiveTexture", out _))
            throw new NotSupportedException("GLB emissive textures are not supported by ground clutter. Remove the emissive channel before import.");
        var emissive = Vector(material, "emissiveFactor", [0, 0, 0]);
        if (emissive.Any(x => x != 0)) throw new NotSupportedException("GLB emissive materials are not supported by ground clutter.");
        string alpha = Text(material, "alphaMode", "OPAQUE");
        if (alpha != "OPAQUE" && alpha != "MASK") throw new NotSupportedException("GLB alpha blending is not supported by ground clutter. Use opaque or alpha-mask materials.");
        var pbr = Property(material, "pbrMetallicRoughness");
        RejectExtensions(pbr);
        var factor = Vector(pbr, "baseColorFactor", [1, 1, 1, 1]);
        var diffuse = Image(Property(pbr, "baseColorTexture"));
        var mr = Image(Property(pbr, "metallicRoughnessTexture"));
        var occlusionInfo = Property(material, "occlusionTexture");
        var occlusion = Image(occlusionInfo);
        var normalInfo = Property(material, "normalTexture");
        var normal = Image(normalInfo);
        float metallic = Number(pbr, "metallicFactor", 1), roughness = Number(pbr, "roughnessFactor", 1);
        float strength = Number(occlusionInfo, "strength", 1), normalScale = Number(normalInfo, "scale", 1, unit: false);
        if (normalScale > 100) throw new NotSupportedException("GLB normal scale above 100 is unsupported.");
        float cutoff = Number(material, "alphaCutoff", .5f);
        var recipe = new MaterialRecipe { SourceId = $"{_sourceKey}/material/{index}", SourceColors = true,
            DoubleSided = Bool(material, "doubleSided"), CastShadows = true, ReceiveShadows = true };
        // Build detached CPU images first. Only ResolveTexture may publish GPU resources.
        var generated = new Dictionary<string, GlbPixels>();
        string Add(string role, GlbPixels pixels) { var id = $"{recipe.SourceId}/{role}"; generated.Add(id, pixels); return id; }
        recipe.DiffuseId = Add("diffuse", GlbPixels.Diffuse(diffuse, factor));
        recipe.PbrId = Add("pbr", GlbPixels.Pbr(mr, occlusion, metallic, roughness, strength));
        if (normal != null) recipe.NormalId = Add("normal", GlbPixels.Normal(normal, normalScale));
        if (alpha == "MASK") recipe.OpacityId = Add("opacity", GlbPixels.Opacity(diffuse, factor[3], cutoff));
        long generatedBytes = generated.Values.Sum(x => (long)x.Data.Length);
        if (_pixelBytes + generatedBytes > 256L * 1024 * 1024) throw new InvalidOperationException("GLB decoded textures exceed the 256 MiB CPU budget.");
        foreach (var (id, pixels) in generated) _pixels.Add(id, pixels);
        _pixelBytes += generatedBytes;
        _materials.Add(index, recipe);
        return RecipeCopy.Clone(recipe);
    }

    /// <summary>Call only from explicit import/preview/apply processing outside GUI rendering.</summary>
    public TextureReference ResolveTexture(string id)
    {
        if (_textures.TryGetValue(id, out var existing)) return existing;
        if (!_pixels.TryGetValue(id, out var pixels))
        {
            var prefix = _sourceKey + "/material/";
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Texture belongs to another GLB source.");
            var remainder = id[prefix.Length..]; var slash = remainder.IndexOf('/');
            if (slash < 1 || !int.TryParse(remainder[..slash], out var index)) throw new InvalidOperationException("Invalid GLB texture identity.");
            GetMaterial(index);
            if (!_pixels.TryGetValue(id, out pixels)) throw new InvalidOperationException("GLB texture channel does not exist.");
        }
        var texture = GlbTexture.Upload(id, pixels);
        _textures.Add(id, texture);
        return texture;
    }

    private GlbPixels? Image(JsonElement info)
    {
        if (info.ValueKind != JsonValueKind.Object) return null;
        RejectExtensions(info);
        if (Integer(info, "texCoord", 0) != 0) throw new NotSupportedException("GLB clutter textures must use UV set 0.");
        var texture = At(_document.Root, "textures", Integer(info, "index", -1));
        RejectExtensions(texture);
        int imageIndex = Integer(texture, "source", -1);
        // The native clutter sampler repeats in both axes; reject modes it cannot reproduce.
        if (texture.TryGetProperty("sampler", out var samplerIndex))
        {
            var sampler = At(_document.Root, "samplers", samplerIndex.GetInt32());
            if (Integer(sampler, "wrapS", 10497) != 10497 || Integer(sampler, "wrapT", 10497) != 10497)
                throw new NotSupportedException("GLB clutter texture samplers must use repeat wrapping in both axes.");
        }
        if (_images.TryGetValue(imageIndex, out var loaded)) return loaded;
        var image = At(_document.Root, "images", imageIndex);
        if (image.TryGetProperty("uri", out _)) throw new NotSupportedException("GLB images must be embedded PNG/JPEG buffer views; external/data image URIs are unsupported.");
        var data = _document.ReadBufferView(Integer(image, "bufferView", -1));
        var pixels = GlbPixels.Decode(data, Text(image, "mimeType", ""));
        if (_pixelBytes + pixels.Data.Length > 256L * 1024 * 1024) throw new InvalidOperationException("GLB decoded textures exceed the 256 MiB CPU budget.");
        _pixelBytes += pixels.Data.Length;
        _images.Add(imageIndex, pixels);
        return pixels;
    }

    internal static JsonElement At(JsonElement parent, string key, int index)
    {
        var array = Property(parent, key);
        if (array.ValueKind != JsonValueKind.Array || index < 0 || index >= array.GetArrayLength())
            throw new InvalidOperationException($"GLB {key} index {index} is invalid.");
        return array[index];
    }
    internal static JsonElement Property(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;
    private static string Text(JsonElement parent, string name, string fallback) => Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined ? e.GetString() ?? fallback : fallback;
    private static int Integer(JsonElement parent, string name, int fallback) => Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined ? e.GetInt32() : fallback;
    private static bool Bool(JsonElement parent, string name) => Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined && e.GetBoolean();
    private static float Number(JsonElement parent, string name, float fallback, bool unit = true)
    {
        float n = Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined ? e.GetSingle() : fallback;
        if (!float.IsFinite(n) || n < 0 || (unit && n > 1)) throw new InvalidOperationException($"GLB {name} is outside its supported range.");
        return n;
    }
    private static float[] Vector(JsonElement parent, string name, float[] fallback)
    {
        var e = Property(parent, name);
        if (e.ValueKind == JsonValueKind.Undefined) return fallback;
        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != fallback.Length) throw new InvalidOperationException($"Invalid GLB {name}.");
        var values = e.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        if (values.Any(x => !float.IsFinite(x) || x < 0 || x > 1)) throw new InvalidOperationException($"Invalid GLB {name}.");
        return values;
    }
    private static void RejectExtensions(JsonElement item)
    {
        var extensions = Property(item, "extensions");
        if (extensions.ValueKind == JsonValueKind.Object && extensions.EnumerateObject().Any())
            throw new NotSupportedException("GLB material/texture extensions are unsupported; export core metallic/roughness materials without extensions.");
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
        _textures.Clear(); _pixels.Clear(); _images.Clear(); _materials.Clear(); _pixelBytes = 0;
    }
}
