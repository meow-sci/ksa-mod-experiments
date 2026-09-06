using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeowSci.PebblesLib;

internal static class GlbTextureChecks
{
    public static void Run()
    {
        Mapping();
        Materials();
        Sources();
        LegacyDiffuse();
        Console.WriteLine("PASS: GLB transformed/secondary UVs, main-texture preservation, optional detail fallbacks, cutouts and alternate image sources.");
    }

    private static void Mapping()
    {
        using var document = GlbDocument.Parse(GlbChecks.Fixture(j =>
        {
            j["extensionsRequired"] = new JsonArray("KHR_texture_transform");
            j["materials"] = JsonNode.Parse("""[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0,"texCoord":1,"extensions":{"KHR_texture_transform":{"texCoord":2,"offset":[0.25,0.5],"scale":[2,3],"rotation":1.57079632679}}}}}]""");
            var p = j["meshes"]![0]!["primitives"]![0]!;
            p["material"] = 0;
            p["attributes"] = JsonNode.Parse("""{"POSITION":0,"TEXCOORD_2":1}""");
            j["nodes"] = JsonNode.Parse("""[{"mesh":0,"scale":[-1,1,1]}]""");
        }));
        var mesh = document.ReadMesh(0).Single();
        Near(mesh.Uvs[0], new(.25f, .5f), "Offset");
        Near(mesh.Uvs[1], new(.25f, 2.5f), "Scale then rotation then offset");
        Near(mesh.Uvs[2], new(-2.75f, .5f), "Rotated V axis");
        Check(document.ReadScene().Single().Uvs.SequenceEqual(mesh.Uvs), "Scene transforms preserve baked texture mapping");
        using var flip = JsonDocument.Parse("""{"extensions":{"KHR_texture_transform":{"offset":[0,1],"scale":[1,-1]}}}""");
        Near(GlbTextureMapping.Read(flip.RootElement).Apply(new(.2f, .3f)), new(.2f, .7f), "Negative texture scale");
        foreach (string json in new[] {
            """{"texCoord":-1}""", """{"extensions":{"KHR_texture_transform":{"scale":[1]}}}""",
            """{"extensions":{"KHR_texture_transform":{"rotation":1e39}}}""" })
        {
            using var bad = JsonDocument.Parse(json);
            Reject<InvalidDataException>(() => GlbTextureMapping.Read(bad.RootElement), "Malformed transform stays an error");
        }
        Reject<InvalidDataException>(() => new GlbTextureMapping(0, Vector2.Zero, new(float.MaxValue), 0).Apply(new(2, 2)), "Transformed UV overflow");
        using var missing = GlbDocument.Parse(GlbChecks.Fixture(j =>
        {
            j["materials"] = JsonNode.Parse("""[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0,"texCoord":2}}}]""");
            j["meshes"]![0]!["primitives"]![0]!["material"] = 0;
        }));
        Reject<InvalidDataException>(() => missing.ReadMesh(0), "Never substitute another UV set for a missing main texture UV set");
    }

    private static void Materials()
    {
        using var document = MaterialDocument("""
        {"alphaMode":"BLEND","pbrMetallicRoughness":{
          "baseColorTexture":{"index":0,"extensions":{"KHR_texture_transform":{"offset":[0.25,0]}}},
          "metallicRoughnessTexture":{"index":1,"texCoord":1},"metallicFactor":0.2,"roughnessFactor":0.7},
         "normalTexture":{"index":1},"occlusionTexture":{"index":1,"extensions":{"EXT_unknown_mapping":{}}}}
        """, j => j["samplers"] = JsonNode.Parse("""[{"wrapS":33071,"wrapT":33648}]"""));
        int decodes = 0;
        var source = new GlbPixels(2, 1, [100, 150, 200, 127, 200, 100, 50, 128]);
        var reader = new GlbMaterialReader(document, "test", (bytes, mime) =>
        { decodes++; Check(mime == "image/png" && bytes.Length > 0, "Decoder input"); return source; });
        var recipe = reader.GetMaterial(0);
        Check(decodes == 1 && recipe.NormalId == "", "Different detail UVs/extensions skip decoding and keep the main texture");
        var color = reader.Pixels[recipe.DiffuseId!];
        Check(color.Data[0] == 100 && color.Data[1] == 150 && color.Data[2] == 200, "Main pixels preserved");
        var opacity = reader.Pixels[recipe.OpacityId!];
        Check(opacity.Data[0] == 0 && opacity.Data[4] == 255, "BLEND becomes a 50% cutout");
        var packed = reader.Pixels[recipe.PbrId!].Data;
        Check(packed[0] == 255 && Math.Abs(packed[1] - 179) <= 1 && packed[2] == 51, "Skipped detail maps retain scalar material factors");
        Check(reader.Warnings.Count(w => w.StartsWith("Skipped")) == 3 && reader.Warnings.Any(w => w.Contains("wrapping")), "Every simplification is reported");
        recipe.DiffuseId = "changed";
        Check(reader.GetMaterial(0).DiffuseId != "changed" && decodes == 1, "Recipe copies and image caching");

        using var compatible = MaterialDocument("""{"pbrMetallicRoughness":{"baseColorTexture":{"index":0},"metallicRoughnessTexture":{"index":0}},"normalTexture":{"index":0},"occlusionTexture":{"index":0}}""");
        var matching = new GlbMaterialReader(compatible, "matching", (_, _) => source);
        Check(matching.GetMaterial(0).NormalId != "" && matching.Warnings.Count == 0, "Matching detail maps are retained");

        using var broken = MaterialDocument("""{"pbrMetallicRoughness":{"baseColorTexture":{"index":0}},"normalTexture":{"index":99}}""");
        Reject<InvalidOperationException>(() => new GlbMaterialReader(broken, "bad", (_, _) => source).GetMaterial(0), "Malformed detail references are not swallowed as compatibility warnings");
    }

    private static void Sources()
    {
        foreach (string extension in new[] { "KHR_texture_basisu", "EXT_texture_webp" })
        {
            using var document = MaterialDocument("""{"pbrMetallicRoughness":{"baseColorTexture":{"index":0}},"normalTexture":{"index":1}}""", j =>
            {
                j["extensionsRequired"] = new JsonArray(extension);
                j["textures"]![0]!["extensions"] = new JsonObject { [extension] = new JsonObject { ["source"] = 1 } };
                j["textures"]![1] = new JsonObject { ["extensions"] = new JsonObject { [extension] = new JsonObject { ["source"] = 1 } } };
            });
            var reader = new GlbMaterialReader(document, extension, (_, _) => new(1, 1, [10,20,30,255]));
            var material = reader.GetMaterial(0);
            Check(material.NormalId == "" && reader.Pixels[material.DiffuseId!].Data[0] == 10, "Core fallback keeps main pixels; extension-only detail is skipped");
            Check(reader.Warnings.Any(w => w.Contains("fallback")) && reader.Warnings.Any(w => w.Contains("Skipped normal")), "Source fallback warnings");
            using var only = MaterialDocument("""{"pbrMetallicRoughness":{"baseColorTexture":{"index":1}}}""", j =>
                j["textures"]![1] = new JsonObject { ["extensions"] = new JsonObject { [extension] = new JsonObject { ["source"] = 1 } } });
            try { new GlbMaterialReader(only, "only", (_, _) => throw new Exception("Must not decode")).GetMaterial(0); throw new Exception("Expected source error"); }
            catch (NotSupportedException ex) { Check(ex.Message.Contains(extension) && ex.Message.Contains("decoder"), "Main texture encoding failure identifies the missing decoder"); }
        }
    }

    private static void LegacyDiffuse()
    {
        using var document = MaterialDocument("""
        {"alphaMode":"MASK","pbrMetallicRoughness":{"baseColorTexture":{"index":99}},
         "extensions":{"KHR_materials_pbrSpecularGlossiness":{
          "diffuseTexture":{"index":0,"extensions":{"KHR_texture_transform":{"scale":[0.5,0.5]}}},
          "diffuseFactor":[0.5,1,1,0.25],"glossinessFactor":0.25,"specularGlossinessTexture":{"index":99}}}}
        """, j =>
        {
            j["extensionsRequired"] = new JsonArray("KHR_materials_pbrSpecularGlossiness", "KHR_texture_transform");
            j["meshes"]![0]!["primitives"]![0]!["material"] = 0;
        });
        var reader = new GlbMaterialReader(document, "legacy", (_, _) => new(1, 1, [255,100,50,255]));
        var material = reader.GetMaterial(0);
        Check(reader.Pixels[material.DiffuseId].Data.SequenceEqual(new byte[] {188,100,50,255}), "Legacy diffuse image/factor preserved in linear color space");
        Check(reader.Pixels[material.PbrId].Data.SequenceEqual(new byte[] {255,191,0,255}), "Legacy scalar glossiness becomes approximate roughness");
        Check(reader.Pixels[material.OpacityId].Data[0] == 0, "Legacy diffuse alpha factor feeds cutouts");
        Near(document.ReadMesh(0).Single().Uvs[1], new(.5f, 0), "Legacy diffuse transform owns the UV stream");
        Check(reader.Warnings.Any(w => w.Contains("diffuse artwork retained")), "Legacy approximation is reported");
    }

    private static GlbDocument MaterialDocument(string material, Action<JsonObject>? edit = null) => GlbDocument.Parse(GlbChecks.Fixture(j =>
    {
        j["materials"] = new JsonArray(JsonNode.Parse(material));
        j["textures"] = JsonNode.Parse("""[{"source":0,"sampler":0},{"source":1}]""");
        j["images"] = JsonNode.Parse("""[{"bufferView":0,"mimeType":"image/png"},{"bufferView":0,"mimeType":"image/png"}]""");
        j["samplers"] = JsonNode.Parse("""[{}]""");
        edit?.Invoke(j);
    }));
    private static void Near(Vector2 actual, Vector2 expected, string message) => Check(Vector2.Distance(actual, expected) < 1e-5f, message);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject<T>(Action action, string message) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception(message); }
}
