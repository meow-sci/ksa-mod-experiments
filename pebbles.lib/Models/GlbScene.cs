using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace MeowSci.PebblesLib;

public sealed partial class GlbDocument
{
    /// <summary>Bakes the selected/default scene's node transforms, including repeated mesh instances.</summary>
    public List<GlbPrimitive> ReadScene()
    {
        var result = new List<GlbPrimitive>();
        if (!Root.TryGetProperty("nodes", out var nodes) || nodes.GetArrayLength() == 0)
        { for (int i = 0; i < MeshCount; i++) { result.AddRange(ReadMesh(i)); CheckBudget(result); } return result; }
        if (nodes.GetArrayLength() > 4096) throw new InvalidDataException("GLB exceeds 4096 scene nodes.");
        int[] roots;
        if (Root.TryGetProperty("scenes", out var scenes) && scenes.GetArrayLength() > 0)
        {
            var scene = Element(scenes, Int(Root, "scene", 0));
            roots = scene.TryGetProperty("nodes", out var sceneNodes) ? sceneNodes.EnumerateArray().Select(n => n.GetInt32()).ToArray() : [];
        }
        else
        {
            var children = nodes.EnumerateArray().Where(n => n.TryGetProperty("children", out _))
                .SelectMany(n => n.GetProperty("children").EnumerateArray()).Select(n => n.GetInt32()).ToHashSet();
            roots = Enumerable.Range(0, nodes.GetArrayLength()).Where(n => !children.Contains(n)).ToArray();
        }
        var visited = new HashSet<int>();
        void Visit(int index, Matrix4x4 parent, int depth)
        {
            if (depth > 64 || !visited.Add(index)) throw new InvalidDataException("GLB scene contains a cycle or repeated node parent.");
            var node = Element(nodes, index);
            if (node.TryGetProperty("skin", out _) || node.TryGetProperty("extensions", out _)) throw new InvalidDataException("Export a static GLB without skins or node extensions.");
            var world = NodeMatrix(node) * parent;
            if (node.TryGetProperty("mesh", out var mesh))
            {
                foreach (var primitive in ReadMesh(mesh.GetInt32())) result.Add(Transform(primitive, world));
                CheckBudget(result);
            }
            if (node.TryGetProperty("children", out var children)) foreach (var child in children.EnumerateArray()) Visit(child.GetInt32(), world, depth + 1);
        }
        foreach (int index in roots) Visit(index, Matrix4x4.Identity, 0);
        if (result.Count == 0) throw new InvalidDataException("The default GLB scene has no static mesh geometry; choose an individual mesh.");
        return result;
    }
    private static Matrix4x4 NodeMatrix(JsonElement node)
    {
        static float[] Values(JsonElement n, string name, float[] fallback)
        {
            if (!n.TryGetProperty(name, out var value)) return fallback;
            var values = value.EnumerateArray().Select(v => v.GetSingle()).ToArray();
            if (values.Length != fallback.Length || values.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid GLB node transform.");
            return values;
        }
        if (node.TryGetProperty("matrix", out _))
        {
            var m = Values(node, "matrix", new float[16]);
            if (m[3] != 0 || m[7] != 0 || m[11] != 0 || m[15] != 1) throw new InvalidDataException("GLB node matrix must be affine.");
            return new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
        }
        var t = Values(node, "translation", [0, 0, 0]); var s = Values(node, "scale", [1, 1, 1]); var r = Values(node, "rotation", [0, 0, 0, 1]);
        var q = new Quaternion(r[0], r[1], r[2], r[3]);
        if (q.LengthSquared() < 1e-12f) throw new InvalidDataException("Invalid GLB node quaternion.");
        return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(q)) * Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
    }
    private static GlbPrimitive Transform(GlbPrimitive primitive, Matrix4x4 matrix)
    {
        if (!Matrix4x4.Invert(matrix, out var inverse)) throw new InvalidDataException("GLB node transform is singular.");
        var positions = primitive.Positions.Select(p => Vector3.Transform(p, matrix)).ToArray();
        var normalMatrix = Matrix4x4.Transpose(inverse);
        var normals = primitive.Normals.Select(n => Vector3.Normalize(Vector3.TransformNormal(n, normalMatrix))).ToArray();
        if (positions.Concat(normals).Any(v => !float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))) throw new InvalidDataException("GLB transformed geometry is invalid.");
        var indices = (uint[])primitive.Indices.Clone();
        if (matrix.GetDeterminant() < 0) for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        return new(positions, normals, primitive.Uvs, indices, primitive.Material);
    }
}
