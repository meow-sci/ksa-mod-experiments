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

    /// <summary>Active instance, read by the render patch to call UpdateScene per-frame.</summary>
    public static SpaceTapeSubmod? Current { get; private set; }

    private readonly SubPartCatalog _catalog = new SubPartCatalog();
    private readonly PartEditorController _controller = new PartEditorController();
    private readonly PartEditorScene _scene = new PartEditorScene();
    private readonly PartEditorGizmos _gizmos = new PartEditorGizmos();
    private readonly PartEditorInteraction _interaction;
    private readonly PartEditorUi _ui = new PartEditorUi();
    private readonly PartModWriter _writer = new PartModWriter();

    public SpaceTapeSubmod()
    {
        _interaction = new PartEditorInteraction(_gizmos);
    }

    public void Initialize()
    {
        Current = this;
        PartRenderHelper.Patch();
    }

    public void Update(double dt)
    {
        _catalog.Update(dt);
    }

    /// <summary>Updates the origin gizmo for the current viewport. Call once per frame from the game's render loop.</summary>
    public void UpdateScene(Viewport viewport)
    {
        _scene.UpdateGizmo(viewport, _controller.CurrentPart);
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

    public void RenderFloatingWindows()
    {
        _ui.RenderEditorWindow(_controller, _scene, _gizmos, _catalog, _writer);
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
            if (ImGui.Button(" Close Editor ##st_exit")) _scene.Exit();
            ImGui.SameLine();
            if (ImGui.Button(" Editor Window ##st_win")) _ui.WindowOpen = !_ui.WindowOpen;
        }
        else
        {
            ImGui.TextDisabled("Editor: Inactive");
            ImGui.SameLine();
            if (ImGui.Button(" Open Editor ##st_enter"))
            {
                _scene.Enter();
                _ui.WindowOpen = true;
            }
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
        Current = null;
        PartRenderHelper.Unpatch();
        _gizmos.Dispose();
        _scene.Dispose();
    }
}
