using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// Core state for the Engine Emissive feature.
///
/// Overrides the Temperature and TfiThickness fields on PartModelDynamic instances
/// to control engine glow effects on a per-engine basis. The game already wires
/// Temperature through PerInstanceData to the DynamicMeshIndirect fragment shader
/// where it drives an emissive color lookup — no shader modifications needed.
///
/// PartModelDynamic.PerInstanceData layout (80 bytes):
///   float4x4 ModelMatrix     64 bytes
///   int      StateBitFlag     4 bytes
///   float    Temperature      4 bytes  ← override target
///   float    TfiThickness     4 bytes  ← override target
///   int      packing1         4 bytes
/// </summary>
public static class EngineEmissive
{
    /// <summary>Per-engine override settings keyed by PartModelDynamic instance.</summary>
    private static readonly Dictionary<PartModelDynamic, EmissiveSettings> _engineSettings = new();

    /// <summary>Global fallback applied to all dynamic parts when enabled.</summary>
    private static bool _globalEnabled;
    private static float _globalTemperature;
    private static float _globalTfi;

    // ---- Public properties ----

    public static IReadOnlyDictionary<PartModelDynamic, EmissiveSettings> Overrides => _engineSettings;

    public static bool GlobalEnabled
    {
        get => _globalEnabled;
        set => _globalEnabled = value;
    }

    public static float GlobalTemperature
    {
        get => _globalTemperature;
        set => _globalTemperature = value;
    }

    public static float GlobalTfi
    {
        get => _globalTfi;
        set => _globalTfi = value;
    }

    // ---- Per-engine API ----

    public static void SetEngine(PartModelDynamic model, float temperature, float tfi)
    {
        _engineSettings[model] = new EmissiveSettings(temperature, tfi);
    }

    public static void ClearEngine(PartModelDynamic model)
    {
        _engineSettings.Remove(model);
    }

    public static void ClearAll()
    {
        _engineSettings.Clear();
        _globalEnabled = false;
    }

    public static bool HasOverride(PartModelDynamic model) => _engineSettings.ContainsKey(model);

    public static EmissiveSettings? GetSettings(PartModelDynamic model)
    {
        if (_engineSettings.TryGetValue(model, out var settings))
            return settings;
        return null;
    }

    /// <summary>
    /// Called by the Harmony prefix to get the effective temperature/TFI for a dynamic part.
    /// Returns true if an override should be applied.
    /// </summary>
    internal static bool TryGetEffective(PartModelDynamic model, out float temperature, out float tfi)
    {
        if (_engineSettings.TryGetValue(model, out var settings))
        {
            temperature = settings.Temperature;
            tfi = settings.Tfi;
            return true;
        }
        if (_globalEnabled)
        {
            temperature = _globalTemperature;
            tfi = _globalTfi;
            return true;
        }
        temperature = 0f;
        tfi = 0f;
        return false;
    }

    /// <summary>Clears all state. Call on mod unload.</summary>
    public static void Cleanup()
    {
        _engineSettings.Clear();
        _globalEnabled = false;
    }

    /// <summary>
    /// Scans a single vehicle for parts that have PartModelDynamicModule and returns
    /// them as a list of (label, PartModelDynamic) pairs for UI display.
    /// </summary>
    public static List<(string Label, PartModelDynamic Model)> ScanDynamicParts(Vehicle vehicle)
    {
        var results = new List<(string, PartModelDynamic)>();
        try
        {
            var parts = PartHelpers.GetAllParts(vehicle);
            foreach (var part in parts)
            {
                var modules = part.Modules.Get<PartModelDynamicModule>();
                for (int i = 0; i < modules.Length; i++)
                {
                    var label = modules.Length > 1
                        ? $"{part.Id} [{i}]"
                        : part.Id;
                    results.Add((label, modules[i].PartModelDynamic));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error scanning dynamic parts: {ex.Message}");
        }
        return results;
    }

    /// <summary>
    /// Scans all vehicles for PartModelDynamic instances, deduplicating by reference.
    /// Returns a disambiguated list of (label, PartModelDynamic) suitable for UI display.
    /// </summary>
    public static List<(string Label, PartModelDynamic Model)> ScanAllDynamicParts()
    {
        var seen = new HashSet<PartModelDynamic>(ReferenceEqualityComparer.Instance);
        var raw = new List<(string BaseName, PartModelDynamic Model)>();

        try
        {
            foreach (var vehicle in VehicleProvider.GetAllVehicles())
            {
                var parts = PartHelpers.GetAllParts(vehicle);
                foreach (var part in parts)
                {
                    var modules = part.Modules.Get<PartModelDynamicModule>();
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var model = modules[i].PartModelDynamic;
                        if (!seen.Add(model)) continue;
                        var baseName = modules.Length > 1 ? $"{part.Id} [{i}]" : part.Id;
                        raw.Add((baseName, model));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: Error scanning all dynamic parts: {ex.Message}");
        }

        // Disambiguate duplicate base names
        var labelCounts = new Dictionary<string, int>();
        foreach (var (name, _) in raw)
        {
            labelCounts.TryGetValue(name, out int c);
            labelCounts[name] = c + 1;
        }

        var seen2 = new Dictionary<string, int>();
        var results = new List<(string, PartModelDynamic)>(raw.Count);
        foreach (var (name, model) in raw)
        {
            if (labelCounts[name] > 1)
            {
                seen2.TryGetValue(name, out int idx);
                seen2[name] = idx + 1;
                results.Add(($"{name} #{idx + 1}", model));
            }
            else
            {
                results.Add((name, model));
            }
        }

        return results;
    }

    // ---- Types ----

    public readonly struct EmissiveSettings
    {
        public readonly float Temperature;
        public readonly float Tfi;

        public EmissiveSettings(float temperature, float tfi)
        {
            Temperature = temperature;
            Tfi = tfi;
        }
    }
}
