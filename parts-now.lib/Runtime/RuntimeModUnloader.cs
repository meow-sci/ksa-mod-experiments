// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Undoes a runtime load: the safety gate (T11.1), the full purge (T11.2) and the rollback of a
/// half-finished load (T11.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reload (T11.3).</b> There is no separate reload path: <c>Reload(modId)</c> is exactly
/// <c>Unload(modId)</c> followed by <c>Load(modDir)</c>, and the purge is the entire reason that
/// works. Constraint C5 says <c>SerializedCollection.Register</c> returns <c>false</c> for a
/// duplicate id and every caller reads that as "this is a reference to the existing entry" — so
/// <c>FileReference.Load()</c> would skip <c>DoLoad()</c> and a changed GLB/KTX2 at the same path
/// would never be re-read. Because <see cref="Purge" /> removes every id this mod introduced from
/// every registry (list <b>and</b> the private hash dictionary behind <c>Find</c>), the following
/// load sees no duplicates, re-registers cleanly, and really does re-read the files from disk.
/// </para>
/// <para>
/// Game-thread only, and only while no load job is in flight — <see cref="GameRegistry.Unregister" />
/// deliberately does not take <c>SerializedCollection</c>'s private lock.
/// </para>
/// </remarks>
public static class RuntimeModUnloader
{
    /// <summary>
    /// T11.1 — decides whether a record may be purged.
    /// </summary>
    /// <param name="record">The record to test.</param>
    /// <param name="loadJobInFlight">True when <c>RuntimeModLoader</c> still has a job running.</param>
    /// <returns>
    /// <c>null</c> when it is safe to purge, otherwise a specific user-facing reason naming the
    /// vehicle or part that blocks it. Never throws: a failure to inspect the game state is itself
    /// a refusal (fail closed).
    /// </returns>
    public static string? CheckCanUnload(LoadedModRecord record, bool loadJobInFlight) =>
        RuntimeModUnloadGate.Check(record, loadJobInFlight);

    /// <summary>
    /// T11.2 — frees everything a load registered, in the strict documented order.
    /// </summary>
    /// <param name="record">The record to purge. Call <see cref="CheckCanUnload" /> first.</param>
    /// <returns>One log line per step, already mirrored to the console.</returns>
    /// <remarks>
    /// Never throws — every step is individually try/caught so one broken object cannot strand the
    /// rest of the mod half-registered. Idempotent at the record level via
    /// <see cref="LoadedModRecord.Purged" />: the GPU disposals below are <b>not</b> safe to repeat.
    /// </remarks>
    public static List<string> Purge(LoadedModRecord record) => Purge(record, recordLeak: true);

    private static List<string> Purge(LoadedModRecord record, bool recordLeak)
    {
        List<string> log = new List<string>();

        if (record is null)
        {
            Log(log, "purge skipped — no record supplied.");
            return log;
        }

        if (record.Purged)
        {
            Log(log, "purge skipped — '" + record.ModId + "' was already purged this session.");
            return log;
        }

        // Set BEFORE any work: ThumbnailReference.Dispose() and TextureReference.Dispose(Device)
        // double-free if they run twice, so even a purge that throws its way through every step must
        // never be attempted a second time.
        record.Purged = true;

        Log(log, "purging '" + record.ModId + "' — " + record.NewParts.Count + " part(s), "
            + record.NewMeshes.Count + " mesh(es), " + record.NewFiles.Count + " file(s), "
            + record.NewMaterials.Count + " material(s).");

        // Textures are split out here so step 5 (which disposes GPU resources) and step 7 (which
        // only unregisters) each touch every FileReference exactly once.
        List<TextureReference> textures = new List<TextureReference>();
        List<FileReference> otherFiles = new List<FileReference>();
        foreach (FileReference file in record.NewFiles)
        {
            if (file is TextureReference texture)
            {
                textures.Add(texture);
            }
            else
            {
                otherFiles.Add(file);
            }
        }

        Core.Renderer? renderer = null;
        Step(log, 0, "clear the part browser's hover preview", () =>
        {
            // VehicleEditor.DynamicThumbnail keeps _requestedTemplate/_activeTemplate and a _rootPart
            // built from them. Left pointing at a purged template it draws freed buffer contents, and
            // if its request version changes it re-runs ThumbnailCreator.AddPart -> PartInstance
            // .GetTemplate() -> ModLibrary.Get<PartTemplate>, which throws NullReferenceException from
            // OUTSIDE ThumbnailDynamic.Render's try/catch, i.e. straight out of Editor.OnPreRender.
            VehicleEditor? editor = Program.Editor;
            if (editor?.DynamicThumbnail is null)
            {
                return "no editor open";
            }

            editor.DynamicThumbnail.SetSelectedPart(null);
            return "hover preview cleared";
        });

        Step(log, 1, "wait for the device to go idle", () =>
        {
            // Nothing in an in-flight frame may still reference the images and buffers freed below.
            renderer = Program.GetRenderer();
            renderer.Device.WaitIdle();
            return "device idle";
        });

        Step(log, 2, "dispose + unregister part templates",
            () => RuntimeModPurgeSteps.PurgeParts(record));
        Step(log, 3, "unregister part game data",
            () => RuntimeModPurgeSteps.PurgeGameData(record));
        Step(log, 4, "purge model instances and ray tracers",
            () => RuntimeModPurgeSteps.PurgeModelInstances(record));
        Step(log, 5, "dispose + unregister textures",
            () => RuntimeModPurgeSteps.PurgeTextures(textures, renderer));
        Step(log, 6, "measure, dispose + unregister meshes",
            () => RuntimeModPurgeSteps.PurgeMeshes(record, recordLeak));
        Step(log, 7, "unregister remaining file references",
            () => RuntimeModPurgeSteps.PurgeFiles(otherFiles));
        Step(log, 8, "unregister materials",
            () => RuntimeModPurgeSteps.PurgeMaterials(record));
        Step(log, 9, "drop loaders and binders",
            () => RuntimeModPurgeSteps.PurgeLoadersAndBinders(record));
        Step(log, 10, "refresh the vehicle editor", () =>
        {
            // Same nudge a load needs: PartWindow._diameterCache is built lazily and would otherwise
            // still offer diameters that only the purged parts provided.
            EditorRefresh.AfterLoad();
            return "part diameter cache reset";
        });
        Step(log, 11, "forget the mod record", () =>
            RuntimeModRegistry.Remove(record.ModId) ? "record removed" : "record was not registered");

        return log;
    }

    /// <summary>
    /// T11.4 — unwinds a load that failed part-way through.
    /// </summary>
    /// <param name="record">The record, populated with whatever the load managed to register.</param>
    /// <returns>The purge log plus the cursor-restore line.</returns>
    /// <remarks>
    /// This is the same purge over a partially populated record, followed by rewinding the shared
    /// interleaved buffer's bump cursors to <see cref="LoadedModRecord.CursorsBefore" />. Rewinding
    /// is only valid because a rollback aborts <b>before</b> anything was bound: a bound mesh has
    /// already copied its data to an absolute offset inside the shared buffer, so reusing that range
    /// would corrupt it. For the same reason no leak is recorded here — the cursors go back, so
    /// nothing was orphaned.
    /// </remarks>
    public static List<string> Rollback(LoadedModRecord record)
    {
        // recordLeak: false — the cursor rewind below reclaims those bytes, so charging them to the
        // leak counter as well would double-count them against the headroom.
        List<string> log = Purge(record, recordLeak: false);

        if (record is null)
        {
            return log;
        }

        try
        {
            if (MeshBudget.RestoreCursors(record.CursorsBefore))
            {
                Log(log, "rollback: mesh allocation cursors restored to "
                    + record.CursorsBefore.VertexBytes + " vtx / "
                    + record.CursorsBefore.IndexBytes + " idx bytes (nothing was bound, so no leak).");
            }
            else
            {
                Log(log, "rollback: the mesh allocation cursors were NOT restored — the snapshot was "
                    + "below the startup watermark. Those bytes stay spent for this session.");
            }
        }
        catch (Exception ex)
        {
            Log(log, "rollback: FAILED to restore mesh allocation cursors — " + ex.Message);
        }

        return log;
    }

    /// <summary>Runs one purge step, logging its outcome and swallowing any failure.</summary>
    /// <param name="log">The log being built.</param>
    /// <param name="number">The step's number in the documented purge order.</param>
    /// <param name="description">What the step does.</param>
    /// <param name="action">The step body; returns a short detail string for the log.</param>
    private static void Step(List<string> log, int number, string description, Func<string> action)
    {
        try
        {
            string detail = action();
            Log(log, number + ". " + description + " — " + detail);
        }
        catch (Exception ex)
        {
            // A failed step must not strand the remaining ones: the mod would stay half-registered,
            // which is strictly worse than a leak.
            Log(log, number + ". FAILED: " + description + " — " + ex.Message);
        }
    }

    private static void Log(List<string> log, string message)
    {
        log.Add(message);
        Console.WriteLine("parts-now: " + message);
    }
}
