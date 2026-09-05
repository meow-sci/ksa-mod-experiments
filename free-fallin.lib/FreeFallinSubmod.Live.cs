using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.FreeFallinLib;

public sealed partial class FreeFallinSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (CanopyMaterialController.AppliedSettings is { } settings)
            yield return new LiveStateItem<CanopyMaterialSettings>("canopy", "Parachute appearance", "All parachutes", settings, RenderCanopyInspector);
    }
    private CanopyMaterialSettings? _liveEdit;
    private CanopyMaterialSettings? _lastApplied;
    private void RenderCanopyInspector(CanopyMaterialSettings applied)
    {
        if (!ReferenceEquals(applied, _lastApplied)) { _lastApplied = applied; _liveEdit = DraftJson.Clone(applied); }
        var edit = _liveEdit!;
        ImGui.Text($"{applied.TextureMode}: {applied.TextureName ?? "Stock"}");
        float brightness = edit.Brightness, ao = edit.AmbientOcclusion, roughness = edit.Roughness, metal = edit.Metallic;
        float size = edit.DecalScale, rotation = edit.FullCanopyRotationDegrees; var tint = edit.Tint;
        bool stock = edit.UseStockPbrMap;
        ImGui.ColorEdit4("Tint", ref tint); edit.Tint = tint;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Brightness"), ref brightness, .01f, 0f, 4f); edit.Brightness = brightness;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Decal size"), ref size, .01f, .05f, 1f); edit.DecalScale = size;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Rotation"), ref rotation, 1f, -180f, 180f); edit.FullCanopyRotationDegrees = rotation;
        ImGui.Checkbox("Preserve stock PBR", ref stock); edit.UseStockPbrMap = stock;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AO"), ref ao, .01f, 0f, stock ? 4f : 1f); edit.AmbientOcclusion = ao;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Roughness"), ref roughness, .01f, 0f, stock ? 4f : 1f); edit.Roughness = roughness;
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Metallic"), ref metal, .01f, 0f, stock ? 4f : 1f); edit.Metallic = metal;
        if (ImGui.Button(" Apply live edits ")) CanopyMaterialController.Apply(edit);
        if (ImGui.Button(" Restore stock ")) FreeFallinPatches.RestoreStock();
        if (ImGui.Button(" Copy settings to form "))
        {
            _textureMode = (int)applied.TextureMode; _selectedTexture = Array.IndexOf(_textures, applied.TextureName); Draft.Select("PNG", applied.TextureName ?? "");
            _tint = applied.Tint; _brightness = applied.Brightness; _decalScale = applied.DecalScale;
            _fullCanopyRotation = applied.FullCanopyRotationDegrees; _useStockPbrMap = applied.UseStockPbrMap;
            _ambientOcclusion = applied.AmbientOcclusion; _roughness = applied.Roughness; _metallic = applied.Metallic;
        }
    }
}
