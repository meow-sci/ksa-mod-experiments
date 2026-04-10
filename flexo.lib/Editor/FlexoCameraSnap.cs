using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.FlexoLib.Editor;

public enum CameraSnapMode
{
    None,
    Front,
    Back,
    Left,
    Right,
    Top,
    Bottom
}

public sealed class FlexoCameraSnap
{
    public CameraSnapMode ActiveMode { get; private set; } = CameraSnapMode.None;
    public bool GridVisible { get; set; }
    public float GridWidth { get; set; } = 5.0f;
    public float GridHeight { get; set; } = 5.0f;
    public float GridSpacing { get; set; } = 0.25f;
    public float4 GridColor { get; set; } = new float4(0.5f, 0.5f, 0.5f, 0.4f);
    public float4 GridAxisColor { get; set; } = new float4(0.8f, 0.8f, 0.2f, 0.6f);

    public void SnapTo(CameraSnapMode mode, FlexoEditorScene scene)
    {
        if (mode != CameraSnapMode.None && (!scene.IsActive || scene.EditingSpace == null))
            return;

        ActiveMode = mode;
        if (mode == CameraSnapMode.None)
        {
            GridVisible = false;
            return;
        }

        GridVisible = true;

        Camera? camera = Program.GetCamera();
        IFollowable? following = camera?.Following;
        OrbitView? orbitView = following?.OrbitView;
        if (orbitView == null) return;

        (double azimuth, double elevation) = GetAzimuthElevation(mode);
        orbitView.Azimuth = azimuth;
        orbitView.Elevation = elevation;
    }

    private static (double azimuth, double elevation) GetAzimuthElevation(CameraSnapMode mode)
    {
        return mode switch
        {
            CameraSnapMode.Front  => (Math.PI, 0.0),
            CameraSnapMode.Back   => (0.0, 0.0),
            CameraSnapMode.Left   => (Math.PI / 2.0, 0.0),
            CameraSnapMode.Right  => (-Math.PI / 2.0, 0.0),
            CameraSnapMode.Top    => (-Math.PI, -Math.PI / 2.0),
            CameraSnapMode.Bottom => (0.0, Math.PI / 2.0),
            _ => (0.0, 0.0)
        };
    }

    public void DrawGrid(Viewport viewport, FlexoEditorScene scene)
    {
        if (!GridVisible || ActiveMode == CameraSnapMode.None || !scene.IsActive) return;

        double4x4 matrixAsmb2Ego = scene.GetMatrixAsmb2Ego(viewport);

        double3 axisU, axisV;
        switch (ActiveMode)
        {
            case CameraSnapMode.Front:
            case CameraSnapMode.Back:
                axisU = double3.UnitZ; axisV = double3.UnitY; break;
            case CameraSnapMode.Left:
            case CameraSnapMode.Right:
                axisU = double3.UnitX; axisV = double3.UnitZ; break;
            case CameraSnapMode.Top:
            case CameraSnapMode.Bottom:
                axisU = double3.UnitX; axisV = double3.UnitY; break;
            default: return;
        }

        float halfU = GridWidth / 2f;
        float halfV = GridHeight / 2f;
        float spacing = Math.Max(GridSpacing, 0.01f);

        int linesV = Math.Min((int)(GridHeight / spacing) + 1, 200);
        for (int i = 0; i <= linesV; i++)
        {
            double v = -halfV + i * spacing;
            double3 startEgo = (axisU * (-halfU) + axisV * v).Transform(matrixAsmb2Ego);
            double3 endEgo = (axisU * halfU + axisV * v).Transform(matrixAsmb2Ego);
            float4 color = Math.Abs(v) < spacing * 0.5 ? GridAxisColor : GridColor;
            Program.GizmosRenderer.DrawLine(startEgo, endEgo, color);
        }

        int linesU = Math.Min((int)(GridWidth / spacing) + 1, 200);
        for (int i = 0; i <= linesU; i++)
        {
            double u = -halfU + i * spacing;
            double3 startEgo = (axisU * u + axisV * (-halfV)).Transform(matrixAsmb2Ego);
            double3 endEgo = (axisU * u + axisV * halfV).Transform(matrixAsmb2Ego);
            float4 color = Math.Abs(u) < spacing * 0.5 ? GridAxisColor : GridColor;
            Program.GizmosRenderer.DrawLine(startEgo, endEgo, color);
        }
    }
}
