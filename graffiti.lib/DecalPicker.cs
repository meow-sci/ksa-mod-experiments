using System;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GraffitiLib;

/// <summary>
/// Turns "where the mouse cursor is pointing" into a decal anchor: a mesh-precise hit on a
/// vehicle part, else a terrain hit on the nearby celestial.
/// </summary>
/// <remarks>
/// <para><b>Vehicles first, terrain behind.</b> The vehicle sweep uses KSA's own watertight
/// triangle raycast (<c>Part.RayCastEgo</c>) — the same call flight-mode hover picking makes —
/// so the hit point is on the art surface, not on a collider primitive. Only if nothing was hit
/// does the terrain march run.</para>
/// <para><c>Cursor.InputRay</c> is the mouse cursor's picking ray in EGO space (origin at the
/// camera, ecliptic axes), refreshed by the game each frame from <c>Cursor.UpdateInputRay</c>.</para>
/// <para>Game thread only — every call here reads live game state.</para>
/// </remarks>
internal static class DecalPicker
{
    private const double Rad2Deg = 180.0 / Math.PI;

    /// <summary>Coarse march steps over the ray before bisection brackets the terrain crossing.</summary>
    private const int TerrainMarchSteps = 64;

    /// <summary>Bisections after the bracket — 2^-24 of the step, i.e. sub-millimetre at 2 km.</summary>
    private const int TerrainBisections = 24;

    /// <summary>What a successful pick resolved to, in the frame the anchor will be stored in.</summary>
    internal readonly record struct PickResult(
        DecalAnchorKind Kind,
        Vehicle? Vehicle,
        Part? Part,
        Celestial? Body,
        double3 Position,
        double3 Normal,
        double Distance,
        double RotationDeg);

    /// <summary>Casts the cursor ray and returns the nearest anchor within <paramref name="range"/> metres.</summary>
    internal static bool TryPick(double range, out PickResult result)
    {
        result = default;
        if (Program.GetMainCamera() is not { } camera)
            return false;

        var ray = Cursor.InputRay;
        if (!double.IsFinite(ray.Direction.X) || ray.Direction.LengthSquared() <= 0)
            return false;

        return TryPickVehicle(camera, ray, range, out result)
               || TryPickTerrain(camera, ray, range, out result);
    }

    /// <summary>Sweeps every live vehicle within range and keeps the nearest triangle hit.</summary>
    /// <remarks>
    /// <c>Part.RayCastEgo</c> loops over the part's <c>SubParts</c> and raycasts each sub-part's
    /// view mesh, returning position/normal in the local frame of <c>closestSubPart</c> — NOT of
    /// the top-level part the call was made on. The anchor is therefore stored against that
    /// sub-part's <c>InstanceId</c>, and <see cref="DecalAnchors"/> re-derives the same matrix
    /// from the same object every frame. The normal is the mesh normal of the triangle's first
    /// vertex — flat, not interpolated — which only sets the projection box's orientation, never
    /// the shading.
    /// </remarks>
    private static bool TryPickVehicle(Camera camera, Ray ray, double range, out PickResult result)
    {
        result = default;
        var found = false;
        var best = range;

        foreach (var vehicle in VehicleProvider.GetAllVehicles())
        {
            var centre = camera.GetPositionEgo(vehicle);
            if (centre.Length() - vehicle.BoundingSphereRadiusBody > range)
                continue;

            var vehicleMatrix = vehicle.GetMatrixAsmb2Ego(camera);
            foreach (var part in vehicle.Parts.Parts)
            {
                if (!part.RayCastEgo(in vehicleMatrix, ray, out var distance, out _,
                        out var positionAsmb, out var normalAsmb, out _, out _,
                        out var closestSubPart, out _))
                    continue;
                if (!(distance >= 0) || !(distance < best))
                    continue;

                var normalLength = normalAsmb.Length();
                if (!double.IsFinite(normalLength) || normalLength <= 0)
                    continue;

                best = distance;
                found = true;
                result = new PickResult(DecalAnchorKind.Vehicle, vehicle, closestSubPart ?? part, null,
                    positionAsmb, normalAsmb / normalLength, distance, 0.0);
            }
        }

        return found;
    }

    /// <summary>
    /// Marches the ray against the CPU height field of the camera's nearby celestial and bisects
    /// the first crossing. KSA has no CPU ray-vs-terrain routine, so this is the shape of
    /// <c>TerrainImpactFinder.TryFind</c> — a coarse march plus bisections over
    /// <c>GetTerrainHeightFromDirCcf</c> — driven by a straight line instead of a trajectory,
    /// all in body-fixed (CCF) coordinates, the one frame the height field is defined in.
    /// </summary>
    private static bool TryPickTerrain(Camera camera, Ray ray, double range, out PickResult result)
    {
        result = default;
        if (camera.NearbyCelestial is not { } body)
            return false;

        var cce2Ccf = body.GetCce2Ccf();
        // The ray is in ego; (rayOrigin - bodyEgo) is the origin relative to the body centre,
        // still in ecliptic axes = CCE. Rotating by cce2Ccf lands in the body-fixed frame.
        var originCcf = (ray.Origin - camera.GetPositionEgo(body)).Transform(cce2Ccf);
        var directionCcf = ray.Direction.Transform(cce2Ccf);
        if (!double.IsFinite(originCcf.X) || !double.IsFinite(directionCcf.X))
            return false;

        // Starting underground means the camera is inside the terrain; marching would report the
        // far wall of the hole.
        if (Depth(body, originCcf, accurate: false) <= 0)
            return false;

        var above = 0.0;
        var below = double.NaN;
        for (var step = 1; step <= TerrainMarchSteps; step++)
        {
            var t = range * step / TerrainMarchSteps;
            if (Depth(body, originCcf + directionCcf * t, accurate: false) <= 0)
            {
                below = t;
                break;
            }

            above = t;
        }

        if (double.IsNaN(below))
            return false;

        for (var i = 0; i < TerrainBisections; i++)
        {
            var middle = 0.5 * (above + below);
            var accurate = i == TerrainBisections - 1;
            if (Depth(body, originCcf + directionCcf * middle, accurate) <= 0)
                below = middle;
            else
                above = middle;
        }

        var hitCcf = originCcf + directionCcf * below;
        var latitude = Celestial.GetLatitudeFromCcf(hitCcf);
        var longitude = Celestial.GetLongitudeFromCcf(hitCcf);
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
            return false;

        result = new PickResult(DecalAnchorKind.Terrain, null, null, body,
            new double3(latitude, longitude, 0), default, below,
            Heading(body, hitCcf, ray.Direction));
        return true;
    }

    /// <summary>Signed metres of the point above the terrain surface (negative = underground).</summary>
    private static double Depth(Celestial body, double3 pointCcf, bool accurate)
    {
        var radius = pointCcf.Length();
        if (!double.IsFinite(radius) || radius <= 0)
            return double.NaN; // NaN <= 0 is false, so a degenerate sample never reports a hit
        return radius - (body.MeanRadius + body.GetTerrainHeightFromDirCcf(pointCcf / radius, accurate));
    }

    /// <summary>
    /// The compass bearing that points the PNG's "up" away from the camera along the ground, so
    /// a sprayed decal reads upright from where the player is standing.
    /// </summary>
    private static double Heading(Celestial body, double3 hitCcf, double3 forwardEgo)
    {
        var hitCce = hitCcf.Transform(body.GetCcf2Cce());
        if (Vehicle.ComputeEnu2Cce(hitCce, body.GetCci2Cce()) is not { } enu2Cce)
            return 0.0;
        var east = double3.UnitX.Transform(enu2Cce);
        var north = double3.UnitY.Transform(enu2Cce);
        var heading = Math.Atan2(double3.Dot(east, forwardEgo), double3.Dot(north, forwardEgo)) * Rad2Deg;
        return double.IsFinite(heading) ? heading : 0.0;
    }
}
