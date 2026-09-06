using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MeowSci.PebblesLib;

/// <summary>Detached material conversion with an injected image decoder; no game or GPU access.</summary>
internal sealed class GlbMaterialReader
{
    private readonly GlbDocument _document;
    private readonly string _sourceKey;
    private readonly Dictionary<int, MaterialRecipe> _materials = new();
    private readonly Dictionary<int, GlbPixels> _images = new();
    private readonly Dictionary<string, GlbPixels> _pixels = new(StringComparer.Ordinal);
    private long _pixelBytes;
    private readonly HashSet<string> _warnings = new(StringComparer.Ordinal);
    public IReadOnlyList<string> Warnings => _warnings.Order(StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> TextureIds => _pixels.Keys.ToArray();

    private readonly Func<byte[], string, GlbPixels> _decode;
    internal IReadOnlyDictionary<string, GlbPixels> Pixels => _pixels;
    internal GlbMaterialReader(GlbDocument document, string sourceKey, Func<byte[], string, GlbPixels> decode)
    { _document = document; _sourceKey = sourceKey; _decode = decode; }

    public MaterialRecipe GetMaterial(int index)
    {
        if (index < -1) throw new InvalidOperationException("Invalid GLB default material index.");
        if (_materials.TryGetValue(index, out var existing)) return RecipeCopy.Clone(existing);
        var material = index < 0 ? default : At(_document.Root, "materials", index);
        var warnings = GlbCompatibility.MaterialWarnings(material);
        string alpha = Text(material, "alphaMode", "OPAQUE");
        if (alpha is not ("OPAQUE" or "MASK" or "BLEND")) throw new InvalidOperationException("Invalid GLB alpha mode.");
        if (alpha == "BLEND") warnings.Add("Blended transparency is approximated as an alpha cutout at 50% coverage; soft transparency is omitted.");
        var pbr = Property(material, "pbrMetallicRoughness");
        RejectExtensions(pbr);
        var specular = GlbCompatibility.SpecularGlossiness(material);
        bool legacy = specular.ValueKind == JsonValueKind.Object;
        if (specular.ValueKind != JsonValueKind.Undefined && !legacy) throw new InvalidOperationException("Invalid specular/glossiness material.");
        var factor = legacy ? Vector(specular, "diffuseFactor", [1, 1, 1, 1]) : Vector(pbr, "baseColorFactor", [1, 1, 1, 1]);
        var mapping = GlbTextureMapping.Read(GlbTextureMapping.PrimaryInfo(material));
        var diffuse = Image(legacy ? Property(specular, "diffuseTexture") : Property(pbr, "baseColorTexture"), warnings);
        var mr = legacy ? null : DetailImage(Property(pbr, "metallicRoughnessTexture"), mapping, "metallic/roughness", warnings);
        var occlusionInfo = Property(material, "occlusionTexture");
        var occlusion = DetailImage(occlusionInfo, mapping, "occlusion", warnings);
        var normalInfo = Property(material, "normalTexture");
        var normal = DetailImage(normalInfo, mapping, "normal", warnings);
        float metallic = legacy ? 0 : Number(pbr, "metallicFactor", 1);
        float roughness = legacy ? 1 - Number(specular, "glossinessFactor", 1) : Number(pbr, "roughnessFactor", 1);
        float strength = Number(occlusionInfo, "strength", 1), normalScale = Number(normalInfo, "scale", 1, unit: false);
        if (normal != null && normalScale > 100)
        {
            normalScale = 100;
            warnings.Add("Normal-map strength above 100 is limited to 100.");
        }
        float cutoff = alpha == "BLEND" ? .5f : Number(material, "alphaCutoff", .5f);
        var recipe = new MaterialRecipe { SourceId = $"{_sourceKey}/material/{index}", SourceColors = true,
            DoubleSided = Bool(material, "doubleSided"), CastShadows = true, ReceiveShadows = true };
        // Build detached CPU images first. Only ResolveTexture may publish GPU resources.
        var generated = new Dictionary<string, GlbPixels>();
        string Add(string role, GlbPixels pixels) { var id = $"{recipe.SourceId}/{role}"; generated.Add(id, pixels); return id; }
        recipe.DiffuseId = Add("diffuse", GlbPixels.Diffuse(diffuse, factor));
        recipe.PbrId = Add("pbr", GlbPixels.Pbr(mr, occlusion, metallic, roughness, strength));
        if (normal != null) recipe.NormalId = Add("normal", GlbPixels.Normal(normal, normalScale));
        if (alpha is "MASK" or "BLEND") recipe.OpacityId = Add("opacity", GlbPixels.Opacity(diffuse, factor[3], cutoff));
        long generatedBytes = generated.Values.Sum(x => (long)x.Data.Length);
        if (_pixelBytes + generatedBytes > 256L * 1024 * 1024) throw new InvalidOperationException("GLB decoded textures exceed the 256 MiB CPU budget.");
        foreach (var (id, pixels) in generated) _pixels.Add(id, pixels);
        _pixelBytes += generatedBytes;
        _materials.Add(index, recipe);
        foreach (var warning in warnings) _warnings.Add(warning);
        return RecipeCopy.Clone(recipe);
    }

    private GlbPixels? DetailImage(JsonElement info, GlbTextureMapping primary, string role, List<string> warnings)
    {
        if (info.ValueKind != JsonValueKind.Object) return null;
        try
        {
            if (GlbTextureMapping.Read(info) != primary)
                throw new NotSupportedException("it uses a different UV set or transform from the main texture");
            return Image(info, warnings);
        }
        catch (NotSupportedException ex)
        {
            warnings.Add($"Skipped {role} detail map: {ex.Message}. Main color texture is retained.");
            return null;
        }
    }

    private GlbPixels? Image(JsonElement info, List<string> warnings)
    {
        if (info.ValueKind != JsonValueKind.Object) return null;
        _ = GlbTextureMapping.Read(info);
        var texture = At(_document.Root, "textures", Integer(info, "index", -1));
        int imageIndex = GlbTextureMapping.ImageSource(texture, warnings.Add);
        // The native sampler repeats; keep the artwork with an explicit wrapping approximation.
        if (texture.TryGetProperty("sampler", out var samplerIndex))
        {
            var sampler = At(_document.Root, "samplers", samplerIndex.GetInt32());
            int wrapS = Integer(sampler, "wrapS", 10497), wrapT = Integer(sampler, "wrapT", 10497);
            if (wrapS is not (10497 or 33071 or 33648) || wrapT is not (10497 or 33071 or 33648))
                throw new InvalidOperationException("Invalid GLB texture wrapping mode.");
            if (wrapS != 10497 || wrapT != 10497)
                warnings.Add("Clamp/mirrored texture wrapping uses repeat sampling; appearance outside the image edges may differ.");
        }
        if (_images.TryGetValue(imageIndex, out var loaded)) return loaded;
        var image = At(_document.Root, "images", imageIndex);
        if (image.TryGetProperty("uri", out _)) throw new NotSupportedException("GLB images must be embedded PNG/JPEG buffer views; external/data image URIs are unsupported.");
        var data = _document.ReadBufferView(Integer(image, "bufferView", -1));
        string mime = Text(image, "mimeType", "");
        if (mime is not ("image/png" or "image/jpeg"))
            throw new NotSupportedException($"Image format '{mime}' needs a decoder that Pebbles does not yet provide (PNG/JPEG supported).");
        var pixels = _decode(data, mime);
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
        GlbCompatibility.RejectExtensions(item);
    }

}
