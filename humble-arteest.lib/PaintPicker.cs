using System;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>Pick the actual static/dynamic render mesh, including every GLTF primitive.</summary>
internal static class PaintPicker
{
    internal static VehiclePaint.MeshInstance? Pick(double range)
    {
        if (Program.GetMainCamera() is not { } camera) return null;
        var ray = Cursor.GetEgoRay(Program.MainViewport);
        if (!double.IsFinite(ray.Direction.LengthSquared()) || ray.Direction.LengthSquared() <= 0) return null;
        double best = range;
        VehiclePaint.MeshInstance? hit = null;
        foreach (var vehicle in VehicleProvider.GetAllVehicles(includeDebris: true))
        {
            if (camera.GetPositionEgo(vehicle).Length() - vehicle.BoundingSphereRadiusBody > range) continue;
            var vehicleMatrix = vehicle.GetMatrixAsmb2Ego(camera);
            foreach (var part in PartHelpers.GetAllParts(vehicle))
            {
                var matrix = part.MatrixAsmb2Ego(in vehicleMatrix);
                foreach (var module in part.Modules.Get<PartModelModule>()) Test(part, module.PartModel.Template.Mesh, matrix);
                // Match PartModelDynamicModule's gimbal transform rather than the un-gimballed view mesh.
                var dynamicMatrix = matrix;
                var gimbal = part.GimbalAsmb;
                var gimbals = part.Modules.Get<Gimbal>();
                if (gimbal != null && !gimbals.IsEmpty)
                {
                    var rotation = part.Tree.Gimbals.GetState(gimbals[0]).Gimbal2Asmb;
                    if (rotation != Brutal.Numerics.doubleQuat.Identity)
                    {
                        var offset = Brutal.Numerics.double4x4.CreateTranslation(-gimbal.PositionAsmb)
                            * Brutal.Numerics.double4x4.CreateFromQuaternion(gimbal.Gimbal2Asmb * rotation * gimbal.Gimbal2Asmb.Inverse())
                            * Brutal.Numerics.double4x4.CreateTranslation(gimbal.PositionAsmb);
                        dynamicMatrix = offset * matrix;
                    }
                }
                foreach (var module in part.Modules.Get<PartModelDynamicModule>()) Test(part, module.PartModelDynamic.Template.Mesh, dynamicMatrix);
            }
        }
        return hit;

        void Test(Part part, MeshReference? mesh, Brutal.Numerics.double4x4 matrix)
        {
            if (mesh?.PositionsCompare == null || string.IsNullOrEmpty(mesh.Id)) return;
            foreach (var vertices in mesh.PositionsCompare)
                if (ray.RaycastWatertight(vertices, in matrix, out var near, out _, out _, out _)
                    && near >= 0 && near < best)
                { best = near; hit = new VehiclePaint.MeshInstance(part, mesh.Id); }
        }
    }
}
