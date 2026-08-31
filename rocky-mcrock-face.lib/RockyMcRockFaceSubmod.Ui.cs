using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RockyMcRockFaceLib;

public sealed partial class RockyMcRockFaceSubmod
{
    private static readonly float4 ErrorColor = new(1f, 0.3f, 0.3f, 1f);
    private static readonly float4 SuccessColor = new(0.4f, 1f, 0.4f, 1f);
    private static readonly float4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    private readonly ImInputString _assetFilter = new(128);
    private int _selectedBodyIndex;
    private string _statusMessage = "";
    private bool _statusIsError;
    private string _allLodsMeshId = "";

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##rockymcrockface_content");

        if (_controller.Bodies.Count == 0)
        {
            ImGui.TextDisabled("No celestial with planetary rings in the current system.");
            ImGui.TextDisabled("Load a save near a ringed body (e.g. Saturn) and this panel will populate.");
            SubmodUI.EndContentArea();
            return;
        }

        RenderStatusHints();
        _selectedBodyIndex = Math.Clamp(_selectedBodyIndex, 0, _controller.Bodies.Count - 1);
        var body = _controller.Bodies[_selectedBodyIndex];
        var selection = GetOrCreateSelection(body.Id);

        if (_controller.Bodies.Count > 1)
            RenderBodySelector();
        else
            ImGui.TextDisabled($"Ringed body: {body.Id}");

        ImGui.Spacing();
        RenderMeshSection(body, selection);
        ImGui.Spacing();
        RenderTextureSection(selection);
        ImGui.Spacing();
        RenderFieldSection(selection);
        ImGui.Spacing();
        RenderActions(body, selection);

        SubmodUI.EndContentArea();
    }

    private void RenderStatusHints()
    {
        if (!GameSettings.ShowRings())
            ImGui.TextColored(ErrorColor, "Planetary rings are disabled in graphics settings - nothing will render.");
        else if (!GameSettings.ShowRingMeshes())
            ImGui.TextColored(WarningColor, "Ring meshes are disabled in graphics settings - only the flat band will show.");
        ImGui.TextDisabled($"{_controller.Catalog.MeshIds.Length} meshes and {_controller.Catalog.TextureIds.Length} textures available");
        ImGui.Spacing();
    }

    private void RenderBodySelector()
    {
        if (!RockyUi.BeginFormTable("##rockymcrockface_body")) return;
        RockyUi.FormLabel("Body");
        if (ImGui.BeginCombo("##rockymcrockface_body_combo", _controller.Bodies[_selectedBodyIndex].Id))
        {
            for (int i = 0; i < _controller.Bodies.Count; i++)
            {
                bool selected = i == _selectedBodyIndex;
                if (ImGui.Selectable(_controller.Bodies[i].Id, selected)) _selectedBodyIndex = i;
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        RockyUi.EndFormTable();
    }

    private void RenderMeshSection(RingedBody body, RingSelection selection)
    {
        bool open = ImGui.CollapsingHeader("Ring Object Meshes (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("The instanced rock meshes, one per LOD (LOD 0 = closest / most detailed).\n" +
                             "Any built-in mesh works, including part subpart meshes - they are converted\n" +
                             "on first use. Off-center meshes will orbit around their own origin.");
        if (!open) return;

        if (!RockyUi.BeginFormTable("##rockymcrockface_meshes")) return;

        RockyUi.FormLabel("All LODs");
        if (RockyUi.IdCombo("##rockymcrockface_all_lods", _controller.Catalog.MeshIds, ref _allLodsMeshId, _assetFilter))
        {
            for (int i = 0; i < body.LodCount; i++)
                selection.LodMeshIds[i] = _allLodsMeshId;
        }

        for (int i = 0; i < body.LodCount; i++)
        {
            float minPixels = body.Rings.RingObjects.Lods[i].MinScreenSizePixels;
            RockyUi.FormLabel($"LOD {i} (>={minPixels:F0}px)");
            RockyUi.IdCombo($"##rockymcrockface_lod{i}", _controller.Catalog.MeshIds, ref selection.LodMeshIds[i], _assetFilter);
        }
        RockyUi.EndFormTable();
    }

    private void RenderTextureSection(RingSelection selection)
    {
        bool open = ImGui.CollapsingHeader("Ring Textures (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Diffuse / Normal / AoRoughMetal texture the rock material samples,\n" +
                             "plus the 2D band texture (also drives the ring's shadow on the planet).");
        if (!open) return;

        if (!RockyUi.BeginFormTable("##rockymcrockface_textures")) return;
        RockyUi.FormLabel("Rock diffuse");
        RockyUi.IdCombo("##rockymcrockface_diffuse", _controller.Catalog.TextureIds, ref selection.DiffuseId, _assetFilter);
        RockyUi.FormLabel("Rock normal");
        RockyUi.IdCombo("##rockymcrockface_normal", _controller.Catalog.NormalTextureIds, ref selection.NormalId, _assetFilter);
        RockyUi.FormLabel("Rock AoRoughMetal");
        RockyUi.IdCombo("##rockymcrockface_pbr", _controller.Catalog.TextureIds, ref selection.PbrId, _assetFilter);
        RockyUi.FormLabel("Ring band");
        RockyUi.IdCombo("##rockymcrockface_band", _controller.Catalog.TextureIds, ref selection.BandTextureId, _assetFilter);
        RockyUi.EndFormTable();
    }

    private void RenderFieldSection(RingSelection selection)
    {
        bool open = ImGui.CollapsingHeader("Rock Field Settings (?)");
        ImGui.SetItemTooltip("Size, density and draw distance of the instanced rock field.\n" +
                             "High density x large render distance costs VRAM and GPU time.");
        if (!open) return;

        ImGui.Checkbox("Override field settings##rockymcrockface_field_override", ref selection.OverrideFieldSettings);
        if (!selection.OverrideFieldSettings) ImGui.BeginDisabled();
        if (RockyUi.BeginParamGrid("##rockymcrockface_field_grid"))
        {
            ImGui.TableNextRow();
            RockyUi.GridDrag("Rock size (m)", "##rockymcrockface_size", ref selection.SizeM, 0.1f, 0.1f, 1000f);
            RockyUi.GridDrag("Density (/km^3)", "##rockymcrockface_density", ref selection.DensityPerKm3, 10f, 1f, 100000f, "%.0f");
            ImGui.TableNextRow();
            RockyUi.GridDrag("Draw dist (km)", "##rockymcrockface_dist", ref selection.RenderDistanceKm, 0.5f, 1f, 500f, "%.1f");
            RockyUi.GridDrag("Thickness (km)", "##rockymcrockface_thick", ref selection.ThicknessKm, 0.05f, 0.01f, 100f);
            RockyUi.EndParamGrid();
        }
        if (!selection.OverrideFieldSettings) ImGui.EndDisabled();
    }

    private void RenderActions(RingedBody body, RingSelection selection)
    {
        if (ImGui.Button(" Apply ##rockymcrockface_apply"))
            ApplySelection(body, selection);
        ImGui.SameLine(0, 8);
        RockyUi.DangerButtonBegin();
        if (ImGui.Button(" Restore Defaults ##rockymcrockface_restore"))
            RestoreDefaults(body, selection);
        RockyUi.DangerButtonEnd();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Rescan Assets ##rockymcrockface_rescan"))
        {
            _controller.Catalog.Refresh();
            _controller.RefreshBodies();
            SetStatus("asset catalog rescanned", false);
        }

        ImGui.TextDisabled("Apply and Restore rebuild the renderer - expect a brief hitch.");

        if (_statusMessage.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(_statusIsError ? ErrorColor : SuccessColor, _statusMessage);
        }
    }

    private void ApplySelection(RingedBody body, RingSelection selection)
    {
        if (!_controller.Apply(body, selection, out var message))
        {
            SetStatus(message, true);
            return;
        }
        SaveSelections();
        bool rebuilt = _controller.RebuildRenderer(out var rebuildMessage);
        SetStatus($"{message}; {rebuildMessage}", !rebuilt);
    }

    private void RestoreDefaults(RingedBody body, RingSelection selection)
    {
        selection.Clear();
        _controller.Restore(body);
        SaveSelections();
        bool rebuilt = _controller.RebuildRenderer(out var rebuildMessage);
        SetStatus(rebuilt ? $"restored game defaults for {body.Id}" : rebuildMessage, !rebuilt);
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }
}
