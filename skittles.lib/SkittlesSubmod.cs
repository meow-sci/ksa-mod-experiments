using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.SkittlesLib;

public sealed class SkittlesSubmod : ISubmod
{
    public string Name => "Skittles \u2014 Theme Manager";

    private ThemeManager _themeManager = null!;
    private int _selectedThemeIndex;
    private readonly ImInputString _filterInput = new(256);
    private readonly ImInputString _themeNameInput = new(128);
    private bool _showSaveInput;
    private bool _editorVisible;

    /// <summary>Read by Harmony patches to block game hotkeys when text inputs are focused.</summary>
    public bool HasFocusedTextInput { get; private set; }

    public void Initialize()
    {
        _themeManager = new ThemeManager();
        _themeManager.Initialize();
        _selectedThemeIndex = FindThemeIndex(_themeManager.ActiveThemeName);
    }

    public void Update(double dt) { }

    public void RenderContent()
    {
        bool anyFocusedText = false;

        // --- Main content (inside grant collapsible header) ---
        ImGui.TextColored(new float4(0.17f, 0.98f, 0.12f, 1.0f), "Skittles");
        ImGui.SameLine();
        ImGui.TextDisabled("Global Theme Manager");
        ImGui.Separator();

        string active = _themeManager.ActiveThemeName ?? "Game Default";
        ImGui.Text($"Active: {active}");
        ImGui.Separator();

        // Theme selector with filter
        string[] themeNames = _themeManager.GetThemeNames();
        string preview = (_selectedThemeIndex >= 0 && _selectedThemeIndex < themeNames.Length)
            ? themeNames[_selectedThemeIndex]
            : "Select Theme...";

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##sk_themedropdown", preview))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##sk_filter", _filterInput);
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

        if (ImGui.Button("Open Theme Editor##sk"))
            _editorVisible = true;

        ImGui.Spacing();
        ImGui.Separator();

        // Quick preset row
        ImGui.TextDisabled("Quick Apply:");
        if (ImGui.Button("Dark##sk"))    { _themeManager.ApplyTheme("Dark");                 UpdateSelectedIndex(); }
        ImGui.SameLine();
        if (ImGui.Button("Light##sk"))   { _themeManager.ApplyTheme("Light");                UpdateSelectedIndex(); }
        ImGui.SameLine();
        if (ImGui.Button("Classic##sk")) { _themeManager.ApplyTheme("Classic");              UpdateSelectedIndex(); }
        ImGui.SameLine();
        if (ImGui.Button("Rod##sk"))     { _themeManager.ApplyTheme("Inanimate Carbon Rod"); UpdateSelectedIndex(); }
        ImGui.SameLine();
        if (ImGui.Button("Reset##sk"))   { _themeManager.ApplyTheme("Game Default");         UpdateSelectedIndex(); }

        // --- Editor window (separate ImGui window) ---
        if (_editorVisible)
            anyFocusedText |= RenderEditorWindow();

        HasFocusedTextInput = anyFocusedText;
    }

    private bool RenderEditorWindow()
    {
        bool hasFocusedText = false;
        ImGui.SetNextWindowSize(new float2(700, 800), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Skittles \u2014 Theme Editor###sk_editor", ref _editorVisible))
        {
            hasFocusedText = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.GetIO().WantTextInput;

            ThemeEntry? activeEntry = _themeManager.AvailableThemes
                .FirstOrDefault(t => t.Name == _themeManager.ActiveThemeName && !t.IsBuiltIn);
            bool isCustom = activeEntry is not null;

            if (!_showSaveInput)
            {
                if (isCustom)
                {
                    if (ImGui.Button($"Save \"{activeEntry!.Name}\"##sk"))
                    {
                        _themeManager.SaveCurrentAsTheme(activeEntry.Name);
                        Console.WriteLine($"grant/skittles: Saved theme '{activeEntry.Name}'");
                        UpdateSelectedIndex();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Save as New...##sk"))
                    {
                        _showSaveInput = true;
                        _themeNameInput.Clear();
                    }
                }
                else
                {
                    if (ImGui.Button("Save as New Theme...##sk"))
                    {
                        _showSaveInput = true;
                        _themeNameInput.Clear();
                    }
                }
            }
            else
            {
                ImGui.Text("Theme Name:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(250);
                ImGui.InputText("##sk_themename", _themeNameInput);
                ImGui.SameLine();

                string nameStr = _themeNameInput.ToString().Trim();
                bool nameValid = !string.IsNullOrWhiteSpace(nameStr);

                if (!nameValid) ImGui.BeginDisabled();
                if (ImGui.Button("Save##sk_save"))
                {
                    _themeManager.SaveCurrentAsTheme(nameStr);
                    Console.WriteLine($"grant/skittles: Saved theme '{nameStr}'");
                    _showSaveInput = false;
                    _themeNameInput.Clear();
                    UpdateSelectedIndex();
                }
                if (!nameValid) ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGui.Button("Cancel##sk_cancel"))
                {
                    _showSaveInput = false;
                    _themeNameInput.Clear();
                }
            }

            ImGui.Separator();
            ImGui.ShowStyleEditor();
        }
        ImGui.End();
        return hasFocusedText;
    }

    public void Dispose()
    {
        _themeManager?.RestoreDefaults();
    }

    private void UpdateSelectedIndex()
    {
        string[] names = _themeManager.GetThemeNames();
        string? activeName = _themeManager.ActiveThemeName;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == activeName)
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
