using System;
using System.IO;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.FreeFallinLib;

internal sealed class PngFileBrowser
{
    private readonly ImInputString _filter = new(128);
    private bool _open;
    private bool _restorePlacement;
    private MeowSci.Unscience.Contracts.WindowPlacement _placement = new() { Width = 620, Height = 480 };
    private string _directory = "";
    private string? _selected;
    private string? _error;
    private string _listedDirectory = "";
    private string[] _directories = Array.Empty<string>();
    private string[] _files = Array.Empty<string>();

    internal void BindDraft(MeowSci.KsaAbstractions.DraftBindings state)
    {
        state.Value("BrowserOpen", () => _open, v => _open = v);
        state.Value("BrowserDirectory", () => _directory, v => _directory = v);
        state.Value("BrowserSelected", () => _selected, v => _selected = v);
        state.Text("BrowserFilter", _filter);
        state.Value("BrowserPlacement", () => _placement, v => { _placement = v; _restorePlacement = true; });
    }
    private void PlaceWindow()
    {
        if (!_restorePlacement) return;
        _restorePlacement = false;
        var display = ImGui.GetIO().DisplaySize;
        var size = new float2(Math.Clamp(_placement.Width, 320, Math.Max(320, display.X)), Math.Clamp(_placement.Height, 240, Math.Max(240, display.Y)));
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowPos(new float2(Math.Clamp(_placement.X, 0, Math.Max(0, display.X - size.X)), Math.Clamp(_placement.Y, 0, Math.Max(0, display.Y - size.Y))), ImGuiCond.Always);
    }
    internal void Open()
    {
        _open = true;
        _selected = null;
        _error = null;
        if (string.IsNullOrEmpty(_directory) || !Directory.Exists(_directory))
            _directory = ParachuteTextureLibrary.DefaultBrowseDir();
        _listedDirectory = "";
    }

    internal void Render(Action<string> onPick)
    {
        if (!_open) return;
        Refresh();
        ImGui.SetNextWindowSize(new float2(620f, 480f), ImGuiCond.FirstUseEver);
        PlaceWindow();
        if (ImGui.Begin("Import Parachute PNG###free_fallin_browser", ref _open))
        {
            bool first = true;
            foreach ((string label, string path) in ParachuteTextureLibrary.QuickLinks())
            {
                if (!first) ImGui.SameLine(0f, 6f);
                first = false;
                if (ImGui.Button($" {label} ##ff_ql_{label}")) Navigate(path);
            }
            ImGui.Spacing();

            string? parent = Directory.GetParent(_directory)?.FullName;
            if (parent == null) ImGui.BeginDisabled();
            if (ImGui.Button(" Up ##ff_up") && parent != null) Navigate(parent);
            if (parent == null) ImGui.EndDisabled();
            ImGui.SameLine(0f, 8f); ImGui.TextDisabled(_directory);

            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##ff_browser_filter", "filter..."u8, _filter);
            string filter = _filter.ToString().Trim();
            float footer = ImGui.GetFrameHeightWithSpacing() * 2f;
            ImGui.BeginChild("##ff_browser_list", new float2(0f, -footer), ImGuiChildFlags.Borders);
            foreach (string dir in _directories)
            {
                if (!Matches(dir, filter)) continue;
                if (ImGui.Selectable($"[dir]  {dir}##ff_d_{dir}", false, ImGuiSelectableFlags.AllowDoubleClick)
                    && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { Navigate(Path.Combine(_directory, dir)); break; }
            }
            foreach (string file in _files)
            {
                if (!Matches(file, filter)) continue;
                bool selected = file == _selected;
                if (ImGui.Selectable($"{file}##ff_f_{file}", selected, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _selected = file;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { Pick(onPick); break; }
                }
            }
            ImGui.EndChild();

            if (_selected == null) ImGui.BeginDisabled();
            if (ImGui.Button(" Import ##ff_import")) Pick(onPick);
            if (_selected == null) ImGui.EndDisabled();
            ImGui.SameLine(0f, 8f);
            if (ImGui.Button(" Cancel ##ff_cancel")) _open = false;
            ImGui.SameLine(0f, 12f);
            if (_error != null) ImGui.TextColored(new float4(1f, .3f, .3f, 1f), _error);
            else ImGui.TextDisabled(_selected ?? "Select a .png file");
        }
        var position = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        _placement.X = position.X; _placement.Y = position.Y; _placement.Width = size.X; _placement.Height = size.Y;
        ImGui.End();
    }

    private void Pick(Action<string> onPick)
    {
        if (_selected == null) return;
        try { onPick(Path.Combine(_directory, _selected)); _open = false; }
        catch (Exception ex) { _error = ex.Message; }
    }

    private void Navigate(string path) { _directory = path; _selected = null; _error = null; _filter.Clear(); _listedDirectory = ""; }
    private static bool Matches(string value, string filter) => filter.Length == 0 || value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void Refresh()
    {
        if (_listedDirectory == _directory) return;
        _listedDirectory = _directory;
        try
        {
            _directories = Directory.EnumerateDirectories(_directory).Select(Path.GetFileName).OfType<string>()
                .Where(name => !name.StartsWith('.')).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            _files = Directory.EnumerateFiles(_directory, "*.png").Select(Path.GetFileName).OfType<string>()
                .Where(name => !name.StartsWith('.')).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            _error = null;
        }
        catch (Exception ex) { _directories = Array.Empty<string>(); _files = Array.Empty<string>(); _error = ex.Message; }
    }
}
