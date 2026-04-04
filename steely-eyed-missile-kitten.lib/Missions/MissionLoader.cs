using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MeowSci.SteelyEyedMissileKittenLib.Missions;

/// <summary>Discovers and deserializes mission YAML files from bundled and user directories.</summary>
public static class MissionLoader
{
    public static List<MissionDefinition> LoadAllMissions(string bundledDir, string userDir)
    {
        var missions = new List<MissionDefinition>();
        LoadFromDirectory(bundledDir, missions);
        LoadFromDirectory(userDir, missions);
        return missions;
    }

    private static void LoadFromDirectory(string directory, List<MissionDefinition> missions)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.GetFiles(directory, "*.yaml"))
        {
            try
            {
                var mission = LoadMission(file);
                if (mission != null)
                    missions.Add(mission);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MissionLoader] Failed to load {file}: {ex.Message}");
            }
        }
    }

    public static MissionDefinition? LoadMission(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var mission = deserializer.Deserialize<MissionDefinition>(yaml);
        if (mission == null) return null;
        if (string.IsNullOrEmpty(mission.Id))
            mission.Id = Path.GetFileNameWithoutExtension(filePath);
        return mission;
    }
}
