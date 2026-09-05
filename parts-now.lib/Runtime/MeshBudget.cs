using System;
using KSA;
using MeowSci.Unscience.Contracts;

namespace MeowSci.PartsNowLib;

/// <summary>Game-thread mesh budget. Startup is allocation-free; loaded meshes retain stable offsets.</summary>
public static class MeshBudget
{
    private const uint BytesPerMiB = 1024u * 1024u;
    private static bool _reserved;
    private static uint _watermarkVertexBytes, _watermarkIndexBytes;
    private static uint _baselineVertexBytes, _baselineIndexBytes;
    private static readonly ReleasedRanges VertexRanges = new(), IndexRanges = new();
    public static bool Reserved => _reserved;
    public static bool Armed => false;
    public static bool IsUsable => _reserved;
    public static string? FailureReason { get; private set; }
    public static uint VertexHeadroomBytes => checked((uint)Math.Max(0, PartsNowSettings.VertexHeadroomMiB) * BytesPerMiB);
    public static uint IndexHeadroomBytes => checked((uint)Math.Max(0, PartsNowSettings.IndexHeadroomMiB) * BytesPerMiB);
    public static uint WatermarkVertexBytes => _watermarkVertexBytes;
    public static uint WatermarkIndexBytes => _watermarkIndexBytes;
    public static uint AllocatedVertexBytes => (uint)DeviceMeshInterleaved.Shared.VertexAllocation.BufferSize;
    public static uint AllocatedIndexBytes => (uint)DeviceMeshInterleaved.Shared.IndexAllocation.BufferSize;
    public static uint UsedVertexBytes => DeviceMeshInterleaved.Shared.RunningVertexBufferSize;
    public static uint UsedIndexBytes => DeviceMeshInterleaved.Shared.RunningIndexBufferSize;
    public static bool WithinBudget => UsedVertexBytes <= AllocatedVertexBytes && UsedIndexBytes <= AllocatedIndexBytes;
    // Logical capacity is a limit, not a permanent GPU reservation.
    public static uint FreeVertexBytes => Remaining(_watermarkVertexBytes, VertexHeadroomBytes, UsedVertexBytes);
    public static uint FreeIndexBytes => Remaining(_watermarkIndexBytes, IndexHeadroomBytes, UsedIndexBytes);
    public static ulong LeakedVertexBytes => VertexRanges.Bytes;
    public static ulong LeakedIndexBytes => IndexRanges.Bytes;
    public static bool LeakWarningTripped => LeakedVertexBytes * 2 > VertexHeadroomBytes || LeakedIndexBytes * 2 > IndexHeadroomBytes;
    private static uint Remaining(uint baseline, uint allowance, uint used) => (uint)Math.Max(0L, (long)baseline + allowance - used);

    /// <summary>Compatibility entry point. Does not mutate the game's startup allocation counters.</summary>
    public static void Reserve() { }
    public static void OnFirstFrame()
    {
        if (_reserved || !DeviceMeshInterleaved.Shared.IsBuilt) return;
        _watermarkVertexBytes = UsedVertexBytes; _watermarkIndexBytes = UsedIndexBytes;
        _baselineVertexBytes = AllocatedVertexBytes; _baselineIndexBytes = AllocatedIndexBytes;
        _reserved = true;
    }

    public static void EnsureCapacity()
    {
        if (!_reserved) throw new InvalidOperationException("The game mesh buffers are not built yet.");
        if ((ulong)UsedVertexBytes > (ulong)_watermarkVertexBytes + VertexHeadroomBytes ||
            (ulong)UsedIndexBytes > (ulong)_watermarkIndexBytes + IndexHeadroomBytes)
            throw new InvalidOperationException("This load exceeds the configured runtime mesh budget.");
        if (WithinBudget) return;
        try
        {
            SharedMeshBuffers.Resize(Math.Max(AllocatedVertexBytes, UsedVertexBytes), Math.Max(AllocatedIndexBytes, UsedIndexBytes));
            FailureReason = null;
        }
        catch (Exception ex) { FailureReason = ex.Message; throw; }
    }

    public readonly record struct Cursors(uint VertexBytes, uint IndexBytes);
    public static Cursors SnapshotCursors() => new(UsedVertexBytes, UsedIndexBytes);
    public static bool RestoreCursors(Cursors cursors)
    {
        if (!_reserved || cursors.VertexBytes < _watermarkVertexBytes || cursors.IndexBytes < _watermarkIndexBytes) return false;
        DeviceMeshInterleaved.Shared.RunningVertexBufferSize = cursors.VertexBytes;
        DeviceMeshInterleaved.Shared.RunningIndexBufferSize = cursors.IndexBytes;
        return true;
    }
    public static Action CaptureMeshRelease(MeshReference mesh)
    {
        var releases = new System.Collections.Generic.List<Action>();
        if (mesh.DeviceMeshesInterleaved == null) return () => { };
        foreach (var primitive in mesh.DeviceMeshesInterleaved)
        {
            if (primitive == null) continue;
            uint vs = (uint)primitive.VerticesOffset, vl = (uint)primitive.VerticesSize;
            uint ins = (uint)primitive.IndicesOffset, inl = (uint)primitive.IndicesSize;
            releases.Add(() => { VertexRanges.Add(vs, vl); IndexRanges.Add(ins, inl); });
        }
        return () => { foreach (var release in releases) release(); };
    }
    public static void TrimReleased()
    {
        if (!_reserved) return;
        DeviceMeshInterleaved.Shared.RunningVertexBufferSize = VertexRanges.Trim(UsedVertexBytes, _watermarkVertexBytes);
        DeviceMeshInterleaved.Shared.RunningIndexBufferSize = IndexRanges.Trim(UsedIndexBytes, _watermarkIndexBytes);
        if (RuntimeModRegistry.Count != 0) return;
        // External allocations beyond our watermark remain owned by their creators.
        uint vertex = Math.Max(_baselineVertexBytes, UsedVertexBytes), index = Math.Max(_baselineIndexBytes, UsedIndexBytes);
        if (vertex == AllocatedVertexBytes && index == AllocatedIndexBytes) return;
        try { SharedMeshBuffers.Resize(vertex, index); FailureReason = null; }
        catch (Exception ex) { if (FailureReason != ex.Message) Console.WriteLine($"parts-now: mesh cleanup pending: {ex.Message}"); FailureReason = ex.Message; }
    }
    public static void MeasureMesh(MeshReference? mesh, out ulong vertexBytes, out ulong indexBytes)
    {
        vertexBytes = indexBytes = 0;
        if (mesh?.DeviceMeshesInterleaved == null) return;
        foreach (var primitive in mesh.DeviceMeshesInterleaved)
            if (primitive != null) { vertexBytes += primitive.VerticesSize; indexBytes += primitive.IndicesSize; }
    }
}
