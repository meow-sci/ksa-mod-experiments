using System;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Handles mouse interaction for the Part editor: hover detection, click-to-select,
/// and gizmo drag for translate/rotate/scale operations.
/// </summary>
public sealed class PartEditorInteraction
{
    private readonly PartEditorGizmos _gizmos;
    private double2 _prevCursorPos;

    // Reflection access to Part's private matrix cache field.
    // Note: Part property setters already invalidate _matrixAsmb, so this is a safety measure.
    private static readonly FieldInfo? _matrixAsmbField =
        typeof(Part).GetField("_matrixAsmb", BindingFlags.NonPublic | BindingFlags.Instance);

    public PartEditorInteraction(PartEditorGizmos gizmos)
    {
        _gizmos = gizmos;
    }

    /// <summary>
    /// Called each frame from SpaceTapeSubmod.UpdateScene(). Handles hover, selection, and dragging.
    /// </summary>
    public void Update(PartEditorScene scene, PartEditorController controller, Viewport viewport)
    {
        if (!scene.IsActive) return;

        if (ImGui.GetIO().WantCaptureMouse)
        {
            _prevCursorPos = CursorPos;
            return;
        }

        double2 cursorPos = CursorPos;
        double4x4 matrixAsmb2Ego = scene.GetMatrixAsmb2Ego(viewport);
        doubleQuat vehicleAsmb2Ego = scene.EditingSpace?.Asmb2Ecl ?? doubleQuat.Identity;
        Camera camera = viewport.GetCamera();

        Part? selectedPart = null;
        if (controller.SelectedPlacementIndex >= 0 && controller.SelectedPlacementIndex < scene.EditorParts.Count)
            selectedPart = scene.EditorParts[controller.SelectedPlacementIndex];

        // Build ray from camera through cursor
        Ray ray = camera.ScreenToEgoRay(cursorPos);
        ray.Direction = ray.Direction.NormalizeOrZero();

        // Only raycast gizmos when NOT dragging — preserves locked axis during drag
        if (!_gizmos.GizmoGrabbed)
        {
            _gizmos.UpdateRaycast(ray, viewport);
        }

        // Raycast parts when no gizmo is hit and not dragging a gizmo
        Part? highlighted = null;
        if (_gizmos.HighlightedGizmo == null && !_gizmos.GizmoGrabbed)
        {
            double closest = double.MaxValue;
            foreach (Part part in scene.EditorParts)
            {
                // Try RayCastEgoSubPart first — editor Parts are leaf-level (no children)
                // so RayCastEgo (which iterates SubParts children) returns false.
                // RayCastEgoSubPart tests THIS Part's own mesh via MeshViewModule.
                if (part.RayCastEgoSubPart(in matrixAsmb2Ego, ray,
                    out double nearT, out double _,
                    out double3 _, out double3 _,
                    out double3 _, out double3 _)
                    && nearT < closest)
                {
                    closest = nearT;
                    highlighted = part;
                }

                // Also try RayCastEgo for imported Parts that may have SubParts children
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
        }

        // Click to select / grab gizmo
        bool leftClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (leftClicked)
        {
            if (_gizmos.HighlightedGizmo != null)
            {
                _gizmos.GizmoGrabbed = true;
            }
            else if (highlighted != null)
            {
                int idx = IndexOf(scene, highlighted);
                if (idx >= 0) controller.SelectedPlacementIndex = idx;
                _gizmos.GizmoGrabbed = false;
            }
            else
            {
                controller.SelectedPlacementIndex = -1;
                _gizmos.GizmoGrabbed = false;
            }
        }

        bool leftReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        if (leftReleased)
            _gizmos.GizmoGrabbed = false;

        // Drag: translate
        if (_gizmos.GizmoGrabbed && _gizmos.HighlightedGizmo == _gizmos.TranslateGizmo && selectedPart != null)
        {
            double3 prevNear = camera.ScreenToEgoNearPlane(_prevCursorPos);
            double nearLen = prevNear.Length();
            double3 currNear = camera.ScreenToEgoNearPlane(cursorPos);
            double3 screenDelta = currNear - prevNear;

            if (screenDelta.NormalizeOrZero().Length() != 0.0 && _gizmos.HighlightedSegmentIndex >= 0)
            {
                GenericGizmo.PerSegmentData[] seg = _gizmos.TranslateGizmo.GetSegmentDataByViewport(viewport);
                double3 axisDir = Double3Ex.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

                if (axisDir.Length() != 0.0)
                {
                    double3 projected = double3.Dot(screenDelta, axisDir) * axisDir;
                    double3 partPosEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
                    double distRatio = partPosEgo.Length() / nearLen;
                    double3 worldDelta = projected * distRatio;

                    double4x4.Invert(selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego), out double4x4 invParent);
                    double3 newPosInParent = (partPosEgo + worldDelta).Transform(invParent);
                    selectedPart.PositionParentAsmb = newPosInParent;

                    InvalidatePartMatrixCache(selectedPart);
                    if (controller.SelectedPlacement != null)
                        controller.SelectedPlacement.Position = newPosInParent;
                }
            }
        }

        // Drag: rotate
        if (_gizmos.GizmoGrabbed && _gizmos.HighlightedGizmo == _gizmos.RotationGizmo && selectedPart != null)
        {
            double3 prev = camera.ScreenToEgoNearPlane(_prevCursorPos);
            double3 curr = camera.ScreenToEgoNearPlane(cursorPos);
            double3 delta = curr - prev;

            if (delta.NormalizeOrZero().Length() != 0.0 && _gizmos.HighlightedSegmentIndex >= 0)
            {
                double angle = MathEx.SafeAcos(double3.Dot(prev, curr) / (prev.Length() * curr.Length()));
                GenericGizmo.PerSegmentData[] seg = _gizmos.RotationGizmo.GetSegmentDataByViewport(viewport);
                double3 axisEgo = Double3Ex.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

                if (axisEgo.Length() != 0.0)
                {
                    double3 posEgo = seg[_gizmos.HighlightedSegmentIndex].PositionEgo;
                    double3 crossVec = double3.Cross(posEgo, prev - posEgo);
                    int signDelta = Math.Sign(double3.Dot(delta, crossVec));
                    int signAxis = Math.Sign(double3.Dot(axisEgo, prev));

                    double3 localAxis = axisEgo.Transform(doubleQuat.Inverse(selectedPart.ParentAsmb2Ego(vehicleAsmb2Ego)));
                    doubleQuat rot = doubleQuat.CreateFromAxisAngle(localAxis, angle * signDelta * signAxis);
                    selectedPart.Asmb2ParentAsmb = doubleQuat.Multiply(rot, selectedPart.Asmb2ParentAsmb);

                    InvalidatePartMatrixCache(selectedPart);
                    if (controller.SelectedPlacement != null)
                        controller.SelectedPlacement.Rotation = selectedPart.Asmb2ParentAsmb;
                }
            }
        }

        // Drag: scale
        if (_gizmos.GizmoGrabbed && _gizmos.HighlightedGizmo == _gizmos.ScaleGizmo && selectedPart != null)
        {
            double3 prev = camera.ScreenToEgoNearPlane(_prevCursorPos);
            double nearLen = prev.Length();
            double3 curr = camera.ScreenToEgoNearPlane(cursorPos);
            double3 delta = curr - prev;

            if (delta.NormalizeOrZero().Length() != 0.0 && _gizmos.HighlightedSegmentIndex >= 0)
            {
                GenericGizmo.PerSegmentData[] seg = _gizmos.ScaleGizmo.GetSegmentDataByViewport(viewport);
                double3 axisDir = Double3Ex.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

                if (axisDir.Length() != 0.0)
                {
                    double3 projected = double3.Dot(delta, axisDir) * axisDir;
                    double partDist = selectedPart.PositionEgo(in matrixAsmb2Ego).Length();
                    double distRatio = partDist / nearLen;
                    double amount = (projected * distRatio).Length() * Math.Sign(double3.Dot(delta, axisDir));

                    double3 scale = selectedPart.Scale;
                    if (_gizmos.HighlightedSegmentIndex == 0)
                        scale.X = double.Clamp(scale.X + amount, double.Epsilon, double.MaxValue);
                    else if (_gizmos.HighlightedSegmentIndex == 1)
                        scale.Y = double.Clamp(scale.Y + amount, double.Epsilon, double.MaxValue);
                    else if (_gizmos.HighlightedSegmentIndex == 2)
                        scale.Z = double.Clamp(scale.Z + amount, double.Epsilon, double.MaxValue);
                    selectedPart.Scale = scale;

                    InvalidatePartMatrixCache(selectedPart);
                    if (controller.SelectedPlacement != null)
                        controller.SelectedPlacement.Scale = scale;
                }
            }
        }

        _prevCursorPos = cursorPos;
    }

    private static double2 CursorPos => new double2(ImGui.GetMousePos().X, ImGui.GetMousePos().Y);

    // Part property setters already reset _matrixAsmb to Identity, but we reset it
    // explicitly here as a belt-and-suspenders guard for any render paths that check it.
    private static void InvalidatePartMatrixCache(Part part)
    {
        _matrixAsmbField?.SetValue(part, double4x4.Identity);
    }

    private static int IndexOf(PartEditorScene scene, Part part)
    {
        for (int i = 0; i < scene.EditorParts.Count; i++)
        {
            if (scene.EditorParts[i] == part) return i;
        }
        return -1;
    }
}
