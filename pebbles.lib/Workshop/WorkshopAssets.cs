using System;
using System.Linq;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PebblesLib;

public sealed partial class WorkshopEditor
{
    private readonly ImInputString _assetFilter = new(128);

    private void AssetEditor()
    {
        if (_assets == null || !Header("Preview LOD meshes and textures")) return;
        var lod = _state.Object.Lods[_state.PreviewLod];
        ImGui.TextWrapped($"Editing LOD {_state.PreviewLod}; mesh entries render together.");
        for (int i = 0; i < lod.MeshIds.Count; i++)
        {
            int slot = i;
            AssetCombo($"Mesh {i + 1}", lod.MeshIds[i], _assets.MeshIds,
                value => Edit(() => { lod.MeshIds[slot] = value; if (_state.Object.Collision == CollisionPolicy.KeepOriginal) _state.Object.Collision = CollisionPolicy.None; }, true));
            if (lod.MeshIds.Count > 1 && ImGui.SmallButton($"Remove mesh {i + 1}"))
            { Edit(() => lod.MeshIds.RemoveAt(slot), true); break; }
        }
        AssetCombo("Add mesh", "", _assets.MeshIds, value => Edit(() => lod.MeshIds.Add(value), true));
        if (ImGui.Button(" Copy this mesh group to all LODs "u8, new float2(-1, 0)))
            Edit(() => { foreach (var destination in _state.Object.Lods) destination.MeshIds = lod.MeshIds.ToList(); }, true);
        if (lod.Materials.Count == 0)
        {
            if (ImGui.Button(" Add material override "u8)) Edit(() => lod.Materials.Add(new MaterialRecipe()), true);
        }
        for (int i = 0; i < lod.Materials.Count; i++)
        {
            string section = $"lod{_state.PreviewLod}/material{i}";
            ImGui.SetNextItemOpen(_state.Sections.GetValueOrDefault(section), ImGuiCond.Always);
            bool open = ImGui.TreeNode($"Material {i + 1}##workshop-material-{i}");
            _state.Sections[section] = open;
            if (!open) continue;
            try
            {
                var material = lod.Materials[i];
                AssetCombo("Diffuse", material.DiffuseId, _assets.TextureIds, value => Edit(() => material.DiffuseId = value, true), true);
                AssetCombo("Normal", material.NormalId, _assets.TextureIds, value => Edit(() => material.NormalId = value, true), true);
                AssetCombo("AO / rough / metal", material.PbrId, _assets.TextureIds, value => Edit(() => material.PbrId = value, true), true);
                AssetCombo("Opacity", material.OpacityId, _assets.TextureIds, value => Edit(() => material.OpacityId = value, true), true);
                AssetCombo("Thickness", material.ThicknessId, _assets.TextureIds, value => Edit(() => material.ThicknessId = value, true), true);
                bool doubleSided = material.DoubleSided;
                if (ImGui.Checkbox("Double sided"u8, ref doubleSided)) Edit(() => material.DoubleSided = doubleSided, true);
                if (ImGui.Button(" Use this material for every primitive "u8))
                { Edit(() => lod.Materials = new() { RecipeCopy.Clone(material) }, true); break; }
                if (ImGui.Button(" Copy material list to all LODs "u8))
                    Edit(() => { foreach (var destination in _state.Object.Lods) destination.Materials = RecipeCopy.Clone(lod.Materials); }, true);
            }
            finally { ImGui.TreePop(); }
        }
    }

    private void AssetCombo(string label, string current, string[] options, Action<string> assign, bool allowDefault = false)
    {
        ImGui.Text(label); ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo($"##workshop-{label}", current.Length == 0 ? "Choose / game default" : current)) return;
        try
        {
            if (ImGui.IsWindowAppearing()) _assetFilter.Value16 = _state.AssetFilter;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##asset-filter"u8, "Filter assets"u8, _assetFilter)) _state.AssetFilter = _assetFilter.ToString();
            if (allowDefault && ImGui.Selectable("Game default"u8, current.Length == 0)) assign("");
            foreach (string option in options)
            {
                if (!option.Contains(_state.AssetFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable(option, option == current)) assign(option);
            }
        }
        finally { ImGui.EndCombo(); }
    }
}
