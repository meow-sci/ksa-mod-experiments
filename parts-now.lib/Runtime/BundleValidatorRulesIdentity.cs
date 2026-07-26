// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using KSA;
using RenderCore.Systems;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Identity rules: document shape (V1), id presence (V2), collisions with the live registries
/// (V3, V14), collisions inside the submitted set (V4) and the bindless texture budget (V15).
/// </summary>
public static partial class BundleValidator
{
    /// <summary>
    /// Slots the bindless texture library keeps in reserve. The pool is a
    /// <c>FreeListIndexPool(maxTextures, allowResize: false)</c>, so exhausting it is fatal rather
    /// than slow; this margin leaves room for the game's own runtime allocations.
    /// </summary>
    private const int BindlessTextureReserve = 16;

    /// <summary>
    /// V1 — the root element must be <c>&lt;Assets&gt;</c>. Read from the XDocument: the object graph
    /// cannot express "wrong root", because <c>AssetBundle</c> carries <c>[XmlRoot("Assets")]</c> and
    /// a mismatched root makes the deserializer throw (which the caller reports via
    /// <see cref="ParseFailure" />). This is the belt-and-braces half of the rule.
    /// </summary>
    private static void RuleV1RootElement(ValidationContext context)
    {
        foreach (ParsedBundle bundle in context.Bundles)
        {
            XElement? root = bundle.Document.Root;
            if (root is null)
            {
                AddError(context, "V1", bundle.SourceName, string.Empty,
                    "'" + bundle.SourceName + "' has no root element. A KSA asset bundle must be a "
                    + "single <" + BundleParser.RootElementName + "> element.");
                continue;
            }

            if (!string.Equals(root.Name.LocalName, BundleParser.RootElementName, StringComparison.Ordinal))
            {
                AddError(context, "V1", bundle.SourceName, root.Name.LocalName,
                    "'" + bundle.SourceName + "' has root element <" + root.Name.LocalName
                    + "> (line " + BundleParser.LineNumber(root) + "). It must be <"
                    + BundleParser.RootElementName + ">.");
            }
        }
    }

    /// <summary>
    /// V2 — every registrable asset needs a non-empty <c>Id</c>. Read from the object graph, which
    /// already resolved each element to its CLR type and so knows which entries are file references.
    /// <para>
    /// <c>SerializedId.OnDataLoad</c> sets <c>IsReferenceable = !string.IsNullOrEmpty(Id)</c>, and
    /// <c>PartTemplate.OnDataLoad</c> only registers when <c>IsReferenceable</c> — an id-less Part is
    /// silently never registered. File references are exempt: <c>FileReference.OnDataLoad</c> falls
    /// back to <c>Id = ModPath</c>, so they need either an <c>Id</c> or a <c>Path</c>.
    /// </para>
    /// </summary>
    private static void RuleV2NonEmptyIds(ValidationContext context)
    {
        foreach (ParsedBundle bundle in context.Bundles)
        {
            List<SerializedId> assets = bundle.Bundle.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                SerializedId asset = assets[i];
                string element = ElementNameOf(asset);
                string position = "entry #" + (i + 1) + " (<" + element + ">) in '"
                    + bundle.SourceName + "'";

                if (asset is FileReference file)
                {
                    if (string.IsNullOrWhiteSpace(file.Id) && string.IsNullOrWhiteSpace(file.LocalPath))
                    {
                        AddError(context, "V2", bundle.SourceName, string.Empty,
                            position + " has neither an Id nor a Path. A file reference needs a Path "
                            + "(its id then defaults to the mod-relative path) or an Id naming an "
                            + "already-loaded file.");
                    }

                    continue;
                }

                if (asset is PartTemplate or PbrMaterialReference)
                {
                    if (string.IsNullOrWhiteSpace(asset.Id))
                    {
                        AddError(context, "V2", bundle.SourceName, string.Empty,
                            position + " has no Id. KSA never registers an id-less "
                            + element + ", so nothing could ever reference it.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// V3 — no declared id may collide with an id that is ALREADY registered in the same KSA
    /// collection. Read from the object graph (identity is an object-level question).
    /// <para>
    /// <c>SerializedCollection.Register</c> returns false on a duplicate <c>KeyHash</c> and every
    /// caller treats that as "this is a reference to the existing one", so a colliding Part is
    /// silently dropped and a colliding file's <c>Load()</c> never reads the file from disk.
    /// </para>
    /// <para>
    /// PartGameData collisions are handled by V14 rather than here, so that a colliding game-data id
    /// is reported exactly once with the explanation that actually matters (its <c>OnDataLoad</c>
    /// merges into the existing reference).
    /// </para>
    /// </summary>
    private static void RuleV3RegistryCollisions(ValidationContext context)
    {
        foreach (string id in context.DeclaredParts.Keys)
        {
            ReportCollision(context, "V3", KindPart, id, GameRegistry.FindPart(id));
        }

        foreach (string id in context.DeclaredMaterials.Keys)
        {
            ReportCollision(context, "V3", KindMaterial, id, GameRegistry.FindMaterial(id));
        }

        foreach (string id in context.DeclaredMeshIds)
        {
            ReportCollision(context, "V3", KindMesh, id, GameRegistry.FindMesh(id));
        }

        foreach (string id in context.DeclaredFileIds)
        {
            ReportCollision(context, "V3", KindFile, id, GameRegistry.FindFile(id));
        }
    }

    /// <summary>
    /// V4 — no id may be declared twice inside the submitted set. Read from the object graph via the
    /// flat declaration list built while indexing, so each duplicate can name the documents involved.
    /// The second registration would be dropped exactly as in V3.
    /// </summary>
    private static void RuleV4DuplicateIdsInSet(ValidationContext context)
    {
        IEnumerable<IGrouping<(string Kind, string Id), (string Kind, string Id, string SourceName)>> groups =
            context.Declared.GroupBy(
                d => (d.Kind, d.Id),
                DeclarationKeyComparer.Instance);

        foreach (IGrouping<(string Kind, string Id), (string Kind, string Id, string SourceName)> group in groups)
        {
            List<string> sources = group.Select(d => d.SourceName).ToList();
            if (sources.Count < 2)
            {
                continue;
            }

            AddError(context, "V4", sources[0], group.Key.Id,
                group.Key.Kind + " id '" + group.Key.Id + "' is declared " + sources.Count
                + " times in this set (" + string.Join(", ", sources.Distinct())
                + "). Only the first declaration would be registered; give each one a unique id.");
        }
    }

    /// <summary>
    /// V14 — a <c>&lt;PartGameData&gt;</c> whose id already exists in
    /// <c>ModLibrary.AllPartGameDataReferences</c>. Read from the object graph.
    /// <para>
    /// <c>PartGameDataReference.OnDataLoad</c> merges into the existing reference when
    /// <c>ModLibrary.Register</c> fails, so the incremental attach in the loader would never see the
    /// new game data — and the merge is additive, permanently corrupting the existing entry. Ids
    /// owned by the mod being reloaded are exempt, because a reload purges them first.
    /// </para>
    /// </summary>
    private static void RuleV14GameDataCollisions(ValidationContext context)
    {
        foreach (string id in context.DeclaredGameData.Keys)
        {
            PartGameDataReference? existing = GameRegistry.FindPartGameData(id);
            if (existing is null || IsOwnedByReloadingMod(context, existing.Mod))
            {
                continue;
            }

            AddError(context, "V14", SourceOf(context, KindGameData, id), id,
                "PartGameData id '" + id + "' is already registered by mod '" + ModNameOf(existing.Mod)
                + "'. KSA would merge this game data into the existing entry instead of registering "
                + "it, so the new part would never receive it. Rename it, or unload the owning mod "
                + "first.");
        }
    }

    /// <summary>
    /// V15 — the bindless texture pool must have room. Read from the object graph: every
    /// <c>TextureReference</c> with a <c>Path</c>, wherever it is declared (top-level
    /// <c>&lt;Texture&gt;</c>, a top-level <c>&lt;PbrMaterial&gt;</c> channel, or a material declared
    /// inline inside a model component), becomes one bindless slot when it binds.
    /// </summary>
    private static void RuleV15TextureBudget(ValidationContext context)
    {
        HashSet<string> newTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (TextureReference texture in BundleParser.AllTextureFiles(bundle))
            {
                newTextures.Add(NormalizePath(texture.LocalPath));
            }
        }

        if (newTextures.Count == 0)
        {
            return;
        }

        Program? program = Program.Instance;
        BindlessTextureLibrary? library = program?.BindlessTextures;
        if (library is null)
        {
            AddWarning(context, "V15", string.Empty, string.Empty,
                "the bindless texture pool could not be read, so the "
                + newTextures.Count + " new texture(s) in this set were not budget-checked.");
            return;
        }

        int used = library.TextureCount;
        int capacity = library.MaxTextures - BindlessTextureReserve;

        if (used + newTextures.Count > capacity)
        {
            AddError(context, "V15", string.Empty, string.Empty,
                "this set declares " + newTextures.Count + " texture(s) but only "
                + Math.Max(0, capacity - used) + " bindless slot(s) are free (" + used + " of "
                + library.MaxTextures + " used, " + BindlessTextureReserve
                + " held in reserve). The pool cannot grow at runtime — unload another mod or reduce "
                + "the texture count.");
        }
    }

    private static void ReportCollision(
        ValidationContext context,
        string rule,
        string kind,
        string id,
        SerializedId? existing)
    {
        if (existing is null || IsOwnedByReloadingMod(context, existing.Mod))
        {
            return;
        }

        AddError(context, rule, SourceOf(context, kind, id), id,
            kind + " id '" + id + "' is already registered by mod '" + ModNameOf(existing.Mod)
            + "'. KSA ids are global and a duplicate registration is silently dropped, so this "
            + "declaration would be ignored. Rename it, or unload the owning mod first.");
    }

    private static bool IsOwnedByReloadingMod(ValidationContext context, Mod? owner)
    {
        if (string.IsNullOrEmpty(context.ReloadingModId) || owner is null)
        {
            return false;
        }

        return string.Equals(owner.Id, context.ReloadingModId, StringComparison.OrdinalIgnoreCase);
    }

    private static string ModNameOf(Mod? owner) =>
        string.IsNullOrEmpty(owner?.Id) ? "unknown" : owner.Id;

    private static string SourceOf(ValidationContext context, string kind, string id)
    {
        foreach ((string Kind, string Id, string SourceName) declaration in context.Declared)
        {
            if (string.Equals(declaration.Kind, kind, StringComparison.Ordinal)
                && string.Equals(declaration.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return declaration.SourceName;
            }
        }

        return string.Empty;
    }

    private static string NormalizePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// The XML element name an asset came from. Must be tested most-derived first:
    /// <c>SubPartGameDataReference : PartGameDataReference : PartTemplate</c> and
    /// <c>SubPartTemplate : PartTemplate</c>, so a naive <c>is PartTemplate</c> matches all four.
    /// </summary>
    private static string ElementNameOf(SerializedId asset) => asset switch
    {
        SubPartGameDataReference => "SubPartGameData",
        PartGameDataReference => "PartGameData",
        SubPartTemplate => "SubPart",
        PartTemplate => "Part",
        MeshAtlasFileReference => "MeshAtlas",
        MeshFileReference => "MeshFile",
        TextureReference => "Texture",
        PbrMaterialReference => "PbrMaterial",
        FileReference => "File",
        _ => asset.GetType().Name,
    };

    /// <summary>
    /// Groups declarations by id space and by id, matching KSA's case-insensitive
    /// <c>KeyHash.Make</c> identity.
    /// </summary>
    private sealed class DeclarationKeyComparer : IEqualityComparer<(string Kind, string Id)>
    {
        internal static readonly DeclarationKeyComparer Instance = new DeclarationKeyComparer();

        public bool Equals((string Kind, string Id) x, (string Kind, string Id) y) =>
            string.Equals(x.Kind, y.Kind, StringComparison.Ordinal)
            && string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Kind, string Id) obj) =>
            HashCode.Combine(obj.Kind, obj.Id.ToLowerInvariant());
    }
}
