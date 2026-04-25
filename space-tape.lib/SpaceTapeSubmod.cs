using System;
using System.Reflection;
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
    private readonly LoadSubPartsModal _loadSubPartsModal = new();
    private readonly PartEditorController _controller = new PartEditorController();
    private readonly PartEditorScene _scene = new PartEditorScene();
    private readonly PartEditorGizmos _gizmos = new PartEditorGizmos();
    private readonly PartEditorInteraction _interaction;
    private readonly PartEditorUi _ui = new PartEditorUi();
    private readonly PartModWriter _writer = new PartModWriter();
    private readonly CameraSnapController _cameraSnap = new CameraSnapController();
    private readonly EditorLighting _lighting = new EditorLighting();
    private readonly SubPartsWindow _subPartsWindow = new();
    private readonly SubpartViewerWindow _subpartViewer = new();

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
        _subPartsWindow.Update(dt);
        _subpartViewer.Update(dt);

        string? selected = _catalog.TakeSelectedSubPartId();
        if (selected != null)
        {
            if (_subPartsWindow.ViewSubPartsMode)
            {
                SubpartThumbnailEntry? entry = SubpartThumbnailCache.Get(selected);
                if (entry != null)
                {
                    _subpartViewer.Open(
                        selected,
                        entry,
                        SubpartGenerationController.ImageSizes[_generation.ImageSizeIndex]);
                }
            }
            else if (_scene.IsActive)
            {
                _controller.AddSubPart(selected);
                _scene.SyncParts(_controller.CurrentPart);
            }
        }
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
        _subPartsWindow.Render(_catalog);
        _subpartViewer.Render();
    }

    private void RenderContentInner()
    {
        if (ImGui.Button(" Load SubParts ##st_load_modal", new float2(-1, 0)))
        {
            ImGui.OpenPopup(LoadSubPartsModal.PopupId);
        }

        _loadSubPartsModal.Render(_generation);

        ImGui.Spacing();

        if (_scene.IsActive)
        {
            if (ImGui.Button(" Close Part Editor ##st_editor_close", new float2(-1, 0)))
            {
                CloseEditor();
            }
        }
        else
        {
            if (ImGui.Button(" Open Part Editor ##st_editor_open", new float2(-1, 0)))
            {
                OpenEditor();
            }
        }
    }

    private void OpenEditor()
    {
        _scene.Enter();
        if (_scene.IsActive)
        {
            _catalog.LoadSubParts();
            _ui.WindowOpen = true;
            _subPartsWindow.IsOpen = true;
            TryAutoLoadSavedAndStockParts();
        }
    }

    private void CloseEditor()
    {
        _scene.Exit();
        _ui.WindowOpen = false;
        _subPartsWindow.IsOpen = false;
    }

    private void TryAutoLoadSavedAndStockParts()
    {
        MethodInfo? method = typeof(PartEditorUi).GetMethod("AutoLoadSavedAndStockParts", new[] { typeof(PartModWriter) });
        method?.Invoke(_ui, new object[] { _writer });
    }

    public void Dispose()
    {
        _cameraSnap.SnapTo(CameraSnapMode.None, _scene);
        _subpartViewer.Dispose();
        _generation.Dispose();
        Current = null;
        PartRenderHelper.Unpatch();
        _interaction.ClearVisualState();
        _gizmos.Dispose();
        _scene.Dispose();
    }
}
