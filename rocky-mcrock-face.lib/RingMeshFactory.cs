using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.GltfApi;
using Brutal.VulkanApi.Abstractions;
using KSA;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>
/// Converts any loaded MeshReference into one the ring renderer can draw.
///
/// The ring pipeline consumes MeshReference.DeviceMesh — a per-attribute-stream
/// SimpleVkMesh that the game only builds for meshes loaded with Simple = true
/// (the stock ring rocks). Part/subpart meshes are atlas-loaded interleaved into a
/// shared buffer, so their DeviceMesh is null; flipping their flags in place would
/// break part rendering and IVA raytracing. Instead they are cloned into a private
/// Simple MeshReference that shares the retained CPU-side HostPrimitives, and a
/// SimpleVkMesh is uploaded for primitive 0 (the only primitive rings ever draw).
///
/// Clones are cached for the factory's lifetime: MeshReference has a finalizer that
/// destroys its GPU buffers, so dropping a clone while the renderer references it
/// would free memory in use.
/// </summary>
public sealed class RingMeshFactory : IDisposable
{
    private static readonly FieldInfo? HostPrimitivesField = typeof(MeshReference).GetField(
        "<HostPrimitives>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly Dictionary<string, MeshReference> _clones = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a MeshReference with a valid DeviceMesh for the given source mesh, or
    /// null with an error message. Simple meshes are returned as-is; interleaved ones
    /// are cloned and uploaded on first use.
    /// </summary>
    public MeshReference? GetRingUsable(MeshReference source, out string? error)
    {
        error = null;
        if (source.Simple && source.DevicePrimitives is { Length: > 0 } && source.DevicePrimitives[0] != null)
            return source;

        if (_clones.TryGetValue(source.Id, out var cached))
            return cached;

        if (HostPrimitivesField == null)
        {
            error = "MeshReference.HostPrimitives backing field not found (game update?)";
            return null;
        }
        if (source.HostPrimitives is not { Length: > 0 } || source.HostPrimitives[0] == null)
        {
            error = $"mesh '{source.Id}' has no retained CPU-side geometry";
            return null;
        }

        var clone = new MeshReference
        {
            Id = $"rocky_mcrock_face/{source.Id}",
            Simple = true,
            Interleaved = false,
            PrimitiveCount = 1,
            BoundingSphereRadius = source.BoundingSphereRadius,
        };
        HostPrimitivesField.SetValue(clone, source.HostPrimitives);
        if (clone.BoundingSphereRadius <= 0.0)
        {
            var host = source.HostPrimitives[0];
            clone.BoundingSphereRadius = Math.Max(host.PositionMaximum.Length(), host.PositionMinimum.Length());
        }
        if (clone.BoundingSphereRadius <= 0.0)
        {
            error = $"mesh '{source.Id}' has a zero bounding radius";
            return null;
        }

        try
        {
            var renderer = Program.GetRenderer();
            // StagingPool disposal submits and waits, so the upload is complete on return.
            using var stagingPool = renderer.Allocator.CreateStagingPool(renderer.GraphicsAndCompute, 1);
            clone.Bind(renderer, stagingPool);
        }
        catch (Exception ex)
        {
            error = $"GPU upload failed for mesh '{source.Id}': {ex.Message}";
            return null;
        }

        if (clone.DevicePrimitives is not { Length: > 0 } || clone.DevicePrimitives[0] == null)
        {
            error = $"mesh '{source.Id}' produced no device mesh";
            return null;
        }

        Console.WriteLine($"rocky-mcrock-face: converted mesh '{source.Id}' for ring use " +
                          $"({clone.DeviceMesh.IndexCount} indices, radius {clone.BoundingSphereRadius:F2} m)");
        _clones[source.Id] = clone;
        return clone;
    }

    /// <summary>
    /// Loads a mesh out of a glTF-file asset (kitten, helmet, MMU...) into a private
    /// ring-usable MeshReference — the same import the game runs for the stock ring
    /// rocks (MeshReference.Load: Position/Normal/Uv0, missing attributes defaulted;
    /// skinned meshes come out in bind pose). Cached under the entry id.
    /// </summary>
    public MeshReference? GetRingUsableFromGltf(GltfMeshEntry entry, out string? error)
    {
        error = null;
        if (_clones.TryGetValue(entry.Id, out var cached))
            return cached;

        try
        {
            using var gltfLoader = new GltfLoader(entry.FilePath);
            var clone = new MeshReference
            {
                Id = $"rocky_mcrock_face/{entry.Id}",
                Simple = true,
                Interleaved = false,
            };
            clone.Load(gltfLoader, entry.MeshIndex, createDeviceMesh: false);
            if (clone.HostPrimitives is not { Length: > 0 } || clone.HostPrimitives[0] == null)
            {
                error = $"glTF mesh '{entry.Id}' loaded no geometry";
                return null;
            }
            if (clone.BoundingSphereRadius <= 0.0)
            {
                error = $"glTF mesh '{entry.Id}' has a zero bounding radius";
                return null;
            }

            var renderer = Program.GetRenderer();
            using var stagingPool = renderer.Allocator.CreateStagingPool(renderer.GraphicsAndCompute, 1);
            clone.Bind(renderer, stagingPool);
            if (clone.DevicePrimitives is not { Length: > 0 } || clone.DevicePrimitives[0] == null)
            {
                error = $"glTF mesh '{entry.Id}' produced no device mesh";
                return null;
            }

            Console.WriteLine($"rocky-mcrock-face: converted glTF mesh '{entry.Id}' for ring use " +
                              $"({clone.DeviceMesh.IndexCount} indices, radius {clone.BoundingSphereRadius:F2})");
            _clones[entry.Id] = clone;
            return clone;
        }
        catch (Exception ex)
        {
            error = $"glTF load failed for '{entry.Id}': {ex.Message}";
            return null;
        }
    }

    /// <summary>Index count of an already-converted mesh's first primitive (0 if not converted).</summary>
    public int GetConvertedIndexCount(string cacheId)
    {
        return _clones.TryGetValue(cacheId, out var clone)
               && clone.DevicePrimitives is { Length: > 0 } && clone.DevicePrimitives[0] != null
            ? clone.DevicePrimitives[0].IndexCount
            : 0;
    }

    /// <summary>
    /// Destroys cached clones whose source mesh id is not in <paramref name="keepSourceIds"/>.
    /// Only call right after a renderer rebuild — the freshly built ring data references
    /// exactly the clones that were resolved for it, so anything outside the keep set is
    /// unreferenced and its GPU buffers can be freed. Returns the number pruned.
    /// </summary>
    public int PruneExcept(IReadOnlySet<string> keepSourceIds)
    {
        var pruned = 0;
        foreach (var sourceId in new List<string>(_clones.Keys))
        {
            if (keepSourceIds.Contains(sourceId)) continue;
            try { _clones[sourceId].Dispose(); }
            catch (Exception ex) { Console.WriteLine($"rocky-mcrock-face: clone dispose failed: {ex.Message}"); }
            _clones.Remove(sourceId);
            pruned++;
        }
        if (pruned > 0)
            Console.WriteLine($"rocky-mcrock-face: freed {pruned} unused converted mesh(es)");
        return pruned;
    }

    /// <summary>
    /// Destroys all converted meshes. Only call after the renderer no longer references
    /// them (defaults restored + renderer rebuilt).
    /// </summary>
    public void Dispose()
    {
        foreach (var clone in _clones.Values)
        {
            try { clone.Dispose(); }
            catch (Exception ex) { Console.WriteLine($"rocky-mcrock-face: clone dispose failed: {ex.Message}"); }
        }
        _clones.Clear();
    }
}
