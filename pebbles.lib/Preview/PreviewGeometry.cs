using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Brutal;
using Brutal.VulkanApi;
using KSA;
using RenderCore;

namespace MeowSci.PebblesLib;

internal sealed class PreviewGeometry
{
    internal sealed record Draw(PreviewVertex[] Vertices, uint[] Indices, TextureReference[] Textures, Vector4 Maps, Vector4 Options);
    internal List<Draw> Draws { get; } = new();
    internal Vector3 Min { get; private set; } = new(float.PositiveInfinity);
    internal Vector3 Max { get; private set; } = new(float.NegativeInfinity);

    internal static PreviewGeometry Prepare(ObjectRecipe recipe, ClutterAssets assets, int lodIndex)
    {
        if (lodIndex < 0 || lodIndex >= recipe.Lods.Count) throw new InvalidOperationException("Select an available LOD to preview.");
        var lod = recipe.Lods[lodIndex];
        if (lod.MeshIds.Count == 0) throw new InvalidOperationException("This LOD has no meshes. Choose a mesh to preview.");
        var meshes = lod.MeshIds.Select(assets.ResolveMesh).ToArray();
        var slots = new GlbMaterialSlots(meshes.Select(m => (m.Id, m.PrimitiveMaterialIds)));
        if (lod.Materials.Count > 1 && lod.Materials.Count != slots.Count)
            throw new InvalidOperationException($"Meshes require {slots.Count} materials, but this LOD has {lod.Materials.Count}. Use one material for all or match the slots.");
        var transform = recipe.Transform;
        if (!Finite(transform.Scale.Vector) || transform.Scale.X <= 0 || transform.Scale.Y <= 0 || transform.Scale.Z <= 0)
            throw new InvalidOperationException("Mesh scales must be finite and positive, matching Apply.");
        var radians = transform.RotationDegrees.Vector * (MathF.PI / 180);
        var matrix = Matrix4x4.CreateScale(transform.Scale.Vector) * Matrix4x4.CreateRotationX(radians.X)
            * Matrix4x4.CreateRotationY(radians.Y) * Matrix4x4.CreateRotationZ(radians.Z)
            * Matrix4x4.CreateTranslation(transform.Position.Vector);
        if (!Matrix4x4.Invert(matrix, out var inverse)) throw new InvalidOperationException("Mesh scale must be nonzero.");
        var normalMatrix = Matrix4x4.Transpose(inverse);
        var result = new PreviewGeometry();
        long totalVertices = 0, totalIndices = 0;
        foreach (var mesh in meshes)
        {
            if (mesh.HostPrimitives == null || mesh.HostPrimitives.Length != mesh.PrimitiveCount)
                throw new InvalidOperationException($"Mesh {mesh.Id} has no complete CPU geometry.");
            for (int p = 0; p < mesh.PrimitiveCount; p++)
            {
                var primitive = mesh.HostPrimitives[p];
                totalVertices += primitive.VertexCount; totalIndices += primitive.IndexCount;
                if (totalVertices > 2_000_000 || totalIndices > 12_000_000)
                    throw new InvalidOperationException("Preview exceeds the two-million-vertex / twelve-million-index budget.");
                var keys = primitive.GetVertexKeys();
                if (!keys.Contains(MeshAttribute.Position) || !keys.Contains(MeshAttribute.Normal) || !keys.Contains(MeshAttribute.Uv0))
                    throw new InvalidOperationException($"Mesh {mesh.Id} requires position, normal and UV0 streams.");
                if (primitive.GetVertexList(MeshAttribute.Position).Stride != ByteSize.Of<Vector3>() ||
                    primitive.GetVertexList(MeshAttribute.Normal).Stride != ByteSize.Of<Vector3>() ||
                    primitive.GetVertexList(MeshAttribute.Uv0).Stride != ByteSize.Of<Vector2>())
                    throw new InvalidOperationException($"Mesh {mesh.Id} has unsupported vertex formats.");
                var positions = primitive.GetVertexSpan<Vector3>(MeshAttribute.Position);
                var normals = primitive.GetVertexSpan<Vector3>(MeshAttribute.Normal);
                var uvs = primitive.GetVertexSpan<Vector2>(MeshAttribute.Uv0);
                if (positions.Length == 0 || normals.Length != positions.Length || uvs.Length != positions.Length)
                    throw new InvalidOperationException($"Mesh {mesh.Id} has inconsistent vertex streams.");
                var vertices = new PreviewVertex[positions.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var position = Vector3.Transform(positions[i], matrix);
                    var normal = Vector3.TransformNormal(normals[i], normalMatrix);
                    if (!Finite(position) || !Finite(normal) || !float.IsFinite(uvs[i].X) || !float.IsFinite(uvs[i].Y))
                        throw new InvalidOperationException($"Mesh {mesh.Id} contains nonfinite geometry.");
                    vertices[i] = new() { Position = position, Normal = normal.LengthSquared() > 1e-16f ? Vector3.Normalize(normal) : Vector3.UnitY, Uv = uvs[i] };
                    result.Min = Vector3.Min(result.Min, position); result.Max = Vector3.Max(result.Max, position);
                }
                var indices = ReadIndices(primitive);
                if (indices.Length == 0 || indices.Length % 3 != 0 || indices.Any(i => i >= vertices.Length))
                    throw new InvalidOperationException($"Mesh {mesh.Id} has invalid triangle indices.");
                if (matrix.GetDeterminant() < 0)
                    for (int i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                int slot = slots.Slot(mesh.Id, mesh.PrimitiveMaterialIds[p]);
                var material = lod.Materials.Count == 0 ? new MaterialRecipe() : lod.Materials[lod.Materials.Count == 1 ? 0 : slot];
                string[] ids = [material.DiffuseId, material.NormalId, material.PbrId, material.OpacityId, material.ThicknessId];
                var textures = ids.Select(id => string.IsNullOrWhiteSpace(id)
                    ? TextureReference.EmptyWhite ?? throw new InvalidOperationException("Default preview texture is unavailable.")
                    : assets.ResolveTexture(id)).ToArray();
                if (textures.Any(t => t.ImageView.IsNull())) throw new InvalidOperationException("A preview texture has no GPU image.");
                result.Draws.Add(new(vertices, indices, textures,
                    new Vector4(Used(ids[0]), Used(ids[1]), Used(ids[2]), Used(ids[3])),
                    new Vector4(Used(ids[4]), material.SourceColors ? 1 : 0, 0, 0)));
            }
        }
        return result;
    }

    private static float Used(string id) => string.IsNullOrWhiteSpace(id) ? 0 : 1;
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static uint[] ReadIndices(MeshAsset primitive)
    {
        var buffer = primitive.IndexBuffer ?? throw new InvalidOperationException("Preview requires indexed triangles.");
        if (buffer.Stride == ByteSize.Of<uint>()) return buffer.AsSpan<uint>().ToArray();
        if (buffer.Stride == ByteSize.Of<ushort>())
        {
            var source = buffer.AsSpan<ushort>();
            var result = new uint[source.Length];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }
        throw new InvalidOperationException("Preview supports 16-bit and 32-bit triangle indices.");
    }
}
