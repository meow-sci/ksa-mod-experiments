// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.IO;
using KSA;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.PartsNowLib;

/// <summary>What a mod folder actually contains.</summary>
public enum ModFolderKind
{
    /// <summary>Has a non-empty <c>assets</c> array — the only kind parts-now can load.</summary>
    Content,

    /// <summary>Has a <c>[StarMap] EntryAssembly</c> and no assets — a code mod.</summary>
    StarMap,

    /// <summary>Has both an <c>assets</c> array and a StarMap entry assembly.</summary>
    Both,

    /// <summary>Neither — e.g. a folder that only defines <c>systems</c>, or an empty stub.</summary>
    Empty
}

/// <summary>How a mod folder relates to the running game.</summary>
public enum ModFolderState
{
    /// <summary>KSA loaded it during startup; it can never be reloaded or unloaded in-session.</summary>
    LoadedAtBoot,

    /// <summary>parts-now loaded it this session, so it may be reloaded or unloaded.</summary>
    LoadedByPartsNow,

    /// <summary>Present on disk but not loaded.</summary>
    NotLoaded
}

/// <summary>One entry of a mod's <c>assets</c> array.</summary>
/// <param name="RelativePath">The path exactly as written in <c>mod.toml</c>, relative to the mod folder.</param>
/// <param name="Exists">True when that file is actually present on disk.</param>
public sealed record ModAssetFile(string RelativePath, bool Exists);

/// <summary>A mod folder found under the game's mods directory.</summary>
/// <param name="ModId">Folder name — the id KSA uses for the mod.</param>
/// <param name="Directory">Absolute path of the folder.</param>
/// <param name="DisplayName"><c>name</c> from <c>mod.toml</c>, falling back to the id.</param>
/// <param name="Version"><c>version</c> from <c>mod.toml</c>; empty when absent.</param>
/// <param name="Author"><c>author</c> from <c>mod.toml</c>; empty when absent.</param>
/// <param name="Kind">What the folder contains.</param>
/// <param name="State">How it relates to the running game.</param>
/// <param name="AssetFiles">Its <c>assets</c> entries and whether each exists.</param>
/// <param name="Loadable">True when parts-now may offer Load / Reload for it.</param>
/// <param name="NotLoadableReason">Why it cannot be loaded, or null when it can.</param>
public sealed record ScannedMod(
    string ModId, string Directory, string DisplayName, string Version, string Author,
    ModFolderKind Kind, ModFolderState State,
    IReadOnlyList<ModAssetFile> AssetFiles,
    bool Loadable, string? NotLoadableReason);

/// <summary>
/// Read-only survey of the mods directory: what is on disk, what it contains, and what parts-now is
/// allowed to do with it.
/// </summary>
/// <remarks>
/// A single unreadable or malformed folder is logged and skipped — it never aborts the scan.
/// Nothing here writes to disk.
/// </remarks>
public static class ModFolderScanner
{
    private const string StarMapSection = "StarMap";
    private const string EntryAssemblyKey = "EntryAssembly";

    /// <summary>
    /// Scans every folder under <see cref="ModIdValidator.ModsDirectory" /> that contains a
    /// <c>mod.toml</c>, sorted by mod id. Returns an empty list when the mods directory is unknown
    /// or missing.
    /// </summary>
    public static List<ScannedMod> Scan()
    {
        var results = new List<ScannedMod>();
        string root = ModIdValidator.ModsDirectory;

        if (root.Length == 0)
        {
            Console.WriteLine("parts-now: the game's mods folder could not be resolved — nothing to scan.");
            return results;
        }

        string[] directories;
        try
        {
            if (!System.IO.Directory.Exists(root))
            {
                Console.WriteLine($"parts-now: the mods folder {root} does not exist — nothing to scan.");
                return results;
            }

            directories = System.IO.Directory.GetDirectories(root);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not list the mods folder {root}: {ex.Message}");
            return results;
        }

        foreach (string directory in directories)
        {
            try
            {
                ScannedMod? scanned = ScanOne(directory);
                if (scanned != null)
                {
                    results.Add(scanned);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: skipping mod folder '{directory}': {ex.Message}");
            }
        }

        results.Sort(static (a, b) => string.Compare(a.ModId, b.ModId, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private static ScannedMod? ScanOne(string directory)
    {
        string tomlPath = Path.Combine(directory, ModLibrary.MOD_TOML);
        if (!File.Exists(tomlPath))
        {
            return null;
        }

        string modId = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(modId))
        {
            return null;
        }

        TomlTable table = new TomlTable();
        string? parseError = null;
        try
        {
            table = Toml.ToModel(File.ReadAllText(tomlPath));
        }
        catch (Exception ex)
        {
            parseError = ex.Message;
            Console.WriteLine($"parts-now: could not parse {tomlPath}: {ex.Message}");
        }

        List<ModAssetFile> assetFiles = ReadAssetFiles(table, directory);
        bool hasStarMap = HasStarMapEntryAssembly(table);
        ModFolderKind kind = ClassifyKind(assetFiles.Count > 0, hasStarMap);
        ModFolderState state = ClassifyState(modId);
        string? reason = NotLoadableReason(kind, state, assetFiles, parseError);

        return new ScannedMod(
            modId,
            directory,
            ReadString(table, "name", modId),
            ReadString(table, "version", string.Empty),
            ReadString(table, "author", string.Empty),
            kind,
            state,
            assetFiles,
            reason == null,
            reason);
    }

    private static List<ModAssetFile> ReadAssetFiles(TomlTable table, string directory)
    {
        var files = new List<ModAssetFile>();

        if (!table.TryGetValue("assets", out object? value) || value is not TomlArray array)
        {
            return files;
        }

        foreach (object? item in array)
        {
            if (item is not string relativePath || relativePath.Length == 0)
            {
                continue;
            }

            bool exists;
            try
            {
                exists = File.Exists(Path.Combine(directory, relativePath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: bad asset path '{relativePath}' in {directory}: {ex.Message}");
                exists = false;
            }

            files.Add(new ModAssetFile(relativePath, exists));
        }

        return files;
    }

    private static bool HasStarMapEntryAssembly(TomlTable table)
    {
        return table.TryGetValue(StarMapSection, out object? section)
            && section is TomlTable starMap
            && starMap.TryGetValue(EntryAssemblyKey, out object? assembly)
            && assembly is string name
            && name.Trim().Length > 0;
    }

    private static ModFolderKind ClassifyKind(bool hasAssets, bool hasStarMap)
    {
        if (hasAssets && hasStarMap)
        {
            return ModFolderKind.Both;
        }

        if (hasAssets)
        {
            return ModFolderKind.Content;
        }

        return hasStarMap ? ModFolderKind.StarMap : ModFolderKind.Empty;
    }

    /// <summary>
    /// Checks our own registry first: a mod parts-now loaded also has a <c>Mod</c> in
    /// <see cref="ModLibrary" />, so testing <c>ModLibrary.Find</c> first would mislabel it
    /// <see cref="ModFolderState.LoadedAtBoot" /> and permanently block reloading it.
    /// </summary>
    private static ModFolderState ClassifyState(string modId)
    {
        if (RuntimeModRegistry.IsLoadedByPartsNow(modId))
        {
            return ModFolderState.LoadedByPartsNow;
        }

        try
        {
            if (ModLibrary.Find(modId) != null)
            {
                return ModFolderState.LoadedAtBoot;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not check whether '{modId}' is loaded: {ex.Message}");
            return ModFolderState.LoadedAtBoot;
        }

        return ModFolderState.NotLoaded;
    }

    private static string? NotLoadableReason(
        ModFolderKind kind, ModFolderState state, List<ModAssetFile> assetFiles, string? parseError)
    {
        if (parseError != null)
        {
            return $"mod.toml could not be parsed: {parseError}";
        }

        if (kind == ModFolderKind.StarMap)
        {
            return "StarMap-only mod — no content assets to load";
        }

        if (kind == ModFolderKind.Empty)
        {
            return "no content assets to load";
        }

        var missing = new List<string>();
        int present = 0;
        foreach (ModAssetFile file in assetFiles)
        {
            if (file.Exists)
            {
                present++;
            }
            else
            {
                missing.Add(file.RelativePath);
            }
        }

        if (present == 0)
        {
            return missing.Count == 0
                ? "no content assets to load"
                : "missing asset file(s): " + string.Join(", ", missing);
        }

        if (state == ModFolderState.LoadedAtBoot)
        {
            return "loaded at startup — restart the game to reload";
        }

        return null;
    }

    private static string ReadString(TomlTable table, string key, string fallback)
    {
        if (table.TryGetValue(key, out object? value) && value is string text && text.Trim().Length > 0)
        {
            return text.Trim();
        }

        return fallback;
    }
}
