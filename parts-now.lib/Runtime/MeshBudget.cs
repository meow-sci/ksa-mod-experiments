// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.
//
// Every member of MeshBudget is game-thread only: Reserve() runs from [StarMapAllModsLoaded],
// OnFirstFrame() and every accessor run from PartsNowSubmod.Update(dt) / RenderContent().

using System;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Reserves headroom in KSA's single shared interleaved vertex/index buffer pair so that meshes
/// can still be created after startup.
/// </summary>
/// <remarks>
/// <para>
/// <c>DeviceMeshInterleaved.Shared</c> owns exactly one vertex buffer and one index buffer, sized
/// from <c>Shared.RunningVertexBufferSize</c> / <c>RunningIndexBufferSize</c> the first (and only)
/// time <c>Shared.Build()</c> runs — inside <c>ModLibrary.Bind()</c>, at <c>Program.cs:985</c>.
/// Each <c>new DeviceMeshInterleaved(...)</c> atomically bumps those counters and records its own
/// offset, so a mesh created after <c>Build()</c> would land past the end of the allocation.
/// </para>
/// <para>
/// The fix is a two-step trick that depends on StarMap's lifecycle:
/// <see cref="Reserve" /> runs from <c>[StarMapAllModsLoaded]</c> (a Harmony postfix on
/// <c>ModLibrary.LoadAll()</c>, <c>Program.cs:956</c>, i.e. before <c>Build()</c>) and inflates the
/// counters by the configured headroom; <see cref="OnFirstFrame" /> runs from the first
/// <c>Update(dt)</c> (long after <c>Build()</c>) and rewinds them to the startup watermark. The
/// buffers end up allocated at <c>watermark + headroom</c> while the bump cursor sits back at
/// <c>watermark</c>, leaving the headroom free for runtime meshes.
/// </para>
/// <para>
/// <c>Shared.Rebuild()</c> is never used to grow the buffers — it copies
/// <c>VertexAllocation.BufferSize</c> bytes out of the <i>old</i> buffer and only reacts to a
/// raytracing usage-flag mismatch anyway.
/// </para>
/// </remarks>
public static class MeshBudget
{
    private const uint BytesPerMiB = 1024u * 1024u;

    private static bool _reserved;
    private static bool _armed;
    private static uint _watermarkVertexBytes;
    private static uint _watermarkIndexBytes;
    private static uint _vertexHeadroomBytes;
    private static uint _indexHeadroomBytes;
    private static ulong _leakedVertexBytes;
    private static ulong _leakedIndexBytes;
    private static string? _failureReason;

    /// <summary>True once <see cref="Reserve" /> has successfully run. Loading is unavailable when false.</summary>
    public static bool Reserved => _reserved;

    /// <summary>True between a successful <see cref="Reserve" /> and <see cref="OnFirstFrame" />.</summary>
    public static bool Armed => _armed;

    /// <summary>Why <see cref="Reserve" /> or <see cref="OnFirstFrame" /> failed, or <c>null</c>.</summary>
    public static string? FailureReason => _failureReason;

    /// <summary>Configured vertex-buffer headroom in bytes (frozen at <see cref="Reserve" /> time).</summary>
    public static uint VertexHeadroomBytes =>
        _reserved ? _vertexHeadroomBytes : ToBytes(PartsNowSettings.VertexHeadroomMiB);

    /// <summary>Configured index-buffer headroom in bytes (frozen at <see cref="Reserve" /> time).</summary>
    public static uint IndexHeadroomBytes =>
        _reserved ? _indexHeadroomBytes : ToBytes(PartsNowSettings.IndexHeadroomMiB);

    /// <summary>Startup vertex watermark captured by <see cref="Reserve" />, in bytes.</summary>
    public static uint WatermarkVertexBytes => _watermarkVertexBytes;

    /// <summary>Startup index watermark captured by <see cref="Reserve" />, in bytes.</summary>
    public static uint WatermarkIndexBytes => _watermarkIndexBytes;

    /// <summary>Size of the shared vertex buffer as reported by the allocation itself (authoritative).</summary>
    public static uint AllocatedVertexBytes => (uint)DeviceMeshInterleaved.Shared.VertexAllocation.BufferSize;

    /// <summary>Size of the shared index buffer as reported by the allocation itself (authoritative).</summary>
    public static uint AllocatedIndexBytes => (uint)DeviceMeshInterleaved.Shared.IndexAllocation.BufferSize;

    /// <summary>Current shared vertex-buffer bump cursor, in bytes.</summary>
    public static uint UsedVertexBytes => DeviceMeshInterleaved.Shared.RunningVertexBufferSize;

    /// <summary>Current shared index-buffer bump cursor, in bytes.</summary>
    public static uint UsedIndexBytes => DeviceMeshInterleaved.Shared.RunningIndexBufferSize;

    /// <summary>True while both bump cursors still sit inside their allocations.</summary>
    public static bool WithinBudget =>
        UsedVertexBytes <= AllocatedVertexBytes && UsedIndexBytes <= AllocatedIndexBytes;

    /// <summary>Unused vertex bytes remaining in the shared buffer. Saturates at zero.</summary>
    public static uint FreeVertexBytes => Saturating(AllocatedVertexBytes, UsedVertexBytes);

    /// <summary>Unused index bytes remaining in the shared buffer. Saturates at zero.</summary>
    public static uint FreeIndexBytes => Saturating(AllocatedIndexBytes, UsedIndexBytes);

    /// <summary>Vertex bytes orphaned by unloads/reloads. The shared allocator never reclaims them.</summary>
    public static ulong LeakedVertexBytes => _leakedVertexBytes;

    /// <summary>Index bytes orphaned by unloads/reloads. The shared allocator never reclaims them.</summary>
    public static ulong LeakedIndexBytes => _leakedIndexBytes;

    /// <summary>True once leaked bytes exceed 50% of the reserved headroom on either buffer.</summary>
    public static bool LeakWarningTripped =>
        _leakedVertexBytes * 2uL > VertexHeadroomBytes || _leakedIndexBytes * 2uL > IndexHeadroomBytes;

    /// <summary>
    /// Inflates the shared buffer size counters by the configured headroom.
    /// MUST be called from <c>[StarMapAllModsLoaded]</c> (and from
    /// <c>PartsNowSubmod.Initialize()</c>, which unscience calls at the same point). Idempotent.
    /// </summary>
    public static void Reserve()
    {
        if (_reserved)
            return;

        try
        {
            // Tripwire for the StarMap ordering invariant: Build() must not have run yet.
            if (DeviceMeshInterleaved.Shared.IsBuilt)
            {
                Console.WriteLine(
                    "parts-now: WARNING — DeviceMeshInterleaved.Shared is already built at reservation "
                    + "time; the shared buffers cannot grow and runtime mesh loading will be unsafe.");
            }

            _vertexHeadroomBytes = ToBytes(PartsNowSettings.VertexHeadroomMiB);
            _indexHeadroomBytes = ToBytes(PartsNowSettings.IndexHeadroomMiB);

            _watermarkVertexBytes = DeviceMeshInterleaved.Shared.RunningVertexBufferSize;
            _watermarkIndexBytes = DeviceMeshInterleaved.Shared.RunningIndexBufferSize;

            DeviceMeshInterleaved.Shared.RunningVertexBufferSize = _watermarkVertexBytes + _vertexHeadroomBytes;
            DeviceMeshInterleaved.Shared.RunningIndexBufferSize = _watermarkIndexBytes + _indexHeadroomBytes;

            _reserved = true;
            _armed = true;
            _failureReason = null;

            Console.WriteLine(
                $"parts-now: reserved mesh headroom {_vertexHeadroomBytes / BytesPerMiB} MiB vtx / "
                + $"{_indexHeadroomBytes / BytesPerMiB} MiB idx (startup watermark "
                + $"{_watermarkVertexBytes / BytesPerMiB} / {_watermarkIndexBytes / BytesPerMiB} MiB)");
        }
        catch (Exception ex)
        {
            _reserved = false;
            _armed = false;
            _failureReason = $"mesh headroom reservation failed: {ex.Message}";
            Console.WriteLine($"parts-now: {_failureReason}");
        }
    }

    /// <summary>
    /// Rewinds the shared buffer bump cursors to the startup watermark. Call unconditionally from
    /// the FIRST <c>PartsNowSubmod.Update(dt)</c>; a no-op afterwards, or if <see cref="Reserve" />
    /// never succeeded.
    /// </summary>
    public static void OnFirstFrame()
    {
        if (!_armed)
            return;

        try
        {
            // By the first UI frame ModLibrary.Bind() (Program.cs:985) has run and Build() has
            // allocated the enlarged buffers. The loading screen never goes through
            // Program.OnDrawUiFrame, so the StarMap gui hooks cannot have fired before Bind().
            if (!DeviceMeshInterleaved.Shared.IsBuilt)
            {
                Console.WriteLine(
                    "parts-now: WARNING — DeviceMeshInterleaved.Shared is not built on the first frame; "
                    + "the reserved headroom may not have been allocated.");
            }

            DeviceMeshInterleaved.Shared.RunningVertexBufferSize = _watermarkVertexBytes;
            DeviceMeshInterleaved.Shared.RunningIndexBufferSize = _watermarkIndexBytes;
            _armed = false;

            Console.WriteLine(
                $"parts-now: mesh budget armed — {FreeVertexBytes / BytesPerMiB} MiB vtx / "
                + $"{FreeIndexBytes / BytesPerMiB} MiB idx free of "
                + $"{AllocatedVertexBytes / BytesPerMiB} / {AllocatedIndexBytes / BytesPerMiB} MiB allocated");
        }
        catch (Exception ex)
        {
            _armed = false;
            _reserved = false;
            _failureReason = $"mesh budget rewind failed: {ex.Message}";
            Console.WriteLine($"parts-now: {_failureReason}");
        }
    }

    /// <summary>Snapshot of the monotonic shared-buffer allocation cursors.</summary>
    /// <param name="VertexBytes">Value of <see cref="UsedVertexBytes" /> when taken.</param>
    /// <param name="IndexBytes">Value of <see cref="UsedIndexBytes" /> when taken.</param>
    public readonly record struct Cursors(uint VertexBytes, uint IndexBytes);

    /// <summary>Captures the current allocation cursors so a failed load can rewind them.</summary>
    public static Cursors SnapshotCursors()
    {
        return new Cursors(UsedVertexBytes, UsedIndexBytes);
    }

    /// <summary>
    /// Restores allocation cursors taken by <see cref="SnapshotCursors" />. Only valid when
    /// nothing created after the snapshot has been bound — bound meshes hold absolute offsets.
    /// </summary>
    /// <param name="cursors">The snapshot to restore.</param>
    public static void RestoreCursors(Cursors cursors)
    {
        try
        {
            DeviceMeshInterleaved.Shared.RunningVertexBufferSize = cursors.VertexBytes;
            DeviceMeshInterleaved.Shared.RunningIndexBufferSize = cursors.IndexBytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to restore mesh allocation cursors: {ex.Message}");
        }
    }

    /// <summary>
    /// Records bytes that an unload or reload orphaned inside the shared buffer. The shared
    /// allocator is a monotonic bump pointer, so these bytes are gone until the game restarts.
    /// </summary>
    /// <param name="vertexBytes">Orphaned vertex bytes.</param>
    /// <param name="indexBytes">Orphaned index bytes.</param>
    public static void RecordLeak(ulong vertexBytes, ulong indexBytes)
    {
        _leakedVertexBytes += vertexBytes;
        _leakedIndexBytes += indexBytes;
    }

    /// <summary>
    /// Sums the vertex and index bytes a mesh occupies in the shared buffer, across every
    /// primitive in <c>MeshReference.DeviceMeshesInterleaved</c>. Null-safe; never throws.
    /// </summary>
    /// <param name="mesh">The mesh to measure; may be null.</param>
    /// <param name="vertexBytes">Total vertex bytes, or zero.</param>
    /// <param name="indexBytes">Total index bytes, or zero.</param>
    public static void MeasureMesh(MeshReference mesh, out ulong vertexBytes, out ulong indexBytes)
    {
        vertexBytes = 0uL;
        indexBytes = 0uL;

        try
        {
            var primitives = mesh?.DeviceMeshesInterleaved;
            if (primitives == null)
                return;

            foreach (var primitive in primitives)
            {
                if (primitive == null)
                    continue;

                vertexBytes += primitive.VerticesSize;
                indexBytes += primitive.IndicesSize;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to measure mesh '{mesh?.Id}': {ex.Message}");
            vertexBytes = 0uL;
            indexBytes = 0uL;
        }
    }

    private static uint ToBytes(int megabytes)
    {
        return megabytes <= 0 ? 0u : (uint)megabytes * BytesPerMiB;
    }

    private static uint Saturating(uint allocated, uint used)
    {
        return used >= allocated ? 0u : allocated - used;
    }
}
