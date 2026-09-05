using System;
using System.IO;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.PebblesLib;

/// <summary>Feature-owned file browser. Picking imports into the asset cache; it never applies clutter.</summary>
internal sealed class GlbFileBrowser
{
    private bool _open, _restorePlacement, _restoreScroll;
    private string _directory = "", _selected = "", _listedDirectory = "", _error = "";
    private readonly ImInputString _path = new(4096), _filter = new(256);
    private string[] _directories = [], _files = [];
    private float _scroll;
    private WindowPlacement _placement = new() { Width = 700, Height = 540 };
    public void Bind(DraftBindings draft)
    {
        draft.Value("glbBrowserOpen", () => _open, v => _open = v);
        draft.Value("glbBrowserDirectory", () => _directory, v => { _directory = v; _listedDirectory = ""; });
        draft.Value("glbBrowserSelectedFile", () => _selected, v => _selected = v);
        draft.Text("glbBrowserPath", _path); draft.Text("glbBrowserFilter", _filter);
        draft.Value("glbBrowserScroll", () => _scroll, v => { _scroll = v; _restoreScroll = true; }, validate: v => { if (!float.IsFinite(v) || v < 0) throw new InvalidOperationException("Invalid GLB browser scroll."); });
        draft.Value("glbBrowserPlacement", () => _placement, v => { _placement = v; _restorePlacement = true; }, validate: v =>
        {
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Width) || !float.IsFinite(v.Height) || v.Width < 1 || v.Height < 1)
                throw new InvalidOperationException("Invalid GLB browser placement.");
        });
    }
    public void Open()
    {
        _open = true;
        if (_directory.Length == 0) Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        _listedDirectory = "";
    }
    public void Draw(Action<string> import)
    {
        if (!_open) return;
        ListDirectory();
        ImGui.SetNextWindowSize(new float2(700, 540), ImGuiCond.FirstUseEver);
        if (_restorePlacement)
        {
            ImGui.SetNextWindowSize(new float2(Math.Max(360, _placement.Width), Math.Max(320, _placement.Height)), ImGuiCond.Always);
            ImGui.SetNextWindowPos(new float2(_placement.X, _placement.Y), ImGuiCond.Always); _restorePlacement = false;
        }
        bool shown = ImGui.Begin("Import GLB###pebbles-glb-browser", ref _open);
        try
        {
            var pos = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize();
            _placement.X = pos.X; _placement.Y = pos.Y; _placement.Width = size.X; _placement.Height = size.Y;
            if (!shown) return;
            if (ImGui.Button(" Home ")) Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Downloads ")) Navigate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            ImGui.SameLine(0, 8);
            if (ImGui.Button(" Up ")) Attempt(() => Navigate(Directory.GetParent(_directory)?.FullName ?? _directory));
            ImGui.SameLine(0, 8); if (ImGui.Button(" Refresh ")) _listedDirectory = "";
            ImGui.InputText(FormField.Label("Folder or .glb path"), _path);
            if (ImGui.Button(" Go ")) Attempt(() =>
            {
                string path = Path.GetFullPath(_path.ToString());
                if (Path.GetExtension(path).Equals(".glb", StringComparison.OrdinalIgnoreCase)) { Navigate(Path.GetDirectoryName(path)!); _selected = path; }
                else Navigate(path);
            });
            ImGui.InputText(FormField.Label("Filter files"), _filter);
            ImGui.BeginChild("##files", new float2(0, Math.Max(100, ImGui.GetContentRegionAvail().Y - 95)), ImGuiChildFlags.Borders);
            try
            {
                if (_restoreScroll) { ImGui.SetScrollY(_scroll); _restoreScroll = false; }
                string filter = _filter.ToString();
                foreach (var path in _directories)
                    if (Path.GetFileName(path).Contains(filter, StringComparison.OrdinalIgnoreCase) && ImGui.Selectable("[folder] " + Path.GetFileName(path) + "##" + path, false, ImGuiSelectableFlags.AllowDoubleClick) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    { Navigate(path); break; }
                foreach (var path in _files)
                    if (Path.GetFileName(path).Contains(filter, StringComparison.OrdinalIgnoreCase) && ImGui.Selectable(Path.GetFileName(path) + "##" + path, path == _selected, ImGuiSelectableFlags.AllowDoubleClick))
                    { _selected = path; if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) Pick(import); }
                _scroll = ImGui.GetScrollY();
            }
            finally { ImGui.EndChild(); }
            ImGui.BeginDisabled(_selected.Length == 0);
            try { if (ImGui.Button(" Import selected GLB ")) Pick(import); }
            finally { ImGui.EndDisabled(); }
            ImGui.SameLine(0, 8); if (ImGui.Button(" Cancel ")) _open = false;
            if (_error.Length > 0) ImGui.TextWrapped(_error);
            else ImGui.TextWrapped(_selected.Length == 0 ? "Choose a GLB 2.0 file. Import does not change the planet." : _selected);
        }
        finally { ImGui.End(); }
    }
    private void Pick(Action<string> import) => Attempt(() => { import(_selected); _open = false; });
    private void Navigate(string path)
    {
        _directories = []; _files = [];
        _directory = path; _path.Value16 = path; _selected = ""; _listedDirectory = ""; _error = ""; _scroll = 0; _restoreScroll = true;
    }
    private void ListDirectory()
    {
        if (_listedDirectory == _directory) return;
        _listedDirectory = _directory; _directories = []; _files = [];
        Attempt(() =>
        {
            _directories = Directory.EnumerateDirectories(_directory).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            _files = Directory.EnumerateFiles(_directory).Where(p => Path.GetExtension(p).Equals(".glb", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        });
    }
    private void Attempt(Action action)
    {
        try { _error = ""; action(); }
        catch (Exception ex) { _error = ex.Message; Console.WriteLine($"pebbles GLB: {ex}"); }
    }
}
