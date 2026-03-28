using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.ConManLib;

public sealed class LayoutManager
{
    private readonly GaugeStateAccessor _accessor;
    private readonly string _configDir;
    private readonly string _layoutDir;
    private readonly string _configPath;

    private string[] _cachedLayoutNames = Array.Empty<string>();
    private bool _cacheValid;
    private string _startupDefault = string.Empty;

    public string StartupDefault => _startupDefault;
    public GaugeStateAccessor Accessor => _accessor;

    public LayoutManager(GaugeStateAccessor accessor)
    {
        _accessor = accessor;

        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var productionConfigRoot = Path.Combine(myDocuments, "My Games", "Kitten Space Agency");
        _configDir = Path.Combine(productionConfigRoot, ".con-man");
        _layoutDir = Path.Combine(_configDir, "layouts");
        _configPath = Path.Combine(_configDir, "config.toml");
    }

    /// <summary>Initialize: create directories, load config, apply startup default if set.</summary>
    public void Initialize()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            Directory.CreateDirectory(_layoutDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to create directories: {ex.Message}");
        }

        _startupDefault = LayoutSerializer.LoadStartupDefault(_configPath);
        InvalidateCache();
    }

    /// <summary>Apply startup default layout if set and file exists. Call after gauges are available.</summary>
    public void ApplyStartupDefault()
    {
        if (string.IsNullOrEmpty(_startupDefault)) return;

        var filePath = GetLayoutPath(_startupDefault);
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[con-man] Startup default layout '{_startupDefault}' not found, skipping");
            return;
        }

        ApplyLayout(_startupDefault);
        Console.WriteLine($"[con-man] Applied startup default layout: {_startupDefault}");
    }

    /// <summary>Get sorted list of layout names (cached).</summary>
    public string[] GetLayoutNames()
    {
        if (!_cacheValid)
        {
            try
            {
                if (Directory.Exists(_layoutDir))
                {
                    _cachedLayoutNames = Directory.GetFiles(_layoutDir, "*.toml")
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .ToArray();
                    Array.Sort(_cachedLayoutNames, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _cachedLayoutNames = Array.Empty<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[con-man] Failed to list layouts: {ex.Message}");
                _cachedLayoutNames = Array.Empty<string>();
            }
            _cacheValid = true;
        }
        return _cachedLayoutNames;
    }

    /// <summary>Save current gauge state as a named layout.</summary>
    public bool SaveLayout(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Console.WriteLine($"[con-man] Invalid layout name: '{name}'");
            return false;
        }

        var canvases = _accessor.GetCanvases();
        if (canvases == null || canvases.Count == 0)
        {
            Console.WriteLine("[con-man] No gauge canvases found to save");
            return false;
        }

        var gauges = new Dictionary<string, GaugeState>();
        foreach (var canvas in canvases)
        {
            var offset = _accessor.GetCustomOffset(canvas);
            var scale = _accessor.GetCustomScale(canvas);
            gauges[canvas.Id] = new GaugeState
            {
                Enabled = _accessor.GetEnabled(canvas),
                OffsetX = offset.X,
                OffsetY = offset.Y,
                ScaleX = scale.X,
                ScaleY = scale.Y
            };
        }

        LayoutSerializer.SaveLayout(GetLayoutPath(name), gauges);
        InvalidateCache();
        Console.WriteLine($"[con-man] Saved layout: {name} ({gauges.Count} gauges)");
        return true;
    }

    /// <summary>Apply a named layout to all gauge canvases.</summary>
    public bool ApplyLayout(string name)
    {
        var layout = LayoutSerializer.LoadLayout(GetLayoutPath(name));
        if (layout == null) return false;

        var canvases = _accessor.GetCanvases();
        if (canvases == null) return false;

        int applied = 0;
        foreach (var canvas in canvases)
        {
            if (!layout.TryGetValue(canvas.Id, out var state)) continue;

            // Read the current base position/size BEFORE setting new offsets.
            // These represent the game's resolution-aware base values:
            //   _windowPosition = ImGui.GetWindowPos() - _customOffset
            //   _windowSize = ImGui.GetWindowSize() / _customScale
            var basePos = _accessor.GetWindowPosition(canvas);
            var baseSize = _accessor.GetWindowSize(canvas);

            // Set the saved delta values via reflection
            _accessor.SetEnabled(canvas, state.Enabled);
            _accessor.SetCustomOffset(canvas, new float2(state.OffsetX, state.OffsetY));
            _accessor.SetCustomScale(canvas, new float2(state.ScaleX, state.ScaleY));

            // Force-reposition the ImGui window directly.
            // SetNextWindowPos/Size with ImGuiCond.Appearing only fires once (first appear),
            // so we must use SetWindowPos/Size by name to move already-visible windows.
            var windowTitle = _accessor.GetWindowTitle(canvas);
            if (!string.IsNullOrEmpty(windowTitle))
            {
                var targetPos = basePos + new float2(state.OffsetX, state.OffsetY);
                var targetSize = baseSize * new float2(state.ScaleX, state.ScaleY);
                ImGui.SetWindowPos(windowTitle, in targetPos, ImGuiCond.Always);
                ImGui.SetWindowSize(windowTitle, in targetSize, ImGuiCond.Always);
            }

            applied++;
        }

        Console.WriteLine($"[con-man] Applied layout: {name} ({applied}/{canvases.Count} gauges matched)");
        return true;
    }

    /// <summary>Delete a named layout.</summary>
    public bool DeleteLayout(string name)
    {
        var filePath = GetLayoutPath(name);
        try
        {
            if (!File.Exists(filePath)) return false;
            File.Delete(filePath);
            InvalidateCache();

            if (string.Equals(_startupDefault, name, StringComparison.OrdinalIgnoreCase))
                SetStartupDefault(string.Empty);

            Console.WriteLine($"[con-man] Deleted layout: {name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[con-man] Failed to delete layout '{name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Set or clear the startup default layout.</summary>
    public void SetStartupDefault(string name)
    {
        _startupDefault = name;
        LayoutSerializer.SaveStartupDefault(_configPath, name);
    }

    /// <summary>Force cache invalidation (e.g. after external changes).</summary>
    public void InvalidateCache()
    {
        _cacheValid = false;
    }

    private string GetLayoutPath(string name)
    {
        return Path.Combine(_layoutDir, name + ".toml");
    }
}
