// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.IO;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The four things the UI calls on <see cref="RuntimeModLoader" />: the three ways to start a load
/// job and the one synchronous unload, plus the preconditions they all share.
/// </summary>
/// <remarks>
/// Every entry point refuses rather than throws, and every refusal is a sentence the UI can show
/// verbatim next to the disabled button.
/// </remarks>
public static partial class RuntimeModLoader
{
    /// <summary>
    /// Paste flow: validate the pasted documents, write a brand new mod folder for them, then load it.
    /// Nothing is written when validation fails.
    /// </summary>
    /// <param name="request">The paste panel's form contents.</param>
    /// <param name="refusal">Why the job could not be started, or null on success.</param>
    /// <returns>True when a job was started.</returns>
    public static bool StartInstall(ModFolderRequest request, out string? refusal)
    {
        if (request is null)
        {
            refusal = "no mod folder request was supplied.";
            return false;
        }

        if (!CanStart(out refusal))
        {
            return false;
        }

        string modId = (request.ModId ?? string.Empty).Trim();
        List<string> problems = ModIdValidator.Validate(modId);
        if (problems.Count > 0)
        {
            refusal = "mod id '" + modId + "' is unusable: " + string.Join(" ", problems);
            return false;
        }

        string modDirectory = ModIdValidator.ResolveTargetPath(modId);
        if (modDirectory.Length == 0)
        {
            refusal = "the game's mods folder could not be resolved, so there is nowhere to write '"
                + modId + "'.";
            return false;
        }

        Begin(new LoadJob
        {
            Kind = LoadJobKind.Install,
            ModId = modId,
            ModDirectory = modDirectory,
            Request = request,
            CreatedByPaste = true,
            Record = new LoadedModRecord
            {
                ModId = modId,
                ModDir = modDirectory,
                CreatedByPaste = true,
            },
        });

        refusal = null;
        return true;
    }

    /// <summary>
    /// Folder flow: load an existing mod folder that is not loaded yet.
    /// </summary>
    /// <param name="modDirectory">Absolute path of the folder containing <c>mod.toml</c>.</param>
    /// <param name="modId">The mod id, which is the folder's name.</param>
    /// <param name="refusal">Why the job could not be started, or null on success.</param>
    /// <returns>True when a job was started.</returns>
    public static bool StartLoad(string modDirectory, string modId, out string? refusal)
    {
        if (!CanStart(out refusal))
        {
            return false;
        }

        string id = (modId ?? string.Empty).Trim();
        string directory = (modDirectory ?? string.Empty).Trim();

        if (id.Length == 0 || directory.Length == 0)
        {
            refusal = "a mod id and a mod folder are both required.";
            return false;
        }

        if (!Directory.Exists(directory))
        {
            refusal = "'" + directory + "' does not exist.";
            return false;
        }

        if (!File.Exists(Path.Combine(directory, ModLibrary.MOD_TOML)))
        {
            refusal = "'" + directory + "' has no " + ModLibrary.MOD_TOML + ".";
            return false;
        }

        if (RuntimeModRegistry.IsLoadedByPartsNow(id))
        {
            refusal = "'" + id + "' is already loaded by parts-now — use Reload instead.";
            return false;
        }

        if (IsLoadedAtBoot(id))
        {
            refusal = "'" + id + "' was loaded at startup — restart the game to reload it. parts-now "
                + "cannot account for what KSA registered on its behalf.";
            return false;
        }

        Begin(new LoadJob
        {
            Kind = LoadJobKind.Load,
            ModId = id,
            ModDirectory = directory,
            Record = new LoadedModRecord { ModId = id, ModDir = directory },
        });

        refusal = null;
        return true;
    }

    /// <summary>
    /// Purges a mod parts-now loaded this session and loads it again from disk, so an edited GLB,
    /// texture or XML file really is re-read.
    /// </summary>
    /// <param name="modId">The mod id to reload.</param>
    /// <param name="refusal">Why the job could not be started, or null on success.</param>
    /// <returns>True when a job was started.</returns>
    /// <remarks>
    /// The purge does not happen here: it runs at the end of <see cref="LoadJobState.Validate" />, so
    /// a bundle that no longer validates leaves the already-loaded mod untouched.
    /// </remarks>
    public static bool StartReload(string modId, out string? refusal)
    {
        if (!CanStart(out refusal))
        {
            return false;
        }

        string id = (modId ?? string.Empty).Trim();
        LoadedModRecord? existing = RuntimeModRegistry.Find(id);
        if (existing is null)
        {
            refusal = "parts-now did not load '" + id + "' this session, so it cannot reload it.";
            return false;
        }

        refusal = RuntimeModUnloader.CheckCanUnload(existing, IsBusy);
        if (refusal is not null)
        {
            return false;
        }

        string directory = existing.ModDir;
        if (directory.Length == 0 || !Directory.Exists(directory))
        {
            refusal = "the folder '" + directory + "' that '" + id + "' was loaded from is gone.";
            return false;
        }

        Begin(new LoadJob
        {
            Kind = LoadJobKind.Reload,
            ModId = id,
            ModDirectory = directory,
            CreatedByPaste = existing.CreatedByPaste,
            Record = new LoadedModRecord
            {
                ModId = id,
                ModDir = directory,
                CreatedByPaste = existing.CreatedByPaste,
            },
        });

        refusal = null;
        return true;
    }

    /// <summary>
    /// Purges a mod parts-now loaded this session without loading anything back. Synchronous — the
    /// purge is bounded work and does not need a job.
    /// </summary>
    /// <param name="modId">The mod id to unload.</param>
    /// <param name="refusal">Why the unload was refused, or null on success.</param>
    /// <returns>True when the mod was purged.</returns>
    public static bool Unload(string modId, out string? refusal)
    {
        string id = (modId ?? string.Empty).Trim();
        LoadedModRecord? record = RuntimeModRegistry.Find(id);
        if (record is null)
        {
            refusal = "parts-now did not load '" + id + "' this session, so it cannot unload it.";
            return false;
        }

        refusal = RuntimeModUnloader.CheckCanUnload(record, IsBusy);
        if (refusal is not null)
        {
            return false;
        }

        // Clear the previous job before logging, so the unload's lines are not rendered under a stale
        // "Done"/"Failed" banner and the results table stops showing parts that no longer exist.
        Reset();

        LogLine("unloading '" + id + "' (" + record.PartIds.Count + " part(s)).");
        foreach (string line in RuntimeModUnloader.Purge(record))
        {
            LogLines.Add(line);
        }

        refusal = null;
        return true;
    }

    /// <summary>Shared preconditions for all three entry points.</summary>
    private static bool CanStart(out string? refusal)
    {
        if (IsBusy)
        {
            refusal = "a parts-now job is already running (" + StatusText + ") — wait for it to finish.";
            return false;
        }

        if (!GameRegistry.IsHealthy)
        {
            refusal = "parts-now cannot reach KSA's asset registries (see the self-test in the log) — "
                + "loading is disabled until that is fixed.";
            return false;
        }

        if (!MeshBudget.IsUsable)
        {
            refusal = "no mesh headroom was reserved at startup"
                + (MeshBudget.FailureReason is null ? string.Empty : " (" + MeshBudget.FailureReason + ")")
                + " — runtime mesh loading would corrupt the shared vertex buffer.";
            return false;
        }

        refusal = null;
        return true;
    }

    /// <summary>Clears the previous result and installs a fresh job in the Validate state.</summary>
    private static void Begin(LoadJob job)
    {
        Reset();
        _job = job;
        _state = LoadJobState.Validate;
        LogLine(job.Kind + " '" + job.ModId + "' from " + job.ModDirectory);
    }

    /// <summary>
    /// True when KSA itself loaded this mod at boot. parts-now must never offer to load or reload
    /// such a mod: it would double-register, and unloading it would purge templates parts-now never
    /// registered. A mod parts-now loaded is deliberately absent from <c>ModLibrary.Lookup</c>, so
    /// this test cannot mistake one of ours for a boot mod.
    /// </summary>
    private static bool IsLoadedAtBoot(string modId)
    {
        try
        {
            return ModLibrary.Find(modId) is not null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("parts-now: could not check whether '" + modId + "' is loaded: " + ex.Message);
            return true;
        }
    }
}
