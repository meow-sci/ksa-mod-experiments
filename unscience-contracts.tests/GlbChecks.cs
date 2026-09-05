using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MeowSci.PebblesLib;

internal static class GlbChecks
{
    public static void Run()
    {
        GlbPixelChecks.Run();
        foreach (int component in new[] { 5121, 5123, 5125 })
        {
            using var document = GlbDocument.Parse(Fixture(component: component));
            var mesh = document.ReadMesh(0).Single();
            Check(mesh.Positions[1] == Vector3.UnitX && mesh.Indices.SequenceEqual(new uint[] { 0, 1, 2 }), "Unsigned index formats and interleaved positions");
            Near(mesh.Normals[0], Vector3.UnitZ, "Generated normals");
            Check(mesh.Uvs[1] == Vector2.UnitX && mesh.Material == -1, "Normalized UVs and default material");
        }
        using (var document = GlbDocument.Parse(Fixture(j =>
        {
            j["nodes"] = JsonNode.Parse("""[{"translation":[10,0,0],"children":[1]},{"mesh":0,"scale":[-2,3,1]},{"mesh":0,"translation":[0,5,0]}]""");
            j["scenes"] = JsonNode.Parse("""[{"nodes":[0,2]}]""");
        })))
        {
            var scene = document.ReadScene();
            Check(scene.Count == 2, "Repeated mesh instances preserved");
            Near(scene[0].Positions[1], new(8, 0, 0), "Parent translation and child scale baked");
            Near(scene[0].Positions[2], new(10, 3, 0), "Nonuniform scale baked");
            Near(scene[1].Positions[0], new(0, 5, 0), "Second instance transform");
            Check(scene[0].Indices.SequenceEqual(new uint[] { 0, 2, 1 }), "Mirrored winding corrected");
            Near(scene[0].Normals[0], Vector3.UnitZ, "Inverse-transpose normals");
            Near(document.ReadMesh(0)[0].Positions[1], Vector3.UnitX, "Individual mesh keeps local coordinates");
        }
        using (var document = GlbDocument.Parse(Fixture(j =>
        {
            j["nodes"] = JsonNode.Parse("""[{"mesh":0,"matrix":[1,0,0,0,0,1,0,0,0,0,1,0,2,3,4,1]}]""");
        }))) Near(document.ReadScene()[0].Positions[0], new(2, 3, 4), "glTF column-major matrix translation");
        using (var document = GlbDocument.Parse(Fixture(j => j["nodes"] = JsonNode.Parse("""[{"mesh":0,"rotation":[0,0,0.7071067811865476,0.7071067811865476]}]"""))))
            Near(document.ReadScene()[0].Positions[1], Vector3.UnitY, "Quaternion rotation");
        using (var document = GlbDocument.Parse(Fixture(j => ((JsonObject)j["meshes"]![0]!["primitives"]![0]!).Remove("indices"))))
            Check(document.ReadMesh(0)[0].Indices.SequenceEqual(new uint[] { 0, 1, 2 }), "Non-indexed triangles");

        RejectBytes(bytes => bytes[..^1]);
        RejectBytes(bytes => { bytes[4] = 1; return bytes; });
        RejectBytes(bytes => { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), uint.MaxValue); return bytes; });
        RejectJson(j => j["buffers"]![0]!["uri"] = "elsewhere.bin");
        RejectJson(j => j["extensionsRequired"] = new JsonArray("KHR_draco_mesh_compression"));
        RejectJson(j => j["bufferViews"]![0]!["byteLength"] = 1);
        RejectJson(j => j["accessors"]![0]!["count"] = GlbDocument.MaximumVertices + 1);
        RejectJson(j => j["accessors"]![0]!["sparse"] = new JsonObject());
        RejectJson(j => j["meshes"]![0]!["primitives"]![0]!["mode"] = 1);
        RejectJson(j => j["meshes"]![0]!["primitives"]![0]!["targets"] = new JsonArray());
        RejectJson(j => j["nodes"] = JsonNode.Parse("""[{"mesh":0,"children":[0]}]"""));
        RejectJson(j => j["nodes"] = JsonNode.Parse("""[{"mesh":0,"scale":[0,1,1]}]"""));
        RejectJson(j => j["nodes"] = JsonNode.Parse("""[{"mesh":0,"skin":0}]"""));
        RejectJson(j => j["nodes"] = JsonNode.Parse("""[{"children":[2]},{"children":[2]},{"mesh":0}]"""));
        RejectJson(j =>
        {
            ((JsonObject)j["meshes"]![0]!["primitives"]![0]!["attributes"]!).Remove("TEXCOORD_0");
            j["meshes"]![0]!["primitives"]![0]!["material"] = 0;
            j["materials"] = JsonNode.Parse("""[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0}}}]""");
        });
        RejectBinary(data => BinaryPrimitives.WriteSingleLittleEndian(data, float.NaN));
        RejectBinary(data => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 99));
        RejectBinary(data => { BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(16), 1e30f); BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(36), 1e30f); });

        byte[] first = Fixture(), second = Fixture(j => j["meshes"]![0]!["name"] = "Renamed");
        using var a = GlbDocument.Parse(first); using var b = GlbDocument.Parse(second);
        Check(a.Hash != b.Hash, "Changed content has a new identity");
        string path = Path.Combine(Path.GetTempPath(), "missing é # % model.glb");
        var source = new GlbIdentity(path, a.Hash, "");
        var parsed = GlbIdentity.Parse(source.MeshId(-1));
        Check(parsed.Path == path && parsed.Hash == a.Hash && parsed.Part == "/mesh/-1", "Exact Unicode path/hash roundtrip without file access");
        var recipe = new ObjectRecipe(); recipe.Lods[0].MeshIds = [source.MeshId(-1)];
        var restored = RecipeCopy.Clone(recipe);
        Check(restored.Lods[0].MeshIds[0] == source.MeshId(-1), "GLB identity survives detached recipe save/load");
        Check(GlbIdentity.Label(source.MeshId(-1)).Contains("missing é # % model.glb"), "Friendly source labels");
        Reject(() => GlbIdentity.Parse(GlbIdentity.Prefix + "broken"));
        var other = new GlbIdentity(path, b.Hash, "");
        (string Id, int[] Materials)[] group = [(source.MeshId(0), [0, 1]), (other.MeshId(0), [0]), (source.MeshId(1), [0]), ("stock-a", [0]), ("stock-b", [0])];
        var slots = new GlbMaterialSlots(group); var reverse = new GlbMaterialSlots(group.Reverse());
        Check(slots.Count == 4, "Separate files retain separate material zero; same-source and stock slots still share");
        Check(slots.Slot(source.MeshId(0), 0) == slots.Slot(source.MeshId(1), 0), "Source material sharing");
        Check(slots.Slot(source.MeshId(0), 0) != slots.Slot(other.MeshId(0), 0), "Source material isolation");
        foreach (var mesh in group) foreach (int material in mesh.Materials)
            Check(slots.Slot(mesh.Id, material) == reverse.Slot(mesh.Id, material), "Slot ordering independent of import order");
        Console.WriteLine("PASS: GLB bounded parsing, indices/UVs/normals, scene transforms/instances, malformed input rejection and exact detached source identities.");
    }
    private static byte[] Fixture(Action<JsonObject>? edit = null, int component = 5123, Action<byte[]>? binaryEdit = null)
    {
        int width = component == 5121 ? 1 : component == 5123 ? 2 : 4;
        var data = new byte[60 + width * 3];
        Vector3[] points = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
        for (int i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 16), points[i].X);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 16 + 4), points[i].Y);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 16 + 8), points[i].Z);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(48 + i * 4), i == 1 ? ushort.MaxValue : (ushort)0);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50 + i * 4), i == 2 ? ushort.MaxValue : (ushort)0);
            if (width == 1) data[60 + i] = (byte)i;
            else if (width == 2) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60 + i * width), (ushort)i);
            else BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(60 + i * width), (uint)i);
        }
        binaryEdit?.Invoke(data);
        var root = JsonSerializer.SerializeToNode(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = data.Length } },
            bufferViews = new object[] { new { buffer = 0, byteOffset = 0, byteLength = 48, byteStride = 16 }, new { buffer = 0, byteOffset = 48, byteLength = 12 }, new { buffer = 0, byteOffset = 60, byteLength = width * 3 } },
            accessors = new object[] { new { bufferView = 0, componentType = 5126, count = 3, type = "VEC3" }, new { bufferView = 1, componentType = 5123, count = 3, type = "VEC2", normalized = true }, new { bufferView = 2, componentType = component, count = 3, type = "SCALAR" } },
            meshes = new[] { new { name = "Triangle", primitives = new[] { new { attributes = new { POSITION = 0, TEXCOORD_0 = 1 }, indices = 2 } } } }
        })!.AsObject();
        edit?.Invoke(root);
        byte[] json = Encoding.UTF8.GetBytes(root.ToJsonString());
        int jsonSize = (json.Length + 3) & ~3, binarySize = (data.Length + 3) & ~3;
        byte[] file = new byte[12 + 8 + jsonSize + 8 + binarySize];
        Write(0, 0x46546C67); Write(4, 2); Write(8, (uint)file.Length);
        Write(12, (uint)jsonSize); Write(16, 0x4E4F534A);
        file.AsSpan(20, jsonSize).Fill(32); json.CopyTo(file, 20);
        Write(20 + jsonSize, (uint)binarySize); Write(24 + jsonSize, 0x004E4942); data.CopyTo(file, 28 + jsonSize);
        return file;
        void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(offset), value);
    }
    private static void RejectBytes(Func<byte[], byte[]> edit) => Reject(() => { using var document = GlbDocument.Parse(edit(Fixture())); document.ReadScene(); });
    private static void RejectJson(Action<JsonObject> edit) => Reject(() => { using var document = GlbDocument.Parse(Fixture(edit)); document.ReadScene(); });
    private static void RejectBinary(Action<byte[]> edit) => Reject(() => { using var document = GlbDocument.Parse(Fixture(binaryEdit: edit)); document.ReadScene(); });
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(Vector3 value, Vector3 expected, string message) => Check(Vector3.Distance(value, expected) < 1e-5f, message);
    private static void Reject(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid GLB was accepted."); }
}
