using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brutal.ImGuiApi;

namespace MeowSci.SkittlesLib;

public sealed class ThemeEntry
{
    public string Name { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public string? FilePath { get; set; }
}

public sealed class ThemeManager
{
    private static readonly string[] BuiltInThemeNames = { "Game Default", "Dark", "Light", "Classic" };

    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly string _themesDirectory;

    public ThemeDefinition? DefaultTheme { get; private set; }
    public List<ThemeEntry> AvailableThemes { get; } = new();
    public string? ActiveThemeName { get; private set; }
    public ModConfig Config { get; private set; } = new();

    public ThemeManager()
    {
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var productionConfigRoot = Path.Combine(myDocuments, "My Games", "Kitten Space Agency");
        _configDirectory = Path.Combine(productionConfigRoot, "skittles");
        _configFilePath = Path.Combine(_configDirectory, "config.toml");
        _themesDirectory = Path.Combine(_configDirectory, "themes");
    }

    public void Initialize()
    {
        // 1. Capture game default style BEFORE any theming
        DefaultTheme = ThemeDefinition.CaptureFromImGui();
        DefaultTheme.Name = "Game Default";
        DefaultTheme.Description = "The game's original default style";

        // 2. Ensure directories exist
        Directory.CreateDirectory(_themesDirectory);

        // 3. Ship the Inanimate Carbon Rod preset if not present
        string icr_path = Path.Combine(_themesDirectory, "inanimate-carbon-rod.toml");
        if (!File.Exists(icr_path))
        {
            var icr = BuiltInThemes.CarbonRod();
            ThemeSerializer.SaveToFile(icr, icr_path);
            Console.WriteLine("skittles: Shipped Inanimate Carbon Rod preset");
        }

        // 4. Load config
        Config = ModConfigSerializer.LoadFromFile(_configFilePath);

        // 5. Discover themes
        RefreshThemeList();

        // 6. Apply startup theme
        if (!string.IsNullOrEmpty(Config.ActiveThemeName))
        {
            bool found = AvailableThemes.Any(t => t.Name == Config.ActiveThemeName);
            if (found)
            {
                ApplyTheme(Config.ActiveThemeName);
            }
        }
    }

    public string[] GetThemeNames()
    {
        return AvailableThemes.Select(t => t.Name).ToArray();
    }

    public void ApplyTheme(string themeName)
    {
        try
        {
            if (themeName == "Game Default")
            {
                DefaultTheme?.ApplyToImGui();
            }
            else if (themeName == "Dark")
            {
                ImGui.StyleColorsDark();
                ApplyDefaultStyleVars();
            }
            else if (themeName == "Light")
            {
                ImGui.StyleColorsLight();
                ApplyDefaultStyleVars();
            }
            else if (themeName == "Classic")
            {
                ImGui.StyleColorsClassic();
                ApplyDefaultStyleVars();
            }
            else
            {
                var entry = AvailableThemes.FirstOrDefault(t => t.Name == themeName);
                if (entry?.FilePath != null)
                {
                    var theme = ThemeSerializer.LoadFromFile(entry.FilePath);
                    theme?.ApplyToImGui();
                }
                else
                {
                    Console.WriteLine($"skittles: Theme '{themeName}' not found");
                    return;
                }
            }

            ActiveThemeName = themeName;
            Config.ActiveThemeName = themeName;
            ModConfigSerializer.SaveToFile(Config, _configFilePath);
            Console.WriteLine($"skittles: Applied theme '{themeName}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error applying theme '{themeName}': {ex.Message}");
        }
    }

    public void SaveCurrentAsTheme(string name)
    {
        try
        {
            string slug = Slugify(name);
            string filePath = Path.Combine(_themesDirectory, $"{slug}.toml");

            var theme = ThemeDefinition.CaptureFromImGui();
            theme.Name = name;
            theme.Description = "Custom theme";

            ThemeSerializer.SaveToFile(theme, filePath);
            Console.WriteLine($"skittles: Saved theme '{name}' to {filePath}");

            RefreshThemeList();
            ActiveThemeName = name;
            Config.ActiveThemeName = name;
            ModConfigSerializer.SaveToFile(Config, _configFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error saving theme '{name}': {ex.Message}");
        }
    }

    public bool DeleteTheme(string name)
    {
        try
        {
            var entry = AvailableThemes.FirstOrDefault(t => t.Name == name && !t.IsBuiltIn);
            if (entry?.FilePath == null) return false;

            File.Delete(entry.FilePath);
            Console.WriteLine($"skittles: Deleted theme '{name}'");

            if (ActiveThemeName == name)
                ApplyTheme("Game Default");

            RefreshThemeList();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error deleting theme '{name}': {ex.Message}");
            return false;
        }
    }

    public void RefreshThemeList()
    {
        AvailableThemes.Clear();

        // Built-ins first
        foreach (var name in BuiltInThemeNames)
            AvailableThemes.Add(new ThemeEntry { Name = name, IsBuiltIn = true });

        // Custom themes from disk, sorted alphabetically by name
        var customThemes = new List<ThemeEntry>();
        if (Directory.Exists(_themesDirectory))
        {
            foreach (var file in Directory.GetFiles(_themesDirectory, "*.toml").OrderBy(f => f))
            {
                try
                {
                    string toml = File.ReadAllText(file);
                    string themeName = ExtractNameFromToml(toml, Path.GetFileNameWithoutExtension(file));
                    customThemes.Add(new ThemeEntry { Name = themeName, IsBuiltIn = false, FilePath = file });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"skittles: Error reading theme file {file}: {ex.Message}");
                }
            }
        }

        customThemes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        AvailableThemes.AddRange(customThemes);
    }

    public void RestoreDefaults()
    {
        try
        {
            DefaultTheme?.ApplyToImGui();
            Console.WriteLine("skittles: Restored game default style");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error restoring defaults: {ex.Message}");
        }
    }

    // Apply style vars from DefaultTheme without touching colors (used by built-in color schemes)
    private void ApplyDefaultStyleVars()
    {
        if (DefaultTheme == null) return;
        var style = ImGui.GetStyle();
        style.Alpha = DefaultTheme.Alpha;
        style.DisabledAlpha = DefaultTheme.DisabledAlpha;
        style.WindowRounding = DefaultTheme.WindowRounding;
        style.WindowBorderSize = DefaultTheme.WindowBorderSize;
        style.WindowBorderHoverPadding = DefaultTheme.WindowBorderHoverPadding;
        style.ChildRounding = DefaultTheme.ChildRounding;
        style.ChildBorderSize = DefaultTheme.ChildBorderSize;
        style.PopupRounding = DefaultTheme.PopupRounding;
        style.PopupBorderSize = DefaultTheme.PopupBorderSize;
        style.FrameRounding = DefaultTheme.FrameRounding;
        style.FrameBorderSize = DefaultTheme.FrameBorderSize;
        style.IndentSpacing = DefaultTheme.IndentSpacing;
        style.ColumnsMinSpacing = DefaultTheme.ColumnsMinSpacing;
        style.ScrollbarSize = DefaultTheme.ScrollbarSize;
        style.ScrollbarRounding = DefaultTheme.ScrollbarRounding;
        style.GrabMinSize = DefaultTheme.GrabMinSize;
        style.GrabRounding = DefaultTheme.GrabRounding;
        style.LogSliderDeadzone = DefaultTheme.LogSliderDeadzone;
        style.ImageBorderSize = DefaultTheme.ImageBorderSize;
        style.TabRounding = DefaultTheme.TabRounding;
        style.TabBorderSize = DefaultTheme.TabBorderSize;
        style.TabMinWidthBase = DefaultTheme.TabMinWidthBase;
        style.TabMinWidthShrink = DefaultTheme.TabMinWidthShrink;
        style.TabCloseButtonMinWidthSelected = DefaultTheme.TabCloseButtonMinWidthSelected;
        style.TabCloseButtonMinWidthUnselected = DefaultTheme.TabCloseButtonMinWidthUnselected;
        style.TabBarBorderSize = DefaultTheme.TabBarBorderSize;
        style.TabBarOverlineSize = DefaultTheme.TabBarOverlineSize;
        style.TableAngledHeadersAngle = DefaultTheme.TableAngledHeadersAngle;
        style.TreeLinesSize = DefaultTheme.TreeLinesSize;
        style.TreeLinesRounding = DefaultTheme.TreeLinesRounding;
        style.SeparatorTextBorderSize = DefaultTheme.SeparatorTextBorderSize;
        style.DockingSeparatorSize = DefaultTheme.DockingSeparatorSize;
        style.MouseCursorScale = DefaultTheme.MouseCursorScale;
        style.CurveTessellationTol = DefaultTheme.CurveTessellationTol;
        style.CircleTessellationMaxError = DefaultTheme.CircleTessellationMaxError;
    }

    private static string ExtractNameFromToml(string toml, string fallback)
    {
        bool inMeta = false;
        foreach (var line in toml.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed == "[meta]") { inMeta = true; continue; }
            if (trimmed.StartsWith('[')) { inMeta = false; continue; }
            if (inMeta && trimmed.StartsWith("name"))
            {
                int eq = trimmed.IndexOf('=');
                if (eq >= 0)
                {
                    string val = trimmed.Substring(eq + 1).Trim().Trim('"');
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
        }
        return fallback;
    }

    private static string Slugify(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }
}
