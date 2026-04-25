using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Renders connector positions as small colored cubes in the 3D editing scene.
/// </summary>
public sealed class ConnectorGizmo : IDisposable
{
    private GenericGizmo? _gizmo;       // box: position marker
    private GenericGizmo? _arrowGizmo;  // arrow: direction indicator
    private int _capacity;

    public void EnsureCapacity(int count)
    {
        int needed = Math.Max(count, 4);
        if (_gizmo != null && _capacity >= needed) return;

        _gizmo?.Dispose();
        _arrowGizmo?.Dispose();
        _capacity = needed;
        _gizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("Box"),
            GenericGizmo.Static.GenericGizmoRenderData,
            _capacity);
        _arrowGizmo = new GenericGizmo(
            ModLibrary.Get<MeshReference>("Arrow"),
            GenericGizmo.Static.GenericGizmoRenderData,
            _capacity);
    }

    public void Update(
        Viewport viewport,
        IReadOnlyList<ConnectorState> connectors,
        int selectedIndex,
        double4x4 matrixAsmb2Ego)
    {
        if (_gizmo == null || _arrowGizmo == null) return;

        var seg = _gizmo.GetSegmentDataByViewport(viewport);
        var arrowSeg = _arrowGizmo.GetSegmentDataByViewport(viewport);

        for (int i = 0; i < _capacity; i++)
        {
            if (i >= connectors.Count)
            {
                seg[i].Active = false;
                arrowSeg[i].Active = false;
                continue;
            }

            var c = connectors[i];
            double3 posEgo = c.Position.Transform(matrixAsmb2Ego);
            double4 color = GetColor(c, i == selectedIndex);

            seg[i].Active = true;
            seg[i].PositionEgo = posEgo;
            seg[i].Body2Cce = c.Rotation;
            seg[i].Scale = new double3(0.05, 0.05, 0.05);
            seg[i].Color = color;

            arrowSeg[i].Active = true;
            arrowSeg[i].PositionEgo = posEgo;
            arrowSeg[i].Body2Cce = c.Rotation;
            arrowSeg[i].Scale = new double3(0.08, 0.12, 0.08);
            arrowSeg[i].Color = color with { W = 0.85 };
        }
    }

    public void Deactivate(Viewport viewport)
    {
        if (_gizmo != null)
        {
            var seg = _gizmo.GetSegmentDataByViewport(viewport);
            for (int i = 0; i < _capacity; i++)
                seg[i].Active = false;
        }
        if (_arrowGizmo != null)
        {
            var arrowSeg = _arrowGizmo.GetSegmentDataByViewport(viewport);
            for (int i = 0; i < _capacity; i++)
                arrowSeg[i].Active = false;
        }
    }

    private static double4 GetColor(ConnectorState c, bool selected)
    {
        if (selected)
            return new double4(0.2, 1.0, 0.2, 1.0); // bright green

        int flagCount = (c.FlagInternal ? 1 : 0) + (c.FlagToSurface ? 1 : 0) + (c.FlagFromSurface ? 1 : 0);
        if (flagCount > 1)
            return new double4(1.0, 1.0, 1.0, 0.9); // white

        if (c.FlagInternal) return new double4(1.0, 1.0, 0.0, 0.9);  // yellow
        if (c.FlagToSurface) return new double4(0.0, 1.0, 1.0, 0.9); // cyan
        if (c.FlagFromSurface) return new double4(1.0, 0.0, 1.0, 0.9); // magenta

        return new double4(0.7, 0.7, 0.7, 0.7); // gray — no flags
    }

    public void Dispose()
    {
        _gizmo?.Dispose();
        _gizmo = null;
        _arrowGizmo?.Dispose();
        _arrowGizmo = null;
    }
}
