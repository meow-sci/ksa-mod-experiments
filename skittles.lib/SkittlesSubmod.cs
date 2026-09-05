using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SkittlesLib;

public sealed partial class SkittlesSubmod : IWorkspaceFeature
{
    public string Name => "Skittles - UI Themes";
    public string Tooltip => "Manages and applies ImGui theme configurations for UI customization.";

    private ThemeManager _themeManager = null!;
    private int _selectedThemeIndex;
    private readonly ImInputString _filterInput = new(256);
    private readonly ImInputString _themeNameInput = new(128);
    private bool _showSaveInput;
    private bool _editorVisible;

    public void Initialize()
    {
        _themeManager = new ThemeManager();
        _themeManager.Initialize();
        _selectedThemeIndex = FindThemeIndex(_themeManager.ActiveThemeName);
    }

    public void Update(double dt) { }

    private ThemeDefinition? _themeDraft;
    private string _templateName = "Game Default";
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##sk-content");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("Theme template", _templateName))
        {
            foreach (var entry in _themeManager.AvailableThemes)
                if (ImGui.Selectable(entry.Name, entry.Name == _templateName))
                { _templateName = entry.Name; _themeDraft = entry.FilePath == null ? null : ThemeSerializer.LoadFromFile(entry.FilePath); }
            ImGui.EndCombo();
        }
        if (ImGui.Button("Copy current style into editor", new float2(-1, 0))) _themeDraft = ThemeDefinition.CaptureFromImGui();
        if (_themeDraft != null) ThemeDraftEditor.Render(_themeDraft);
        if (ImGui.Button("Apply theme", new float2(-1, 0)))
        { if (_themeDraft != null) _themeManager.ApplyDefinition(_themeDraft); else _themeManager.ApplyTheme(_templateName); }
        SubmodUI.EndContentArea();
    }

    public void RenderFloatingWindows() { }

    private void RenderDeleteConfirmPopup()
    {
        string[] themeNames = _themeManager.GetThemeNames();
        bool canDelete = _selectedThemeIndex >= 0 && _selectedThemeIndex < themeNames.Length
            && !_themeManager.AvailableThemes[_selectedThemeIndex].IsBuiltIn;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 16f));
        bool open = true;
        bool began = ImGui.BeginPopupModal("##sk_confirm_delete", ref open, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.PopStyleVar();
        if (!began)
            return;

        if (canDelete)
        {
            string deleteName = themeNames[_selectedThemeIndex];
            ImGui.Text($"Delete theme '{deleteName}'?");
            ImGui.Spacing();
            if (ImGui.Button(" Yes, Delete ##sk"))
            {
                _themeManager.DeleteTheme(deleteName);
                _selectedThemeIndex = FindThemeIndex(_themeManager.ActiveThemeName);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Cancel ##sk"))
                ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void RenderEditorWindow()
    {
        ImGui.SetNextWindowSize(new float2(700, 800), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Skittles \u2014 Theme Editor###sk_editor", ref _editorVisible))
        {

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
                        Console.WriteLine($"unscience/skittles: Saved theme '{activeEntry.Name}'");
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
                    Console.WriteLine($"unscience/skittles: Saved theme '{nameStr}'");
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
    }

    public void Dispose()
    {
        ReleaseLiveState();
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
