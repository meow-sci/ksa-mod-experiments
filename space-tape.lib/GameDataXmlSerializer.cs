using System.Collections.Generic;
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

        foreach (var tank in gameData.Tanks)
            el.Add(new XElement("Tank", SerializeTank(tank)));

        foreach (var battery in gameData.Batteries)
            el.Add(new XElement("Battery",
                new XElement("MaximumCapacity", new XAttribute("KWh", battery.CapacityKWh.ToString("G6")))));

        foreach (var gen in gameData.Generators)
            el.Add(new XElement("Generator",
                new XElement("Produced", new XAttribute("W", gen.OutputWatts.ToString("G6")))));

        foreach (var pc in gameData.PowerConsumers)
            el.Add(new XElement("PowerConsumer",
                new XElement("Consumed", new XAttribute("W", pc.ConsumedWatts.ToString("G6")))));

        foreach (var c in gameData.Connectors)
            el.Add(SerializeConnector(c));

        if (gameData.Decoupler != null)
            el.Add(SerializeDecoupler(gameData.Decoupler));

        if (gameData.DockingPort != null)
            el.Add(SerializeDockingPort(gameData.DockingPort));

        if (gameData.EVADoor != null)
            el.Add(SerializeEVADoor(gameData.EVADoor));

        return el;
    }

    private static XElement SerializeTank(TankState tank)
    {
        string elName = tank.Shape == TankShape.Cylindrical ? "CylindricalTank" : "SphericalTank";
        var el = new XElement(elName);

        if (!string.IsNullOrWhiteSpace(tank.WallMaterialId))
            el.Add(new XElement("Material", new XAttribute("Id", tank.WallMaterialId)));

        if (tank.Shape == TankShape.Cylindrical)
            el.Add(new XElement("Length", new XAttribute("M", tank.LengthM.ToString("G6"))));

        el.Add(new XElement("OuterRadius", new XAttribute("M", tank.OuterRadiusM.ToString("G6"))));
        el.Add(new XElement("WallThickness", new XAttribute("Mm", tank.WallThicknessMm.ToString("G6"))));

        return el;
    }

    private static XElement SerializeConnector(ConnectorState c)
    {
        var el = new XElement("Connector", new XAttribute("Id", c.Id));
        var flags = new List<string>();
        if (c.FlagInternal) flags.Add("Internal");
        if (c.FlagToSurface) flags.Add("ToSurface");
        if (c.FlagFromSurface) flags.Add("FromSurface");
        if (flags.Count > 0)
            el.Add(new XElement("Flags", string.Join(", ", flags)));
        return el;
    }

    private static XElement SerializeDecoupler(DecouplerState d)
        => new XElement("Decoupler",
            new XAttribute("ConnectorId", d.ConnectorId),
            new XAttribute("Force", d.Force.ToString("G6")));

    // KSA 4750 (rev 4683): DockingPortTemplate uses child elements — a StringReference
    // ConnectorId (<ConnectorId Value=".."/>) and an ImpulseReference PushoffImpulse
    // (<PushoffImpulse Ns=".."/>). LatchingKineticEnergy is omitted (game default applies).
    private static XElement SerializeDockingPort(DockingPortState dp)
        => new XElement("DockingPort",
            new XElement("ConnectorId", new XAttribute("Value", dp.ConnectorId)),
            new XElement("PushoffImpulse", new XAttribute("Ns", dp.PushoffImpulseNs.ToString("G6"))));

    private static XElement SerializeEVADoor(EVADoorState e)
        => new XElement("EVADoor",
            new XAttribute("ConnectorId", e.ConnectorId));

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
