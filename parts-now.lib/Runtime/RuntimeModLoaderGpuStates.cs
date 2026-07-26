// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.
//
// Every state in this file is game-thread only: they submit command buffers to renderer.Graphics
// and block on fences, which is only safe from Program.OnDrawUiFrame (i.e. ISubmod.Update(dt)).

using System;
using System.Collections.Generic;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The GPU and game-state half of the <see cref="RuntimeModLoader" /> state machine: the mesh budget
/// guard, binding, the incremental game-data attach, model warming, thumbnails and the editor
/// refresh.
/// </summary>
public static partial class RuntimeModLoader
{
    /// <summary>
    /// Refuses to bind anything if the loader step overflowed the shared interleaved buffer.
    /// </summary>
    /// <remarks>
    /// Nothing has been uploaded at this point — the offending <c>DeviceMeshInterleaved</c> objects
    /// merely hold offsets past the end of the allocation, and binding one would
    /// <c>vkCmdCopyBuffer</c> out of range. Failing here lets the rollback purge them and rewind the
    /// bump cursors, leaving the game exactly as it was.
    /// </remarks>
    private static void StateCheckMeshBudget(LoadJob job)
    {
        MeshBudget.Cursors before = job.Record.CursorsBefore;
        ulong vertexNeeded = SaturatingDelta(MeshBudget.UsedVertexBytes, before.VertexBytes);
        ulong indexNeeded = SaturatingDelta(MeshBudget.UsedIndexBytes, before.IndexBytes);

        LogLine("this load wants " + Mib(vertexNeeded) + " MiB of vertex data and " + Mib(indexNeeded)
            + " MiB of index data.");

        if (MeshBudget.WithinBudget)
        {
            Transition(LoadJobState.Bind);
            return;
        }

        bool vertexOverflowed = MeshBudget.UsedVertexBytes > MeshBudget.AllocatedVertexBytes;
        ulong needed = vertexOverflowed ? vertexNeeded : indexNeeded;
        ulong free = vertexOverflowed
            ? SaturatingDelta(MeshBudget.AllocatedVertexBytes, before.VertexBytes)
            : SaturatingDelta(MeshBudget.AllocatedIndexBytes, before.IndexBytes);
        string setting = vertexOverflowed ? "vertexHeadroomMiB" : "indexHeadroomMiB";

        Fail(job, "Mesh headroom exhausted (needed " + Mib(needed) + " MiB, " + Mib(free)
            + " MiB free). Increase `" + setting + "` in parts-now.toml and restart the game.");
    }

    /// <summary>
    /// Uploads this load's meshes and textures, <see cref="BindersPerFrame" /> per frame.
    /// </summary>
    /// <remarks>
    /// This mirrors <c>ModLibrary.Bind</c>'s per-binder body minus its <c>Parallel.ForEachAsync</c>:
    /// the stock method binds EVERY binder ever registered, which would reallocate every existing
    /// mesh's device primitives. Binders must run on the game thread —
    /// <c>StagingPool.Dispose()</c> submits to <c>renderer.Graphics</c> and blocks, and
    /// <c>vkQueueSubmit</c> is externally synchronised, so submitting from a worker while the main
    /// thread submits the frame is a race.
    /// </remarks>
    private static void StateBind(LoadJob job)
    {
        LoadedModRecord record = job.Record;
        if (record.NewBinders.Count == 0)
        {
            LogLine("nothing to upload to the GPU.");
            Transition(LoadJobState.AttachGameData);
            return;
        }

        // Set before the first submit, not after: from here on a failure must purge (recording the
        // leak) instead of rolling the shared-buffer cursors back over data already copied in.
        job.BoundAny = true;

        Renderer renderer = Program.GetRenderer();
        int batchEnd = Math.Min(job.BinderIndex + BindersPerFrame, record.NewBinders.Count);

        for (; job.BinderIndex < batchEnd; job.BinderIndex++)
        {
            IBinder binder = record.NewBinders[job.BinderIndex];
            try
            {
                using StagingPool pool = renderer.Allocator.CreateStagingPool(renderer.Graphics, 1);
                binder.Bind(renderer, pool);
            }
            catch (Exception ex)
            {
                // One bad binder degrades its own asset; it must not abort a load whose other
                // meshes and textures uploaded cleanly.
                job.BindFailures++;
                LogLine("could not upload " + Describe(binder) + " — " + ex.Message);
            }
        }

        if (job.BinderIndex < record.NewBinders.Count)
        {
            return;
        }

        LogLine("uploaded " + (record.NewBinders.Count - job.BindFailures) + "/" + record.NewBinders.Count
            + " binder(s)" + (job.BindFailures > 0 ? " — " + job.BindFailures + " failed." : "."));
        Transition(LoadJobState.AttachGameData);
    }

    /// <summary>
    /// Merges this load's <c>PartGameData</c> onto its Parts and re-resolves their consumer feeds.
    /// </summary>
    /// <remarks>
    /// This deliberately replaces <c>ModLibrary.AttachGameData()</c> rather than calling it.
    /// <c>PartTemplate.ApplyGameData</c> is additive (<c>AddRange</c> on connectors, masses, rockets,
    /// components...), so the stock method — which walks every registered game-data entry — would
    /// double every part already attached at boot. <c>ResolveConsumerFeedPoints()</c> on the other
    /// hand starts with <c>ConsumerFeeds.Clear()</c>, so it IS idempotent and is safe to re-run on a
    /// part that only gained game data.
    /// <para>
    /// <c>gd.Hash</c> is valid here because <c>OnDataLoad</c> ran during <c>RegisterBundles</c>;
    /// before that it is <c>KeyHash.Zero</c>.
    /// </para>
    /// </remarks>
    private static void StateAttachGameData(LoadJob job)
    {
        LoadedModRecord record = job.Record;
        HashSet<PartTemplate> resolve = new HashSet<PartTemplate>(ReferenceEqualityComparer.Instance);
        int attached = 0;

        foreach (PartTemplate template in record.NewParts)
        {
            resolve.Add(template);
        }

        foreach (PartGameDataReference gameData in record.NewGameData)
        {
            PartTemplate? target = GameRegistry.AllParts.Find(gameData.Hash);
            if (target is null)
            {
                LogLine("WARNING — PartGameData '" + gameData.Id + "' matches no Part.");
                continue;
            }

            try
            {
                target.ApplyGameData(gameData);
                resolve.Add(target);
                attached++;
            }
            catch (Exception ex)
            {
                LogLine("could not apply PartGameData '" + gameData.Id + "' — " + ex.Message);
                record.SetResult(target.Id, PartLoadStatus.Degraded, "game data failed to apply: " + ex.Message);
            }
        }

        int resolved = 0;
        foreach (PartTemplate template in resolve)
        {
            if (template.IsSubPart)
            {
                continue;
            }

            try
            {
                template.ResolveConsumerFeedPoints();
                resolved++;
            }
            catch (Exception ex)
            {
                LogLine("could not resolve consumer feeds for '" + template.Id + "' — " + ex.Message);
                record.SetResult(template.Id, PartLoadStatus.Degraded, "consumer feeds unresolved: " + ex.Message);
            }
        }

        LogLine("attached " + attached + " game-data entr(ies) and resolved consumer feeds on "
            + resolved + " part(s).");
        Transition(LoadJobState.WarmModels);
    }

    /// <summary>
    /// Instantiates the <c>PartModel</c> / <c>PartModelGlass</c> / <c>PartModelDynamic</c> for every
    /// new part, mirroring what <c>Program</c> does at boot.
    /// </summary>
    /// <remarks>
    /// These are created lazily at spawn time anyway, but warming them here turns an unresolvable
    /// <c>&lt;Mesh Id&gt;</c> into a catchable exception at load time instead of a crash the first
    /// time the player clicks the part in the browser.
    /// </remarks>
    private static void StateWarmModels(LoadJob job)
    {
        LoadedModRecord record = job.Record;
        int warmed = 0;
        int failed = 0;

        foreach (PartTemplate template in record.NewParts)
        {
            foreach (ModuleBase.TemplateDataBase component in template.Components)
            {
                try
                {
                    switch (component)
                    {
                        case PartModelModule.Template model:
                            PartModel.Get(model);
                            warmed++;
                            break;
                        case PartModelGlassModule.Template glass:
                            PartModelGlass.Get(glass);
                            warmed++;
                            break;
                        case PartModelDynamicModule.Template dynamicModel:
                            PartModelDynamic.Get(dynamicModel);
                            warmed++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    LogLine("model '" + component.Id + "' on part '" + template.Id + "' would not warm — "
                        + ex.Message);
                    record.SetResult(
                        template.Id,
                        PartLoadStatus.Degraded,
                        "model '" + component.Id + "' failed: " + ex.Message);
                }
            }
        }

        LogLine("warmed " + warmed + " model(s)" + (failed > 0 ? " — " + failed + " failed." : "."));
        Transition(LoadJobState.Thumbnails);
    }

    /// <summary>
    /// Renders a part-browser thumbnail for each new top-level Part, a couple per frame, then folds
    /// the outcome into the record.
    /// </summary>
    private static void StateThumbnails(LoadJob job)
    {
        if (job.Thumbnails is null)
        {
            job.Thumbnails = new PartThumbnailGenerator();
            job.Thumbnails.Begin(job.Record.TopLevelParts());
        }

        if (!job.Thumbnails.Step())
        {
            return;
        }

        FoldThumbnailResults(job);
        DisposeThumbnails(job);
        Transition(LoadJobState.RefreshEditor);
    }

    /// <summary>Resets the vehicle editor's part-diameter cache, then completes the job.</summary>
    private static void StateRefreshEditor(LoadJob job)
    {
        EditorRefresh.AfterLoad();
        CompleteJob(job);
    }

    /// <summary>
    /// Records a thumbnail outcome per part. A part that legitimately has no thumbnail (a SubPart) is
    /// <see cref="PartLoadStatus.Skipped" />; anything else that failed to render is
    /// <see cref="PartLoadStatus.Degraded" />. A successful render never upgrades a part that an
    /// earlier state already degraded.
    /// </summary>
    private static void FoldThumbnailResults(LoadJob job)
    {
        PartThumbnailGenerator generator = job.Thumbnails!;
        LoadedModRecord record = job.Record;
        int rendered = 0;

        foreach ((string partId, bool wasRendered, string reason) in generator.Results)
        {
            if (wasRendered)
            {
                rendered++;
                continue;
            }

            bool deliberate = FindNewPart(record, partId)?.IsSubPart ?? false;
            record.SetResult(
                partId,
                deliberate ? PartLoadStatus.Skipped : PartLoadStatus.Degraded,
                string.IsNullOrEmpty(reason) ? "no thumbnail" : "no thumbnail: " + reason);
        }

        LogLine("rendered " + rendered + "/" + generator.Results.Count + " thumbnail(s).");
    }

    /// <summary>Finishes a successful job: measures it, registers it and stamps the manifest.</summary>
    private static void CompleteJob(LoadJob job)
    {
        LoadedModRecord record = job.Record;
        record.LoadedAtUtc = DateTime.UtcNow;
        record.VertexBytesUsed = SaturatingDelta(MeshBudget.UsedVertexBytes, record.CursorsBefore.VertexBytes);
        record.IndexBytesUsed = SaturatingDelta(MeshBudget.UsedIndexBytes, record.CursorsBefore.IndexBytes);

        RuntimeModRegistry.Add(record);

        if (job.CreatedByPaste)
        {
            // Without a manifest entry the mod would not load at the next launch, and any vehicle
            // saved with these parts would fail to resolve them.
            ModFolderWriter.EnsureManifestEntry(job.ModId);
        }

        _state = LoadJobState.Done;
        LogLine("done — '" + record.ModId + "' loaded " + record.NewParts.Count + " part(s) using "
            + Mib(record.VertexBytesUsed) + " MiB vtx / " + Mib(record.IndexBytesUsed) + " MiB idx.");
    }

    private static PartTemplate? FindNewPart(LoadedModRecord record, string partId)
    {
        foreach (PartTemplate part in record.NewParts)
        {
            if (string.Equals(part.Id, partId, StringComparison.OrdinalIgnoreCase))
            {
                return part;
            }
        }

        return null;
    }

    private static string Describe(IBinder binder) => binder switch
    {
        MeshReference mesh => "mesh '" + mesh.Id + "'",
        TextureReference texture => "texture '" + texture.Id + "'",
        _ => binder.GetType().Name,
    };

    private static ulong SaturatingDelta(uint now, uint before) => now <= before ? 0uL : now - before;
}
