using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.FlexoLib.Editor;

public sealed class FlexoEditorInteraction
{
    private Part? _highlightedPart;
    private Part? _selectedPart;

    public Part? HighlightedPart => _highlightedPart;
    public Part? SelectedPart => _selectedPart;

    public void Update(FlexoEditorScene scene, Viewport viewport)
    {
        if (!scene.IsActive) return;
        if (ImGui.GetIO().WantCaptureMouse) return;

        double4x4 matrixAsmb2Ego = scene.GetMatrixAsmb2Ego(viewport);
        Camera camera = viewport.GetCamera();
        double2 cursorPos = new double2(ImGui.GetMousePos().X, ImGui.GetMousePos().Y);

        Ray ray = camera.ScreenToEgoRay(cursorPos);
        ray.Direction = ray.Direction.NormalizeOrZero();

        // Raycast parts
        Part? highlighted = null;
        double closest = double.MaxValue;

        foreach (Part part in scene.EditorParts)
        {
            if (part.RayCastEgoSubPart(in matrixAsmb2Ego, ray,
                out double nearT, out double _,
                out double3 _, out double3 _,
                out double3 _, out double3 _)
                && nearT < closest)
            {
                closest = nearT;
                highlighted = part;
            }

            if (part.RayCastEgo(in matrixAsmb2Ego, ray,
                out double nearT2, out double _2,
                out double3 _3, out double3 _4,
                out double3 _5, out double3 _6,
                out Part? closestSub, out Part? _7)
                && nearT2 < closest)
            {
                closest = nearT2;
                highlighted = closestSub?.PartParent ?? closestSub ?? part;
            }
        }

        // Update hover highlight
        if (highlighted != _highlightedPart)
        {
            if (_highlightedPart != null) _highlightedPart.Highlighted = false;
            if (highlighted != null) highlighted.Highlighted = true;
            _highlightedPart = highlighted;
        }

        // Click to select
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            SelectPart(highlighted);
        }
    }

    public void SelectPart(Part? part)
    {
        if (_selectedPart != null) _selectedPart.Selected = false;
        _selectedPart = part;
        if (_selectedPart != null) _selectedPart.Selected = true;
    }

    public void ClearVisualState()
    {
        if (_highlightedPart != null) { _highlightedPart.Highlighted = false; _highlightedPart = null; }
        if (_selectedPart != null) { _selectedPart.Selected = false; _selectedPart = null; }
    }
}
