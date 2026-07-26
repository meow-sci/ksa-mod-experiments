// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Reference-resolution rules: sub-part instances (V5), meshes (V6), editor tags (V7), game-data
/// cross-library references (V10) and the missing-game-data warning (V13). Every one of these fails
/// lazily at spawn or thumbnail time in stock KSA, far away from the load that caused it.
/// </summary>
public static partial class BundleValidator
{
    /// <summary>
    /// V5 — every <c>&lt;SubPart InstanceOf="X"/&gt;</c> must resolve. Read from the object graph:
    /// the deserializer already put these into <c>PartTemplate.SubPartInstances</c> as
    /// <c>PartInstance</c>s, which is exactly what <c>PartInstance.GetTemplate()</c> reads.
    /// <para>
    /// <c>GetTemplate()</c> calls <c>ModLibrary.Get&lt;PartTemplate&gt;</c>, which throws
    /// <c>NullReferenceException</c> on a miss — at spawn or thumbnail time, not at load time.
    /// </para>
    /// <para>
    /// Game-data entries are checked too: <c>PartTemplate.ApplyGameData</c> merges their
    /// <c>SubPartInstances</c> into the target part, so an unresolvable id there fails the same way.
    /// </para>
    /// </summary>
    private static void RuleV5SubPartInstances(ValidationContext context)
    {
        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (PartTemplate template in BundleParser.AllPartTemplates(bundle))
            {
                foreach (PartInstance instance in template.SubPartInstances)
                {
                    if (string.IsNullOrWhiteSpace(instance.InstanceOf))
                    {
                        AddError(context, "V5", bundle.SourceName, template.Id,
                            "'" + template.Id + "' declares a <SubPart" + DescribeInstance(instance)
                            + "> with no InstanceOf attribute, so it names no template to instance.");
                        continue;
                    }

                    if (context.DeclaredParts.ContainsKey(instance.InstanceOf)
                        || GameRegistry.FindPart(instance.InstanceOf) is not null)
                    {
                        continue;
                    }

                    AddError(context, "V5", bundle.SourceName, template.Id,
                        "'" + template.Id + "' instances SubPart '" + instance.InstanceOf
                        + "', which is neither declared in this set nor already registered. "
                        + "The part would throw a NullReferenceException the first time it is drawn.");
                }
            }
        }
    }

    /// <summary>
    /// V6 — every <c>&lt;Mesh Id="X"/&gt;</c> must resolve to a mesh this set creates or one that is
    /// already registered. Read from the XDocument, because mesh references appear inside many
    /// different module templates (<c>&lt;PartModel&gt;</c>, <c>&lt;PartModelGlass&gt;</c>,
    /// <c>&lt;PartModelDynamic&gt;</c>, <c>&lt;MeshView&gt;</c>, ...) and enumerating the module types
    /// would silently miss any type added by a future KSA build.
    /// <para>
    /// The ids a <c>&lt;MeshAtlas&gt;</c> creates are the GLB's mesh node names (minus the ones
    /// starting with <c>'_'</c>), so the atlas file has to be readable at validation time. When it is
    /// not, every unresolved mesh in this rule is downgraded to a Warning — an Error there would be a
    /// guess.
    /// </para>
    /// </summary>
    private static void RuleV6MeshReferences(ValidationContext context)
    {
        bool atlasInspectionFailed = context.AtlasProblems.Count > 0;

        foreach ((string sourceName, string path, string reason) in context.AtlasProblems)
        {
            AddWarning(context, "V6", sourceName, path,
                "the mesh ids of atlas '" + path + "' could not be read (" + reason
                + "), so the meshes it declares cannot be verified.");
        }

        IssueSeverity severity = atlasInspectionFailed ? IssueSeverity.Warning : IssueSeverity.Error;

        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (XElement element in bundle.Document.Descendants())
            {
                if (!string.Equals(element.Name.LocalName, "Mesh", StringComparison.Ordinal))
                {
                    continue;
                }

                string id = element.Attribute("Id")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    AddError(context, "V6", bundle.SourceName, string.Empty,
                        "a <Mesh> element on line " + BundleParser.LineNumber(element)
                        + " has no Id attribute.");
                    continue;
                }

                if (context.DeclaredMeshIds.Contains(id) || GameRegistry.FindMesh(id) is not null)
                {
                    continue;
                }

                Add(context, severity, "V6", bundle.SourceName, id,
                    "mesh '" + id + "' (line " + BundleParser.LineNumber(element)
                    + ") is neither declared by a <MeshAtlas>/<MeshFile> in this set nor already "
                    + "registered. A mesh atlas names its meshes after the GLB's mesh nodes."
                    + (atlasInspectionFailed
                        ? " (Downgraded to a warning: an atlas in this set could not be inspected.)"
                        : string.Empty));
            }
        }
    }

    /// <summary>
    /// V7 — every declared editor tag must already exist. Read from the object graph:
    /// <c>PartTemplate.EditorTags</c> is still empty before <c>OnDataLoad</c>, so the declared tags
    /// are read from <c>PartTemplate.EditorTagsStrings</c> (<c>[XmlElement("EditorTag")]</c>,
    /// a <c>List&lt;StringReference&gt;</c> whose value lives in <c>StringReference.Value</c>).
    /// <para>
    /// <c>VehicleEditor.MarkEditorTagDefinitionsLoaded()</c> locks the tag list at boot; after that
    /// <c>RegisterTag</c> logs a warning and adds nothing, so a part carrying a new tag would sit in
    /// a category button that does not exist.
    /// </para>
    /// </summary>
    private static void RuleV7EditorTags(ValidationContext context)
    {
        IReadOnlyCollection<string>? known = context.KnownEditorTags;
        if (known is null)
        {
            AddWarning(context, "V7", string.Empty, string.Empty,
                "KSA's editor tag list could not be read, so declared <EditorTag> values were not "
                + "verified. An unknown tag will leave the part without a category button.");
            return;
        }

        HashSet<string> lookup = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
        string valid = string.Join(", ", known.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));

        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (PartTemplate template in BundleParser.AllPartTemplates(bundle))
            {
                foreach (StringReference tag in template.EditorTagsStrings)
                {
                    string value = tag.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        AddError(context, "V7", bundle.SourceName, template.Id,
                            "'" + template.Id + "' declares an <EditorTag> with an empty Value.");
                        continue;
                    }

                    if (lookup.Contains(value))
                    {
                        continue;
                    }

                    AddError(context, "V7", bundle.SourceName, template.Id,
                        "'" + template.Id + "' declares editor tag '" + value
                        + "', which KSA does not know. New tags cannot be registered after boot. "
                        + "Valid tags: " + valid + ".");
                }
            }
        }
    }

    /// <summary>
    /// V10 — references into the libraries parts-now does not extend. Read from the XDocument: these
    /// live on module templates that are not worth enumerating type by type, and the element/attribute
    /// names are the stable part of the contract.
    /// <para>
    /// Verified against the 5018 <c>Content/Core</c> XML: reactions are referenced as
    /// <c>&lt;Reaction Id="..."/&gt;</c> (inside <c>&lt;Combustor&gt;</c> and
    /// <c>&lt;SolidMotor&gt;</c>), grain geometry as <c>&lt;Grain Id="..."/&gt;</c>, exhausts as
    /// <c>&lt;VolumetricExhaust Id="..."/&gt;</c> and sounds as
    /// <c>&lt;SoundEvent SoundId="..."/&gt;</c>. There is no <c>&lt;Combustion&gt;</c> element in this
    /// build, and <c>&lt;GrainGeometry&gt;</c> is a definition (rejected by V8), never a reference.
    /// </para>
    /// </summary>
    private static void RuleV10GameDataReferences(ValidationContext context)
    {
        bool reactionsLoaded = SubstanceLibrary.AllReactions().Length > 0;
        bool grainsLoaded = GrainGeometryLibrary.All().Length > 0;

        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (XElement element in bundle.Document.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case "Reaction":
                        CheckReference(context, bundle, element, "Id", "reaction", reactionsLoaded,
                            id => SubstanceLibrary.TryGetReaction(KeyHash.Make(id.AsSpan())) is not null);
                        break;
                    case "Grain":
                        CheckReference(context, bundle, element, "Id", "grain geometry", grainsLoaded,
                            id => GrainGeometryLibrary.TryGet(KeyHash.Make(id.AsSpan())) is not null);
                        break;
                    case "VolumetricExhaust":
                        CheckReference(context, bundle, element, "Id", "volumetric exhaust", true,
                            id => VolumetricExhaustTemplate.Get(id) is not null);
                        break;
                    case "SoundEvent":
                        CheckSoundEvent(context, bundle, element);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// V13 — a top-level <c>&lt;Part&gt;</c> with no matching <c>&lt;PartGameData&gt;</c> gets no
    /// masses, connectors or rockets. Read from the object graph, where "matching" is the id equality
    /// that <c>ModLibrary.AttachGameData</c> uses. Warning only: a massless part still loads.
    /// </summary>
    private static void RuleV13MissingGameData(ValidationContext context)
    {
        foreach (ParsedBundle bundle in context.Bundles)
        {
            foreach (PartTemplate part in BundleParser.TopLevelParts(bundle))
            {
                if (string.IsNullOrEmpty(part.Id)
                    || context.DeclaredGameData.ContainsKey(part.Id)
                    || GameRegistry.FindPartGameData(part.Id) is not null)
                {
                    continue;
                }

                AddWarning(context, "V13", bundle.SourceName, part.Id,
                    "Part '" + part.Id + "' has no matching <PartGameData Id=\"" + part.Id
                    + "\">, so it will load with no mass, connectors or modules from game data.");
            }
        }
    }

    private static void CheckReference(
        ValidationContext context,
        ParsedBundle bundle,
        XElement element,
        string attributeName,
        string what,
        bool libraryAvailable,
        Func<string, bool> resolves)
    {
        string id = element.Attribute(attributeName)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!libraryAvailable)
        {
            AddWarning(context, "V10", bundle.SourceName, id,
                "KSA's " + what + " library is empty, so '" + id + "' (line "
                + BundleParser.LineNumber(element) + ") could not be verified.");
            return;
        }

        if (resolves(id))
        {
            return;
        }

        AddError(context, "V10", bundle.SourceName, id,
            "unknown " + what + " '" + id + "' (line " + BundleParser.LineNumber(element)
            + "). parts-now cannot register new entries in that library, so the id must already "
            + "exist. The part would throw when it is spawned.");
    }

    private static void CheckSoundEvent(ValidationContext context, ParsedBundle bundle, XElement element)
    {
        string id = element.Attribute("SoundId")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        try
        {
            // ModLibrary.AllSoundBehaviours is internal and TryGet<SoundBehavior> takes the strict
            // IsSubclassOf path (so it never matches the base type). Get<SoundBehavior> is the only
            // public lookup; it throws NullReferenceException when the id is missing.
            ModLibrary.Get<SoundBehavior>(id);
        }
        catch (NullReferenceException)
        {
            AddError(context, "V10", bundle.SourceName, id,
                "unknown sound '" + id + "' (line " + BundleParser.LineNumber(element)
                + "). <SoundEvent SoundId> must name a sound that is already registered.");
        }
        catch (Exception ex)
        {
            AddWarning(context, "V10", bundle.SourceName, id,
                "sound '" + id + "' (line " + BundleParser.LineNumber(element)
                + ") could not be verified — " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string DescribeInstance(PartInstance instance) =>
        string.IsNullOrEmpty(instance.Id) ? string.Empty : " Id=\"" + instance.Id + "\"";
}
