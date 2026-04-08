using System;
using System.Linq;
using System.Xml.Linq;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

public static class PartXmlSerializer
{
    private const double Epsilon = 1e-9;

    /// <summary>Serializes an EditingPart to a &lt;Part&gt; XElement.</summary>
    public static XElement SerializePart(EditingPart part)
    {
        var partEl = new XElement("Part", new XAttribute("Id", part.PartId));
        foreach (var placement in part.Placements)
        {
            var subPartEl = new XElement("SubPart",
                new XAttribute("Id", placement.InstanceId),
                new XAttribute("InstanceOf", placement.SubPartTemplateId));
            var transformEl = SerializeTransform(placement.Position, placement.Rotation, placement.Scale);
            if (transformEl != null)
                subPartEl.Add(transformEl);
            partEl.Add(subPartEl);
        }
        return partEl;
    }

    /// <summary>Creates an XDocument with a root &lt;Assets&gt; element containing the given &lt;Part&gt; element.</summary>
    public static XDocument CreateAssetsDocument(XElement partElement)
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Assets", partElement));
    }

    /// <summary>
    /// Merges a Part into an existing Assets XDocument.
    /// If a &lt;Part&gt; with the same Id already exists, replaces it.
    /// Otherwise appends as a new child.
    /// </summary>
    public static XDocument MergeIntoAssets(XDocument existing, XElement newPart)
    {
        var root = existing.Root;
        if (root == null) return CreateAssetsDocument(newPart);

        string id = newPart.Attribute("Id")?.Value ?? "";
        var existingEl = root.Elements("Part")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == id);

        if (existingEl != null)
            existingEl.ReplaceWith(newPart);
        else
            root.Add(newPart);

        return existing;
    }

    /// <summary>Returns null if transform is identity/default. Otherwise returns a &lt;Transform&gt; XElement.</summary>
    private static XElement? SerializeTransform(double3 position, doubleQuat rotation, double3 scale)
    {
        var posEl = SerializeVector3("Position", position, double3.Zero);
        var rotEl = SerializeRotation(rotation);
        var scaleEl = SerializeVector3("Scale", scale, double3.One);

        if (posEl == null && rotEl == null && scaleEl == null)
            return null;

        var transform = new XElement("Transform");
        if (posEl != null) transform.Add(posEl);
        if (rotEl != null) transform.Add(rotEl);
        if (scaleEl != null) transform.Add(scaleEl);
        return transform;
    }

    // Serializes a double3 as e.g. <Position X="1.5" Z="-0.5"/> — only includes non-default axis attributes.
    private static XElement? SerializeVector3(string elementName, double3 value, double3 defaultValue)
    {
        bool xDiff = Math.Abs(value.X - defaultValue.X) > Epsilon;
        bool yDiff = Math.Abs(value.Y - defaultValue.Y) > Epsilon;
        bool zDiff = Math.Abs(value.Z - defaultValue.Z) > Epsilon;

        if (!xDiff && !yDiff && !zDiff) return null;

        var el = new XElement(elementName);
        if (xDiff) el.Add(new XAttribute("X", value.X.ToString("G6")));
        if (yDiff) el.Add(new XAttribute("Y", value.Y.ToString("G6")));
        if (zDiff) el.Add(new XAttribute("Z", value.Z.ToString("G6")));
        return el;
    }

    // Converts a doubleQuat to Euler XYZ radians (matching KSA's TransformReference serialization),
    // then serializes as a <Rotation> element.
    private static XElement? SerializeRotation(doubleQuat rotation)
    {
        double3 euler = rotation.NormalizedOrIdentity().ToXyzRadians();
        return SerializeVector3("Rotation", euler, double3.Zero);
    }
}
