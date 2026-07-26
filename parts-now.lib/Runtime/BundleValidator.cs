// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.Collections.Generic;

namespace MeowSci.PartsNowLib;

/// <summary>
/// All of parts-now's bundle validation rules (V1-V15), as pure functions over a set of
/// <see cref="ParsedBundle" />s plus a read-only look at the live KSA registries.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here registers, writes, binds or mutates anything. Reading the live registries through
/// <see cref="GameRegistry" /> and reading files under the mod folder are the only outside effects.
/// </para>
/// <para>
/// Each rule is evaluated inside its own try/catch so that one rule throwing (for example because a
/// KSA rename broke a <see cref="GameRegistry" /> accessor) cannot swallow the other rules' findings;
/// a failed rule is reported as a Warning naming itself.
/// </para>
/// <para>
/// Rule-by-rule notes on why a rule reads the <see cref="System.Xml.Linq.XDocument" /> instead of the
/// deserialized object graph (or vice versa) live with the rule implementations in
/// <c>BundleValidatorRules*.cs</c>. The shared context the rules read is built in
/// <c>BundleValidatorContext.cs</c>.
/// </para>
/// </remarks>
public static partial class BundleValidator
{
    /// <summary>Id-space label for Parts and SubParts, which share <c>ModLibrary.AllParts</c>.</summary>
    private const string KindPart = "Part";

    /// <summary>Id-space label for PartGameData and SubPartGameData.</summary>
    private const string KindGameData = "PartGameData";

    /// <summary>Id-space label for PbrMaterials.</summary>
    private const string KindMaterial = "PbrMaterial";

    /// <summary>Id-space label for meshes (GLB node names and MeshFile ids).</summary>
    private const string KindMesh = "Mesh";

    /// <summary>Id-space label for file references (mesh atlases, mesh files, textures).</summary>
    private const string KindFile = "File";

    /// <summary>
    /// Runs every rule over <paramref name="bundles" />. Any <see cref="IssueSeverity.Error" /> in the
    /// result must block the load.
    /// </summary>
    /// <param name="bundles">
    /// Every document submitted together. They are validated as one set because they can legitimately
    /// cross-reference each other (a Part in one file, its SubParts and PartGameData in others).
    /// </param>
    /// <param name="reloadingModId">
    /// The id of an already-loaded mod that is being reloaded. Ids owned by that mod are exempt from
    /// the "already registered" rules V3 and V14, because a reload purges them first. Pass null for a
    /// fresh install.
    /// </param>
    /// <param name="modDirectory">
    /// Absolute path of the folder every <c>Path=</c> attribute resolves against. May be empty or
    /// non-existent (for example when validating pasted XML before the folder is written); the
    /// file-existence half of V6 and V11 then degrades to a warning instead of failing.
    /// </param>
    /// <returns>Every finding, in rule order.</returns>
    public static List<ValidationIssue> Validate(
        IReadOnlyList<ParsedBundle> bundles,
        string? reloadingModId,
        string modDirectory)
    {
        ArgumentNullException.ThrowIfNull(bundles);

        ValidationContext context = BuildContext(bundles, reloadingModId, modDirectory);

        RunRule(context, "V1", RuleV1RootElement);
        RunRule(context, "V2", RuleV2NonEmptyIds);
        RunRule(context, "V3", RuleV3RegistryCollisions);
        RunRule(context, "V4", RuleV4DuplicateIdsInSet);
        RunRule(context, "V5", RuleV5SubPartInstances);
        RunRule(context, "V6", RuleV6MeshReferences);
        RunRule(context, "V7", RuleV7EditorTags);
        RunRule(context, "V8", RuleV8UnsupportedElements);
        RunRule(context, "V9", RuleV9MaterialChannels);
        RunRule(context, "V10", RuleV10GameDataReferences);
        RunRule(context, "V11", RuleV11Paths);
        RunRule(context, "V12", RuleV12SubPartMeshView);
        RunRule(context, "V13", RuleV13MissingGameData);
        RunRule(context, "V14", RuleV14GameDataCollisions);
        RunRule(context, "V15", RuleV15TextureBudget);

        return context.Issues;
    }

    /// <summary>True when at least one finding is an <see cref="IssueSeverity.Error" />.</summary>
    /// <param name="issues">The findings to inspect.</param>
    public static bool HasErrors(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        for (int i = 0; i < issues.Count; i++)
        {
            if (issues[i].Severity == IssueSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The canonical V1 issue for a document that <see cref="BundleParser.TryParse" /> rejected.
    /// Such a document never reaches <see cref="Validate" />, so its caller reports it with this.
    /// </summary>
    /// <param name="sourceName">The document that failed to parse.</param>
    /// <param name="error">The reason produced by <see cref="BundleParser.TryParse" />.</param>
    public static ValidationIssue ParseFailure(string sourceName, string error) =>
        new ValidationIssue(
            IssueSeverity.Error,
            "V1",
            "'" + sourceName + "' could not be parsed as a KSA asset bundle: " + error,
            string.Empty,
            sourceName);

    private static void RunRule(ValidationContext context, string rule, Action<ValidationContext> body)
    {
        try
        {
            body(context);
        }
        catch (Exception ex)
        {
            // One broken rule must never cost us the other fourteen rules' findings.
            context.Issues.Add(new ValidationIssue(
                IssueSeverity.Warning,
                rule,
                "rule " + rule + " could not be evaluated (" + ex.GetType().Name + ": " + ex.Message
                + "). Its findings are missing from this report.",
                string.Empty,
                string.Empty));
            Console.WriteLine("parts-now: validation rule " + rule + " threw — " + ex);
        }
    }

    private static void AddError(
        ValidationContext context,
        string rule,
        string sourceName,
        string elementId,
        string message) =>
        context.Issues.Add(new ValidationIssue(IssueSeverity.Error, rule, message, elementId, sourceName));

    private static void AddWarning(
        ValidationContext context,
        string rule,
        string sourceName,
        string elementId,
        string message) =>
        context.Issues.Add(new ValidationIssue(IssueSeverity.Warning, rule, message, elementId, sourceName));

    private static void Add(
        ValidationContext context,
        IssueSeverity severity,
        string rule,
        string sourceName,
        string elementId,
        string message) =>
        context.Issues.Add(new ValidationIssue(severity, rule, message, elementId, sourceName));
}
