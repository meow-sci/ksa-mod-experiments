using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Lighting;

namespace MeowSci.SpaceTapeLib;

/// <summary>Lighting arrangement mode for the part editor workspace.</summary>
public enum LightArrangement { Off, BoxCorners, Sphere }

/// <summary>
/// Manages helper point lights around the part editor workspace to improve visibility.
/// Lights are added each frame during the render pass and do not affect the part being edited.
/// </summary>
public sealed class EditorLighting
{
    public LightArrangement Arrangement { get; set; } = LightArrangement.Off;
    public float Intensity { get; set; } = 3f;
    public float Range { get; set; } = 15f;
    public float3 Color { get; set; } = new float3(1f, 1f, 1f);
    public float Radius { get; set; } = 3f;

    // Sphere mode settings
    public int LightsPerRing { get; set; } = 4;
    public int Rings { get; set; } = 3;

    /// <summary>
    /// Adds point lights to the LightSystem for the current frame.
    /// Call once per frame from the render patch when the editor scene is active.
    /// </summary>
    public void UpdateLights(double4x4 matrixAsmb2Ego)
    {
        if (Arrangement == LightArrangement.Off) return;

        var positions = CalculatePositions();
        float3 lightColor = Color;

        foreach (double3 posAsmb in positions)
        {
            double3 posEgo = posAsmb.Transform(matrixAsmb2Ego);
            Program.LightSystem.CreateLightInstance(
                new PointLight(posEgo, Range, lightColor, Intensity));
        }
    }

    private List<double3> CalculatePositions()
    {
        var positions = new List<double3>();
        double r = Radius;

        switch (Arrangement)
        {
            case LightArrangement.BoxCorners:
                // 8 corners of a cube centered on origin
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
                    // Distribute rings from top to bottom, excluding poles
                    double elevation = Math.PI * (ri + 1) / (rings + 1) - Math.PI / 2.0;
                    double cosElev = Math.Cos(elevation);
                    double sinElev = Math.Sin(elevation);

                    for (int ai = 0; ai < perRing; ai++)
                    {
                        double azimuth = 2.0 * Math.PI * ai / perRing;
                        double x = r * cosElev * Math.Cos(azimuth);
                        double y = r * cosElev * Math.Sin(azimuth);
                        double z = r * sinElev;
                        positions.Add(new double3(x, y, z));
                    }
                }
                break;
        }

        return positions;
    }
}
