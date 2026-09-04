using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HotPursuitLib;

/// <summary>Recomposes a mounted camera pose in the same frame as the render camera.</summary>
internal static class HotPursuitPose
{
    private const double Deg2Rad = Math.PI / 180.0;
    private const double BasisEpsilon = 1e-10;

    internal static bool TryApply(HotPursuitCamera entry, IViewport viewport)
    {
        if (entry.Vehicle is not { } vehicle || entry.Part is not { } part ||
            entry.Viewport is not { } ownedViewport || !ReferenceEquals(ownedViewport, viewport) ||
            !entry.Visible)
            return false;

        if (ownedViewport.BaseCamera.Following != vehicle)
        {
            // FixedController normally does this only when the slot is first configured. Reassert
            // it here too: KSA may clear/repoint a camera during scene changes or target reloads.
            ownedViewport.BaseCamera.SetFollow(vehicle, tidalLocking: true,
                changeControl: false, alert: false);
        }

        var referenceCamera = Program.MainViewport.GetCamera();
        var vehicleMatrix = vehicle.GetMatrixAsmb2Ego(referenceCamera);
        var partMatrix = part.MatrixAsmb2Ego(in vehicleMatrix);
        var mount = entry.MountPoint + entry.Translation;
        var positionEgo = double3.Transform(mount, partMatrix);
        var positionEcl = referenceCamera.EgoToEcl(positionEgo);

        if (!IsFinite(positionEcl))
            return false;

        if (!TryBuildBasis(entry, partMatrix, out var forwardEcl, out var upEcl,
                out var rightEcl))
            return false;

        // The basis is already in ECL and orthonormal. Applying Euler here avoids skewing camera
        // angles through a non-uniformly scaled part while retaining the scaled surface's true
        // inverse-transpose normal.
        ApplyEuler(ref forwardEcl, ref upEcl, ref rightEcl, entry.RotationDeg);

        var forwardLength = forwardEcl.Length();
        var upLength = upEcl.Length();
        if (!double.IsFinite(forwardLength) || !double.IsFinite(upLength) ||
            forwardLength <= BasisEpsilon || upLength <= BasisEpsilon)
            return false;

        forwardEcl /= forwardLength;
        // Re-orthogonalize up after Euler/scale so LookAtRotation never receives a nearly parallel
        // pair. A final fallback preserves a valid camera even on a badly scaled imported part.
        upEcl -= forwardEcl * double3.Dot(upEcl, forwardEcl);
        upLength = upEcl.Length();
        if (!double.IsFinite(upLength) || upLength <= BasisEpsilon)
        {
            upEcl = double3.Cross(rightEcl, forwardEcl);
            upLength = upEcl.Length();
        }
        if (!double.IsFinite(upLength) || upLength <= BasisEpsilon)
            return false;
        upEcl /= upLength;

        var camera = ownedViewport.BaseCamera;
        camera.PositionEcl = positionEcl;
        // GameViewport calls Camera.OnFrame after this prefix, which terrain-clamps the camera.
        // Clamp once now as well so the celestial metrics below describe the final render pose;
        // Camera.OnFrame's second clamp is then idempotent.
        camera.ClampCamera();
        HotPursuitCelestialState.Synchronize(camera);
        camera.WorldRotation = Camera.LookAtRotation(forwardEcl, upEcl);
        camera.SetFieldOfView(entry.FieldOfView);
        return true;
    }

    private static bool TryBuildBasis(HotPursuitCamera entry, double4x4 partMatrix,
        out double3 forward, out double3 up, out double3 right)
    {
        forward = up = right = double3.Zero;
        if (!double4x4.Invert(partMatrix, out var inversePartMatrix))
            return false;

        // Positions/tangents use the full part matrix. Surface normals use inverse-transpose;
        // using the ordinary direction transform points the camera the wrong way on a
        // non-uniformly scaled face.
        var normalMatrix = double4x4.Transpose(inversePartMatrix);
        forward = Normalize(double3.TransformNormal(entry.SurfaceNormal, normalMatrix));
        up = double3.TransformNormal(entry.MountTangent, partMatrix);
        up -= forward * double3.Dot(up, forward);
        up = Normalize(up);
        if (forward.LengthSquared() <= BasisEpsilon || up.LengthSquared() <= BasisEpsilon)
            return false;
        right = Normalize(double3.Cross(forward, up));
        return right.LengthSquared() > BasisEpsilon;
    }

    /// <summary>Apply pitch (right), yaw (up), then roll (forward) in the mount basis.</summary>
    private static void ApplyEuler(ref double3 forward, ref double3 up, ref double3 right,
        double3 rotationDeg)
    {
        var yaw = doubleQuat.CreateFromAxisAngle(up, rotationDeg.Y * Deg2Rad);
        forward = forward.Transform(yaw);
        right = right.Transform(yaw);

        var pitch = doubleQuat.CreateFromAxisAngle(right, rotationDeg.X * Deg2Rad);
        forward = forward.Transform(pitch);
        up = up.Transform(pitch);

        var roll = doubleQuat.CreateFromAxisAngle(forward, rotationDeg.Z * Deg2Rad);
        up = up.Transform(roll);
        right = right.Transform(roll);
    }

    private static double3 Normalize(double3 value)
    {
        var length = value.Length();
        return double.IsFinite(length) && length > BasisEpsilon ? value / length : double3.Zero;
    }

    private static bool IsFinite(double3 value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
