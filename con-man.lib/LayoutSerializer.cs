using System;
using System.Collections.Generic;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.ConManLib;

public sealed class GaugeState
{
    public bool Enabled { get; set; } = true;
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float ScaleX { get; set; } = 1f;
    public float ScaleY { get; set; } = 1f;
}

public static class LayoutSerializer
{
    /// <summary>Serialize a layout (gauge id -> state) to a TOML string.</summary>
    public static string SerializeLayout(Dictionary<string, GaugeState> gauges)
    {
        var root = new TomlTable();
        var gaugesTable = new TomlTable();

        foreach (var (id, state) in gauges)
        {
            var entry = new TomlTable
            {
                ["enabled"] = state.Enabled,
                ["offset_x"] = (double)state.OffsetX,
                ["offset_y"] = (double)state.OffsetY,
                ["scale_x"] = (double)state.ScaleX,
                ["scale_y"] = (double)state.ScaleY
            };
            gaugesTable[id] = entry;
        }

        root["gauges"] = gaugesTable;
        return Toml.FromModel(root);
    }

    /// <summary>Deserialize a TOML string to a layout dict.</summary>
    public static Dictionary<string, GaugeState> DeserializeLayout(string toml)
    {
        var result = new Dictionary<string, GaugeState>();

        try
        {
            var model = Toml.ToModel(toml);
            if (model["gauges"] is not TomlTable gaugesTable)
                return result;

            foreach (var (id, value) in gaugesTable)
            {
                if (value is not TomlTable entry)
                    continue;

                result[id] = new GaugeState
                {
                    Enabled = entry.TryGetValue("enabled", out var e) && e is bool b ? b : true,
                    OffsetX = (float)(double)(entry.TryGetValue("offset_x", out var ox) ? ox ?? 0.0 : 0.0),
                    OffsetY = (float)(double)(entry.TryGetValue("offset_y", out var oy) ? oy ?? 0.0 : 0.0),
                    ScaleX = (float)(double)(entry.TryGetValue("scale_x", out var sx) ? sx ?? 1.0 : 1.0),
                    ScaleY = (float)(double)(entry.TryGetValue("scale_y", out var sy) ? sy ?? 1.0 : 1.0)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to deserialize layout: {ex.Message}");
        }

        return result;
    }

    /// <summary>Write a layout to a TOML file.</summary>
    public static void SaveLayout(string filePath, Dictionary<string, GaugeState> gauges)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, SerializeLayout(gauges));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to save layout to {filePath}: {ex.Message}");
        }
    }

    /// <summary>Read a layout from a TOML file. Returns null if the file cannot be read.</summary>
    public static Dictionary<string, GaugeState>? LoadLayout(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[con-man] Layout file not found: {filePath}");
                return null;
            }

            return DeserializeLayout(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to load layout from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Read the startup default layout name from a config file.</summary>
    public static string LoadStartupDefault(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return string.Empty;

            var model = Toml.ToModel(File.ReadAllText(configPath));
            if (model.TryGetValue("settings", out var s) && s is TomlTable settings)
                return settings.TryGetValue("startup_default", out var v) ? v as string ?? string.Empty : string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to load startup default from {configPath}: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>Write the startup default layout name to a config file.</summary>
    public static void SaveStartupDefault(string configPath, string layoutName)
    {
        try
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var root = new TomlTable();
            var settings = new TomlTable { ["startup_default"] = layoutName };
            root["settings"] = settings;

            File.WriteAllText(configPath, Toml.FromModel(root));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to save startup default to {configPath}: {ex.Message}");
        }
    }
}
