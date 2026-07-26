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
/// Construction of the shared validation context: the indexes every rule reads, built once per
/// <see cref="BundleValidator.Validate" /> call.
/// </summary>
public static partial class BundleValidator
{
    private static ValidationContext BuildContext(
        IReadOnlyList<ParsedBundle> bundles,
        string? reloadingModId,
        string modDirectory)
    {
        ValidationContext context = new ValidationContext(bundles, reloadingModId, modDirectory);

        try
        {
            context.ModDirectoryFullPath = string.IsNullOrWhiteSpace(modDirectory)
                ? string.Empty
                : Path.GetFullPath(modDirectory);
        }
        catch (Exception ex)
        {
            context.ModDirectoryFullPath = string.Empty;
            Console.WriteLine("parts-now: mod directory '" + modDirectory + "' is unusable — " + ex.Message);
        }

        context.ModDirectoryAvailable = context.ModDirectoryFullPath.Length > 0
            && Directory.Exists(context.ModDirectoryFullPath);

        foreach (ParsedBundle bundle in bundles)
        {
            IndexBundle(context, bundle);
        }

        try
        {
            // GetKnownEditorTags() rebuilds its set on every call, so it is resolved exactly once here
            // and reused by V7.
            context.KnownEditorTags = GameRegistry.GetKnownEditorTags();
        }
        catch (Exception ex)
        {
            context.KnownEditorTags = null;
            Console.WriteLine("parts-now: known editor tags could not be resolved — " + ex.Message);
        }

        return context;
    }

    private static void IndexBundle(ValidationContext context, ParsedBundle bundle)
    {
        foreach (PartTemplate part in BundleParser.TopLevelParts(bundle))
        {
            Index(context.DeclaredParts, part.Id, part);
            Declare(context, KindPart, part.Id, bundle.SourceName);
        }

        foreach (PartTemplate subPart in BundleParser.SubParts(bundle))
        {
            // Parts and SubParts share ModLibrary.AllParts, so they are one id space.
            Index(context.DeclaredParts, subPart.Id, subPart);
            Index(context.DeclaredSubParts, subPart.Id, subPart);
            Declare(context, KindPart, subPart.Id, bundle.SourceName);
        }

        foreach (PartGameDataReference gameData in BundleParser.GameData(bundle))
        {
            Index(context.DeclaredGameData, gameData.Id, gameData);
            Declare(context, KindGameData, gameData.Id, bundle.SourceName);
        }

        foreach (PbrMaterialReference material in BundleParser.Materials(bundle))
        {
            Index(context.DeclaredMaterials, material.Id, material);
            Declare(context, KindMaterial, material.Id, bundle.SourceName);
        }

        foreach (FileReference file in BundleParser.Files(bundle))
        {
            string id = BundleParser.EffectiveFileId(file, context.ModDirectory);
            if (id.Length > 0)
            {
                context.DeclaredFileIds.Add(id);
                Declare(context, KindFile, id, bundle.SourceName);
            }
        }

        foreach (MeshFileReference meshFile in BundleParser.MeshFiles(bundle))
        {
            // MeshFileReference.DoLoad names its single MeshReference after the file reference's id.
            string id = BundleParser.EffectiveFileId(meshFile, context.ModDirectory);
            if (id.Length > 0)
            {
                context.DeclaredMeshIds.Add(id);
                Declare(context, KindMesh, id, bundle.SourceName);
            }
        }

        foreach (MeshAtlasFileReference atlas in BundleParser.MeshAtlases(bundle))
        {
            IndexAtlasMeshes(context, bundle, atlas);
        }
    }

    private static void IndexAtlasMeshes(
        ValidationContext context,
        ParsedBundle bundle,
        MeshAtlasFileReference atlas)
    {
        // A mesh atlas declares one mesh per glTF mesh node, named by that node — the ids are only
        // knowable by reading the file, so it must exist on disk at validation time.
        string relative = atlas.LocalPath;
        if (string.IsNullOrEmpty(relative))
        {
            return;
        }

        if (!context.ModDirectoryAvailable)
        {
            context.AtlasProblems.Add((bundle.SourceName, relative,
                "the mod folder is not available yet, so its mesh ids could not be read"));
            return;
        }

        string absolute;
        try
        {
            absolute = Path.GetFullPath(Path.Combine(context.ModDirectoryFullPath, relative));
        }
        catch (Exception ex)
        {
            context.AtlasProblems.Add((bundle.SourceName, relative, ex.Message));
            return;
        }

        if (!File.Exists(absolute))
        {
            // V11 reports the missing file itself; here it only costs us the mesh id list.
            context.AtlasProblems.Add((bundle.SourceName, relative, "the file does not exist"));
            return;
        }

        try
        {
            foreach (string name in GlbMeshNames.Read(absolute))
            {
                context.DeclaredMeshIds.Add(name);
                Declare(context, KindMesh, name, bundle.SourceName);
            }
        }
        catch (Exception ex)
        {
            context.AtlasProblems.Add((bundle.SourceName, relative,
                ex.GetType().Name + ": " + ex.Message));
        }
    }

    private static void Index<T>(Dictionary<string, T> map, string id, T value)
    {
        if (!string.IsNullOrEmpty(id))
        {
            map[id] = value;
        }
    }

    private static void Declare(ValidationContext context, string kind, string id, string sourceName)
    {
        if (!string.IsNullOrEmpty(id))
        {
            context.Declared.Add((kind, id, sourceName));
        }
    }


    /// <summary>
    /// Everything the rules share: the submitted bundles, the indexes built from them and the
    /// accumulating issue list. Built once per <see cref="Validate" /> call.
    /// </summary>
    private sealed class ValidationContext
    {
        internal ValidationContext(
            IReadOnlyList<ParsedBundle> bundles,
            string? reloadingModId,
            string modDirectory)
        {
            Bundles = bundles;
            ReloadingModId = reloadingModId;
            ModDirectory = modDirectory ?? string.Empty;
            ModDirectoryFullPath = string.Empty;
        }

        internal IReadOnlyList<ParsedBundle> Bundles { get; }

        internal string? ReloadingModId { get; }

        internal string ModDirectory { get; }

        internal string ModDirectoryFullPath { get; set; }

        internal bool ModDirectoryAvailable { get; set; }

        internal List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();

        /// <summary>Top-level Parts and SubParts declared in this set, by id.</summary>
        /// <remarks>
        /// Every index is <see cref="StringComparer.OrdinalIgnoreCase" /> because KSA identity is
        /// <c>KeyHash.Make</c>, which lowercases its input before hashing.
        /// </remarks>
        internal Dictionary<string, PartTemplate> DeclaredParts { get; } =
            new Dictionary<string, PartTemplate>(StringComparer.OrdinalIgnoreCase);

        /// <summary>SubParts only.</summary>
        internal Dictionary<string, PartTemplate> DeclaredSubParts { get; } =
            new Dictionary<string, PartTemplate>(StringComparer.OrdinalIgnoreCase);

        /// <summary>PartGameData and SubPartGameData declared in this set, by id.</summary>
        internal Dictionary<string, PartGameDataReference> DeclaredGameData { get; } =
            new Dictionary<string, PartGameDataReference>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Top-level PbrMaterials declared in this set, by id.</summary>
        internal Dictionary<string, PbrMaterialReference> DeclaredMaterials { get; } =
            new Dictionary<string, PbrMaterialReference>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Mesh ids this set would create: GLB node names plus MeshFile ids.</summary>
        internal HashSet<string> DeclaredMeshIds { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>File ids this set would create (declared Id, or the mod-relative path).</summary>
        internal HashSet<string> DeclaredFileIds { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Every id this set declares, with its id space and the document it came from. Kept flat so
        /// V4 can report duplicates with their provenance.
        /// </summary>
        internal List<(string Kind, string Id, string SourceName)> Declared { get; } =
            new List<(string, string, string)>();

        /// <summary>Mesh atlases whose contents could not be inspected, with the reason.</summary>
        internal List<(string SourceName, string Path, string Reason)> AtlasProblems { get; } =
            new List<(string, string, string)>();

        /// <summary>Editor tags KSA already knows about, or null when they could not be resolved.</summary>
        internal IReadOnlyCollection<string>? KnownEditorTags { get; set; }
    }
}
