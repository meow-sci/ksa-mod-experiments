// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KSA;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Everything the user typed into the "Paste XML" panel. Any of the three XML documents may be
/// null or blank; a blank document simply produces no file.
/// </summary>
/// <param name="ModId">Validated kebab-case mod id; also the folder name and the file-name prefix.</param>
/// <param name="DisplayName">Human-readable name written as <c>name</c> in <c>mod.toml</c>.</param>
/// <param name="Author">Written as <c>author</c> in <c>mod.toml</c>.</param>
/// <param name="Version">Written as <c>version</c> in <c>mod.toml</c>.</param>
/// <param name="AssetsXml">Contents of the Assets tab (meshes, textures, materials).</param>
/// <param name="PartXml">Contents of the Part tab (Part / SubPart templates).</param>
/// <param name="GameDataXml">Contents of the GameData tab (PartGameData).</param>
public sealed record ModFolderRequest(
    string ModId, string DisplayName, string Author, string Version,
    string? AssetsXml, string? PartXml, string? GameDataXml);

/// <summary>Outcome of <see cref="ModFolderWriter.Write" />.</summary>
/// <param name="Success">True when every planned file landed on disk.</param>
/// <param name="ModDirectory">Absolute folder that was written to, even on failure.</param>
/// <param name="WrittenFiles">File names (not paths) that were written, relative to <paramref name="ModDirectory" />.</param>
/// <param name="Error">Human-readable failure reason, or null on success.</param>
public sealed record ModFolderResult(bool Success, string ModDirectory, List<string> WrittenFiles, string? Error);

/// <summary>
/// Writes a parts-now mod folder (<c>mod.toml</c> plus up to three XML asset bundles) into the
/// mods directory KSA discovered, and registers the mod in the game's manifest so it also loads at
/// the next launch.
/// </summary>
/// <remarks>
/// Files are written UTF-8 without a BOM and with <c>\n</c> line endings, each via a <c>.tmp</c>
/// sibling that is moved into place, so an interrupted write never leaves a half-valid mod folder.
/// <see cref="Write" /> never throws.
/// </remarks>
public static class ModFolderWriter
{
    /// <summary>The <c>description</c> value stamped into every folder parts-now creates.</summary>
    public const string Description = "Created in-game by parts-now";

    private const string DefaultVersion = "1.0.0";
    private const string DefaultAuthor = "parts-now";

    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates (or updates) <c>&lt;mods&gt;/&lt;modId&gt;/</c> with a <c>mod.toml</c> and one XML file
    /// per non-empty document. Never throws — failures come back as
    /// <see cref="ModFolderResult.Success" /> false with a reason.
    /// </summary>
    /// <param name="request">The mod folder to write.</param>
    public static ModFolderResult Write(ModFolderRequest request)
    {
        if (request == null)
        {
            return new ModFolderResult(false, string.Empty, new List<string>(), "no request was supplied.");
        }

        string modId = (request.ModId ?? string.Empty).Trim();
        if (modId.Length == 0)
        {
            return new ModFolderResult(false, string.Empty, new List<string>(), "mod id is empty.");
        }

        string modDir = ModIdValidator.ResolveTargetPath(modId);
        if (modDir.Length == 0)
        {
            return new ModFolderResult(false, string.Empty, new List<string>(),
                "the game's mods folder could not be resolved.");
        }

        List<PlannedFile> assetFiles = PlanAssetFiles(request);
        if (assetFiles.Count == 0)
        {
            return new ModFolderResult(false, modDir, new List<string>(),
                "nothing to write — all three XML documents were empty.");
        }

        var temporaryFiles = new List<string>();
        var written = new List<string>();

        try
        {
            Directory.CreateDirectory(modDir);

            var assetNames = new List<string>(assetFiles.Count);
            foreach (PlannedFile file in assetFiles)
            {
                WriteAtomic(Path.Combine(modDir, file.FileName), file.Content, temporaryFiles);
                assetNames.Add(file.FileName);
                written.Add(file.FileName);
            }

            // mod.toml goes last so a failed asset write never advertises a file that is not there.
            string toml = BuildModToml(modDir, request, assetNames);
            WriteAtomic(Path.Combine(modDir, ModLibrary.MOD_TOML), toml, temporaryFiles);
            written.Insert(0, ModLibrary.MOD_TOML);

            Console.WriteLine($"parts-now: wrote {written.Count} file(s) to {modDir}");
            return new ModFolderResult(true, modDir, written, null);
        }
        catch (Exception ex)
        {
            CleanUpTemporaries(temporaryFiles);
            Console.WriteLine($"parts-now: failed to write mod folder '{modDir}': {ex}");
            return new ModFolderResult(false, modDir, written, ex.Message);
        }
    }

    /// <summary>
    /// Adds an enabled, non-new manifest entry for the mod so KSA also loads it at the next launch.
    /// A vehicle saved with runtime-loaded parts will not resolve without this.
    /// </summary>
    /// <param name="modId">The mod id to register.</param>
    /// <returns>True when the manifest already had the entry or gained one; false on any failure.</returns>
    /// <remarks>
    /// Deliberately does not use <c>new ModEntry(id, count)</c>: that constructor sets
    /// <c>Enabled = false, New = true</c>, which pops the game's "confirm mods" dialog at next boot.
    /// An entry that already exists is left exactly as the user configured it.
    /// </remarks>
    public static bool EnsureManifestEntry(string modId)
    {
        string id = (modId ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            Console.WriteLine("parts-now: cannot add a manifest entry for an empty mod id.");
            return false;
        }

        try
        {
            ModManifest? manifest = ModLibrary.Manifest;
            if (manifest?.Mods == null)
            {
                Console.WriteLine(
                    $"parts-now: the game's mod manifest is not available — '{id}' will not load at "
                    + "the next launch. Enable it manually in the mods menu.");
                return false;
            }

            foreach (ModEntry existing in manifest.Mods)
            {
                if (existing == null || !string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!existing.Enabled)
                {
                    Console.WriteLine(
                        $"parts-now: '{id}' is already in the mod manifest but disabled — leaving it "
                        + "alone; enable it in the mods menu to load it at the next launch.");
                }

                return true;
            }

            manifest.Mods.Add(new ModEntry { Id = id, Enabled = true, New = false });
            manifest.Save();
            Console.WriteLine($"parts-now: added '{id}' to {ModLibrary.LocalManifestPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to add '{id}' to the mod manifest: {ex}");
            return false;
        }
    }

    /// <summary>
    /// The file names <see cref="Write" /> would produce for a request, in write order
    /// (<c>mod.toml</c> first), for showing a preview before anything touches disk.
    /// </summary>
    /// <param name="request">The mod folder that would be written.</param>
    public static List<string> PlanFileNames(ModFolderRequest request)
    {
        var names = new List<string>();
        if (request == null)
        {
            return names;
        }

        names.Add(ModLibrary.MOD_TOML);
        foreach (PlannedFile file in PlanAssetFiles(request))
        {
            names.Add(file.FileName);
        }

        return names;
    }

    private static List<PlannedFile> PlanAssetFiles(ModFolderRequest request)
    {
        string id = (request.ModId ?? string.Empty).Trim();
        var files = new List<PlannedFile>(3);

        if (id.Length == 0)
        {
            return files;
        }

        // Order is assets -> part -> gamedata for readability; KSA merges PartGameData onto its Part
        // after every bundle in the mod has been registered, so the order does not affect loading.
        AddIfPresent(files, $"{id}-assets.xml", request.AssetsXml);
        AddIfPresent(files, $"{id}-part.xml", request.PartXml);
        AddIfPresent(files, $"{id}-gamedata.xml", request.GameDataXml);
        return files;
    }

    private static void AddIfPresent(List<PlannedFile> files, string fileName, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            files.Add(new PlannedFile(fileName, content));
        }
    }

    /// <summary>
    /// Builds the <c>mod.toml</c> text, merging into an existing file when the folder is being
    /// reused so unrelated keys (a <c>[StarMap]</c> section, <c>systems</c>, hand-added assets) survive.
    /// </summary>
    private static string BuildModToml(string modDir, ModFolderRequest request, List<string> assetNames)
    {
        string tomlPath = Path.Combine(modDir, ModLibrary.MOD_TOML);
        TomlTable root = new TomlTable();

        if (File.Exists(tomlPath))
        {
            try
            {
                root = Toml.ToModel(File.ReadAllText(tomlPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"parts-now: {tomlPath} is not valid TOML ({ex.Message}) — writing a fresh one.");
                root = new TomlTable();
            }
        }

        root["name"] = Fallback(request.DisplayName, request.ModId);
        root["description"] = Description;
        root["version"] = Fallback(request.Version, DefaultVersion);
        root["author"] = Fallback(request.Author, DefaultAuthor);
        root["assets"] = MergeAssets(root, assetNames);

        return Toml.FromModel(ScalarsBeforeTables(root));
    }

    private static TomlArray MergeAssets(TomlTable root, List<string> assetNames)
    {
        var merged = new List<string>();

        if (root.TryGetValue("assets", out object? existing) && existing is TomlArray existingArray)
        {
            foreach (object? item in existingArray)
            {
                if (item is string entry && entry.Length > 0)
                {
                    merged.Add(entry);
                }
            }
        }

        foreach (string name in assetNames)
        {
            if (!merged.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(name);
            }
        }

        var array = new TomlArray();
        foreach (string entry in merged)
        {
            array.Add(entry);
        }

        return array;
    }

    /// <summary>
    /// Re-orders a table so every scalar key precedes every sub-table. TOML puts keys written after
    /// a <c>[section]</c> header inside that section, so a merged file whose <c>[StarMap]</c> table
    /// was parsed first would otherwise swallow the <c>assets</c> array.
    /// </summary>
    private static TomlTable ScalarsBeforeTables(TomlTable table)
    {
        var ordered = new TomlTable();

        foreach (KeyValuePair<string, object> pair in table)
        {
            if (!IsTableLike(pair.Value))
            {
                ordered[pair.Key] = pair.Value;
            }
        }

        foreach (KeyValuePair<string, object> pair in table)
        {
            if (IsTableLike(pair.Value))
            {
                ordered[pair.Key] = pair.Value;
            }
        }

        return ordered;
    }

    private static bool IsTableLike(object value) => value is TomlTable || value is TomlTableArray;

    /// <summary>
    /// Writes <paramref name="content" /> to a <c>.tmp</c> sibling and moves it into place. The temp
    /// path is tracked while it exists so a later failure can clean it up.
    /// </summary>
    private static void WriteAtomic(string destination, string content, List<string> temporaryFiles)
    {
        string temp = destination + ".tmp";
        temporaryFiles.Add(temp);

        File.WriteAllText(temp, NormaliseLineEndings(content), Utf8NoBom);

        // overwrite:false is what the plan asks for and is what a fresh mod folder always hits; a
        // reused folder (or a merged mod.toml) has the destination already present, and there
        // overwrite:true is the only way the move can succeed.
        File.Move(temp, destination, overwrite: File.Exists(destination));

        temporaryFiles.Remove(temp);
    }

    private static void CleanUpTemporaries(List<string> temporaryFiles)
    {
        foreach (string temp in temporaryFiles)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: could not remove the temporary file {temp}: {ex.Message}");
            }
        }

        temporaryFiles.Clear();
    }

    private static string NormaliseLineEndings(string content) =>
        content.Replace("\r\n", "\n").Replace("\r", "\n");

    private static string Fallback(string? value, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
    }

    private sealed record PlannedFile(string FileName, string Content);
}
