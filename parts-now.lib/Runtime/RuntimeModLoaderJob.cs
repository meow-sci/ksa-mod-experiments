// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System.Collections.Generic;
using System.Threading.Tasks;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>Which of the three entry points started a load job.</summary>
internal enum LoadJobKind
{
    /// <summary>Paste flow: write a brand new mod folder from XML, then load it.</summary>
    Install,

    /// <summary>Folder flow: load an existing mod folder that nothing has loaded yet.</summary>
    Load,

    /// <summary>Purge a mod parts-now loaded this session, then load it again from disk.</summary>
    Reload,
}

/// <summary>
/// Everything one <see cref="RuntimeModLoader" /> job carries between <see cref="RuntimeModLoader.Step" />
/// calls. One job at a time; the loader owns exactly one instance while it is busy.
/// </summary>
/// <remarks>
/// Game-thread only, with a single exception: <see cref="LoaderTask" /> is written on the game thread
/// and read on the game thread, and the worker it represents touches only the <see cref="ILoader" />
/// instances captured in <see cref="PendingLoaders" /> — never this object.
/// </remarks>
internal sealed class LoadJob
{
    /// <summary>Which entry point created this job.</summary>
    internal LoadJobKind Kind { get; init; }

    /// <summary>The KSA mod id being loaded, which is also its folder name.</summary>
    internal string ModId { get; init; } = string.Empty;

    /// <summary>Absolute path of the mod folder. Known up front, even for the paste flow.</summary>
    internal string ModDirectory { get; set; } = string.Empty;

    /// <summary>The paste-flow form contents, or null for the folder flow.</summary>
    internal ModFolderRequest? Request { get; init; }

    /// <summary>True when the resulting record should also gain a manifest entry.</summary>
    internal bool CreatedByPaste { get; init; }

    /// <summary>The documents being loaded, parsed but deliberately not <c>OnDataLoad</c>ed yet.</summary>
    internal List<ParsedBundle> Bundles { get; } = new List<ParsedBundle>();

    /// <summary>The record this job is filling in, created when the job starts.</summary>
    internal LoadedModRecord Record { get; init; } = new LoadedModRecord();

    /// <summary>The <see cref="Mod" /> the bundles are registered against.</summary>
    internal Mod? Mod { get; set; }

    /// <summary>Count of <c>ModLibrary.AllParts</c> before this job registered anything.</summary>
    internal int PartsMark { get; set; }

    /// <summary>Count of <c>ModLibrary.AllPartGameDataReferences</c> before this job registered anything.</summary>
    internal int GameDataMark { get; set; }

    /// <summary>Count of <c>ModLibrary.AllMaterials</c> before this job registered anything.</summary>
    internal int MaterialsMark { get; set; }

    /// <summary>Count of <c>ModLibrary.Loaders</c> before this job registered anything.</summary>
    internal int LoaderMark { get; set; }

    /// <summary>Count of <c>ModLibrary.AllFiles</c> before this job registered anything.</summary>
    internal int FilesMark { get; set; }

    /// <summary>Count of <c>ModLibrary.AllMeshes</c> before this job registered anything.</summary>
    internal int MeshesMark { get; set; }

    /// <summary>Count of <c>ModLibrary.Binders</c> before this job registered anything.</summary>
    internal int BinderMark { get; set; }

    /// <summary>The loaders this job appended, snapshotted before the worker starts.</summary>
    internal List<ILoader> PendingLoaders { get; } = new List<ILoader>();

    /// <summary>The background worker running <see cref="PendingLoaders" />, or null before it starts.</summary>
    internal Task? LoaderTask { get; set; }

    /// <summary>Index of the next binder to bind, into <c>Record.NewBinders</c>.</summary>
    internal int BinderIndex { get; set; }

    /// <summary>
    /// True once the <c>Bind</c> state has begun. From that moment a failure must purge (which
    /// records the leak) rather than roll back (which rewinds the shared-buffer cursors) — a bound
    /// mesh has already copied its data to an absolute offset inside the shared buffer, so handing
    /// that range back out would corrupt it.
    /// </summary>
    internal bool BoundAny { get; set; }

    /// <summary>How many binders threw. Each one degrades the result rather than aborting the job.</summary>
    internal int BindFailures { get; set; }

    /// <summary>
    /// Reload only: true once the previous load has been purged. From that point a failure leaves the
    /// mod unloaded rather than untouched, which the user has to be told.
    /// </summary>
    internal bool PreviousPurged { get; set; }

    /// <summary>The thumbnail generator for this job, alive only during the Thumbnails state.</summary>
    internal PartThumbnailGenerator? Thumbnails { get; set; }
}
