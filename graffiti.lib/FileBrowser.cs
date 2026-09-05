using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;

namespace MeowSci.GraffitiLib;

/// <summary>
/// A minimal ImGui file browser window for picking a PNG anywhere on the filesystem. ImGui has no
/// native OS file dialog, so this is a small floating window: quick links, an Up button, a
/// filterable directory listing (double-click a folder to enter, double-click a PNG or press
/// Import to pick it).
/// </summary>
internal sealed class FileBrowser
{
    private bool _open;
    private bool _restorePlacement;
    private MeowSci.Unscience.Contracts.WindowPlacement _placement = new() { Width = 620, Height = 480 };
    private string _currentDir = "";
    private string? _selectedFile;
    private string? _error;
    private readonly ImInputString _filter = new(128);

    private string[] _dirs = Array.Empty<string>();
    private string[] _files = Array.Empty<string>();
    private string _listedDir = "";

    /// <summary>True while the browser window is showing.</summary>
    internal bool Visible => _open;

    /// <summary>Opens (or re-focuses) the browser at the last directory, or a sensible default.</summary>
    internal void BindDraft(MeowSci.KsaAbstractions.DraftBindings state)
    {
        state.Value("BrowserOpen", () => _open, v => _open = v);
        state.Value("BrowserDirectory", () => _currentDir, v => _currentDir = v);
        state.Value("BrowserSelected", () => _selectedFile, v => _selectedFile = v);
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
        _error = null;
        _selectedFile = null;
        if (string.IsNullOrEmpty(_currentDir) || !Directory.Exists(_currentDir))
            _currentDir = DecalLibrary.DefaultBrowseDir();
    }

    /// <summary>
    /// Renders the window when open. <paramref name="onPick"/> is invoked with the picked file's
    /// full path; the window closes itself on a successful pick or Cancel.
    /// </summary>
    internal void Render(Action<string> onPick)
    {
        if (!_open)
            return;

        RefreshListing();

        ImGui.SetNextWindowSize(new float2(620, 480), ImGuiCond.FirstUseEver);
        PlaceWindow();
        if (ImGui.Begin("Import Decal PNG###graffiti_browser", ref _open))
        {
            RenderQuickLinks();
            RenderPathRow();

            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##graffiti_browser_filter", "filter..."u8, _filter);
            string filterText = _filter.ToString().Trim();

            var footerH = ImGui.GetFrameHeightWithSpacing() * 2f;
            ImGui.BeginChild("##graffiti_browser_list", new float2(0, -footerH), ImGuiChildFlags.Borders);
            RenderEntries(filterText, onPick);
            ImGui.EndChild();

            RenderFooter(onPick);
        }
        var position = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
        _placement.X = position.X; _placement.Y = position.Y; _placement.Width = size.X; _placement.Height = size.Y;
        ImGui.End();
    }

    private void RenderQuickLinks()
    {
        var first = true;
        foreach (var (label, path) in DecalLibrary.QuickLinks())
        {
            if (!first) ImGui.SameLine(0, 6);
            first = false;
            if (ImGui.Button($" {label} ##graffiti_ql_{label}"))
                Navigate(path);
        }
        ImGui.Spacing();
    }

    private void RenderPathRow()
    {
        var parent = Directory.GetParent(_currentDir)?.FullName;
        if (parent == null) ImGui.BeginDisabled();
        if (ImGui.Button(" Up ##graffiti_browser_up") && parent != null)
            Navigate(parent);
        if (parent == null) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(_currentDir);
    }

    private void RenderEntries(string filterText, Action<string> onPick)
    {
        foreach (var dir in _dirs)
        {
            if (!Matches(dir, filterText)) continue;
            if (ImGui.Selectable($"[dir]  {dir}##graffiti_d_{dir}", false,
                    ImGuiSelectableFlags.AllowDoubleClick)
                && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                Navigate(Path.Combine(_currentDir, dir));
                return; // the listing just changed; stop iterating the stale arrays
            }
        }

        foreach (var file in _files)
        {
            if (!Matches(file, filterText)) continue;
            bool selected = _selectedFile == file;
            if (ImGui.Selectable($"{file}##graffiti_f_{file}", selected,
                    ImGuiSelectableFlags.AllowDoubleClick))
            {
                _selectedFile = file;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    Pick(onPick);
                    return;
                }
            }
        }

        if (_dirs.Length == 0 && _files.Length == 0)
            ImGui.TextDisabled("No folders or PNG files here.");
    }

    private void RenderFooter(Action<string> onPick)
    {
        bool canImport = _selectedFile != null;
        if (!canImport) ImGui.BeginDisabled();
        if (ImGui.Button(" Import ##graffiti_browser_import"))
            Pick(onPick);
        if (!canImport) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##graffiti_browser_cancel"))
            _open = false;
        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        if (!string.IsNullOrEmpty(_error))
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _error);
        else if (_selectedFile != null)
            ImGui.TextDisabled(_selectedFile);
        else
            ImGui.TextDisabled("Select a .png file");
    }

    private void Pick(Action<string> onPick)
    {
        if (_selectedFile == null)
            return;
        onPick(Path.Combine(_currentDir, _selectedFile));
        _open = false;
    }

    private void Navigate(string dir)
    {
        _currentDir = dir;
        _selectedFile = null;
        _error = null;
        _filter.Clear();
    }

    /// <summary>Re-lists the current directory when it changed (navigation, first open).</summary>
    private void RefreshListing()
    {
        if (_listedDir == _currentDir)
            return;
        _listedDir = _currentDir;
        _dirs = Array.Empty<string>();
        _files = Array.Empty<string>();
        try
        {
            _dirs = Directory.EnumerateDirectories(_currentDir)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(n => !n.StartsWith('.'))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _files = Directory.EnumerateFiles(_currentDir, "*.png")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(n => !n.StartsWith('.'))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _error = null;
        }
        catch (Exception ex)
        {
            _error = ex is UnauthorizedAccessException ? "Access denied." : ex.Message;
        }
    }

    private static bool Matches(string name, string filterText)
        => filterText.Length == 0 || name.Contains(filterText, StringComparison.OrdinalIgnoreCase);
}
