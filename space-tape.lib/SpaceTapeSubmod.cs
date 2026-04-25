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
    private readonly SubpartGenerationController _generation = new();
    private readonly PartEditorController _controller = new PartEditorController();
    private readonly PartEditorScene _scene = new PartEditorScene();
    private readonly PartEditorGizmos _gizmos = new PartEditorGizmos();
    private readonly PartEditorInteraction _interaction;
    private readonly PartEditorUi _ui = new PartEditorUi();
    private readonly PartModWriter _writer = new PartModWriter();
    private readonly CameraSnapController _cameraSnap = new CameraSnapController();
    private readonly EditorLighting _lighting = new EditorLighting();

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
        _generation.Update();
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

            _cameraSnap.DrawGrid(viewport, _scene);
            _lighting.UpdateLights(matrix);
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
        _ui.RenderEditorWindow(_controller, _scene, _gizmos, _interaction, _catalog, _writer, _cameraSnap, _lighting);
    }

    private void RenderContentInner()
    {
        // SubPart catalog
        bool editorWindowOpen = _ui.WindowOpen;
        _catalog.Render(_scene, ref editorWindowOpen);
        _ui.WindowOpen = editorWindowOpen;

        string? selected = _catalog.TakeSelectedSubPartId();
        if (selected != null && _scene.IsActive)
        {
            _controller.AddSubPart(selected);
            _scene.SyncParts(_controller.CurrentPart);
        }
    }

    public void Dispose()
    {
        _cameraSnap.SnapTo(CameraSnapMode.None, _scene);
        _generation.Dispose();
        Current = null;
        PartRenderHelper.Unpatch();
        _interaction.ClearVisualState();
        _gizmos.Dispose();
        _scene.Dispose();
    }
}
