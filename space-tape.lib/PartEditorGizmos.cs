using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Manages the three transform gizmos (Translate, Rotate, Scale) for the Part editor.
/// Handles per-frame visual updates and mouse-over detection.
/// </summary>
public sealed class PartEditorGizmos : IDisposable
{
    public enum GizmoMode { None, Translate, Rotate, Scale }

    private static readonly double4 GIZMO_HIGHLIGHT = new(1.0, 1.0, 1.0, 0.75);

    public readonly GenericGizmo TranslateGizmo;
    public readonly GenericGizmo RotationGizmo;
    public readonly GenericGizmo ScaleGizmo;

    public GizmoMode ActiveMode { get; set; } = GizmoMode.Translate;
    public GenericGizmo? HighlightedGizmo { get; private set; }
    public int HighlightedSegmentIndex { get; private set; } = -1;
    public bool GizmoGrabbed { get; set; }

    /// <summary>Uniform scale multiplier applied to all gizmo segments (default 1.0).</summary>
    public float GizmoScale { get; set; } = 1.0f;

    public PartEditorGizmos()
    {
        TranslateGizmo = new GenericGizmo(ModLibrary.Get<MeshReference>("ArrowMesh"), GenericGizmo.Static.GenericGizmoRenderData, 3);
        RotationGizmo = new GenericGizmo(ModLibrary.Get<MeshReference>("CircleMesh"), GenericGizmo.Static.GenericGizmoRenderData, 4);
        ScaleGizmo = new GenericGizmo(ModLibrary.Get<MeshReference>("BoxedArrowMesh"), GenericGizmo.Static.GenericGizmoRenderData, 3);
    }

    /// <summary>
    /// Updates the active gizmo's segment data for the current frame.
    /// Positions gizmos at the selected part and applies highlight coloring.
    /// </summary>
    public void Update(Part? selectedPart, ref readonly double4x4 matrixAsmb2Ego, doubleQuat vehicleAsmb2Ego, Viewport viewport)
    {
        if (selectedPart == null)
        {
            DeactivateAll(viewport);
            return;
        }

        switch (ActiveMode)
        {
            case GizmoMode.None:
                DeactivateAll(viewport);
                break;
            case GizmoMode.Translate:
                DeactivateGizmo(RotationGizmo, viewport);
                DeactivateGizmo(ScaleGizmo, viewport);
                UpdateTranslateGizmo(selectedPart, in matrixAsmb2Ego, vehicleAsmb2Ego, viewport);
                break;
            case GizmoMode.Rotate:
                DeactivateGizmo(TranslateGizmo, viewport);
                DeactivateGizmo(ScaleGizmo, viewport);
                UpdateRotationGizmo(selectedPart, in matrixAsmb2Ego, vehicleAsmb2Ego, viewport);
                break;
            case GizmoMode.Scale:
                DeactivateGizmo(TranslateGizmo, viewport);
                DeactivateGizmo(RotationGizmo, viewport);
                UpdateScaleGizmo(selectedPart, in matrixAsmb2Ego, vehicleAsmb2Ego, viewport);
                break;
        }
    }

    /// <summary>
    /// Performs a raycast against all three gizmos to detect mouse-over.
    /// Only tests gizmos that are relevant to the current mode.
    /// </summary>
    public void UpdateRaycast(Ray ray, Viewport viewport)
    {
        double closestT = double.MaxValue;
        GenericGizmo? closest = null;
        int closestSeg = -1;

        if (TranslateGizmo.RaycastEgo(ray, viewport, out double t, out int s) && t < closestT)
        {
            closestT = t;
            closest = TranslateGizmo;
            closestSeg = s;
        }
        if (RotationGizmo.RaycastEgo(ray, viewport, out t, out s) && t < closestT)
        {
            closestT = t;
            closest = RotationGizmo;
            closestSeg = s;
        }
        if (ScaleGizmo.RaycastEgo(ray, viewport, out t, out s) && t < closestT)
        {
            closestT = t;
            closest = ScaleGizmo;
            closestSeg = s;
        }

        HighlightedGizmo = closest;
        HighlightedSegmentIndex = closestSeg;
    }

    public void Dispose()
    {
        TranslateGizmo.Dispose();
        RotationGizmo.Dispose();
        ScaleGizmo.Dispose();
    }

    private void UpdateTranslateGizmo(Part selectedPart, ref readonly double4x4 matrixAsmb2Ego, doubleQuat vehicleAsmb2Ego, Viewport viewport)
    {
        doubleQuat orientation = selectedPart.Asmb2Ego(vehicleAsmb2Ego);
        double3 positionEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
        double s = GizmoScale * 2.0;
        double3 gScale = new double3(s, s, s);

        GenericGizmo.PerSegmentData[] seg = TranslateGizmo.GetSegmentDataByViewport(viewport);

        seg[0].PositionEgo = positionEgo;
        seg[0].Body2Cce = orientation;
        seg[0].Scale = gScale;
        seg[0].Color = new double4(1.0, 0.0, 0.0, 0.75);
        seg[0].Active = true;

        seg[1].PositionEgo = positionEgo;
        seg[1].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Backward.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[1].Scale = gScale;
        seg[1].Color = new double4(0.0, 1.0, 0.0, 0.75);
        seg[1].Active = true;

        seg[2].PositionEgo = positionEgo;
        seg[2].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Down.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[2].Scale = gScale;
        seg[2].Color = new double4(0.0, 0.0, 1.0, 0.75);
        seg[2].Active = true;

        if (HighlightedGizmo == TranslateGizmo && HighlightedSegmentIndex >= 0)
            seg[HighlightedSegmentIndex].Color = double4.Lerp(seg[HighlightedSegmentIndex].Color, GIZMO_HIGHLIGHT, 0.5);
    }

    private void UpdateRotationGizmo(Part selectedPart, ref readonly double4x4 matrixAsmb2Ego, doubleQuat vehicleAsmb2Ego, Viewport viewport)
    {
        doubleQuat orientation = selectedPart.Asmb2Ego(vehicleAsmb2Ego);
        double3 positionEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
        double s = GizmoScale * 2.0;
        double3 gScale = new double3(s, s, s);

        GenericGizmo.PerSegmentData[] seg = RotationGizmo.GetSegmentDataByViewport(viewport);

        // segment 0: X-axis (red)
        seg[0].PositionEgo = positionEgo;
        seg[0].Body2Cce = orientation;
        seg[0].Scale = gScale;
        seg[0].Color = new double4(1.0, 0.0, 0.0, 0.75);
        double3 upDir0 = Double3Ex.Up.Transform(seg[0].Body2Cce).NormalizeOrZero();
        seg[0].Active = Math.Abs(double3.Dot(upDir0, positionEgo)) >= 0.15;

        // segment 1: Y-axis (green)
        seg[1].PositionEgo = positionEgo;
        seg[1].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Forward.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[1].Scale = gScale;
        seg[1].Color = new double4(0.0, 1.0, 0.0, 0.75);
        double3 upDir1 = Double3Ex.Up.Transform(seg[1].Body2Cce).NormalizeOrZero();
        seg[1].Active = Math.Abs(double3.Dot(upDir1, positionEgo)) >= 0.15;

        // segment 2: Z-axis (blue)
        seg[2].PositionEgo = positionEgo;
        seg[2].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Down.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[2].Scale = gScale;
        seg[2].Color = new double4(0.0, 0.0, 1.0, 0.75);
        double3 upDir2 = Double3Ex.Up.Transform(seg[2].Body2Cce).NormalizeOrZero();
        seg[2].Active = Math.Abs(double3.Dot(upDir2, positionEgo)) >= 0.15;

        // segment 3: screen-space ring — hidden in this implementation
        seg[3].Active = false;

        if (HighlightedGizmo == RotationGizmo && HighlightedSegmentIndex >= 0 && HighlightedSegmentIndex < 3)
            seg[HighlightedSegmentIndex].Color = double4.Lerp(seg[HighlightedSegmentIndex].Color, GIZMO_HIGHLIGHT, 0.5);
    }

    private void UpdateScaleGizmo(Part selectedPart, ref readonly double4x4 matrixAsmb2Ego, doubleQuat vehicleAsmb2Ego, Viewport viewport)
    {
        doubleQuat orientation = selectedPart.Asmb2Ego(vehicleAsmb2Ego);
        double3 positionEgo = selectedPart.PositionEgo(in matrixAsmb2Ego);
        double s = GizmoScale * 2.0;
        double3 gScale = new double3(s, s, s);

        GenericGizmo.PerSegmentData[] seg = ScaleGizmo.GetSegmentDataByViewport(viewport);

        seg[0].PositionEgo = positionEgo;
        seg[0].Body2Cce = orientation;
        seg[0].Scale = gScale;
        seg[0].Color = new double4(1.0, 0.0, 0.0, 0.75);
        seg[0].Active = true;

        seg[1].PositionEgo = positionEgo;
        seg[1].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Backward.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[1].Scale = gScale;
        seg[1].Color = new double4(0.0, 1.0, 0.0, 0.75);
        seg[1].Active = true;

        seg[2].PositionEgo = positionEgo;
        seg[2].Body2Cce = doubleQuat.CreateFromAxisAngle(Double3Ex.Down.Transform(orientation), Math.PI / 2.0) * orientation;
        seg[2].Scale = gScale;
        seg[2].Color = new double4(0.0, 0.0, 1.0, 0.75);
        seg[2].Active = true;

        if (HighlightedGizmo == ScaleGizmo && HighlightedSegmentIndex >= 0)
            seg[HighlightedSegmentIndex].Color = double4.Lerp(seg[HighlightedSegmentIndex].Color, GIZMO_HIGHLIGHT, 0.5);
    }

    private static void DeactivateGizmo(GenericGizmo g, Viewport vp)
    {
        GenericGizmo.PerSegmentData[] seg = g.GetSegmentDataByViewport(vp);
        for (int i = 0; i < seg.Length; i++)
            seg[i].Active = false;
    }

    private void DeactivateAll(Viewport vp)
    {
        DeactivateGizmo(TranslateGizmo, vp);
        DeactivateGizmo(RotationGizmo, vp);
        DeactivateGizmo(ScaleGizmo, vp);
    }
}
