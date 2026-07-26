// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// The one and only place in parts-now that uses reflection against KSA internals.
/// <para>
/// KSA keeps its asset registries as <c>internal static readonly SerializedCollection&lt;T&gt;</c>
/// fields on <see cref="ModLibrary"/>, and <c>SerializedCollection&lt;T&gt;</c> offers no removal API.
/// This class resolves every member it needs exactly once in its static constructor and re-exposes
/// them as typed accessors, so no other file in the mod ever calls
/// <c>GetField</c>/<c>GetMethod</c>/<c>GetProperty</c>.
/// </para>
/// <para>
/// The static constructor never throws. Failed lookups are recorded as human-readable strings and
/// reported by <see cref="SelfTest"/>; the accessor for a member that failed to resolve throws a
/// descriptive <see cref="InvalidOperationException"/> instead. A throwing static constructor would
/// turn every later access into an opaque <c>TypeInitializationException</c>.
/// </para>
/// </summary>
public static class GameRegistry
{
    private const BindingFlags StaticFieldFlags =
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private const BindingFlags CollectionFieldFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>
    /// Editor tags KSA hardcodes in <c>EditorTag</c> and registers unconditionally at boot.
    /// Used as the fallback set when <c>VehicleEditor._editorTagLookup</c> cannot be read.
    /// </summary>
    private static readonly string[] BuiltInEditorTags =
    {
        "All", "Hidden", "Engines", "Capsules", "Interstage", "Radial",
    };

    private static readonly List<string> Problems = new List<string>();

    private static readonly SerializedCollection<PartTemplate>? PartsField;
    private static readonly SerializedCollection<MeshReference>? MeshesField;
    private static readonly SerializedCollection<FileReference>? FilesField;
    private static readonly SerializedCollection<PbrMaterialReference>? MaterialsField;
    private static readonly SerializedCollection<PartGameDataReference>? PartGameDataField;
    private static readonly SerializedCollection<EditorTagDefinition>? EditorTagDefsField;

    private static readonly FieldInfo? EditorTagLookupField;

    static GameRegistry()
    {
        PartsField = Collection<PartTemplate>("AllParts");
        MeshesField = Collection<MeshReference>("AllMeshes");
        FilesField = Collection<FileReference>("AllFiles");
        MaterialsField = Collection<PbrMaterialReference>("AllMaterials");
        PartGameDataField = Collection<PartGameDataReference>("AllPartGameDataReferences");
        EditorTagDefsField = Collection<EditorTagDefinition>("AllEditorTagDefinitions");

        EditorTagLookupField = ResolveEditorTagLookup();

        // Probe the removal path once, on an arbitrary closed generic, so a rename shows up in
        // SelfTest() rather than at the first Unregister() call during a rollback.
        if (CollectionFields<PartTemplate>.Collection is null)
        {
            Problems.Add(
                "SerializedCollection<T>._collection not found — KSA internals changed "
                + "(unload and rollback are unavailable).");
        }
    }

    /// <summary>All registered <see cref="PartTemplate"/>s (includes SubParts and game-data refs).</summary>
    public static SerializedCollection<PartTemplate> AllParts =>
        PartsField ?? throw Missing("ModLibrary.AllParts");

    /// <summary>All registered <see cref="MeshReference"/>s.</summary>
    public static SerializedCollection<MeshReference> AllMeshes =>
        MeshesField ?? throw Missing("ModLibrary.AllMeshes");

    /// <summary>All registered <see cref="FileReference"/>s (mesh atlases, textures, shaders...).</summary>
    public static SerializedCollection<FileReference> AllFiles =>
        FilesField ?? throw Missing("ModLibrary.AllFiles");

    /// <summary>All registered <see cref="PbrMaterialReference"/>s.</summary>
    public static SerializedCollection<PbrMaterialReference> AllMaterials =>
        MaterialsField ?? throw Missing("ModLibrary.AllMaterials");

    /// <summary>All registered <see cref="PartGameDataReference"/>s (KSA field <c>AllPartGameDataReferences</c>).</summary>
    public static SerializedCollection<PartGameDataReference> AllPartGameData =>
        PartGameDataField ?? throw Missing("ModLibrary.AllPartGameDataReferences");

    /// <summary>All registered <see cref="EditorTagDefinition"/>s (KSA field <c>AllEditorTagDefinitions</c>).</summary>
    public static SerializedCollection<EditorTagDefinition> AllEditorTagDefs =>
        EditorTagDefsField ?? throw Missing("ModLibrary.AllEditorTagDefinitions");

    /// <summary>True when every reflected member resolved; false means <see cref="SelfTest"/> has content.</summary>
    public static bool IsHealthy => Problems.Count == 0;

    /// <summary>
    /// True when <c>VehicleEditor._editorTagLookup</c> resolved. When false,
    /// <see cref="GetKnownEditorTags"/> still works but only sees the built-in tags plus
    /// <see cref="AllEditorTagDefs"/> ids, so tag validation is degraded rather than broken.
    /// </summary>
    public static bool EditorTagLookupAvailable => EditorTagLookupField is not null;

    /// <summary>
    /// Removes <paramref name="item"/> from both the live <c>GetList()</c> list and the private
    /// <c>ConcurrentDictionary&lt;KeyHash, T&gt;</c> that backs <c>Find(KeyHash)</c>. Removing from
    /// only one of the two leaves the collection inconsistent — <c>Find</c> would still resolve a
    /// purged item.
    /// <para>
    /// Only ever call this from the game thread while no load is in flight.
    /// <c>SerializedCollection</c> has a private <c>Lock</c> that we deliberately do not take;
    /// single-threaded access is what makes that safe.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The collection's element type.</typeparam>
    /// <param name="collection">The collection to remove from.</param>
    /// <param name="item">The item to remove.</param>
    /// <returns>True if the item was present in the list or the dictionary.</returns>
    /// <exception cref="InvalidOperationException">The private backing field could not be resolved.</exception>
    public static bool Unregister<T>(SerializedCollection<T> collection, T item)
        where T : ILibraryData, IListable
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(item);

        // GetList() hands back the live backing List<T>, so this really does remove it.
        bool removedFromList = collection.GetList().Remove(item);

        FieldInfo field = CollectionFields<T>.Collection
            ?? throw Missing("SerializedCollection<" + typeof(T).Name + ">._collection");

        if (field.GetValue(collection) is not ConcurrentDictionary<KeyHash, T> dictionary)
        {
            throw new InvalidOperationException(
                "parts-now: SerializedCollection<" + typeof(T).Name
                + ">._collection is not a ConcurrentDictionary<KeyHash, T> — KSA internals changed.");
        }

        bool removedFromDictionary = dictionary.TryRemove(item.Hash, out _);
        return removedFromList || removedFromDictionary;
    }

    /// <summary>Finds a registered part (or SubPart) by id, or null.</summary>
    /// <param name="id">The part id as written in XML.</param>
    public static PartTemplate? FindPart(string id) => AllParts.Find(KeyHash.Make(id.AsSpan()));

    /// <summary>Finds a registered mesh by id, or null.</summary>
    /// <param name="id">The mesh id as written in XML (or the GLB node name).</param>
    public static MeshReference? FindMesh(string id) => AllMeshes.Find(KeyHash.Make(id.AsSpan()));

    /// <summary>Finds a registered file reference by id, or null.</summary>
    /// <param name="id">The file id, which defaults to the file's mod-relative path when unset.</param>
    public static FileReference? FindFile(string id) => AllFiles.Find(KeyHash.Make(id.AsSpan()));

    /// <summary>Finds a registered PBR material by id, or null.</summary>
    /// <param name="id">The material id as written in XML.</param>
    public static PbrMaterialReference? FindMaterial(string id) =>
        AllMaterials.Find(KeyHash.Make(id.AsSpan()));

    /// <summary>Finds a registered part game-data reference by id, or null.</summary>
    /// <param name="id">The game-data id, which must match the owning part's id.</param>
    public static PartGameDataReference? FindPartGameData(string id) =>
        AllPartGameData.Find(KeyHash.Make(id.AsSpan()));

    /// <summary>
    /// The set of editor tags KSA already knows about: the union of
    /// <c>VehicleEditor._editorTagLookup</c>'s values and every registered
    /// <see cref="EditorTagDefinition"/> id, plus the six built-ins. Comparison is
    /// case-insensitive, matching <c>KeyHash.Make</c>, which lowercases its input.
    /// <para>
    /// Never throws: if <c>_editorTagLookup</c> did not resolve the result degrades to the built-ins
    /// plus the tag definitions (see <see cref="EditorTagLookupAvailable"/>).
    /// </para>
    /// </summary>
    /// <returns>A fresh, case-insensitive set of known tag names.</returns>
    public static IReadOnlyCollection<string> GetKnownEditorTags() => BuildKnownEditorTags();

    /// <summary>
    /// True when <paramref name="tag"/> is a tag KSA already knows about. New tags registered after
    /// boot get no category button in the part browser, so a bundle declaring one must be rejected.
    /// </summary>
    /// <param name="tag">The tag name as written in <c>&lt;EditorTag Value="..."/&gt;</c>.</param>
    public static bool IsKnownEditorTag(string tag) =>
        !string.IsNullOrEmpty(tag) && BuildKnownEditorTags().Contains(tag);

    private static HashSet<string> BuildKnownEditorTags()
    {
        HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string builtIn in BuiltInEditorTags)
        {
            tags.Add(builtIn);
        }

        if (EditorTagLookupField is not null)
        {
            try
            {
                if (EditorTagLookupField.GetValue(null) is Dictionary<uint, string> lookup)
                {
                    foreach (string tag in lookup.Values)
                    {
                        if (!string.IsNullOrEmpty(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "parts-now: could not read VehicleEditor._editorTagLookup — " + ex.Message);
            }
        }

        if (EditorTagDefsField is not null)
        {
            foreach (EditorTagDefinition definition in EditorTagDefsField.GetList())
            {
                if (!string.IsNullOrEmpty(definition.Id))
                {
                    tags.Add(definition.Id);
                }
            }
        }

        return tags;
    }

    /// <summary>
    /// Reports every reflected member that failed to resolve. An empty list means healthy.
    /// Call once from <c>PartsNowSubmod.Initialize()</c>; if it returns anything, log it and disable
    /// the mod's Load buttons — that turns a future KSA rename into a clear message, not a crash.
    /// </summary>
    /// <returns>A copy of the problem list, one human-readable line per failed lookup.</returns>
    public static List<string> SelfTest()
    {
        List<string> problems = new List<string>(Problems);

        if (problems.Count == 0)
        {
            Console.WriteLine("parts-now: GameRegistry self-test passed — all KSA internals resolved.");
            return problems;
        }

        Console.WriteLine(
            "parts-now: GameRegistry self-test FAILED with " + problems.Count + " problem(s):");
        foreach (string problem in problems)
        {
            Console.WriteLine("parts-now:   - " + problem);
        }

        return problems;
    }

    private static SerializedCollection<T>? Collection<T>(string field)
        where T : ILibraryData, IListable
    {
        try
        {
            FieldInfo? info = typeof(ModLibrary).GetField(field, StaticFieldFlags);
            if (info is null)
            {
                Problems.Add("ModLibrary." + field + " not found — KSA internals changed.");
                return null;
            }

            if (info.GetValue(null) is not SerializedCollection<T> collection)
            {
                Problems.Add(
                    "ModLibrary." + field + " is not a SerializedCollection<" + typeof(T).Name
                    + "> — KSA internals changed.");
                return null;
            }

            return collection;
        }
        catch (Exception ex)
        {
            Problems.Add("ModLibrary." + field + " could not be read — " + ex.Message);
            return null;
        }
    }

    private static FieldInfo? ResolveEditorTagLookup()
    {
        try
        {
            FieldInfo? info = typeof(VehicleEditor).GetField("_editorTagLookup", StaticFieldFlags);
            if (info is null)
            {
                Problems.Add(
                    "VehicleEditor._editorTagLookup not found — KSA internals changed "
                    + "(editor tag validation degrades to the built-in tags).");
                return null;
            }

            if (!typeof(Dictionary<uint, string>).IsAssignableFrom(info.FieldType))
            {
                Problems.Add(
                    "VehicleEditor._editorTagLookup is not a Dictionary<uint, string> — "
                    + "KSA internals changed (editor tag validation degrades to the built-in tags).");
                return null;
            }

            return info;
        }
        catch (Exception ex)
        {
            Problems.Add("VehicleEditor._editorTagLookup could not be read — " + ex.Message);
            return null;
        }
    }

    private static InvalidOperationException Missing(string member) =>
        new InvalidOperationException("parts-now: " + member + " not found — KSA internals changed.");

    /// <summary>
    /// Per-closed-generic cache of <c>SerializedCollection&lt;T&gt;._collection</c>. A generic
    /// holder gives us one lazily-initialised static field per <c>T</c> with no dictionary and no
    /// lock; the CLR handles the thread safety of the initialiser.
    /// </summary>
    private static class CollectionFields<T> where T : ILibraryData, IListable
    {
        internal static readonly FieldInfo? Collection =
            typeof(SerializedCollection<T>).GetField("_collection", CollectionFieldFlags);
    }
}
