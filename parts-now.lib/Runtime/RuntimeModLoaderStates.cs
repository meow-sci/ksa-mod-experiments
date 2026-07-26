// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KSA;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The disk and CPU half of the <see cref="RuntimeModLoader" /> state machine: validation, writing
/// the mod folder, building the <c>Mod</c>, registering the bundles and running the loaders.
/// </summary>
public static partial class RuntimeModLoader
{
    /// <summary>
    /// Parses every submitted document and runs all fifteen validation rules over them as one set.
    /// For a reload this is also where the previous load is purged — after validation passes, so a
    /// bundle that no longer validates leaves the loaded mod exactly as it was.
    /// </summary>
    private static void StateValidate(LoadJob job)
    {
        List<(string Name, string Xml)> documents;
        if (job.Kind == LoadJobKind.Install)
        {
            documents = CollectPastedDocuments(job);
        }
        else
        {
            List<(string Name, string Xml)>? fromDisk = CollectFolderDocuments(job, out string? readError);
            if (fromDisk is null)
            {
                Fail(job, readError ?? "the mod's documents could not be read.");
                return;
            }

            documents = fromDisk;
        }

        if (documents.Count == 0)
        {
            Fail(job, "there is nothing to load — no non-empty XML document was supplied.");
            return;
        }

        foreach ((string name, string xml) in documents)
        {
            if (BundleParser.TryParse(name, xml, out ParsedBundle? parsed, out string? error) && parsed is not null)
            {
                job.Bundles.Add(parsed);
            }
            else
            {
                IssueList.Add(BundleValidator.ParseFailure(name, error ?? "unknown parse failure."));
            }
        }

        // A reload's own ids are exempt from the "already registered" rules: the purge below removes
        // them before anything is registered again.
        string? reloadingModId = job.Kind == LoadJobKind.Reload ? job.ModId : null;
        IssueList.AddRange(BundleValidator.Validate(job.Bundles, reloadingModId, job.ModDirectory));

        int errors = 0;
        int warnings = 0;
        foreach (ValidationIssue issue in IssueList)
        {
            if (issue.Severity == IssueSeverity.Error)
            {
                errors++;
            }
            else
            {
                warnings++;
            }
        }

        LogLine("validated " + job.Bundles.Count + " document(s): " + errors + " error(s), "
            + warnings + " warning(s).");

        if (errors > 0)
        {
            Fail(job, "validation found " + errors + " error(s) — nothing was written, registered or bound.");
            return;
        }

        if (job.Kind == LoadJobKind.Reload && !PurgePreviousLoad(job))
        {
            return;
        }

        Transition(job.Kind == LoadJobKind.Install ? LoadJobState.WriteModFolder : LoadJobState.CreateMod);
    }

    /// <summary>Paste flow: creates the mod folder and its XML files on disk.</summary>
    private static void StateWriteModFolder(LoadJob job)
    {
        ModFolderRequest? request = job.Request;
        if (request is null)
        {
            Fail(job, "the paste flow reached WriteModFolder with no request.");
            return;
        }

        ModFolderResult result = ModFolderWriter.Write(request);
        if (!result.Success)
        {
            Fail(job, "the mod folder could not be written: " + (result.Error ?? "unknown error") + ".");
            return;
        }

        if (!string.Equals(result.ModDirectory, job.ModDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // Both come from ModIdValidator.ResolveTargetPath, so this should be impossible; the
            // record's ModDir is init-only and a purge would look in the wrong place.
            LogLine("WARNING — the mod folder was written to '" + result.ModDirectory
                + "' but the job expected '" + job.ModDirectory + "'.");
        }

        LogLine("wrote " + result.WrittenFiles.Count + " file(s) to " + result.ModDirectory);
        Transition(LoadJobState.CreateMod);
    }

    /// <summary>
    /// Builds the <c>Mod</c> the bundles are registered against, reusing KSA's own object if one
    /// exists for this id.
    /// </summary>
    /// <remarks>
    /// The <c>Mod</c> is deliberately NOT registered into <c>ModLibrary.Lookup</c>: nothing after
    /// boot iterates that collection, and staying out of it removes any chance of KSA loading the
    /// same bundles a second time. <c>Preload</c> is forced false because
    /// <c>FileReference.OnDataLoad</c> only calls <c>RegisterLoader</c> while it is false — a
    /// preloading mod would register templates whose files are never read.
    /// </remarks>
    private static void StateCreateMod(LoadJob job)
    {
        string tomlPath = Path.Combine(job.ModDirectory, ModLibrary.MOD_TOML);

        Mod mod;
        try
        {
            mod = ModLibrary.Find(job.ModId) ?? Mod.MakeUsing(job.ModId, tomlPath);
        }
        catch (Exception ex)
        {
            Fail(job, "could not build a Mod from " + tomlPath + ": " + ex.Message);
            return;
        }

        if (mod.Preload)
        {
            LogLine("WARNING — " + ModLibrary.MOD_TOML + " asked for preload; forcing it off, because "
                + "FileReference.OnDataLoad only registers loaders while Preload is false.");
            mod.Preload = false;
        }

        if (string.IsNullOrEmpty(mod.DirectoryPath))
        {
            Fail(job, "the Mod for '" + job.ModId + "' has no DirectoryPath, so no asset path can resolve.");
            return;
        }

        job.Mod = mod;
        job.Record.Mod = mod;
        LogLine("mod '" + mod.Id + "' ready at " + mod.DirectoryPath);
        Transition(LoadJobState.RegisterBundles);
    }

    /// <summary>
    /// Runs <c>AssetBundle.OnDataLoad</c> for each parsed bundle, which is what registers templates,
    /// materials and loaders. Cheap and free of file I/O.
    /// </summary>
    private static void StateRegisterBundles(LoadJob job)
    {
        Mod? mod = job.Mod;
        if (mod is null)
        {
            Fail(job, "RegisterBundles reached with no Mod.");
            return;
        }

        TakeMarks(job);

        string? failure = null;
        try
        {
            foreach (ParsedBundle parsed in job.Bundles)
            {
                parsed.Bundle.OnDataLoad(mod);
            }
        }
        catch (Exception ex)
        {
            // Whatever the earlier bundles registered is already live, so the deltas below still have
            // to be captured — otherwise the rollback would leave it behind.
            failure = "registering the bundles threw " + ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            CaptureRegistrationDeltas(job);
            CollectPartMetadata(job);
        }

        LoadedModRecord record = job.Record;
        LogLine("registered " + record.NewParts.Count + " part(s), " + record.NewGameData.Count
            + " game-data entr(ies), " + record.NewMaterials.Count + " material(s), "
            + record.NewLoaders.Count + " loader(s).");

        if (failure is not null)
        {
            Fail(job, failure);
            return;
        }

        Transition(LoadJobState.RunLoaders);
    }

    /// <summary>
    /// Reads the mod's GLB/KTX2 files on a background worker, then polls it to completion.
    /// </summary>
    /// <remarks>
    /// This MUST be off the main thread. <c>FileReference.Load()</c> calls <c>Loading.Task()</c> →
    /// <c>Loading.PushTask()</c> → <c>Loading.Current.OnFrame()</c>, which renders a complete ImGui
    /// frame and submits it — catastrophic inside the game's own frame. <c>Loading.OnFrame()</c>
    /// early-returns when <c>!Program.IsMainThread()</c>, so on a worker the whole thing is a no-op.
    /// Never try to null <c>Loading.Current</c> instead: <c>LoadTask</c>'s field initialiser throws
    /// when it is null, and that throw escapes <c>FileReference.Load</c>'s try block.
    /// <para>
    /// Serial rather than <c>Parallel.ForEachAsync</c>: a handful of files is not worth the
    /// concurrency risk, and serial keeps registration order deterministic.
    /// </para>
    /// </remarks>
    private static void StateRunLoaders(LoadJob job)
    {
        if (job.LoaderTask is null)
        {
            job.PendingLoaders.AddRange(job.Record.NewLoaders);
            List<ILoader> loaders = job.PendingLoaders;

            LogLine("loading " + loaders.Count + " file(s) on a background thread.");
            job.LoaderTask = Task.Run(() =>
            {
                foreach (ILoader loader in loaders)
                {
                    loader.Load();
                }
            });

            return;
        }

        if (!job.LoaderTask.IsCompleted)
        {
            return;
        }

        // Capture first: a faulted task may still have registered files, meshes and binders before it
        // threw, and the rollback has to know about them.
        CaptureLoadDeltas(job);

        if (job.LoaderTask.IsFaulted)
        {
            Exception? error = job.LoaderTask.Exception?.GetBaseException();
            Fail(job, "the loader step threw " + (error?.GetType().Name ?? "an exception") + ": "
                + (error?.Message ?? "unknown error"));
            return;
        }

        LoadedModRecord record = job.Record;
        LogLine("loaded " + record.NewFiles.Count + " file(s), producing " + record.NewMeshes.Count
            + " mesh(es) and " + record.NewBinders.Count + " binder(s).");

        string? problem = VerifyLoadersProduced(job);
        if (problem is not null)
        {
            Fail(job, problem);
            return;
        }

        Transition(LoadJobState.CheckMeshBudget);
    }

    /// <summary>The three paste-panel tabs, in write order, skipping the blank ones.</summary>
    private static List<(string Name, string Xml)> CollectPastedDocuments(LoadJob job)
    {
        List<(string Name, string Xml)> documents = new List<(string Name, string Xml)>(3);
        ModFolderRequest? request = job.Request;
        if (request is null)
        {
            return documents;
        }

        AddDocument(documents, "Assets", request.AssetsXml);
        AddDocument(documents, "Part", request.PartXml);
        AddDocument(documents, "GameData", request.GameDataXml);
        return documents;
    }

    /// <summary>
    /// The XML files a mod folder's <c>mod.toml</c> lists in its <c>assets</c> array, read in order.
    /// </summary>
    /// <remarks>
    /// <c>Mod.Assets</c> is a private field, so the array is read straight out of the TOML rather
    /// than off the <c>Mod</c> — which also means this works before <c>CreateMod</c> has run.
    /// </remarks>
    private static List<(string Name, string Xml)>? CollectFolderDocuments(LoadJob job, out string? error)
    {
        error = null;
        List<(string Name, string Xml)> documents = new List<(string Name, string Xml)>();
        string tomlPath = Path.Combine(job.ModDirectory, ModLibrary.MOD_TOML);

        TomlTable table;
        try
        {
            table = Toml.ToModel(File.ReadAllText(tomlPath));
        }
        catch (Exception ex)
        {
            error = tomlPath + " could not be read: " + ex.Message;
            return null;
        }

        if (!table.TryGetValue("assets", out object? value) || value is not TomlArray array)
        {
            error = tomlPath + " has no 'assets' array, so it declares no content to load.";
            return null;
        }

        foreach (object? item in array)
        {
            if (item is not string relativePath || relativePath.Length == 0)
            {
                continue;
            }

            string fullPath = Path.Combine(job.ModDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                error = "the asset file '" + relativePath + "' listed in " + ModLibrary.MOD_TOML
                    + " does not exist.";
                return null;
            }

            try
            {
                AddDocument(documents, relativePath, File.ReadAllText(fullPath));
            }
            catch (Exception ex)
            {
                error = "'" + relativePath + "' could not be read: " + ex.Message;
                return null;
            }
        }

        return documents;
    }

    private static void AddDocument(List<(string Name, string Xml)> documents, string name, string? xml)
    {
        if (!string.IsNullOrWhiteSpace(xml))
        {
            documents.Add((name, xml));
        }
    }

    /// <summary>
    /// Reload only: purges the previous load so the fresh one sees no duplicate ids and
    /// <c>FileReference.Load()</c> really re-reads changed files (constraint C5).
    /// </summary>
    /// <returns>True when the purge succeeded and the job may continue.</returns>
    private static bool PurgePreviousLoad(LoadJob job)
    {
        LoadedModRecord? previous = RuntimeModRegistry.Find(job.ModId);
        if (previous is null)
        {
            Fail(job, "'" + job.ModId + "' is no longer registered with parts-now, so it cannot be reloaded.");
            return false;
        }

        string? refusal = RuntimeModUnloader.CheckCanUnload(previous, loadJobInFlight: false);
        if (refusal is not null)
        {
            Fail(job, "the previous load of '" + job.ModId + "' cannot be purged: " + refusal);
            return false;
        }

        foreach (string line in RuntimeModUnloader.Purge(previous))
        {
            LogLines.Add(line);
        }

        job.PreviousPurged = true;
        return true;
    }
}
