using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod
{
    private readonly GlbFileBrowser _glbBrowser = new();
    private readonly ImInputString _glbPath = new(4096);
    private string _glbSelected = "", _glbStatus = "";
    private IReadOnlyList<GlbMeshOption> _glbOptions = [];
    private bool _releaseImports;
    private void ImportGlb(string path)
    {
        if (_workshop.IsOpen) throw new InvalidOperationException("Finish or cancel collider setup before selecting another mesh.");
        var options = _assets.ImportGlb(path);
        SelectMesh(options[0].Id);
        _glbOptions = options;
        _glbPath.Value16 = System.IO.Path.GetFullPath(path);
        _glbSelected = options[0].Id;
    }
    private void ImportControls()
    {
        if (ImGui.Button(" Import GLB… ")) _glbBrowser.Open();
        if (WorkspaceUi.Header("GLB file path"))
        {
            ImGui.InputText(FormField.Label("GLB file"), _glbPath);
            if (ImGui.Button(" Load file ")) ImportAttempt(() => ImportGlb(_glbPath.ToString()));
        }
        if (_glbOptions.Count > 0 && ImGui.BeginCombo(FormField.Label("Imported scene / mesh"),
            _glbOptions.FirstOrDefault(o => o.Id == _glbSelected)?.Label ?? GlbIdentity.Label(_glbSelected)))
        {
            try
            {
                foreach (var option in _glbOptions)
                    if (ImGui.Selectable(option.Label + "##" + option.Id, option.Id == _glbSelected))
                        ImportAttempt(() => { SelectMesh(option.Id); _glbSelected = option.Id; });
            }
            finally { ImGui.EndCombo(); }
        }
        if (_glbStatus.Length > 0) ImGui.TextWrapped(_glbStatus);
    }
    private void ImportAttempt(Action action)
    {
        try { action(); }
        catch (Exception ex) { _glbStatus = ex.Message; Console.WriteLine($"pebbles GLB: {ex}"); }
    }
}
