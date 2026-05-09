using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Lighting;

namespace MeowSci.FlexoLib.Editor;

public enum LightArrangement { Off, BoxCorners, Sphere }

public sealed class FlexoEditorLighting
{
    public LightArrangement Arrangement { get; set; } = LightArrangement.Off;
    public float Intensity { get; set; } = 3f;
    public float Range { get; set; } = 15f;
    public float3 Color { get; set; } = new float3(1f, 1f, 1f);
    public float Radius { get; set; } = 3f;
    public int LightsPerRing { get; set; } = 4;
    public int Rings { get; set; } = 3;

    public void UpdateLights(double4x4 matrixAsmb2Ego)
    {
        if (Arrangement == LightArrangement.Off) return;

        var positions = CalculatePositions();
        float3 lightColor = Color;

        foreach (double3 posAsmb in positions)
        {
            double3 posEgo = posAsmb.Transform(matrixAsmb2Ego);
            Program.LightSystem.CreateLightInstance(
                Light.CreatePointLight(posEgo, Range, lightColor, Intensity));
        }
    }

    private List<double3> CalculatePositions()
    {
        var positions = new List<double3>();
        double r = Radius;

        switch (Arrangement)
        {
            case LightArrangement.BoxCorners:
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    positions.Add(new double3(x * r, y * r, z * r));
                break;

            case LightArrangement.Sphere:
                int rings = Math.Max(1, Rings);
                int perRing = Math.Max(2, LightsPerRing);
                for (int ri = 0; ri < rings; ri++)
                {
                    double elevation = Math.PI * (ri + 1) / (rings + 1) - Math.PI / 2.0;
                    double cosElev = Math.Cos(elevation);
                    double sinElev = Math.Sin(elevation);
                    for (int ai = 0; ai < perRing; ai++)
                    {
                        double azimuth = 2.0 * Math.PI * ai / perRing;
                        positions.Add(new double3(
                            r * cosElev * Math.Cos(azimuth),
                            r * cosElev * Math.Sin(azimuth),
                            r * sinElev));
                    }
                }
                break;
        }

        return positions;
    }
}
