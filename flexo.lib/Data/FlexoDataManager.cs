using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MeowSci.KsaAbstractions;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.FlexoLib.Data;

public sealed class FlexoDataManager
{
    private readonly string _flexoDir;
    private readonly List<FlexoPartDefinition> _definitions = new();

    public IReadOnlyList<FlexoPartDefinition> Definitions => _definitions;

    public FlexoDataManager()
    {
        _flexoDir = Path.Combine(KsaPaths.UserDataDir, ".flexo");
    }

    public void LoadAll()
    {
        _definitions.Clear();
        if (!Directory.Exists(_flexoDir)) return;

        foreach (var file in Directory.GetFiles(_flexoDir, "flexo_part_*.toml"))
        {
            try
            {
                var def = LoadDefinition(file);
                if (def != null)
                    _definitions.Add(def);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"flexo: Failed to load {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"flexo: Loaded {_definitions.Count} definition(s) from {_flexoDir}");
    }

    public FlexoPartDefinition? LoadDefinition(string filePath)
    {
        var tomlString = File.ReadAllText(filePath);
        if (!Toml.TryToModel<TomlTable>(tomlString, out var root, out var diagnostics))
        {
            foreach (var d in diagnostics)
                Console.WriteLine($"flexo: TOML parse error in {filePath}: {d}");
            return null;
        }

        var def = new FlexoPartDefinition
        {
            FileName = Path.GetFileName(filePath)
        };

        // [flexo] section
        if (root.TryGetValue("flexo", out var flexoObj) && flexoObj is TomlTable flexoTable)
        {
            if (flexoTable.TryGetValue("part_type", out var ptObj) && ptObj is string ptStr)
                def.PartType = ParsePartType(ptStr);
            if (flexoTable.TryGetValue("display_name", out var dnObj) && dnObj is string dnStr)
                def.DisplayName = dnStr;
            if (flexoTable.TryGetValue("created_from_vehicle", out var cvObj) && cvObj is string cvStr)
                def.CreatedFromVehicle = cvStr;
        }

        // [hinge] section
        if (def.PartType == FlexoPartType.Hinge &&
            root.TryGetValue("hinge", out var hingeObj) && hingeObj is TomlTable hingeTable)
        {
            def.Hinge = ParseHingeDefinition(hingeTable);
        }

        return def;
    }

    public void SaveDefinition(FlexoPartDefinition def)
    {
        Directory.CreateDirectory(_flexoDir);

        string filename = string.IsNullOrWhiteSpace(def.FileName)
            ? $"flexo_part_{SanitizeForFilename(def.DisplayName)}.toml"
            : def.FileName;
        def.FileName = filename;
        string path = Path.Combine(_flexoDir, filename);

        var root = new TomlTable();

        // [flexo] section
        var flexoTable = new TomlTable
        {
            ["part_type"] = def.PartType.ToString().ToLowerInvariant(),
            ["display_name"] = def.DisplayName,
            ["created_from_vehicle"] = def.CreatedFromVehicle,
        };
        root["flexo"] = flexoTable;

        // [hinge] section
        if (def.PartType == FlexoPartType.Hinge && def.Hinge != null)
        {
            var hingeTable = new TomlTable
            {
                ["fixed_part_template_id"] = def.Hinge.FixedPartTemplateId,
                ["moving_part_template_id"] = def.Hinge.MovingPartTemplateId,
                ["axis_x"] = def.Hinge.AxisX,
                ["axis_y"] = def.Hinge.AxisY,
                ["axis_z"] = def.Hinge.AxisZ,
                ["min_degrees"] = def.Hinge.MinDegrees,
                ["max_degrees"] = def.Hinge.MaxDegrees,
                ["resting_degrees"] = def.Hinge.RestingDegrees,
                ["speed_degrees_per_second"] = def.Hinge.SpeedDegreesPerSecond,
            };
            root["hinge"] = hingeTable;
        }

        File.WriteAllText(path, Toml.FromModel(root));
        Console.WriteLine($"flexo: Saved definition to {path}");
    }

    public void DeleteDefinition(string fileName)
    {
        var path = Path.Combine(_flexoDir, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Console.WriteLine($"flexo: Deleted {path}");
        }
    }

    private static HingeDefinition ParseHingeDefinition(TomlTable table)
    {
        var hinge = new HingeDefinition();

        if (table.TryGetValue("fixed_part_template_id", out var fpObj) && fpObj is string fpStr)
            hinge.FixedPartTemplateId = fpStr;
        if (table.TryGetValue("moving_part_template_id", out var mpObj) && mpObj is string mpStr)
            hinge.MovingPartTemplateId = mpStr;

        hinge.AxisX = GetDouble(table, "axis_x", 0);
        hinge.AxisY = GetDouble(table, "axis_y", 1);
        hinge.AxisZ = GetDouble(table, "axis_z", 0);
        hinge.MinDegrees = GetDouble(table, "min_degrees", 0);
        hinge.MaxDegrees = GetDouble(table, "max_degrees", 180);
        hinge.RestingDegrees = GetDouble(table, "resting_degrees", 0);
        hinge.SpeedDegreesPerSecond = GetDouble(table, "speed_degrees_per_second", 45);

        return hinge;
    }

    private static double GetDouble(TomlTable table, string key, double defaultValue)
    {
        if (!table.TryGetValue(key, out var obj)) return defaultValue;
        return obj switch
        {
            double d => d,
            long l => l,
            float f => f,
            int i => i,
            _ => defaultValue
        };
    }

    private static FlexoPartType ParsePartType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "hinge" => FlexoPartType.Hinge,
            _ => FlexoPartType.Hinge,
        };
    }

    private static string SanitizeForFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unnamed";
        string sanitized = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "_");
        return sanitized.Trim('_');
    }
}
