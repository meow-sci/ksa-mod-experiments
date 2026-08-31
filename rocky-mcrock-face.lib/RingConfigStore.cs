using System;
using System.Collections.Generic;
using System.IO;
using MeowSci.KsaAbstractions;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>
/// Persists per-body ring selections to Documents/My Games/Kitten Space Agency/.unscience/rocky-mcrock-face.toml.
/// </summary>
public sealed class RingConfigStore
{
    private readonly string _configDir = Path.Combine(KsaPaths.UserDataDir, ".unscience");
    private readonly string _filePath;

    public RingConfigStore()
    {
        _filePath = Path.Combine(_configDir, "rocky-mcrock-face.toml");
    }

    public Dictionary<string, RingSelection> Load()
    {
        var result = new Dictionary<string, RingSelection>();
        try
        {
            if (!File.Exists(_filePath)) return result;
            var content = File.ReadAllText(_filePath);
            if (!Toml.TryToModel<TomlTable>(content, out var root, out var diagnostics))
            {
                foreach (var diagnostic in diagnostics)
                    Console.WriteLine($"rocky-mcrock-face: config parse error: {diagnostic}");
                return result;
            }
            if (root.TryGetValue("bodies", out var bodiesObj) && bodiesObj is TomlTable bodies)
            {
                foreach (var (bodyId, value) in bodies)
                {
                    if (value is not TomlTable body) continue;
                    result[bodyId] = ReadSelection(body);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: failed to load config: {ex.Message}");
        }
        return result;
    }

    public void Save(Dictionary<string, RingSelection> selections)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var bodies = new TomlTable();
            foreach (var (bodyId, selection) in selections)
            {
                if (!selection.HasAnyOverride) continue;
                bodies[bodyId] = WriteSelection(selection);
            }
            var root = new TomlTable { ["bodies"] = bodies };
            File.WriteAllText(_filePath, Toml.FromModel(root));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"rocky-mcrock-face: failed to save config: {ex.Message}");
        }
    }

    private static RingSelection ReadSelection(TomlTable body)
    {
        var selection = new RingSelection();
        if (body.TryGetValue("lod_meshes", out var lodObj) && lodObj is TomlArray lods)
        {
            for (int i = 0; i < lods.Count && i < RingSelection.MaxLods; i++)
                selection.LodMeshIds[i] = lods[i] as string ?? "";
        }
        selection.DiffuseId = ReadString(body, "diffuse");
        selection.NormalId = ReadString(body, "normal");
        selection.PbrId = ReadString(body, "pbr");
        selection.BandTextureId = ReadString(body, "band_texture");
        selection.OverrideFieldSettings = body.TryGetValue("override_field_settings", out var o) && o is true;
        selection.SizeM = ReadDouble(body, "size_m", selection.SizeM);
        selection.DensityPerKm3 = ReadDouble(body, "density_per_km3", selection.DensityPerKm3);
        selection.RenderDistanceKm = ReadDouble(body, "render_distance_km", selection.RenderDistanceKm);
        selection.ThicknessKm = ReadDouble(body, "thickness_km", selection.ThicknessKm);
        return selection;
    }

    private static TomlTable WriteSelection(RingSelection selection)
    {
        var lods = new TomlArray();
        foreach (var id in selection.LodMeshIds) lods.Add(id);
        return new TomlTable
        {
            ["lod_meshes"] = lods,
            ["diffuse"] = selection.DiffuseId,
            ["normal"] = selection.NormalId,
            ["pbr"] = selection.PbrId,
            ["band_texture"] = selection.BandTextureId,
            ["override_field_settings"] = selection.OverrideFieldSettings,
            ["size_m"] = selection.SizeM,
            ["density_per_km3"] = selection.DensityPerKm3,
            ["render_distance_km"] = selection.RenderDistanceKm,
            ["thickness_km"] = selection.ThicknessKm,
        };
    }

    private static string ReadString(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) && value is string s ? s : "";

    private static double ReadDouble(TomlTable table, string key, double fallback) =>
        table.TryGetValue(key, out var value) && value is double d ? d : fallback;
}
