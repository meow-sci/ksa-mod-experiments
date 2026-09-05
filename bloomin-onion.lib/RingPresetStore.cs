using MeowSci.KsaRings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeowSci.KsaAbstractions;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.BloominOnionLib;

/// <summary>
/// Named ring definitions persisted to <c>.unscience/bloomin-onion-rings.toml</c>. Presets are
/// the authored work; which body they are applied to is deliberately session-only.
/// </summary>
public sealed class RingPresetStore
{
    private readonly string _configDir;
    private readonly string _filePath;
    private readonly Dictionary<string, RingDefinition> _presets = new(StringComparer.OrdinalIgnoreCase);
    private string[] _names = Array.Empty<string>();
    private bool _namesValid;

    public RingPresetStore()
    {
        _configDir = Path.Combine(KsaPaths.UserDataDir, ".unscience");
        _filePath = Path.Combine(_configDir, "bloomin-onion-rings.toml");
    }

    public string FilePath => _filePath;

    public void Initialize()
    {
        try { Directory.CreateDirectory(_configDir); }
        catch (Exception ex) { Console.WriteLine($"bloomin-onion: failed to create config directory: {ex.Message}"); }
        Load();
    }

    public string[] Names
    {
        get
        {
            if (!_namesValid)
            {
                _names = _presets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
                _namesValid = true;
            }
            return _names;
        }
    }

    /// <summary>A copy of the named preset so the editor can't mutate the stored one.</summary>
    public RingDefinition? Get(string name) => _presets.TryGetValue(name, out var d) ? d.Clone() : null;

    public bool Exists(string name) => _presets.ContainsKey(name);

    public bool Save(string name, RingDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var stored = definition.Clone();
        stored.Name = name;
        _presets[name] = stored;
        _namesValid = false;
        Write();
        Console.WriteLine($"bloomin-onion: saved ring preset '{name}'");
        return true;
    }

    public bool Delete(string name)
    {
        if (!_presets.Remove(name)) return false;
        _namesValid = false;
        Write();
        Console.WriteLine($"bloomin-onion: deleted ring preset '{name}'");
        return true;
    }

    private void Load()
    {
        _presets.Clear();
        _namesValid = false;
        try
        {
            if (!File.Exists(_filePath)) return;
            if (!Toml.TryToModel<TomlTable>(File.ReadAllText(_filePath), out var root, out var diagnostics))
            {
                foreach (var diagnostic in diagnostics) Console.WriteLine($"bloomin-onion: preset parse: {diagnostic}");
                return;
            }
            if (root.TryGetValue("presets", out var p) && p is TomlTable table)
            {
                foreach (var (name, value) in table)
                {
                    if (value is not TomlTable entry) continue;
                    var definition = RingDefinitionSerializer.FromToml(entry);
                    definition.Name = name;
                    _presets[name] = definition;
                }
            }
            Console.WriteLine($"bloomin-onion: loaded {_presets.Count} ring preset(s)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"bloomin-onion: failed to load presets: {ex.Message}");
        }
    }

    private void Write()
    {
        try
        {
            var presets = new TomlTable();
            foreach (var (name, definition) in _presets)
                presets[name] = RingDefinitionSerializer.ToToml(definition);
            var root = new TomlTable { ["presets"] = presets };
            File.WriteAllText(_filePath, Toml.FromModel(root));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"bloomin-onion: failed to save presets: {ex.Message}");
        }
    }
}
