using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace MeowSci.PebblesLib;

public sealed record GlbPrimitive(Vector3[] Positions, Vector3[] Normals, Vector2[] Uvs, uint[] Indices, int Material);

public sealed partial class GlbDocument
{
    public const int MaximumVertices = 2_000_000, MaximumIndices = 12_000_000;
    public List<GlbPrimitive> ReadMesh(int index)
    {
        var mesh = Element(Required(Root, "meshes"), index);
        var primitives = Required(mesh, "primitives");
        if (primitives.GetArrayLength() is < 1 or > 256) throw new InvalidDataException("A GLB mesh needs 1–256 primitives.");
        var result = new List<GlbPrimitive>();
        foreach (var primitive in primitives.EnumerateArray())
        {
            if (Int(primitive, "mode", 4) != 4 || primitive.TryGetProperty("extensions", out _) || primitive.TryGetProperty("targets", out _))
                throw new InvalidDataException("Export triangulated, uncompressed meshes without morph targets.");
            var attributes = Required(primitive, "attributes");
            float[] pos = ReadAttribute(Required(attributes, "POSITION").GetInt32(), 3, "VEC3");
            var positions = Enumerable.Range(0, pos.Length / 3).Select(i => new Vector3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2])).ToArray();
            uint[] indices = primitive.TryGetProperty("indices", out var accessor) ? ReadIndices(accessor.GetInt32()) : Enumerable.Range(0, positions.Length).Select(i => (uint)i).ToArray();
            if (indices.Length == 0 || indices.Length % 3 != 0 || indices.Any(i => i >= positions.Length)) throw new InvalidDataException("GLB triangle indices are invalid.");
            var normals = new Vector3[positions.Length];
            if (attributes.TryGetProperty("NORMAL", out accessor))
            {
                float[] data = ReadAttribute(accessor.GetInt32(), 3, "VEC3");
                if (data.Length != pos.Length) throw new InvalidDataException("GLB normals do not match positions.");
                for (int i = 0; i < normals.Length; i++) normals[i] = new(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
            }
            else
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var n = Vector3.Cross(positions[indices[i + 1]] - positions[indices[i]], positions[indices[i + 2]] - positions[indices[i]]);
                    normals[indices[i]] += n; normals[indices[i + 1]] += n; normals[indices[i + 2]] += n;
                }
            for (int i = 0; i < normals.Length; i++)
            {
                float length = normals[i].LengthSquared();
                if (!float.IsFinite(length)) throw new InvalidDataException("GLB normals overflow; export geometry at a smaller scale.");
                normals[i] = length > 1e-20f ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
            }
            var uv = new Vector2[positions.Length];
            if (attributes.TryGetProperty("TEXCOORD_0", out accessor))
            {
                float[] data = ReadAttribute(accessor.GetInt32(), 2, "VEC2");
                if (data.Length != uv.Length * 2) throw new InvalidDataException("GLB UVs do not match positions.");
                for (int i = 0; i < uv.Length; i++) uv[i] = new(data[i * 2], data[i * 2 + 1]);
            }
            int material = Int(primitive, "material", -1);
            if (material < -1 || material >= (Root.TryGetProperty("materials", out var materials) ? materials.GetArrayLength() : 0))
                throw new InvalidDataException("GLB material index is invalid.");
            if (material >= 0 && !attributes.TryGetProperty("TEXCOORD_0", out _))
            {
                var m = materials[material];
                bool textured = m.TryGetProperty("normalTexture", out _) || m.TryGetProperty("occlusionTexture", out _);
                if (m.TryGetProperty("pbrMetallicRoughness", out var pbr))
                    textured |= pbr.TryGetProperty("baseColorTexture", out _) || pbr.TryGetProperty("metallicRoughnessTexture", out _);
                if (textured) throw new InvalidDataException("Textured GLB geometry requires TEXCOORD_0 UVs.");
            }
            result.Add(new(positions, normals, uv, indices, material));
            CheckBudget(result);
        }
        return result;
    }
    public static void CheckBudget(IReadOnlyList<GlbPrimitive> primitives)
    {
        if (primitives.Count > 2048 || primitives.Sum(p => (long)p.Positions.Length) > MaximumVertices || primitives.Sum(p => (long)p.Indices.Length) > MaximumIndices)
            throw new InvalidDataException("GLB selection exceeds 2 million vertices, 12 million indices or 2048 primitives.");
    }
    private float[] ReadAttribute(int index, int components, string type)
    {
        var a = Accessor(index, type, MaximumVertices);
        int componentType = Required(a, "componentType").GetInt32();
        int width = ComponentSize(componentType);
        bool normalized = a.TryGetProperty("normalized", out var normalizedValue) && normalizedValue.GetBoolean();
        if (componentType != 5126 && !(type == "VEC2" && normalized && componentType is 5121 or 5123))
            throw new InvalidDataException("Positions/normals require floats; UVs support floats or normalized unsigned bytes/shorts.");
        int count = Required(a, "count").GetInt32();
        var result = new float[checked(count * components)];
        var view = AccessorBytes(a, components * width, count, out int stride);
        for (int i = 0; i < count; i++)
            for (int j = 0; j < components; j++)
            {
                var data = view.Slice(i * stride + j * width, width);
                float value = componentType switch { 5126 => BinaryPrimitives.ReadSingleLittleEndian(data), 5123 => BinaryPrimitives.ReadUInt16LittleEndian(data) / 65535f, _ => data[0] / 255f };
                if (!float.IsFinite(value)) throw new InvalidDataException("GLB attributes must be finite.");
                result[i * components + j] = value;
            }
        return result;
    }
    private uint[] ReadIndices(int index)
    {
        var a = Accessor(index, "SCALAR", MaximumIndices);
        int component = Required(a, "componentType").GetInt32();
        if (component is not (5121 or 5123 or 5125)) throw new InvalidDataException("Unsupported GLB index format.");
        int width = ComponentSize(component), count = Required(a, "count").GetInt32();
        var data = AccessorBytes(a, width, count, out int stride); var result = new uint[count];
        for (int i = 0; i < count; i++) result[i] = component switch
        { 5121 => data[i * stride], 5123 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(i * stride, 2)), _ => BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i * stride, 4)) };
        return result;
    }
    private JsonElement Accessor(int index, string type, int maximum)
    {
        var a = Element(Required(Root, "accessors"), index);
        int count = Required(a, "count").GetInt32();
        if (Required(a, "type").GetString() != type || count <= 0 || count > maximum || a.TryGetProperty("sparse", out _))
            throw new InvalidDataException("Unsupported or oversized GLB accessor; export dense vertex data.");
        return a;
    }
    private ReadOnlySpan<byte> AccessorBytes(JsonElement a, int elementSize, int count, out int stride)
    {
        int index = Required(a, "bufferView").GetInt32(); var view = BufferView(index);
        stride = Int(Element(Required(Root, "bufferViews"), index), "byteStride", elementSize);
        int start = Int(a, "byteOffset", 0);
        if (start < 0 || stride < elementSize || stride > 252 || (long)start + (long)(count - 1) * stride + elementSize > view.Length)
            throw new InvalidDataException("GLB accessor exceeds its buffer view.");
        return view[start..];
    }
    private static int ComponentSize(int type) => type switch { 5121 => 1, 5123 => 2, 5125 or 5126 => 4, _ => throw new InvalidDataException("Unsupported GLB component type.") };
}
