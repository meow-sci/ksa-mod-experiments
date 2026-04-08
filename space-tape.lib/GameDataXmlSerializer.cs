using System.Linq;
using System.Xml.Linq;

namespace MeowSci.SpaceTapeLib;

public static class GameDataXmlSerializer
{
    /// <summary>Serializes a PartGameDataState to a &lt;PartGameData&gt; XElement.</summary>
    public static XElement SerializeGameData(string partId, PartGameDataState gameData)
    {
        var el = new XElement("PartGameData", new XAttribute("Id", partId));

        if (!string.IsNullOrWhiteSpace(gameData.DisplayName))
            el.Add(new XAttribute("DisplayName", gameData.DisplayName));

        foreach (var tag in gameData.EditorTags)
            if (!string.IsNullOrWhiteSpace(tag))
                el.Add(new XElement("EditorTag", new XAttribute("Value", tag)));

        if (gameData.CustomMass.HasValue && gameData.CustomMass.Value > 0)
            el.Add(new XElement("CustomMass",
                new XElement("Mass", new XAttribute("Kg", gameData.CustomMass.Value.ToString("G6")))));

        if (gameData.BatteryCapacity.HasValue && gameData.BatteryCapacity.Value > 0)
            el.Add(new XElement("Battery",
                new XElement("MaximumCapacity", new XAttribute("KWh", gameData.BatteryCapacity.Value.ToString("G6")))));

        if (gameData.GeneratorOutput.HasValue && gameData.GeneratorOutput.Value > 0)
            el.Add(new XElement("Generator",
                new XElement("Produced", new XAttribute("W", gameData.GeneratorOutput.Value.ToString("G6")))));

        return el;
    }

    /// <summary>Creates a complete Assets XDocument containing the given &lt;PartGameData&gt; element.</summary>
    public static XDocument CreateGameDataDocument(XElement gameDataElement)
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Assets", gameDataElement));
    }

    /// <summary>
    /// Merges a PartGameData into an existing Assets XDocument.
    /// If a &lt;PartGameData&gt; with the same Id already exists, replaces it.
    /// Otherwise appends as a new child.
    /// </summary>
    public static XDocument MergeIntoGameData(XDocument existing, XElement newGameData)
    {
        var root = existing.Root;
        if (root == null) return CreateGameDataDocument(newGameData);

        string id = newGameData.Attribute("Id")?.Value ?? "";
        var existingEl = root.Elements("PartGameData")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == id);

        if (existingEl != null)
            existingEl.ReplaceWith(newGameData);
        else
            root.Add(newGameData);

        return existing;
    }
}
