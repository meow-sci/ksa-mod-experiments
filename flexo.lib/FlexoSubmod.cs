using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.FlexoLib.Data;
using MeowSci.FlexoLib.Editor;
using MeowSci.FlexoLib.Runtime;
using MeowSci.KsaAbstractions;

namespace MeowSci.FlexoLib;

public sealed class FlexoSubmod : ISubmod
{
    public string Name => "Flexo - Robotics";
    public string Tooltip => "Robotics — hinges, rotors, and articulated parts.";

    public static FlexoSubmod? Current { get; private set; }

    private readonly FlexoRuntime _runtime = new();
    private readonly FlexoEditorScene _editorScene = new();
    private readonly FlexoEditorInteraction _editorInteraction = new();
    private readonly FlexoEditorState _editorState = new();
    private readonly FlexoCameraSnap _cameraSnap = new();
    private readonly FlexoEditorLighting _lighting = new();
    private FlexoEditorUi? _editorUi;
    private bool _editorOpen = false;

    public FlexoRuntime Runtime => _runtime;
    public FlexoEditorScene EditorScene => _editorScene;
    public bool EditorOpen { get => _editorOpen; set => _editorOpen = value; }

    public void Initialize()
    {
        Current = this;
        _runtime.Initialize();
        _editorUi = new FlexoEditorUi(
            _editorScene, _editorInteraction, _editorState,
            _cameraSnap, _lighting, _runtime.DataManager);
        Console.WriteLine("flexo: Initialized");
    }

    public void Update(double dt)
    {
        if (_editorScene.IsActive)
        {
            Viewport viewport = Program.MainViewport;
            _editorInteraction.Update(_editorScene, viewport);
            _cameraSnap.DrawGrid(viewport, _editorScene);

            double4x4 matrixAsmb2Ego = _editorScene.GetMatrixAsmb2Ego(viewport);
            _lighting.UpdateLights(matrixAsmb2Ego);
        }
    }

    /// <summary>
    /// Called from a Harmony prefix on Universe.ExecuteNextVehicleSolvers.
    /// This is the only safe phase to mutate vehicle part trees and call
    /// UpdateAfterPartTreeModification() — it runs before the solver task
    /// is prepared, so kinematic and analytic state timestamps stay coherent.
    /// </summary>
    public void UpdateBeforeVehicleSolvers(double dt)
    {
        _runtime.UpdateBeforeVehicleSolvers(dt);
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##flexo_panel");
        try
        {
            if (!_editorOpen)
            {
                if (ImGui.Button(" Open Editor ##flexo_open_editor"))
                    _editorOpen = true;
            }
            else
            {
                ImGui.TextDisabled("Editor is open.");
            }

            ImGui.Separator();
            FlexoRuntimeUi.Render(_runtime);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {ex.Message}");
        }
        SubmodUI.EndContentArea();
    }

    public void RenderFloatingWindows()
    {
        if (_editorOpen && _editorUi != null)
        {
            _editorUi.Render(ref _editorOpen);
            if (!_editorOpen)
            {
                _editorInteraction.ClearVisualState();
                _editorState.Reset();
                _editorScene.Exit();
            }
        }
    }

    public void Dispose()
    {
        _editorScene.Dispose();
        Current = null;
        Console.WriteLine("flexo: Disposed");
    }
}
