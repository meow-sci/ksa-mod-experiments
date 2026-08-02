using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

/// <summary>Pan constraint mode for click-and-drag SubPart movement.</summary>
public enum PanMode { Normal, PlaneX, PlaneY, PlaneZ }

/// <summary>
/// Handles mouse interaction for the Part editor: hover detection, click-to-select,
/// and gizmo drag for translate/rotate/scale operations.
/// </summary>
public sealed class PartEditorInteraction
{
    private readonly PartEditorGizmos _gizmos;
    private double2 _prevCursorPos;
    private Part? _highlightedPart;
    private Part? _selectedPart;

    /// <summary>Current plane-lock mode, toggled by P key.</summary>
    public PanMode CurrentPanMode { get; private set; } = PanMode.Normal;
    private bool _planeDragging;
    private double3 _planeDragPartPosEgo;   // part's ego-space position at drag start
    private double3 _planeDragClickHitEgo;  // where the initial click ray hit the plane
    private double3 _planeDragNormal;

    /// <summary>When true, translate gizmo drag and pan mode movement snap to GridSnapStep increments.</summary>
    public bool GridSnapEnabled { get; set; }
    /// <summary>Grid snap step size in meters.</summary>
    public float GridSnapStep { get; set; } = 0.05f;

    /// <summary>When true, rotation gizmo drag snaps to RotSnapDeg increments.</summary>
    public bool RotSnapEnabled { get; set; }
    /// <summary>Rotation snap increment in degrees.</summary>
    public float RotSnapDeg { get; set; } = 15f;

    // Rotation gizmo drag tracking — captures start state so snapping is applied from the original orientation
    private doubleQuat _rotDragStartQuat;
    private double3 _rotDragAxisLocal;  // fixed rotation axis in parent-assembly space at drag start
    private double _rotAccumRad;        // accumulated raw signed angle since drag start

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

        // Sync selection visual if external code changed SelectedPlacementIndex (e.g. UI hierarchy click)
        if (selectedPart != _selectedPart)
        {
            if (_selectedPart != null) _selectedPart.Selected = false;
            _selectedPart = selectedPart;
            if (_selectedPart != null) _selectedPart.Selected = true;
        }

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

        // Update hover highlight — set Part.Highlighted for GPU shader feedback
        if (highlighted != _highlightedPart)
        {
            if (_highlightedPart != null) _highlightedPart.Highlighted = false;
            if (highlighted != null) highlighted.Highlighted = true;
            _highlightedPart = highlighted;
        }

        // Click to select / grab gizmo
        bool leftClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (leftClicked)
        {
            if (_gizmos.HighlightedGizmo != null)
            {
                _gizmos.GizmoGrabbed = true;

                // Capture rotation drag start state so snap can be applied relative to the original orientation
                if (selectedPart != null
                    && _gizmos.HighlightedGizmo == _gizmos.RotationGizmo
                    && _gizmos.HighlightedSegmentIndex >= 0)
                {
                    GenericGizmo.PerSegmentData[] rseg = _gizmos.RotationGizmo.GetSegmentDataByViewport(viewport);
                    double3 startAxisEgo = Directions.Right.Transform(rseg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();
                    _rotDragAxisLocal = startAxisEgo.Transform(doubleQuat.Inverse(selectedPart.ParentAsmb2Ego(vehicleAsmb2Ego)));
                    _rotDragStartQuat = selectedPart.Asmb2ParentAsmb;
                    _rotAccumRad = 0.0;
                }
            }
            // Pan mode hijacks click+drag when a part is already selected —
            // starts plane drag regardless of where the click lands (no raycast needed)
            else if (CurrentPanMode != PanMode.Normal && selectedPart != null)
            {
                _planeDragNormal = CurrentPanMode switch
                {
                    PanMode.PlaneX => new double3(1, 0, 0),
                    PanMode.PlaneY => new double3(0, 1, 0),
                    PanMode.PlaneZ => new double3(0, 0, 1),
                    _ => new double3(0, 1, 0)
                };
                // Compute where the click ray hits the constraint plane through the part
                _planeDragPartPosEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
                double denom = double3.Dot(ray.Direction, _planeDragNormal);
                if (Math.Abs(denom) > 1e-10)
                {
                    double t = double3.Dot(_planeDragPartPosEgo - ray.Origin, _planeDragNormal) / denom;
                    if (t > 0)
                    {
                        _planeDragClickHitEgo = ray.Origin + ray.Direction * t;
                        _planeDragging = true;
                        controller.PushUndo();
                    }
                }
            }
            else if (highlighted != null)
            {
                int idx = IndexOf(scene, highlighted);
                if (idx >= 0)
                {
                    UpdateSelection(scene, controller, idx);
                }
                _gizmos.GizmoGrabbed = false;
            }
            else
            {
                UpdateSelection(scene, controller, -1);
                _gizmos.GizmoGrabbed = false;
            }
        }

        bool leftReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        if (leftReleased)
        {
            _gizmos.GizmoGrabbed = false;
            _planeDragging = false;
        }

        // Quick-flip hotkeys: D = +45° around Y-axis, F = +45° around X-axis
        if (selectedPart != null && !ImGui.GetIO().WantCaptureKeyboard)
        {
            bool flipD = ImGui.IsKeyPressed(ImGuiKey.D);
            bool flipF = ImGui.IsKeyPressed(ImGuiKey.F);
            if (flipD || flipF)
            {
                controller.PushUndo();
                double3 axis = flipD ? new double3(0, 1, 0) : new double3(1, 0, 0);
                doubleQuat rot = doubleQuat.CreateFromAxisAngle(axis, Math.PI / 4.0);
                selectedPart.Asmb2ParentAsmb = doubleQuat.Multiply(rot, selectedPart.Asmb2ParentAsmb);
                InvalidatePartMatrixCache(selectedPart);
                if (controller.SelectedPlacement != null)
                    controller.SelectedPlacement.Rotation = selectedPart.Asmb2ParentAsmb;
            }
        }

        // P key cycles pan mode: Normal → PlaneX → PlaneY → PlaneZ → Normal
        if (!ImGui.GetIO().WantCaptureKeyboard && ImGui.IsKeyPressed(ImGuiKey.P))
        {
            CurrentPanMode = CurrentPanMode switch
            {
                PanMode.Normal => PanMode.PlaneX,
                PanMode.PlaneX => PanMode.PlaneY,
                PanMode.PlaneY => PanMode.PlaneZ,
                PanMode.PlaneZ => PanMode.Normal,
                _ => PanMode.Normal
            };
            Console.WriteLine($"space-tape: Pan mode → {CurrentPanMode}");
        }

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
                double3 axisDir = Directions.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

                if (axisDir.Length() != 0.0)
                {
                    double3 projected = double3.Dot(screenDelta, axisDir) * axisDir;
                    double3 partPosEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
                    double distRatio = partPosEgo.Length() / nearLen;
                    double3 worldDelta = projected * distRatio;

                    double4x4.Invert(selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego), out double4x4 invParent);
                    double3 newPosInParent = (partPosEgo + worldDelta).Transform(invParent);

                    // Apply grid snap
                    if (GridSnapEnabled && GridSnapStep > 0f)
                    {
                        double step = GridSnapStep;
                        newPosInParent = new double3(
                            Math.Round(newPosInParent.X / step) * step,
                            Math.Round(newPosInParent.Y / step) * step,
                            Math.Round(newPosInParent.Z / step) * step);
                    }

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
                double3 axisEgo = Directions.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

                if (axisEgo.Length() != 0.0)
                {
                    double3 posEgo = seg[_gizmos.HighlightedSegmentIndex].PositionEgo;
                    double3 crossVec = double3.Cross(posEgo, prev - posEgo);
                    int signDelta = Math.Sign(double3.Dot(delta, crossVec));
                    int signAxis = Math.Sign(double3.Dot(axisEgo, prev));

                    // Accumulate the raw signed angle from drag start
                    _rotAccumRad += angle * signDelta * signAxis;

                    // Determine the angle to apply (snapped or raw) relative to the start orientation
                    double applyAngle;
                    if (RotSnapEnabled && RotSnapDeg > 0f)
                    {
                        double snapRad = RotSnapDeg * (Math.PI / 180.0);
                        applyAngle = Math.Round(_rotAccumRad / snapRad) * snapRad;
                    }
                    else
                    {
                        applyAngle = _rotAccumRad;
                    }

                    // Apply total rotation from the captured start orientation using the fixed drag-start axis.
                    // Fall back to incremental if start axis wasn't captured (shouldn't normally happen).
                    if (_rotDragAxisLocal.Length() > 0.5)
                    {
                        selectedPart.Asmb2ParentAsmb = doubleQuat.CreateFromAxisAngle(_rotDragAxisLocal, applyAngle) * _rotDragStartQuat;
                    }
                    else
                    {
                        double3 localAxis = axisEgo.Transform(doubleQuat.Inverse(selectedPart.ParentAsmb2Ego(vehicleAsmb2Ego)));
                        selectedPart.Asmb2ParentAsmb = doubleQuat.Multiply(
                            doubleQuat.CreateFromAxisAngle(localAxis, angle * signDelta * signAxis),
                            selectedPart.Asmb2ParentAsmb);
                    }

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
                double3 axisDir = Directions.Right.Transform(seg[_gizmos.HighlightedSegmentIndex].Body2Cce).NormalizeOrZero();

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

        // Plane-constrained drag: move SubPart on locked plane using delta from click origin
        if (_planeDragging && selectedPart != null)
        {
            double denom = double3.Dot(ray.Direction, _planeDragNormal);
            if (Math.Abs(denom) > 1e-10)
            {
                double t = double3.Dot(_planeDragPartPosEgo - ray.Origin, _planeDragNormal) / denom;
                if (t > 0)
                {
                    double3 hitPointEgo = ray.Origin + ray.Direction * t;
                    // Delta from where the user first clicked, not absolute position
                    double3 deltaEgo = hitPointEgo - _planeDragClickHitEgo;
                    double3 newPosEgo = _planeDragPartPosEgo + deltaEgo;

                    double4x4.Invert(selectedPart.MatrixParentAsmb2Ego(in matrixAsmb2Ego), out double4x4 invParent);
                    double3 newPosInParent = newPosEgo.Transform(invParent);

                    // Snap to grid if enabled
                    if (GridSnapEnabled && GridSnapStep > 0f)
                    {
                        double step = GridSnapStep;
                        newPosInParent = new double3(
                            Math.Round(newPosInParent.X / step) * step,
                            Math.Round(newPosInParent.Y / step) * step,
                            Math.Round(newPosInParent.Z / step) * step);
                    }

                    selectedPart.PositionParentAsmb = newPosInParent;

                    InvalidatePartMatrixCache(selectedPart);
                    if (controller.SelectedPlacement != null)
                        controller.SelectedPlacement.Position = newPosInParent;
                }
            }
        }

        _prevCursorPos = cursorPos;
    }

    private static double2 CursorPos => new double2(ImGui.GetMousePos().X, ImGui.GetMousePos().Y);

    // Part's property setters already invalidate the cached transforms; this is a belt-and-suspenders
    // guard for any render path that reads them without going through a setter first.
    //
    // This used to reflect Part._matrixAsmb and write double4x4.Identity, which was the game's
    // "uncached" sentinel. KSA build 2026.8.3.5117 (rev 5112) added caching for
    // Part.MatrixAsmb2VehicleAsmb and changed the sentinel to an all-NaN matrix, which turned that
    // write into "the cached transform *is* identity" — silently collapsing the part's transform.
    // Part.ResetCachedPosMatrixValues() is public (and was already public on 5018), resets all five
    // cache fields, and cannot drift out from under us the way the sentinel did.
    private static void InvalidatePartMatrixCache(Part part)
    {
        part.ResetCachedPosMatrixValues();
    }

    private static int IndexOf(PartEditorScene scene, Part part)
    {
        for (int i = 0; i < scene.EditorParts.Count; i++)
        {
            if (scene.EditorParts[i] == part) return i;
        }
        return -1;
    }

    private void UpdateSelection(PartEditorScene scene, PartEditorController controller, int newIndex)
    {
        if (_selectedPart != null) _selectedPart.Selected = false;

        controller.SelectedPlacementIndex = newIndex;

        if (newIndex >= 0 && newIndex < scene.EditorParts.Count)
            _selectedPart = scene.EditorParts[newIndex];
        else
            _selectedPart = null;

        if (_selectedPart != null) _selectedPart.Selected = true;
    }

    /// <summary>Clears hover/selection visual state on all tracked parts. Call when editor scene exits.</summary>
    public void ClearVisualState()
    {
        if (_highlightedPart != null) { _highlightedPart.Highlighted = false; _highlightedPart = null; }
        if (_selectedPart != null) { _selectedPart.Selected = false; _selectedPart = null; }
    }
}
