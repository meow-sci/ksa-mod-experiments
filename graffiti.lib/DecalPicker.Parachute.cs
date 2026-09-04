using System;
using Brutal.Numerics;
using KSA;

namespace MeowSci.GraffitiLib;

internal static partial class DecalPicker
{
    /// <summary>
    /// Raycasts the live 8-ring × 16-spoke cloth surface. The hit is stored as barycentric
    /// coordinates on three cloth nodes, allowing the decal to follow inflation and flutter.
    /// </summary>
    private static bool TryPickParachute(Vehicle vehicle, Parachute parachute,
        ref readonly double4x4 vehicleMatrix, Ray ray, double best, out PickResult result)
    {
        result = default;
        var positions = parachute.ClothPositionsFront;
        var topology = ChuteClothSystem.Topology;
        if (positions == null || positions.Length < topology.CanopyNodeCount)
            return false;

        var bestResult = default(PickResult);
        var attachAsmb = double3.Unpack(in parachute.AttachLocationPartAsmb)
            .Transform(parachute.Parent.MatrixAsmb2VehicleAsmb);
        var local2Ego = double4x4.CreateTranslation(attachAsmb) * vehicleMatrix;
        if (!double4x4.Invert(local2Ego, out var ego2Local))
            return false;

        var found = false;
        TestFan(topology.ApexIndex, ring: 0);
        for (var ring = 0; ring < ChuteClothTopology.Rings - 1; ring++)
        {
            for (var spoke = 0; spoke < ChuteClothTopology.Spokes; spoke++)
            {
                var a = ChuteClothTopology.NodeIndex(ring, spoke);
                var b = ChuteClothTopology.NodeIndex(ring + 1, spoke);
                var c = ChuteClothTopology.NodeIndex(ring + 1, spoke + 1);
                var d = ChuteClothTopology.NodeIndex(ring, spoke + 1);
                TestTriangle(a, b, c);
                TestTriangle(a, c, d);
            }
        }
        if (found)
            result = bestResult;
        return found;

        void TestFan(int apex, int ring)
        {
            for (var spoke = 0; spoke < ChuteClothTopology.Spokes; spoke++)
                TestTriangle(apex, ChuteClothTopology.NodeIndex(ring, spoke),
                    ChuteClothTopology.NodeIndex(ring, spoke + 1));
        }

        void TestTriangle(int ia, int ib, int ic)
        {
            var a = double3.Unpack(in positions[ia]);
            var b = double3.Unpack(in positions[ib]);
            var c = double3.Unpack(in positions[ic]);
            var aEgo = a.Transform(local2Ego);
            var bEgo = b.Transform(local2Ego);
            var cEgo = c.Transform(local2Ego);
            if (!ray.RaycastWatertight(aEgo, bEgo, cEgo, out var distance)
                || !(distance >= 0) || !(distance < best))
                return;

            var hitLocal = (ray.Origin + ray.Direction * distance).Transform(ego2Local);
            if (!TryBarycentric(hitLocal, a, b, c, out var barycentric))
                return;

            var normal = double3.Cross(b - a, c - a);
            var normalLength = normal.Length();
            if (!double.IsFinite(normalLength) || normalLength <= 0)
                return;
            normal /= normalLength;
            var normalEgo = double3.TransformNormal(normal, local2Ego);
            var normalSign = double3.Dot(normalEgo, ray.Direction) <= 0 ? 1.0 : -1.0;

            best = distance;
            found = true;
            bestResult = new PickResult(DecalAnchorKind.Parachute, vehicle, parachute.Parent,
                parachute, null, hitLocal, normal * normalSign, distance, 0.0,
                ParachuteCanopyIndex: parachute.CanopyIndex,
                ClothNodeA: ia, ClothNodeB: ib, ClothNodeC: ic,
                ClothBarycentric: barycentric, ClothNormalSign: normalSign);
        }
    }

    private static bool TryBarycentric(double3 point, double3 a, double3 b, double3 c,
        out double3 barycentric)
    {
        barycentric = default;
        var v0 = b - a;
        var v1 = c - a;
        var v2 = point - a;
        var d00 = double3.Dot(v0, v0);
        var d01 = double3.Dot(v0, v1);
        var d11 = double3.Dot(v1, v1);
        var d20 = double3.Dot(v2, v0);
        var d21 = double3.Dot(v2, v1);
        var denominator = d00 * d11 - d01 * d01;
        if (!double.IsFinite(denominator) || Math.Abs(denominator) <= 1e-12)
            return false;
        var v = (d11 * d20 - d01 * d21) / denominator;
        var w = (d00 * d21 - d01 * d20) / denominator;
        barycentric = new double3(1.0 - v - w, v, w);
        return double.IsFinite(barycentric.X) && double.IsFinite(barycentric.Y)
               && double.IsFinite(barycentric.Z);
    }
}
