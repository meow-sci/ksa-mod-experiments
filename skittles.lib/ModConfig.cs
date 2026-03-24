using System;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.SkittlesLib;

public sealed class ModConfig
{
    public string ActiveThemeName { get; set; } = "";
}

public static class ModConfigSerializer
{
    public static string Serialize(ModConfig config)
    {
        var root = new TomlTable();
        root["active_theme"] = config.ActiveThemeName;
        return Toml.FromModel(root);
    }

    public static ModConfig Deserialize(string toml)
    {
        var config = new ModConfig();
        if (!Toml.TryToModel<TomlTable>(toml, out var root, out _))
            return config;
        if (root.TryGetValue("active_theme", out var v))
            config.ActiveThemeName = v?.ToString() ?? "";
        return config;
    }

    public static void SaveToFile(ModConfig config, string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, Serialize(config));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error saving config to {filePath}: {ex.Message}");
        }
    }

    public static ModConfig LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return new ModConfig();
            return Deserialize(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error loading config from {filePath}: {ex.Message}");
            return new ModConfig();
        }
    }
}
