using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Camera snap modes for standard orthographic-style vantage points.
/// </summary>
public enum CameraSnapMode
{
    None,
    Front,   // look along -Z, up = +Y
    Back,    // look along +Z, up = +Y
    Left,    // look along +X, up = +Y
    Right,   // look along -X, up = +Y
    Top,     // look along -Y, up = -Z
    Bottom   // look along +Y, up = +Z
}

/// <summary>
/// Manages camera snap-to-view functionality and an optional grid plane overlay
/// for the Space Tape part editor. Supports six orthographic-style snap views and
/// draws a translucent reference grid on the plane facing the camera.
/// </summary>
public sealed class CameraSnapController
{
    public CameraSnapMode ActiveMode { get; private set; } = CameraSnapMode.None;

    /// <summary>Whether the grid plane overlay is visible.</summary>
    public bool GridVisible { get; set; } = true;

    /// <summary>Whether to draw the grid plane perpendicular to the X axis (YZ plane).</summary>
    public bool ShowXPlane { get; set; }

    /// <summary>Whether to draw the grid plane perpendicular to the Y axis (XZ plane).</summary>
    public bool ShowYPlane { get; set; }

    /// <summary>Whether to draw the grid plane perpendicular to the Z axis (XY plane).</summary>
    public bool ShowZPlane { get; set; } = true;

    /// <summary>Grid width in meters (horizontal extent).</summary>
    public float GridWidth { get; set; } = 5.0f;

    /// <summary>Grid height in meters (vertical extent).</summary>
    public float GridHeight { get; set; } = 5.0f;

    /// <summary>Spacing between grid lines in meters.</summary>
    public float GridSpacing { get; set; } = 0.25f;

    /// <summary>Color of regular grid lines (translucent gray).</summary>
    public float4 GridColor { get; set; } = new float4(0.5f, 0.5f, 0.5f, 0.4f);

    /// <summary>Color of axis-aligned grid lines through the origin.</summary>
    public float4 GridAxisColor { get; set; } = new float4(0.8f, 0.8f, 0.2f, 0.6f);

    /// <summary>Whether to show a debug readout of current azimuth/elevation values.</summary>
    public bool DebugReadout { get; set; }

    /// <summary>
    /// Snaps the camera to the specified vantage point by setting the OrbitView's
    /// Azimuth and Elevation on the editing space's follow target.
    /// </summary>
    public void SnapTo(CameraSnapMode mode, PartEditorScene scene)
    {
        if (mode != CameraSnapMode.None && (!scene.IsActive || scene.EditingSpace == null))
            return;

        ActiveMode = mode;
        if (mode == CameraSnapMode.None)
            return;

        SetPlaneVisibilityForSnap(mode);

        Camera? camera = Program.GetCamera();
        IFollowable? following = camera?.Following;
        OrbitView? orbitView = following?.OrbitView;
        if (orbitView == null)
        {
            Console.WriteLine("space-tape: CameraSnapController.SnapTo - OrbitView is null");
            return;
        }

        (double azimuth, double elevation) = GetAzimuthElevation(mode);
        orbitView.Azimuth = azimuth;
        orbitView.Elevation = elevation;
        // Preserve the user's current zoom level (DistancePower)
    }

    private void SetPlaneVisibilityForSnap(CameraSnapMode mode)
    {
        ShowXPlane = mode is CameraSnapMode.Front or CameraSnapMode.Back;
        ShowYPlane = mode is CameraSnapMode.Left or CameraSnapMode.Right;
        ShowZPlane = mode is CameraSnapMode.Top or CameraSnapMode.Bottom;
    }

    /// <summary>
    /// Returns the OrbitView Azimuth and Elevation for each snap mode.
    /// These values are computed for the VehicleEditingSpace frame where
    /// frame2Ecl = CreateFromAxisAngle(UnitX, PI) with identity Asmb2Ecl.
    /// In this frame: initial direction = +X, up axis = -Z.
    /// </summary>
    private static (double azimuth, double elevation) GetAzimuthElevation(CameraSnapMode mode)
    {
        // Frame convention (180° X rotation applied by OrbitController for VehicleEditingSpace):
        //   Initial camera offset direction (azimuth=0, elevation=0) = +X
        //   Frame "up" axis = -Z
        //   Azimuth rotates the offset direction around -Z
        //   Elevation tilts toward/away from the -Z axis
        //
        // Labels are mapped to match user expectations in the part editor:
        //   Front  = look along -X (toward the vehicle nose)
        //   Back   = look along +X
        //   Left   = look along -Y
        //   Right  = look along +Y
        //   Top    = look along +Z
        //   Bottom = look along -Z
        return mode switch
        {
            CameraSnapMode.Front  => (Math.PI, 0.0),                      // look -X
            CameraSnapMode.Back   => (0.0, 0.0),                          // look +X
            CameraSnapMode.Left   => (Math.PI / 2.0, 0.0),               // look -Y
            CameraSnapMode.Right  => (-Math.PI / 2.0, 0.0),              // look +Y
            CameraSnapMode.Top    => (-Math.PI, -Math.PI / 2.0),         // look +Z
            CameraSnapMode.Bottom => (0.0, Math.PI / 2.0),               // look -Z
            _ => (0.0, 0.0)
        };
    }

    /// <summary>
    /// Draws the grid plane overlay using KSA's orbit line renderer.
    /// Must be called once per frame from the render update when the grid is visible.
    /// </summary>
    public void DrawGrid(Viewport viewport, PartEditorScene scene)
    {
        if (!GridVisible || !scene.IsActive)
            return;

        double4x4 matrixAsmb2Ego = scene.GetMatrixAsmb2Ego(viewport);
        DrawEnabledGridPlanes(viewport, matrixAsmb2Ego);
    }

    private void DrawEnabledGridPlanes(Viewport viewport, double4x4 matrixAsmb2Ego)
    {
        if (ShowXPlane)
            DrawGridPlane(double3.UnitZ, double3.UnitY, viewport, matrixAsmb2Ego);
        if (ShowYPlane)
            DrawGridPlane(double3.UnitX, double3.UnitZ, viewport, matrixAsmb2Ego);
        if (ShowZPlane)
            DrawGridPlane(double3.UnitX, double3.UnitY, viewport, matrixAsmb2Ego);
    }

    private void DrawGridPlane(double3 axisU, double3 axisV, Viewport viewport, double4x4 matrixAsmb2Ego)
    {
        float halfU = GridWidth / 2f;
        float halfV = GridHeight / 2f;
        float spacing = Math.Max(GridSpacing, 0.01f);
        int maxLines = 200;

        float4 gridColor = GridColor;
        float4 gridAxisColor = GridAxisColor;

        // Lines along U axis (varying V position)
        int linesV = Math.Min((int)(GridHeight / spacing) + 1, maxLines);
        for (int i = 0; i <= linesV; i++)
        {
            double v = -halfV + i * spacing;
            double3 startAsmb = axisU * (-halfU) + axisV * v;
            double3 endAsmb = axisU * halfU + axisV * v;
            double3 startEgo = startAsmb.Transform(matrixAsmb2Ego);
            double3 endEgo = endAsmb.Transform(matrixAsmb2Ego);
            float4 color = Math.Abs(v) < spacing * 0.5 ? gridAxisColor : gridColor;
            DrawGridLine(viewport, startEgo, endEgo, color);
        }

        // Lines along V axis (varying U position)
        int linesU = Math.Min((int)(GridWidth / spacing) + 1, maxLines);
        for (int i = 0; i <= linesU; i++)
        {
            double u = -halfU + i * spacing;
            double3 startAsmb = axisU * u + axisV * (-halfV);
            double3 endAsmb = axisU * u + axisV * halfV;
            double3 startEgo = startAsmb.Transform(matrixAsmb2Ego);
            double3 endEgo = endAsmb.Transform(matrixAsmb2Ego);
            float4 color = Math.Abs(u) < spacing * 0.5 ? gridAxisColor : gridColor;
            DrawGridLine(viewport, startEgo, endEgo, color);
        }
    }

    private static void DrawGridLine(Viewport viewport, double3 startEgo, double3 endEgo, float4 color)
    {
        byte4 lineColor = ToByteColor(color);
        OrbitLinePass.AddLineVertex(viewport, float3.Pack(in startEgo), lineColor);
        OrbitLinePass.AddLineVertex(viewport, float3.Pack(in endEgo), lineColor);
        OrbitLinePass.AddLineEnd(viewport);
    }

    private static byte4 ToByteColor(float4 color)
    {
        float4 clamped = new float4(
            Math.Clamp(color.X, 0f, 1f),
            Math.Clamp(color.Y, 0f, 1f),
            Math.Clamp(color.Z, 0f, 1f),
            Math.Clamp(color.W, 0f, 1f));

        return byte4.Pack(in clamped, Pack.Float.Normalize);
    }
}
