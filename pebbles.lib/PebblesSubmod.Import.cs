using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
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
        _glbOptions = _assets.ImportGlb(path);
        _releaseImports = false;
        _glbPath.Value16 = System.IO.Path.GetFullPath(path);
        _glbSelected = _glbOptions[0].Id;
        _bulkMesh = _glbSelected;
        _glbStatus = $"Loaded {_glbOptions.Count - 1} meshes. Select a mesh or the complete scene, then assign it to a draft below.";
    }
    private void ImportControls()
    {
        if (!WorkspaceUi.Header("Load GLB from disk", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.InputText(FormField.Label("GLB file"), _glbPath);
        if (ImGui.Button(" Browse .glb ")) _glbBrowser.Open();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Load file ")) ImportAttempt(() => ImportGlb(_glbPath.ToString()));
        if (_glbSelected.Length > 0)
        {
            if (ImGui.BeginCombo(FormField.Label("Imported mesh"), _glbOptions.FirstOrDefault(o => o.Id == _glbSelected)?.Label ?? GlbIdentity.Label(_glbSelected)))
            {
                try { foreach (var option in _glbOptions) if (ImGui.Selectable(option.Label + "##" + option.Id, option.Id == _glbSelected)) _glbSelected = option.Id; }
                finally { ImGui.EndCombo(); }
            }
            ImGui.BeginDisabled(SelectedObject == null);
            try
            {
                if (ImGui.Button(" Use in selected variant and open Workshop ", new float2(-1, 0))) ImportAttempt(() =>
                {
                    var item = SelectedObject!;
                    AssignGlb([item]);
                    _workshopBody = _bodyId; _workshopEcotype = _ecotypeName; _workshopObject = _objectId;
                    _workshop.Open(item, CompleteWorkshop);
                });
            }
            finally { ImGui.EndDisabled(); }
            ImGui.BeginDisabled(_recipe.Ecotypes.Count == 0);
            try { if (ImGui.Button(" Use in all variants on this body ", new float2(-1, 0))) ImportAttempt(() => AssignGlb(_recipe.Ecotypes.SelectMany(e => e.Objects).ToArray())); }
            finally { ImGui.EndDisabled(); }
        }
        if (_glbStatus.Length > 0) ImGui.TextWrapped(_glbStatus);
        ImGui.TextWrapped("Import is for static geometry and supported embedded materials. Complete scene bakes node transforms; individual meshes use their own local coordinates. Apply remains separate.");
    }
    private void AssignGlb(IEnumerable<ObjectRecipe> targets)
    {
        // Resolve and validate before changing any destination. This prepares CPU data only.
        var materials = _assets.GlbMaterials(_glbSelected);
        _releaseImports = false;
        foreach (var item in targets)
        {
            foreach (var lod in item.Lods) { lod.MeshIds = [_glbSelected]; lod.Materials = RecipeCopy.Clone(materials); }
            item.Collision = CollisionPolicy.None;
        }
        _bulkMesh = _glbSelected;
        _glbStatus = "GLB geometry and materials assigned to the draft. Configure collision in the Workshop, then Apply.";
    }
    private void ImportAttempt(Action action)
    {
        try { action(); }
        catch (Exception ex) { _glbStatus = ex.Message; Console.WriteLine($"pebbles GLB: {ex}"); }
    }
}
