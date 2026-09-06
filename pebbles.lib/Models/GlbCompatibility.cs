using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MeowSci.PebblesLib;

/// <summary>Explicit appearance fallbacks, with texture mapping/source policy handled separately.</summary>
public static class GlbCompatibility
{
    private static readonly Dictionary<string, string> MaterialFallbacks = new(StringComparer.Ordinal)
    {
        ["KHR_materials_pbrSpecularGlossiness"] = "specular color/detail (diffuse artwork retained; scalar glossiness approximated as roughness)",
        ["KHR_materials_specular"] = "custom specular highlights",
        ["KHR_materials_ior"] = "custom index of refraction",
        ["KHR_materials_clearcoat"] = "clearcoat",
        ["KHR_materials_sheen"] = "sheen",
        ["KHR_materials_anisotropy"] = "anisotropic highlights",
        ["KHR_materials_iridescence"] = "iridescence",
        ["KHR_materials_transmission"] = "glass transmission (rendered solid)",
        ["KHR_materials_volume"] = "volume absorption/refraction",
        ["KHR_materials_dispersion"] = "dispersion",
        ["KHR_materials_unlit"] = "unlit shading (normal game lighting is used)",
        ["KHR_materials_emissive_strength"] = "emissive glow"
    };

    internal static JsonElement SpecularGlossiness(JsonElement material) =>
        GlbTextureMapping.Property(GlbTextureMapping.Property(material, "extensions"), "KHR_materials_pbrSpecularGlossiness");

    public static void RequiredExtensions(JsonElement root)
    {
        if (!root.TryGetProperty("extensionsRequired", out var required)) return;
        foreach (var extension in required.EnumerateArray())
        {
            string name = extension.GetString() ?? "(unnamed)";
            if (!MaterialFallbacks.ContainsKey(name) && name is not ("KHR_texture_transform" or "KHR_texture_basisu" or "EXT_texture_webp")) throw Unsupported(name);
        }
    }

    public static List<string> MaterialWarnings(JsonElement material)
    {
        var warnings = new List<string>();
        if (material.ValueKind != JsonValueKind.Object) return warnings;
        string label = material.TryGetProperty("name", out var name) ? name.GetString() ?? "Material" : "Material";
        if (material.TryGetProperty("extensions", out var extensions))
            foreach (var extension in extensions.EnumerateObject())
            {
                if (!MaterialFallbacks.TryGetValue(extension.Name, out string? effect)) throw Unsupported(extension.Name);
                warnings.Add($"{label}: using base-color/PBR textures without {effect} ({extension.Name}).");
            }
        bool emissive = material.TryGetProperty("emissiveTexture", out _);
        if (material.TryGetProperty("emissiveFactor", out var factor))
            foreach (var channel in factor.EnumerateArray()) emissive |= channel.GetSingle() != 0;
        if (emissive) warnings.Add($"{label}: emissive glow is omitted; the base-color texture is retained. Bake glow into base color if it carries the visible artwork.");
        return warnings;
    }

    public static void RejectExtensions(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("extensions", out var extensions)) return;
        foreach (var extension in extensions.EnumerateObject()) throw Unsupported(extension.Name);
    }

    private static InvalidDataException Unsupported(string extension) => new(extension switch
    {
        "KHR_texture_transform" => "KHR_texture_transform is supported on textureInfo, but was found in an unsupported location.",
        "KHR_draco_mesh_compression" or "EXT_meshopt_compression" => $"{extension}: export the GLB with mesh compression disabled.",
        "EXT_texture_webp" or "KHR_texture_basisu" => $"{extension}: export embedded PNG/JPEG textures instead of WebP/KTX2.",
        _ => $"Unsupported GLB extension '{extension}'. Export a core metallic/roughness material or bake the material to a base-color image in Blender."
    });
}
