using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.PyroLib;

/// <summary>Euler-degree helpers for the plume rotation offset.</summary>
public static class RotationHelper
{
    /// <summary>Builds a rotation from per-axis degrees (X, Y, Z), applied in Z·Y·X order (X first).</summary>
    public static doubleQuat FromEulerDegrees(float3 degrees)
    {
        double hx = degrees.X * Math.PI / 360.0;
        double hy = degrees.Y * Math.PI / 360.0;
        double hz = degrees.Z * Math.PI / 360.0;
        var qx = new doubleQuat(Math.Sin(hx), 0, 0, Math.Cos(hx));
        var qy = new doubleQuat(0, Math.Sin(hy), 0, Math.Cos(hy));
        var qz = new doubleQuat(0, 0, Math.Sin(hz), Math.Cos(hz));
        return doubleQuat.Concatenate(doubleQuat.Concatenate(qx, qy), qz).NormalizedOrZero();
    }
}
