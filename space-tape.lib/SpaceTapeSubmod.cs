using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

public sealed class SpaceTapeSubmod : ISubmod
{
    public string Name => "Space Tape";
    public string Tooltip => "In-game Part editor. Compose new Parts from existing SubParts.";

    private readonly SubPartCatalog _catalog = new SubPartCatalog();
    private readonly PartEditorController _controller = new PartEditorController();
    private readonly PartEditorScene _scene = new PartEditorScene();
    private readonly PartEditorGizmos _gizmos = new PartEditorGizmos();
    private readonly PartEditorInteraction _interaction;

    public SpaceTapeSubmod()
    {
        _interaction = new PartEditorInteraction(_gizmos);
    }

    public void Initialize()
    {
        PartRenderHelper.Patch();
    }

    public void Update(double dt)
    {
        _catalog.Update(dt);
    }

    /// <summary>Updates the origin gizmo for the current viewport. Call once per frame from the game's render loop.</summary>
    public void UpdateScene(Viewport viewport)
    {
        _scene.UpdateGizmo(viewport);
        if (_scene.IsActive)
        {
            double4x4 matrix = _scene.GetMatrixAsmb2Ego(viewport);
            doubleQuat asmb2Ecl = _scene.EditingSpace?.Asmb2Ecl ?? doubleQuat.Identity;
            Part? selectedPart = _controller.SelectedPlacementIndex >= 0 && _controller.SelectedPlacementIndex < _scene.EditorParts.Count
                ? _scene.EditorParts[_controller.SelectedPlacementIndex]
                : null;
            _gizmos.Update(selectedPart, in matrix, asmb2Ecl, viewport);
            _interaction.Update(_scene, _controller, viewport);
        }
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##space_tape_content");
        try
        {
            RenderContentInner();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Render error: {ex.Message}");
            Console.WriteLine($"space-tape: RenderContent error - {ex}");
        }
        SubmodUI.EndContentArea();
    }

    private void RenderContentInner()
    {
        // Editor scene controls
        ImGui.SeparatorText("Part Editor");

        bool isActive = _scene.IsActive;
        if (isActive)
        {
            ImGui.TextColored(new float4(0.2f, 1f, 0.2f, 1f), "Editor: ACTIVE");
            ImGui.SameLine();
            if (ImGui.Button("Close Editor##st_exit"))
                _scene.Exit();
        }
        else
        {
            ImGui.TextDisabled("Editor: Inactive");
            ImGui.SameLine();
            if (ImGui.Button("Open Editor##st_enter"))
                _scene.Enter();
        }

        ImGui.Spacing();

        // SubPart catalog (only useful when editor is active)
        ImGui.SeparatorText("SubPart Catalog");
        _catalog.Render();

        string? selected = _catalog.TakeSelectedSubPartId();
        if (selected != null && _scene.IsActive)
        {
            _controller.AddSubPart(selected);
            _scene.SyncParts(_controller.CurrentPart);
        }
    }

    public void Dispose()
    {
        PartRenderHelper.Unpatch();
        _gizmos.Dispose();
        _scene.Dispose();
    }
}
