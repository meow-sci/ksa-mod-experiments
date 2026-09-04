using System;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HotPursuitLib;

/// <summary>Vehicle-only, mesh-precise cursor picker used by camera placement.</summary>
internal static class HotPursuitPicker
{
    internal readonly record struct PickResult(
        Vehicle Vehicle,
        Part Part,
        double3 Position,
        double3 Normal,
        double Distance);

    internal static bool TryPick(double range, out PickResult result)
    {
        result = default;
        var camera = Program.MainViewport.GetCamera();
        var ray = Cursor.GetEgoRay(Program.MainViewport);
        if (!IsFinite(ray.Direction) || ray.Direction.LengthSquared() <= 0)
            return false;

        var best = range;
        var found = false;
        foreach (var vehicle in VehicleProvider.GetAllVehicles(includeDebris: true))
        {
            // Cheap broad phase before walking the part tree. This is deliberately camera-space,
            // matching KSA's own hover path and avoiding an ECL/floating-origin comparison.
            var centre = camera.GetPositionEgo(vehicle);
            if (!IsFinite(centre) || centre.Length() - vehicle.BoundingSphereRadiusBody > best)
                continue;

            var vehicleMatrix = vehicle.GetMatrixAsmb2Ego(camera);
            foreach (var part in vehicle.Parts.Parts)
            {
                // RayCastEgo returns the closest subpart and returns hit position/normal in that
                // closest subpart's local assembly frame. Keep those values exactly as returned.
                if (!part.RayCastEgo(in vehicleMatrix, ray, out var distance, out _,
                        out var position, out var normal, out _, out _, out var closestSubPart, out _)
                    || closestSubPart == null || distance < 0 || distance >= best)
                    continue;

                var length = normal.Length();
                if (!double.IsFinite(length) || length <= 0)
                    continue;

                best = distance;
                result = new PickResult(vehicle, closestSubPart,
                    position, normal / length, distance);
                found = true;
            }
        }

        return found;
    }

    private static bool IsFinite(double3 value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
