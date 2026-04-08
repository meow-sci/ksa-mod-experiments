using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Manages reading and writing Part XML/GameData files to the space-tape-parts mod directory.
/// </summary>
public sealed class PartModWriter
{
    private const string ModTomlContent = """
        name = "Space Tape Custom Parts"
        description = "Parts created with the Space Tape Part Editor"
        version = "1.0.0"
        author = "Space Tape Editor"

        [StarMap]
        EntryAssembly = ""
        """;

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
            EnsureModDir();

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

        var massEl = gdEl.Element("CustomMass")?.Element("Mass");
        if (massEl != null && double.TryParse(massEl.Attribute("Kg")?.Value,
                NumberStyles.Any, CultureInfo.InvariantCulture, out double mass))
            part.GameData.CustomMass = mass;

        var battEl = gdEl.Element("Battery")?.Element("MaximumCapacity");
        if (battEl != null && double.TryParse(battEl.Attribute("KWh")?.Value,
                NumberStyles.Any, CultureInfo.InvariantCulture, out double kwh))
            part.GameData.BatteryCapacity = kwh;

        var genEl = gdEl.Element("Generator")?.Element("Produced");
        if (genEl != null && double.TryParse(genEl.Attribute("W")?.Value,
                NumberStyles.Any, CultureInfo.InvariantCulture, out double watts))
            part.GameData.GeneratorOutput = watts;
    }

    private void EnsureModDir()
    {
        Directory.CreateDirectory(ModDir);

        string tomlPath = Path.Combine(ModDir, "mod.toml");
        if (!File.Exists(tomlPath))
            File.WriteAllText(tomlPath, ModTomlContent);
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
}
