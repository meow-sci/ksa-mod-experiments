using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Manages reading and writing Part XML/GameData files to the space-tape-parts mod directory.
/// </summary>
public sealed class PartModWriter
{


    /// <summary>The absolute path to the space-tape-parts mod directory.</summary>
    public string ModDir { get; }

    /// <summary>Currently selected filename (without extension) for saving.</summary>
    public string CurrentFileName { get; set; } = "MyParts";

    private readonly List<string> _existingFiles = new();

    /// <summary>Cached list of existing asset file base names (without extension) in mod dir.</summary>
    public IReadOnlyList<string> ExistingFiles => _existingFiles;

    /// <summary>Last error message from a Save/Load operation, or null if no error.</summary>
    public string? LastError { get; private set; }

    public bool HasError => LastError != null;

    public PartModWriter()
    {
        ModDir = Path.Combine(KsaPaths.UserDataDir, "mods", "space-tape-parts");
    }

    /// <summary>Refreshes the list of existing asset file base names in the mod directory.</summary>
    public void RefreshFileList()
    {
        _existingFiles.Clear();
        if (!Directory.Exists(ModDir)) return;

        foreach (var file in Directory.GetFiles(ModDir, "*.xml")
            .Where(f => !f.EndsWith(".gamedata.xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f))
        {
            _existingFiles.Add(Path.GetFileNameWithoutExtension(file));
        }
    }

    /// <summary>
    /// Writes a Part to the current file. Creates the mod directory and mod.toml if needed.
    /// If the file already exists, merges the Part into it (replaces if same Id, else appends).
    /// Also writes a corresponding .gamedata.xml file.
    /// </summary>
    /// <returns>True on success; false on failure (check <see cref="LastError"/>).</returns>
    public bool SavePart(EditingPart part)
    {
        LastError = null;
        try
        {
            Directory.CreateDirectory(ModDir);

            string assetsPath = Path.Combine(ModDir, CurrentFileName + ".xml");
            string gameDataPath = Path.Combine(ModDir, CurrentFileName + ".gamedata.xml");

            // Write Assets XML
            XDocument assetsDoc;
            if (File.Exists(assetsPath))
            {
                assetsDoc = XDocument.Load(assetsPath);
                PartXmlSerializer.MergeIntoAssets(assetsDoc, PartXmlSerializer.SerializePart(part));
            }
            else
            {
                assetsDoc = PartXmlSerializer.CreateAssetsDocument(PartXmlSerializer.SerializePart(part));
            }
            assetsDoc.Save(assetsPath);

            // Write GameData XML
            XDocument gameDataDoc;
            if (File.Exists(gameDataPath))
            {
                gameDataDoc = XDocument.Load(gameDataPath);
                GameDataXmlSerializer.MergeIntoGameData(gameDataDoc, GameDataXmlSerializer.SerializeGameData(part.PartId, part.GameData));
            }
            else
            {
                gameDataDoc = GameDataXmlSerializer.CreateGameDataDocument(GameDataXmlSerializer.SerializeGameData(part.PartId, part.GameData));
            }
            gameDataDoc.Save(gameDataPath);

            // Keep mod.toml assets list up to date
            UpdateModToml(CurrentFileName);

            RefreshFileList();
            Console.WriteLine($"space-tape: Saved part '{part.PartId}' to {assetsPath}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Console.WriteLine($"space-tape: SavePart failed: {ex}");
            return false;
        }
    }

    /// <summary>Lists all saved (partId, fileName) pairs from all asset XML files in the mod directory.</summary>
    public List<(string partId, string fileName)> ListSavedParts()
    {
        var results = new List<(string, string)>();
        if (!Directory.Exists(ModDir)) return results;

        foreach (var file in Directory.GetFiles(ModDir, "*.xml")
            .Where(f => !f.EndsWith(".gamedata.xml", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var doc = XDocument.Load(file);
                var fileName = Path.GetFileNameWithoutExtension(file);
                foreach (var partEl in doc.Root?.Elements("Part") ?? Enumerable.Empty<XElement>())
                {
                    string partId = partEl.Attribute("Id")?.Value ?? "";
                    if (!string.IsNullOrEmpty(partId))
                        results.Add((partId, fileName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"space-tape: Failed to read {file}: {ex.Message}");
            }
        }
        return results;
    }

    /// <summary>Loads a specific Part from a file by partId. Returns null if not found or on error.</summary>
    public EditingPart? LoadPart(string partId, string fileName)
    {
        string assetsPath = Path.Combine(ModDir, fileName + ".xml");
        if (!File.Exists(assetsPath)) return null;

        try
        {
            var doc = XDocument.Load(assetsPath);
            var partEl = doc.Root?.Elements("Part")
                .FirstOrDefault(e => e.Attribute("Id")?.Value == partId);
            if (partEl == null) return null;

            var editingPart = new EditingPart { PartId = partId };

            foreach (var spEl in partEl.Elements("SubPart"))
            {
                var instanceId = spEl.Attribute("Id")?.Value ?? "";
                var templateId = spEl.Attribute("InstanceOf")?.Value ?? "";
                if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(templateId))
                    continue;

                var placement = new SubPartPlacement
                {
                    InstanceId = instanceId,
                    SubPartTemplateId = templateId
                };

                var transformEl = spEl.Element("Transform");
                if (transformEl != null)
                {
                    placement.Position = ParseVector3(transformEl.Element("Position"), double3.Zero);
                    placement.Rotation = ParseRotation(transformEl.Element("Rotation"));
                    placement.Scale = ParseVector3(transformEl.Element("Scale"), double3.One);
                }

                editingPart.Placements.Add(placement);
            }

            // Parse Connectors from Assets XML (geometry: position/rotation/scale + flags)
            foreach (var connEl in partEl.Elements("Connector"))
            {
                var connId = connEl.Attribute("Id")?.Value ?? "";
                if (string.IsNullOrEmpty(connId)) continue;

                var conn = new ConnectorState { Id = connId };
                var transformEl = connEl.Element("Transform");
                if (transformEl != null)
                {
                    conn.Position = ParseVector3(transformEl.Element("Position"), double3.Zero);
                    conn.Rotation = ParseRotation(transformEl.Element("Rotation"));
                    conn.Scale = ParseVector3(transformEl.Element("Scale"), double3.One);
                }
                var flagsStr = connEl.Element("Flags")?.Value ?? "";
                conn.FlagInternal = flagsStr.Contains("Internal");
                conn.FlagToSurface = flagsStr.Contains("ToSurface");
                conn.FlagFromSurface = flagsStr.Contains("FromSurface");

                editingPart.GameData.Connectors.Add(conn);
            }

            string gameDataPath = Path.Combine(ModDir, fileName + ".gamedata.xml");
            if (File.Exists(gameDataPath))
                LoadGameData(editingPart, gameDataPath, partId);

            return editingPart;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"space-tape: LoadPart failed for '{partId}' in '{fileName}': {ex}");
            return null;
        }
    }

    private static void LoadGameData(EditingPart part, string gameDataPath, string partId)
    {
        var doc = XDocument.Load(gameDataPath);
        var gdEl = doc.Root?.Elements("PartGameData")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == partId);
        if (gdEl == null) return;

        part.GameData.DisplayName = gdEl.Attribute("DisplayName")?.Value ?? "";

        foreach (var tagEl in gdEl.Elements("EditorTag"))
        {
            var val = tagEl.Attribute("Value")?.Value;
            if (!string.IsNullOrEmpty(val))
                part.GameData.EditorTags.Add(val);
        }

        // CustomMass
        var massEl = gdEl.Element("CustomMass")?.Element("Mass");
        if (massEl != null && TryParseDouble(massEl, "Kg", out double mass))
            part.GameData.CustomMass = mass;

        // Tank (CylindricalTank or SphericalTank)
        // Look inside <Tank> wrapper first (correct KSA format), then fall back to direct children
        var tankWrapper = gdEl.Element("Tank");
        var cylEl = tankWrapper?.Element("CylindricalTank") ?? gdEl.Element("CylindricalTank");
        var sphEl = tankWrapper?.Element("SphericalTank") ?? gdEl.Element("SphericalTank");
        var tankEl = cylEl ?? sphEl;
        if (tankEl != null)
        {
            var tank = new TankState
            {
                Shape = cylEl != null ? TankShape.Cylindrical : TankShape.Spherical,
            };
            tank.WallMaterialId = tankEl.Element("Material")?.Attribute("Id")?.Value
                                ?? tankEl.Element("Material")?.Attribute("Value")?.Value
                                ?? "Aluminum.2014(s)";
            if (TryParseDouble(tankEl.Element("OuterRadius"), "M", out double outerR))
                tank.OuterRadiusM = outerR;
            if (TryParseDouble(tankEl.Element("WallThickness"), "Mm", out double wallMm))
                tank.WallThicknessMm = wallMm;
            if (cylEl != null)
            {
                if (TryParseDouble(tankEl.Element("Length"), "M", out double length))
                    tank.LengthM = length;
            }
            part.GameData.Tank = tank;
        }

        // Batteries (multiple)
        foreach (var battEl in gdEl.Elements("Battery"))
        {
            var capEl = battEl.Element("MaximumCapacity");
            if (capEl != null && TryParseDouble(capEl, "KWh", out double kwh))
                part.GameData.Batteries.Add(new BatteryState { CapacityKWh = kwh });
        }

        // Generators (multiple)
        foreach (var genEl in gdEl.Elements("Generator"))
        {
            var prodEl = genEl.Element("Produced");
            if (prodEl != null && TryParseDouble(prodEl, "W", out double watts))
                part.GameData.Generators.Add(new GeneratorState { OutputWatts = watts });
        }

        // PowerConsumers (multiple)
        foreach (var pcEl in gdEl.Elements("PowerConsumer"))
        {
            var consEl = pcEl.Element("Consumed");
            if (consEl != null && TryParseDouble(consEl, "W", out double pcWatts))
                part.GameData.PowerConsumers.Add(new PowerConsumerState { ConsumedWatts = pcWatts });
        }

        // Connectors (GameData flags — merge with any already loaded from Assets XML)
        foreach (var connEl in gdEl.Elements("Connector"))
        {
            var connId = connEl.Attribute("Id")?.Value ?? "";
            var flagsStr = connEl.Element("Flags")?.Value ?? "";

            var existing = part.GameData.Connectors.FirstOrDefault(c => c.Id == connId);
            if (existing != null)
            {
                if (flagsStr.Contains("Internal")) existing.FlagInternal = true;
                if (flagsStr.Contains("ToSurface")) existing.FlagToSurface = true;
                if (flagsStr.Contains("FromSurface")) existing.FlagFromSurface = true;
            }
            else
            {
                part.GameData.Connectors.Add(new ConnectorState
                {
                    Id = connId,
                    FlagInternal = flagsStr.Contains("Internal"),
                    FlagToSurface = flagsStr.Contains("ToSurface"),
                    FlagFromSurface = flagsStr.Contains("FromSurface"),
                });
            }
        }

        // Decoupler
        var decEl = gdEl.Element("Decoupler");
        if (decEl != null)
        {
            part.GameData.Decoupler = new DecouplerState
            {
                ConnectorId = decEl.Attribute("ConnectorId")?.Value ?? "",
                Force = double.TryParse(decEl.Attribute("Force")?.Value,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double f) ? f : 500.0
            };
        }

        // DockingPort
        var dpEl = gdEl.Element("DockingPort");
        if (dpEl != null)
        {
            part.GameData.DockingPort = new DockingPortState
            {
                ConnectorId = dpEl.Attribute("ConnectorId")?.Value ?? "",
                Force = double.TryParse(dpEl.Attribute("Force")?.Value,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double f) ? f : 500.0
            };
        }

        // EVADoor
        var evaEl = gdEl.Element("EVADoor");
        if (evaEl != null)
        {
            part.GameData.EVADoor = new EVADoorState
            {
                ConnectorId = evaEl.Attribute("ConnectorId")?.Value ?? "",
            };
        }
    }

    /// <summary>
    /// Reads mod.toml (creating it if absent), ensures the given file base name's .xml and
    /// .gamedata.xml entries appear in the <c>assets</c> array, then writes it back.
    /// </summary>
    private void UpdateModToml(string fileBaseName)
    {
        string tomlPath = Path.Combine(ModDir, "mod.toml");

        TomlTable root;
        if (File.Exists(tomlPath))
        {
            root = Toml.ToModel(File.ReadAllText(tomlPath));
        }
        else
        {
            root = new TomlTable
            {
                ["name"]        = "Space Tape Custom Parts",
                ["description"] = "Parts created with the Space Tape Part Editor",
                ["version"]     = "1.0.0",
                ["author"]      = "Space Tape Editor",
                ["StarMap"]     = new TomlTable { ["EntryAssembly"] = "" },
            };
        }

        // Gather existing assets into a list, then add missing entries
        var assetsList = new List<string>();
        if (root.TryGetValue("assets", out var existing) && existing is TomlArray existingArr)
            foreach (var item in existingArr)
                if (item is string s) assetsList.Add(s);

        string xmlEntry      = fileBaseName + ".xml";
        string gameDataEntry = fileBaseName + ".gamedata.xml";

        if (!assetsList.Contains(xmlEntry,      StringComparer.OrdinalIgnoreCase))
            assetsList.Add(xmlEntry);
        if (!assetsList.Contains(gameDataEntry, StringComparer.OrdinalIgnoreCase))
            assetsList.Add(gameDataEntry);

        var assetsArray = new TomlArray();
        foreach (var entry in assetsList)
            assetsArray.Add(entry);
        root["assets"] = assetsArray;

        File.WriteAllText(tomlPath, Toml.FromModel(root));
    }

    private static double3 ParseVector3(XElement? el, double3 defaultValue)
    {
        if (el == null) return defaultValue;
        double x = defaultValue.X, y = defaultValue.Y, z = defaultValue.Z;
        if (double.TryParse(el.Attribute("X")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double px)) x = px;
        if (double.TryParse(el.Attribute("Y")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double py)) y = py;
        if (double.TryParse(el.Attribute("Z")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double pz)) z = pz;
        return new double3(x, y, z);
    }

    private static doubleQuat ParseRotation(XElement? el)
    {
        if (el == null) return doubleQuat.Identity;
        var euler = ParseVector3(el, double3.Zero);
        return QuaternionEx.CreateFromXyzRadians(euler);
    }

    private static bool TryParseDouble(XElement? el, string attrName, out double value)
    {
        value = 0;
        return el != null && double.TryParse(
            el.Attribute(attrName)?.Value,
            NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
