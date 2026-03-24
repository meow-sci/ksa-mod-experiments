using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.SkittlesLib;

namespace MeowSci.Skittles;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    internal static bool SkittlesHasFocusedTextInput;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;
    private bool _editorVisible = false;
    private ThemeManager _themeManager = null!;
    private int _selectedThemeIndex = 0;
    private readonly ImInputString _filterInput = new ImInputString(256);
    private readonly ImInputString _themeNameInput = new ImInputString(128);
    private bool _showSaveInput = false;


    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _themeManager = new ThemeManager();
            _themeManager.Initialize();
            _selectedThemeIndex = FindThemeIndex(_themeManager.ActiveThemeName);
            _isInitialized = true;
            Console.WriteLine("skittles: Initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
                _windowVisible = !_windowVisible;

            bool anySkittlesTextInput = false;

            if (_windowVisible)
                anySkittlesTextInput |= RenderMainWindow();

            if (_editorVisible)
                anySkittlesTextInput |= RenderEditorWindow();

            SkittlesHasFocusedTextInput = anySkittlesTextInput;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _themeManager?.RestoreDefaults();
            Patcher.Unload();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"skittles: Error during unload: {ex.Message}");
        }
    }

    private bool RenderMainWindow()
    {
        bool hasFocusedText = false;
        ImGui.SetNextWindowSize(new float2(420, 360), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Skittles — Theme Manager", ref _windowVisible))
        {
            hasFocusedText = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().WantTextInput;
            // Header
            ImGui.TextColored(new float4(0.17f, 0.98f, 0.12f, 1.0f), "Skittles");
            ImGui.SameLine();
            ImGui.TextDisabled("Global Theme Manager");
            ImGui.Separator();

            // Active theme
            string active = _themeManager.ActiveThemeName ?? "Game Default";
            ImGui.Text($"Active: {active}");
            ImGui.Separator();

            // Theme selector with filter
            string[] themeNames = _themeManager.GetThemeNames();
            string preview = (_selectedThemeIndex >= 0 && _selectedThemeIndex < themeNames.Length)
                ? themeNames[_selectedThemeIndex]
                : "Select Theme...";

            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##themedropdown", preview))
            {
                // Filter input
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##filter", _filterInput);
                ImGui.Separator();

                string filterText = _filterInput.ToString();
                for (int i = 0; i < themeNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(filterText) &&
                        !themeNames[i].Contains(filterText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool selected = _selectedThemeIndex == i;
                    if (ImGui.Selectable(themeNames[i], selected))
                    {
                        _selectedThemeIndex = i;
                        _themeManager.ApplyTheme(themeNames[i]);
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            if (ImGui.Button("Open Theme Editor"))
                _editorVisible = true;

            ImGui.Spacing();
            ImGui.Separator();

            // Quick preset row
            ImGui.TextDisabled("Quick Apply:");
            if (ImGui.Button("Dark"))    { _themeManager.ApplyTheme("Dark");                 UpdateSelectedIndex(); }
            ImGui.SameLine();
            if (ImGui.Button("Light"))   { _themeManager.ApplyTheme("Light");                UpdateSelectedIndex(); }
            ImGui.SameLine();
            if (ImGui.Button("Classic")) { _themeManager.ApplyTheme("Classic");              UpdateSelectedIndex(); }
            ImGui.SameLine();
            if (ImGui.Button("Rod"))     { _themeManager.ApplyTheme("Inanimate Carbon Rod"); UpdateSelectedIndex(); }
            ImGui.SameLine();
            if (ImGui.Button("Reset"))   { _themeManager.ApplyTheme("Game Default");         UpdateSelectedIndex(); }
        }
        ImGui.End();
        return hasFocusedText;
    }

    private bool RenderEditorWindow()
    {
        bool hasFocusedText = false;
        ImGui.SetNextWindowSize(new float2(700, 800), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Skittles — Theme Editor", ref _editorVisible))
        {
            hasFocusedText = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().WantTextInput;
            // Determine if a custom (non-built-in) theme is currently active
            ThemeEntry? activeEntry = _themeManager.AvailableThemes
                .FirstOrDefault(t => t.Name == _themeManager.ActiveThemeName && !t.IsBuiltIn);
            bool isCustom = activeEntry is not null;

            if (!_showSaveInput)
            {
                if (isCustom)
                {
                    // Quick-save overwrites the existing file
                    if (ImGui.Button($"Save \"{activeEntry!.Name}\""))
                    {
                        _themeManager.SaveCurrentAsTheme(activeEntry.Name);
                        Console.WriteLine($"skittles: Saved theme '{activeEntry.Name}'");
                        UpdateSelectedIndex();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Save as New..."))
                    {
                        _showSaveInput = true;
                        _themeNameInput.Clear();
                    }
                }
                else
                {
                    // Built-in or game default — only save as new makes sense
                    if (ImGui.Button("Save as New Theme..."))
                    {
                        _showSaveInput = true;
                        _themeNameInput.Clear();
                    }
                }
            }
            else
            {
                // Save-as-new input row
                ImGui.Text("Theme Name:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(250);
                ImGui.InputText("##themename", _themeNameInput);
                ImGui.SameLine();

                string nameStr = _themeNameInput.ToString().Trim();
                bool nameValid = !string.IsNullOrWhiteSpace(nameStr);

                if (!nameValid) ImGui.BeginDisabled();
                if (ImGui.Button("Save"))
                {
                    _themeManager.SaveCurrentAsTheme(nameStr);
                    Console.WriteLine($"skittles: Saved theme '{nameStr}'");
                    _showSaveInput = false;
                    _themeNameInput.Clear();
                    UpdateSelectedIndex();
                }
                if (!nameValid) ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    _showSaveInput = false;
                    _themeNameInput.Clear();
                }
            }

            ImGui.Separator();

            // Built-in ImGui style editor — modifies global style in real time
            ImGui.ShowStyleEditor();
        }
        ImGui.End();
        return hasFocusedText;
    }

    private void UpdateSelectedIndex()
    {
        string[] names = _themeManager.GetThemeNames();
        string? active = _themeManager.ActiveThemeName;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == active)
            {
                _selectedThemeIndex = i;
                return;
            }
        }
        _selectedThemeIndex = 0;
    }

    private int FindThemeIndex(string? themeName)
    {
        if (themeName == null) return 0;
        string[] names = _themeManager.GetThemeNames();
        for (int i = 0; i < names.Length; i++)
            if (names[i] == themeName) return i;
        return 0;
    }
}

