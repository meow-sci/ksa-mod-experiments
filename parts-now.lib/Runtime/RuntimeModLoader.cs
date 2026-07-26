// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MeowSci.PartsNowLib;

/// <summary>Where a <see cref="RuntimeModLoader" /> job currently is.</summary>
public enum LoadJobState
{
    /// <summary>No job is running and no result is waiting to be cleared.</summary>
    Idle,

    /// <summary>Parsing the submitted documents and running every <see cref="BundleValidator" /> rule.</summary>
    Validate,

    /// <summary>Paste flow only: writing the mod folder to disk.</summary>
    WriteModFolder,

    /// <summary>Building (or reusing) the <c>Mod</c> the bundles are registered against.</summary>
    CreateMod,

    /// <summary><c>AssetBundle.OnDataLoad</c> per bundle: registers templates, materials and loaders.</summary>
    RegisterBundles,

    /// <summary>Background worker reading GLB/KTX2 files off disk. The only off-thread state.</summary>
    RunLoaders,

    /// <summary>Verifying the shared interleaved buffer still has room for what the loaders created.</summary>
    CheckMeshBudget,

    /// <summary>Uploading meshes and textures to the GPU, a few binders per frame.</summary>
    Bind,

    /// <summary>Merging this load's <c>PartGameData</c> onto its Parts, incrementally.</summary>
    AttachGameData,

    /// <summary>Instantiating <c>PartModel</c> / <c>Glass</c> / <c>Dynamic</c> so bad mesh ids surface here.</summary>
    WarmModels,

    /// <summary>Rendering a part-browser thumbnail per new top-level Part, a couple per frame.</summary>
    Thumbnails,

    /// <summary>Resetting the vehicle editor's part-diameter cache.</summary>
    RefreshEditor,

    /// <summary>The job finished successfully. Call <see cref="RuntimeModLoader.Reset" /> to clear it.</summary>
    Done,

    /// <summary>The job failed and was rolled back. Call <see cref="RuntimeModLoader.Reset" /> to clear it.</summary>
    Failed,
}

/// <summary>
/// The one-job-at-a-time load state machine that turns validated XML into Parts the player can use,
/// without restarting the game.
/// </summary>
/// <remarks>
/// <para>
/// <b>Driving it.</b> <see cref="Step" /> runs exactly one state per call and must be called exactly
/// once per frame from <c>PartsNowSubmod.Update(dt)</c> — i.e. from <c>Program.OnDrawUiFrame</c>,
/// before the frame's swapchain image is acquired. That is what makes it safe for the
/// <see cref="LoadJobState.Bind" /> and <see cref="LoadJobState.Thumbnails" /> states to submit their
/// own command buffers and block on their own fences.
/// </para>
/// <para>
/// <b>Threading.</b> Everything is game-thread except the <see cref="LoadJobState.RunLoaders" />
/// worker, which touches only <c>ILoader.Load()</c> and whose completion is polled from
/// <see cref="Step" />.
/// </para>
/// <para>
/// <b>Failure.</b> Any state may fail. A failure appends its reason to <see cref="Log" />, sets
/// <see cref="FailureMessage" />, unwinds everything the job registered so far and lands in
/// <see cref="LoadJobState.Failed" />. That is why <see cref="LoadedModRecord" /> is filled in
/// incrementally as each state completes rather than only at the end.
/// </para>
/// </remarks>
public static partial class RuntimeModLoader
{
    /// <summary>
    /// Binders uploaded per frame during <see cref="LoadJobState.Bind" />. Each one creates a
    /// <c>StagingPool</c> whose <c>Dispose()</c> submits and waits on a fence, so a large batch is a
    /// visible hitch.
    /// </summary>
    public const int BindersPerFrame = 4;

    /// <summary>The states <see cref="Progress" /> divides by, in execution order.</summary>
    private static readonly LoadJobState[] ProgressStates =
    {
        LoadJobState.Validate,
        LoadJobState.WriteModFolder,
        LoadJobState.CreateMod,
        LoadJobState.RegisterBundles,
        LoadJobState.RunLoaders,
        LoadJobState.CheckMeshBudget,
        LoadJobState.Bind,
        LoadJobState.AttachGameData,
        LoadJobState.WarmModels,
        LoadJobState.Thumbnails,
        LoadJobState.RefreshEditor,
    };

    private static readonly List<string> LogLines = new List<string>();
    private static readonly List<ValidationIssue> IssueList = new List<ValidationIssue>();

    private static LoadJob? _job;
    private static LoadJobState _state = LoadJobState.Idle;
    private static string? _failureMessage;
    private static bool _cancelRequested;

    /// <summary>Where the current (or last) job is.</summary>
    public static LoadJobState State => _state;

    /// <summary>True while a job is running — that is, the state is neither Idle, Done nor Failed.</summary>
    public static bool IsBusy =>
        _state != LoadJobState.Idle && _state != LoadJobState.Done && _state != LoadJobState.Failed;

    /// <summary>Why the last job failed, or null.</summary>
    public static string? FailureMessage => _failureMessage;

    /// <summary>Every line the current (or last) job logged, oldest first. Mirrored to the console.</summary>
    public static IReadOnlyList<string> Log => LogLines;

    /// <summary>Validation findings from the current (or last) job, in rule order.</summary>
    public static IReadOnlyList<ValidationIssue> Issues => IssueList;

    /// <summary>The record the current (or last) job filled in, or null before the first job.</summary>
    public static LoadedModRecord? CurrentRecord => _job?.Record;

    /// <summary>A short human-readable status such as <c>"Bind (4/17)"</c>.</summary>
    public static string StatusText
    {
        get
        {
            LoadJob? job = _job;
            if (job is null)
            {
                return _state.ToString();
            }

            return _state switch
            {
                LoadJobState.RunLoaders => Format(_state, LoadersDone(job), job.PendingLoaders.Count),
                LoadJobState.Bind => Format(_state, job.BinderIndex, job.Record.NewBinders.Count),
                LoadJobState.Thumbnails => Format(
                    _state, job.Thumbnails?.ProgressCurrent ?? 0, job.Thumbnails?.ProgressTotal ?? 0),
                _ => _state.ToString(),
            };
        }
    }

    /// <summary>
    /// Rough completion of the current job, 0..1. Best effort: state boundaries dominate, with the
    /// two batched states interpolating between them.
    /// </summary>
    public static float Progress
    {
        get
        {
            if (_state == LoadJobState.Idle)
            {
                return 0f;
            }

            if (_state == LoadJobState.Done)
            {
                return 1f;
            }

            int index = Array.IndexOf(ProgressStates, _state);
            if (index < 0)
            {
                // Failed: report however far the job had got.
                return 0f;
            }

            LoadJob? job = _job;
            float within = 0f;
            if (job is not null)
            {
                within = _state switch
                {
                    LoadJobState.Bind => Fraction(job.BinderIndex, job.Record.NewBinders.Count),
                    LoadJobState.Thumbnails => Fraction(
                        job.Thumbnails?.ProgressCurrent ?? 0, job.Thumbnails?.ProgressTotal ?? 0),
                    _ => 0f,
                };
            }

            return (index + within) / ProgressStates.Length;
        }
    }

    /// <summary>
    /// Drives the state machine. Call exactly once per frame from <c>PartsNowSubmod.Update(dt)</c>.
    /// A no-op when no job is running.
    /// </summary>
    public static void Step()
    {
        LoadJob? job = _job;
        if (job is null || !IsBusy)
        {
            return;
        }

        // Cancellation is honoured only at a state boundary, never mid-Vulkan and never while the
        // loader worker is still running (it would keep registering into ModLibrary behind us).
        if (_cancelRequested && CanHonourCancel(job))
        {
            _cancelRequested = false;
            Fail(job, "cancelled by the user during the " + _state + " step.");
            return;
        }

        try
        {
            switch (_state)
            {
                case LoadJobState.Validate:
                    StateValidate(job);
                    break;
                case LoadJobState.WriteModFolder:
                    StateWriteModFolder(job);
                    break;
                case LoadJobState.CreateMod:
                    StateCreateMod(job);
                    break;
                case LoadJobState.RegisterBundles:
                    StateRegisterBundles(job);
                    break;
                case LoadJobState.RunLoaders:
                    StateRunLoaders(job);
                    break;
                case LoadJobState.CheckMeshBudget:
                    StateCheckMeshBudget(job);
                    break;
                case LoadJobState.Bind:
                    StateBind(job);
                    break;
                case LoadJobState.AttachGameData:
                    StateAttachGameData(job);
                    break;
                case LoadJobState.WarmModels:
                    StateWarmModels(job);
                    break;
                case LoadJobState.Thumbnails:
                    StateThumbnails(job);
                    break;
                case LoadJobState.RefreshEditor:
                    StateRefreshEditor(job);
                    break;
                default:
                    Fail(job, "reached the unexpected state " + _state + ".");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("parts-now: the " + _state + " step threw — " + ex);
            Fail(job, "the " + _state + " step threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// Asks the running job to stop. Honoured only between states — never mid-Vulkan and never while
    /// the loader worker is still running. A cancel is treated as a failure so the rollback runs.
    /// </summary>
    public static void RequestCancel()
    {
        if (!IsBusy)
        {
            return;
        }

        _cancelRequested = true;
        LogLine("cancellation requested — it will be honoured at the next state boundary.");
    }

    /// <summary>
    /// Clears a <see cref="LoadJobState.Done" /> or <see cref="LoadJobState.Failed" /> result back to
    /// <see cref="LoadJobState.Idle" /> so another job can start. A no-op while a job is running.
    /// </summary>
    public static void Reset()
    {
        if (IsBusy)
        {
            return;
        }

        DisposeThumbnails(_job);
        _job = null;
        _state = LoadJobState.Idle;
        _failureMessage = null;
        _cancelRequested = false;
        LogLines.Clear();
        IssueList.Clear();
    }

    /// <summary>
    /// Releases the current job's GPU resources during a StarMap unload, without attempting a purge.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT roll back: the game is tearing the mod down, and a purge here would
    /// submit a <c>Device.WaitIdle</c> and free images during shutdown. Whatever the job registered
    /// stays registered until the game exits, which is harmless — parts-now is going away.
    /// </remarks>
    public static void AbandonForShutdown()
    {
        if (_job is null)
        {
            return;
        }

        if (IsBusy)
        {
            LogLine("abandoning the in-flight job — parts-now is being unloaded.");
        }

        DisposeThumbnails(_job);
        _job = null;
        _state = LoadJobState.Idle;
        _cancelRequested = false;
    }

    private static bool CanHonourCancel(LoadJob job) =>
        _state != LoadJobState.RunLoaders || job.LoaderTask is null || job.LoaderTask.IsCompleted;

    private static int LoadersDone(LoadJob job) =>
        job.LoaderTask is { IsCompleted: true } ? job.PendingLoaders.Count : 0;

    private static string Format(LoadJobState state, int current, int total) =>
        total <= 0 ? state.ToString() : state + " (" + current + "/" + total + ")";

    private static float Fraction(int current, int total) =>
        total <= 0 ? 0f : Math.Clamp((float)current / total, 0f, 1f);

    /// <summary>Appends a line to <see cref="Log" /> and mirrors it to the console.</summary>
    private static void LogLine(string message)
    {
        LogLines.Add(message);
        Console.WriteLine("parts-now: " + message);
    }

    /// <summary>Moves to the next state, logging the transition.</summary>
    private static void Transition(LoadJobState next)
    {
        _state = next;
        LogLine("-> " + next);
    }

    /// <summary>
    /// Aborts the job: records the reason, unwinds everything the record accumulated, and lands in
    /// <see cref="LoadJobState.Failed" />.
    /// </summary>
    /// <remarks>
    /// The unwind is <see cref="RuntimeModUnloader.Rollback" /> before anything was bound and
    /// <see cref="RuntimeModUnloader.Purge" /> after — Rollback additionally rewinds the shared
    /// interleaved buffer's bump cursors, which is only sound while no mesh has copied itself into
    /// that range yet (T2.4 / T11.4). After a bind those bytes are genuinely spent, so the purge's
    /// leak accounting is the honest answer.
    /// </remarks>
    private static void Fail(LoadJob job, string reason)
    {
        _failureMessage = reason;
        LogLine("FAILED: " + reason);

        DisposeThumbnails(job);

        if (HasRegisteredAnything(job.Record))
        {
            try
            {
                List<string> unwind = job.BoundAny
                    ? RuntimeModUnloader.Purge(job.Record)
                    : RuntimeModUnloader.Rollback(job.Record);

                foreach (string line in unwind)
                {
                    LogLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                LogLine("the rollback itself failed — " + ex.Message
                    + ". The game may hold a partially registered mod; restart before loading again.");
            }
        }
        else
        {
            // Nothing reached a registry, so there is nothing to undo — and no reason to stall the
            // device with the purge's WaitIdle.
            LogLine("nothing had been registered yet, so there was nothing to roll back.");
        }

        DiscardWrittenModFolder(job);

        if (job.PreviousPurged)
        {
            LogLine("'" + job.ModId + "' is now UNLOADED: the reload purged the previous load before "
                + "this failure. Fix the problem and load it again.");
        }

        _state = LoadJobState.Failed;
    }

    /// <summary>
    /// Deletes a mod folder the paste flow created, when the install that created it failed.
    /// </summary>
    /// <remarks>
    /// <c>ModIdValidator</c> rejects any id whose folder already exists, so a leftover folder would
    /// make that mod id permanently unusable with no in-game way to recover. Only ever removes a
    /// folder this very job wrote — never one that was already on disk.
    /// </remarks>
    private static void DiscardWrittenModFolder(LoadJob job)
    {
        if (!job.CreatedByPaste || !job.WroteModFolder || string.IsNullOrEmpty(job.ModDirectory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(job.ModDirectory))
            {
                Directory.Delete(job.ModDirectory, recursive: true);
                LogLine("removed the mod folder this failed install created (" + job.ModDirectory
                    + "), so the id '" + job.ModId + "' can be reused.");
            }
        }
        catch (Exception ex)
        {
            LogLine("WARNING — could not remove '" + job.ModDirectory + "' (" + ex.Message
                + "). Delete it by hand before reusing the id '" + job.ModId + "'.");
        }

        job.WroteModFolder = false;
    }

    private static void DisposeThumbnails(LoadJob? job)
    {
        if (job?.Thumbnails is null)
        {
            return;
        }

        try
        {
            job.Thumbnails.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine("parts-now: failed to dispose the thumbnail generator: " + ex.Message);
        }

        job.Thumbnails = null;
    }

    /// <summary>Formats a byte count as MiB with one decimal, for user-facing messages.</summary>
    private static string Mib(ulong bytes) =>
        (bytes / (1024.0 * 1024.0)).ToString("F1", CultureInfo.InvariantCulture);
}
