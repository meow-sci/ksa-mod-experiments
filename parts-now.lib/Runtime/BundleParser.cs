// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using KSA;

namespace MeowSci.PartsNowLib;

/// <summary>
/// One submitted XML document, its deserialized <see cref="AssetBundle" /> and its
/// <see cref="XDocument" />. The bundle has deliberately NOT had <c>OnDataLoad</c> called on it, so
/// nothing in it is registered with KSA and every <c>SerializedId.Hash</c> is still default.
/// </summary>
/// <param name="SourceName">File name or UI tab label, used in validation messages.</param>
/// <param name="Xml">The exact text that was parsed.</param>
/// <param name="Bundle">The deserialized asset bundle (no side effects applied).</param>
/// <param name="Document">The same text as an <see cref="XDocument" />, with line info retained.</param>
public sealed record ParsedBundle(string SourceName, string Xml, AssetBundle Bundle, XDocument Document);

/// <summary>
/// Side-effect-free parsing of a KSA <c>&lt;Assets&gt;</c> document into an <see cref="AssetBundle" />.
/// The classification helpers over <c>AssetBundle.Assets</c> live in <c>BundleParserQueries.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parsing MUST stay side-effect free: <c>AssetBundle.OnDataLoad(mod)</c> is what registers templates,
/// materials, meshes and loaders into <c>ModLibrary</c>, and validation has to be able to reject a
/// bundle without having touched the live game state.
/// </para>
/// <para>
/// Because <c>OnDataLoad</c> has not run, <c>SerializedId.Hash</c> is <c>KeyHash.Zero</c>,
/// <c>SerializedId.IsReferenceable</c> is <c>false</c>, <c>SerializedId.Mod</c> is <c>null</c> and
/// <c>PartTemplate.EditorTags</c> is empty. Always work off the <c>Id</c> string
/// (<c>KeyHash.Make(id.AsSpan())</c> when a hash is required) and off
/// <c>PartTemplate.EditorTagsStrings</c> for declared editor tags.
/// </para>
/// </remarks>
public static partial class BundleParser
{
    /// <summary>The only legal root element name for a KSA asset bundle.</summary>
    public const string RootElementName = "Assets";

    /// <summary>
    /// Deserializes <paramref name="xml" /> into an <see cref="AssetBundle" /> using KSA's own
    /// serializer and also parses it into an <see cref="XDocument" />.
    /// </summary>
    /// <remarks>
    /// KSA's serializer instance (<c>XmlHelper.Serializers[typeof(AssetBundle)]</c>) is built with the
    /// <c>XmlAttributeOverrides</c> that map <c>&lt;PartModel&gt;</c>, <c>&lt;Tank&gt;</c>,
    /// <c>&lt;Collider&gt;</c>, <c>&lt;Light&gt;</c>... onto <c>PartTemplate.Components</c> and
    /// <c>PartInstance.Components</c>. A hand-built <c>new XmlSerializer(typeof(AssetBundle))</c>
    /// misses those overrides and silently drops every component, so this method never constructs one.
    /// </remarks>
    /// <param name="sourceName">File name or UI tab label, echoed into validation messages.</param>
    /// <param name="xml">The XML text to parse.</param>
    /// <param name="parsed">The parsed bundle on success, null on failure.</param>
    /// <param name="error">A human-readable reason on failure, null on success.</param>
    /// <returns>True when both the XDocument and the AssetBundle were produced.</returns>
    public static bool TryParse(string sourceName, string xml, out ParsedBundle? parsed, out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(xml))
        {
            error = "the document is empty.";
            return false;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            error = "the XML is not well formed (line " + ex.LineNumber + ", position "
                + ex.LinePosition + "): " + ex.Message;
            return false;
        }

        if (XmlHelper.Serializers is null
            || !XmlHelper.Serializers.TryGetValue(typeof(AssetBundle), out XmlSerializer? serializer)
            || serializer is null)
        {
            error = "KSA has not registered an AssetBundle serializer in XmlHelper.Serializers — "
                + "the game's internals changed and parts-now cannot parse bundles safely.";
            return false;
        }

        AssetBundle? bundle;
        try
        {
            using StringReader reader = new StringReader(xml);
            bundle = serializer.Deserialize(reader) as AssetBundle;
        }
        catch (Exception ex)
        {
            error = "the document could not be deserialized as an <Assets> bundle: " + Innermost(ex);
            return false;
        }

        if (bundle is null)
        {
            error = "the document deserialized to something that is not an <Assets> bundle.";
            return false;
        }

        parsed = new ParsedBundle(sourceName, xml, bundle, document);
        return true;
    }

    /// <summary>
    /// The 1-based line number an XML node came from, or 0 when line info is unavailable.
    /// </summary>
    /// <param name="node">The node to locate.</param>
    public static int LineNumber(XObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
    }

    private static string Innermost(Exception ex)
    {
        Exception current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
