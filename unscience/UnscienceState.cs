using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using Tomlyn;
using Tomlyn.Model;

namespace MeowSci.Unscience;

internal static class UnscienceState
{
    private const string WindowIniFile = "window.ini";
    private const string StateTomlFile = "state.toml";
    private const string WindowName = "Unscience Toolbox";
    private const int DefaultSaveInterval = 5;
    private const bool DefaultAutoSaveEnabled = false;

    private static readonly string _stateDir =
        Path.Combine(KsaPaths.UserDataDir, ".unscience");

    public static int SaveIntervalSeconds { get; set; } = DefaultSaveInterval;
    public static bool AutoSaveEnabled { get; set; } = DefaultAutoSaveEnabled;
    public static bool ShowModTooltips { get; set; } = true;

    public static void LoadImGuiWindowState()
    {
        var path = Path.Combine(_stateDir, WindowIniFile);
        if (!File.Exists(path)) return;

        try
        {
            var iniData = File.ReadAllText(path);
            ImGui.LoadIniSettingsFromMemory(iniData);
            Console.WriteLine("unscience: Loaded window state");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Failed to load window state: {ex.Message}");
        }
    }

    public static void SaveImGuiWindowState()
    {
        try
        {
            var fullIni = ImGui.SaveIniSettingsToMemory().ToString();
            var filtered = FilterIniForUnscienceWindows(fullIni);
            if (string.IsNullOrEmpty(filtered)) return;

            Directory.CreateDirectory(_stateDir);
            File.WriteAllText(Path.Combine(_stateDir, WindowIniFile), filtered);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Failed to save window state: {ex.Message}");
        }
    }

    private static string FilterIniForUnscienceWindows(string iniData)
    {
        // Extract only the [Window][Unscience Toolbox] section from ImGui ini data
        // so we don't persist/restore state for unrelated game windows
        var sb = new StringBuilder();
        var lines = iniData.Split('\n');
        bool capturing = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("[Window][", StringComparison.Ordinal))
            {
                capturing = trimmed == $"[Window][{WindowName}]";
            }
            else if (trimmed.Length > 0 && trimmed[0] == '[')
            {
                capturing = false;
            }

            if (capturing)
                sb.AppendLine(trimmed);
        }

        return sb.ToString();
    }

    public static (Dictionary<string, bool> headerOpen, Dictionary<string, bool> visibility) LoadSubmodState()
    {
        var headerOpen = new Dictionary<string, bool>();
        var visibility = new Dictionary<string, bool>();

        var path = Path.Combine(_stateDir, StateTomlFile);
        if (!File.Exists(path)) return (headerOpen, visibility);

        try
        {
            var tomlString = File.ReadAllText(path);
            if (!Toml.TryToModel<TomlTable>(tomlString, out var root, out var diagnostics))
            {
                foreach (var d in diagnostics)
                    Console.WriteLine($"unscience: TOML parse error: {d}");
                return (headerOpen, visibility);
            }

            if (root.TryGetValue("header_open", out var headerObj) && headerObj is TomlTable headerTable)
                foreach (var kvp in headerTable)
                    if (kvp.Value is bool b)
                        headerOpen[kvp.Key] = b;

            if (root.TryGetValue("visibility", out var visObj) && visObj is TomlTable visTable)
                foreach (var kvp in visTable)
                    if (kvp.Value is bool b)
                        visibility[kvp.Key] = b;

            if (root.TryGetValue("settings", out var settingsObj) && settingsObj is TomlTable settings)
            {
                if (settings.TryGetValue("save_interval", out var intervalObj) && intervalObj is long interval)
                    SaveIntervalSeconds = Math.Clamp((int)interval, 1, 30);
                if (settings.TryGetValue("auto_save_enabled", out var autoSaveObj) && autoSaveObj is bool autoSave)
                    AutoSaveEnabled = autoSave;
                if (settings.TryGetValue("show_mod_tooltips", out var tooltipsObj) && tooltipsObj is bool tooltips)
                    ShowModTooltips = tooltips;
            }

            Console.WriteLine("unscience: Loaded submod state");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Failed to load submod state: {ex.Message}");
        }

        return (headerOpen, visibility);
    }

    public static void SaveSubmodState(
        Dictionary<string, bool> headerOpen,
        Dictionary<string, bool> visibility)
    {
        try
        {
            Directory.CreateDirectory(_stateDir);

            var root = new TomlTable();

            var headerTable = new TomlTable();
            foreach (var kvp in headerOpen)
                headerTable[kvp.Key] = kvp.Value;
            root["header_open"] = headerTable;

            var visTable = new TomlTable();
            foreach (var kvp in visibility)
                visTable[kvp.Key] = kvp.Value;
            root["visibility"] = visTable;

            var settings = new TomlTable();
            settings["save_interval"] = (long)SaveIntervalSeconds;
            settings["auto_save_enabled"] = AutoSaveEnabled;
            settings["show_mod_tooltips"] = ShowModTooltips;
            root["settings"] = settings;

            File.WriteAllText(Path.Combine(_stateDir, StateTomlFile), Toml.FromModel(root));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Failed to save submod state: {ex.Message}");
        }
    }
}
