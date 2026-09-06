using System;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace MeowSci.PebblesLib;

/// <summary>One native UV stream, chosen for the main texture. Detail maps must share it.</summary>
public readonly record struct GlbTextureMapping(int TexCoord, Vector2 Offset, Vector2 Scale, float Rotation)
{
    public static GlbTextureMapping Identity => new(0, Vector2.Zero, Vector2.One, 0);

    public static JsonElement PrimaryInfo(JsonElement material)
    {
        var pbr = Property(material, "pbrMetallicRoughness");
        var specular = GlbCompatibility.SpecularGlossiness(material);
        bool legacy = specular.ValueKind == JsonValueKind.Object;
        foreach (var info in new[] { legacy ? Property(specular, "diffuseTexture") : Property(pbr, "baseColorTexture"),
            legacy ? default : Property(pbr, "metallicRoughnessTexture"),
            Property(material, "normalTexture"), Property(material, "occlusionTexture") })
            if (info.ValueKind == JsonValueKind.Object) return info;
        return default;
    }

    public static GlbTextureMapping Read(JsonElement info)
    {
        if (info.ValueKind == JsonValueKind.Undefined) return Identity;
        if (info.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid GLB texture info object.");
        int uv = Integer(info, "texCoord", 0);
        var transform = Property(Property(info, "extensions"), "KHR_texture_transform");
        var extensions = Property(info, "extensions");
        if (extensions.ValueKind != JsonValueKind.Undefined)
            foreach (var extension in extensions.EnumerateObject())
                if (extension.Name != "KHR_texture_transform")
                    throw new NotSupportedException($"Unsupported texture mapping extension '{extension.Name}'.");
        if (transform.ValueKind != JsonValueKind.Undefined && transform.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid KHR_texture_transform object.");
        uv = Integer(transform, "texCoord", uv);
        if (uv < 0) throw new InvalidDataException("GLB texture UV set must be nonnegative.");
        float rotation = Number(transform, "rotation", 0);
        return new(uv, Vector(transform, "offset", Vector2.Zero), Vector(transform, "scale", Vector2.One), rotation);
    }

    public Vector2 Apply(Vector2 uv)
    {
        var scaled = uv * Scale;
        float c = MathF.Cos(Rotation), s = MathF.Sin(Rotation);
        var result = Offset + new Vector2(c * scaled.X - s * scaled.Y, s * scaled.X + c * scaled.Y);
        if (!float.IsFinite(result.X) || !float.IsFinite(result.Y)) throw new InvalidDataException("GLB texture coordinates overflow after transformation.");
        return result;
    }

    /// <summary>Known alternate encodings may use a core PNG/JPEG source supplied by the file.</summary>
    public static int ImageSource(JsonElement texture, Action<string> warn)
    {
        var extensions = Property(texture, "extensions");
        if (extensions.ValueKind != JsonValueKind.Undefined)
            foreach (var extension in extensions.EnumerateObject())
            {
                if (extension.Name is not ("KHR_texture_basisu" or "EXT_texture_webp"))
                    throw new NotSupportedException($"Unsupported texture source extension '{extension.Name}'.");
                if (Property(texture, "source").ValueKind == JsonValueKind.Undefined)
                    throw new NotSupportedException($"{extension.Name}: this texture has only a WebP/KTX2 image and no PNG/JPEG fallback. Pebbles needs an additional image decoder to read it; texture transforms and material simplification cannot decode these pixels.");
                warn($"{extension.Name}: using the file's PNG/JPEG fallback texture.");
            }
        int source = Integer(texture, "source", -1);
        if (source < 0) throw new InvalidDataException("GLB texture image source is missing or invalid.");
        return source;
    }

    internal static JsonElement Property(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;
    private static int Integer(JsonElement parent, string name, int fallback) =>
        Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined ? e.GetInt32() : fallback;
    private static float Number(JsonElement parent, string name, float fallback)
    {
        float value = Property(parent, name) is var e && e.ValueKind != JsonValueKind.Undefined ? e.GetSingle() : fallback;
        if (!float.IsFinite(value)) throw new InvalidDataException($"GLB texture {name} must be finite.");
        return value;
    }
    private static Vector2 Vector(JsonElement parent, string name, Vector2 fallback)
    {
        var array = Property(parent, name);
        if (array.ValueKind == JsonValueKind.Undefined) return fallback;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 2)
            throw new InvalidDataException($"GLB texture {name} must contain two numbers.");
        var value = new Vector2(array[0].GetSingle(), array[1].GetSingle());
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new InvalidDataException($"GLB texture {name} must be finite.");
        return value;
    }
}
