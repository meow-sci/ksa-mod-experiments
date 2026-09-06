// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>How a single top-level Part came out of a load.</summary>
public enum PartLoadStatus
{
    /// <summary>Loaded, warmed and thumbnailed without complaint.</summary>
    Ok,

    /// <summary>Loaded, but something non-fatal failed (a model would not warm, no thumbnail, ...).</summary>
    Degraded,

    /// <summary>Deliberately skipped, with <see cref="PartLoadResult.Reason" /> saying why.</summary>
    Skipped,
}

/// <summary>Per-part outcome of a load, shown in the results table.</summary>
/// <param name="PartId">The part's id.</param>
/// <param name="Status">How the part fared.</param>
/// <param name="Reason">Empty for <see cref="PartLoadStatus.Ok" />, otherwise the explanation.</param>
public sealed record PartLoadResult(string PartId, PartLoadStatus Status, string Reason);

/// <summary>
/// Everything parts-now registered on behalf of one mod during one load, in the exact shape an
/// unload or a rollback needs to undo it.
/// </summary>
/// <remarks>
/// <para>
/// This is populated <b>incrementally</b> as each load state completes, never only at the end — a
/// failure half-way through has to be able to purge exactly what got registered before it.
/// </para>
/// <para>
/// Session state only; nothing here is persisted. The mod folder on disk is the durable artefact.
/// </para>
/// </remarks>
public sealed class LoadedModRecord
{
    /// <summary>KSA mod id, which is also the mod's folder name.</summary>
    public string ModId { get; init; } = string.Empty;

    /// <summary>Absolute path of the mod folder.</summary>
    public string ModDir { get; init; } = string.Empty;

    /// <summary>The <see cref="Mod" /> the bundles were loaded against.</summary>
    public Mod? Mod { get; set; }

    /// <summary>True when parts-now created this mod folder from pasted XML during this load.</summary>
    public bool CreatedByPaste { get; init; }

    /// <summary>Parts and SubParts registered into <c>ModLibrary.AllParts</c> by this load.</summary>
    public List<PartTemplate> NewParts { get; } = new List<PartTemplate>();

    /// <summary>Game-data references registered into <c>ModLibrary.AllPartGameDataReferences</c>.</summary>
    public List<PartGameDataReference> NewGameData { get; } = new List<PartGameDataReference>();

    /// <summary>Meshes registered into <c>ModLibrary.AllMeshes</c> (including GLB atlas nodes).</summary>
    public List<MeshReference> NewMeshes { get; } = new List<MeshReference>();

    /// <summary>File references registered into <c>ModLibrary.AllFiles</c> — atlases, mesh files, textures.</summary>
    public List<FileReference> NewFiles { get; } = new List<FileReference>();

    /// <summary>Materials registered into <c>ModLibrary.AllMaterials</c>.</summary>
    public List<PbrMaterialReference> NewMaterials { get; } = new List<PbrMaterialReference>();

    /// <summary>Loaders appended to <c>ModLibrary.Loaders</c> by this load.</summary>
    public List<ILoader> NewLoaders { get; } = new List<ILoader>();

    /// <summary>Binders appended to <c>ModLibrary.Binders</c> by this load.</summary>
    public List<IBinder> NewBinders { get; } = new List<IBinder>();

    /// <summary>
    /// Every part id this load registered. Answers "is this part ours?" when the unload safety gate
    /// walks live vehicles and the editor.
    /// </summary>
    public HashSet<string> PartIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The actual <c>PartModel</c> / <c>PartModelGlass</c> / <c>PartModelDynamic</c> template objects
    /// this load introduced. The purge uses these to prune the static <c>Instances</c> /
    /// <c>InstancesRayTrace</c> / <c>RayTracers</c> lists, which KSA never prunes itself.
    /// </summary>
    /// <remarks>
    /// Identity, not id. <c>ModuleBase.TemplateDataBase.Id</c> is an optional XML attribute that
    /// nothing requires to be present or unique, so pruning by id would both miss every id-less
    /// template (leaving a stale <c>PartModel</c> that <c>PartModel.Get</c> would hand back after a
    /// reload, complete with the purged mesh's old buffer offsets) and evict another mod's instances
    /// on an id collision.
    /// </remarks>
    public HashSet<object> ModelTemplates { get; } =
        new HashSet<object>(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Ids of the templates in <see cref="ModelTemplates" /> that declare one, for logging and
    /// diagnostics only. Never use these to decide what to purge — see <see cref="ModelTemplates" />.
    /// </summary>
    public HashSet<string> ModelTemplateIds { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Per-part outcomes, in registration order.</summary>
    public List<PartLoadResult> Results { get; } = new List<PartLoadResult>();

    /// <summary>Shared-buffer allocation cursors as they stood before this load registered anything.</summary>
    public MeshBudget.Cursors CursorsBefore { get; set; }

    /// <summary>Shared vertex bytes this load consumed, measured when the load completed.</summary>
    public ulong VertexBytesUsed { get; set; }

    /// <summary>Shared index bytes this load consumed, measured when the load completed.</summary>
    public ulong IndexBytesUsed { get; set; }

    /// <summary>When the load completed, in UTC.</summary>
    public DateTime LoadedAtUtc { get; set; }

    /// <summary>
    /// True once <see cref="RuntimeModUnloader.Purge" /> (or <see cref="RuntimeModUnloader.Rollback" />)
    /// has run over this record.
    /// </summary>
    /// <remarks>
    /// The purge is <b>not</b> idempotent at the item level — <c>ThumbnailReference.Dispose()</c> and
    /// <c>TextureReference.Dispose(Device)</c> both double-free if called twice. This flag makes it
    /// idempotent at the record level, so a double Unload (or an Unload racing a rollback) cannot
    /// double-free GPU objects.
    /// </remarks>
    public bool Purged { get; set; }

    /// <summary>Top-level (non-SubPart) parts this load registered.</summary>
    /// <returns>Every entry of <see cref="NewParts" /> whose <c>IsSubPart</c> is false.</returns>
    public List<PartTemplate> TopLevelParts()
    {
        List<PartTemplate> parts = new List<PartTemplate>();
        foreach (PartTemplate part in NewParts)
        {
            if (!part.IsSubPart)
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    /// <summary>Records a per-part outcome, replacing any earlier entry for the same part.</summary>
    /// <param name="partId">The part id.</param>
    /// <param name="status">How the part fared.</param>
    /// <param name="reason">Explanation, or empty when OK.</param>
    public void SetResult(string partId, PartLoadStatus status, string reason = "")
    {
        for (int i = 0; i < Results.Count; i++)
        {
            if (string.Equals(Results[i].PartId, partId, StringComparison.OrdinalIgnoreCase))
            {
                Results[i] = new PartLoadResult(partId, status, reason);
                return;
            }
        }

        Results.Add(new PartLoadResult(partId, status, reason));
    }
}
