using MeowSci.KsaRings;
using System;
using Brutal.Numerics;

namespace MeowSci.BloominOnionLib;

/// <summary>
/// Rasterizes a <see cref="RingDefinition"/>'s painted band into the two 1-row RGBA8 strips
/// the game's ring system samples: the color/alpha band (GPU sampled, also drives the ring
/// shadow on the planet) and the control strip (CPU + GPU sampled: R = rock field allowed,
/// G = volumetric thickness blend, B/A unused).
/// </summary>
public static class RingBandPainter
{
    public static int Width => RingDefinition.PainterWidth;

    /// <summary>The band strip as tightly packed RGBA8, <see cref="Width"/> texels wide, one row.</summary>
    public static byte[] PaintBand(RingDefinition definition)
    {
        var pixels = new byte[Width * 4];
        for (int x = 0; x < Width; x++)
        {
            float4 color = Evaluate(definition, x);
            pixels[x * 4 + 0] = ToByte(color.X);
            pixels[x * 4 + 1] = ToByte(color.Y);
            pixels[x * 4 + 2] = ToByte(color.Z);
            pixels[x * 4 + 3] = ToByte(color.W);
        }
        return pixels;
    }

    /// <summary>The control strip derived from the band's opacity.</summary>
    public static byte[] PaintControl(RingDefinition definition)
    {
        var pixels = new byte[Width * 4];
        float threshold = (float)Math.Clamp(definition.MeshCoverageThreshold, 0.0, 1.0);
        for (int x = 0; x < Width; x++)
        {
            float alpha = Evaluate(definition, x).W;
            float meshes = threshold >= 1f ? 0f : Math.Clamp((alpha - threshold) / 0.1f, 0f, 1f);
            pixels[x * 4 + 0] = ToByte(meshes);
            pixels[x * 4 + 1] = ToByte(alpha);
            pixels[x * 4 + 2] = 0;
            pixels[x * 4 + 3] = 255;
        }
        return pixels;
    }

    /// <summary>Stable id for the painted band of this definition: same paint settings, same id.</summary>
    public static string BandId(RingDefinition definition) => $"bloomin_onion/band_{HashPaint(definition):x8}";

    public static string ControlId(RingDefinition definition) =>
        $"bloomin_onion/control_{HashPaint(definition) ^ (uint)Math.Round(definition.MeshCoverageThreshold * 1000):x8}";

    /// <summary>Straight (non-premultiplied) RGBA at texel <paramref name="x"/>, 0..1 per channel.</summary>
    public static float4 Evaluate(RingDefinition definition, int x)
    {
        double t = (x + 0.5) / Width;
        float4 color = definition.BaseColor;
        foreach (var stripe in definition.Stripes)
        {
            float coverage = (float)(Coverage(t, stripe) * Math.Clamp(stripe.Color.W, 0f, 1f));
            if (coverage <= 0f) continue;
            // Standard "over" compositing: stripe color over what is already there.
            color.X = Lerp(color.X, stripe.Color.X, coverage);
            color.Y = Lerp(color.Y, stripe.Color.Y, coverage);
            color.Z = Lerp(color.Z, stripe.Color.Z, coverage);
            color.W = color.W + coverage * (1f - color.W);
        }

        if (definition.NoiseAmount > 0.0 && color.W > 0f)
        {
            double noise = Ringlets(t, definition.NoiseScale, definition.NoiseSeed);
            float scale = (float)(1.0 - definition.NoiseAmount * noise);
            color.W = Math.Clamp(color.W * scale, 0f, 1f);
            // A touch of the same noise on brightness sells the ringlet structure.
            float tint = 1f - (float)definition.NoiseAmount * 0.25f * (float)noise;
            color.X *= tint;
            color.Y *= tint;
            color.Z *= tint;
        }

        return new float4(Math.Clamp(color.X, 0f, 1f), Math.Clamp(color.Y, 0f, 1f),
            Math.Clamp(color.Z, 0f, 1f), Math.Clamp(color.W, 0f, 1f));
    }

    private static double Coverage(double t, RingStripe stripe)
    {
        double start = Math.Min(stripe.Start, stripe.End);
        double end = Math.Max(stripe.Start, stripe.End);
        double feather = Math.Max(stripe.Feather, 1e-6);
        double inner = SmoothStep((t - start) / feather);
        double outer = SmoothStep((end - t) / feather);
        return Math.Min(inner, outer);
    }

    /// <summary>Two octaves of value noise in 0..1 — fine "ringlet" banding across the ring.</summary>
    private static double Ringlets(double t, double scale, int seed)
    {
        double frequency = Math.Max(scale, 0.01);
        double coarse = ValueNoise(t * 96.0 * frequency, seed);
        double fine = ValueNoise(t * 384.0 * frequency, seed + 977);
        return Math.Clamp(coarse * 0.65 + fine * 0.35, 0.0, 1.0);
    }

    private static double ValueNoise(double x, int seed)
    {
        long cell = (long)Math.Floor(x);
        double fraction = x - cell;
        double a = Hash01(cell, seed);
        double b = Hash01(cell + 1, seed);
        double weight = fraction * fraction * (3.0 - 2.0 * fraction);
        return a + (b - a) * weight;
    }

    private static double Hash01(long cell, int seed)
    {
        unchecked
        {
            ulong h = (ulong)cell * 0x9E3779B97F4A7C15UL ^ ((ulong)(uint)seed * 0xBF58476D1CE4E5B9UL);
            h ^= h >> 31;
            h *= 0x94D049BB133111EBUL;
            h ^= h >> 29;
            return (h >> 11) * (1.0 / 9007199254740992.0);
        }
    }

    private static uint HashPaint(RingDefinition definition)
    {
        unchecked
        {
            uint hash = 2166136261u;
            void Mix(double value)
            {
                long bits = BitConverter.DoubleToInt64Bits(Math.Round(value, 6));
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(bits >> (i * 8));
                    hash *= 16777619u;
                }
            }
            Mix(definition.BaseColor.X); Mix(definition.BaseColor.Y); Mix(definition.BaseColor.Z); Mix(definition.BaseColor.W);
            Mix(definition.NoiseAmount); Mix(definition.NoiseScale); Mix(definition.NoiseSeed);
            foreach (var stripe in definition.Stripes)
            {
                Mix(stripe.Start); Mix(stripe.End); Mix(stripe.Feather);
                Mix(stripe.Color.X); Mix(stripe.Color.Y); Mix(stripe.Color.Z); Mix(stripe.Color.W);
            }
            return hash;
        }
    }

    private static double SmoothStep(double x)
    {
        x = Math.Clamp(x, 0.0, 1.0);
        return x * x * (3.0 - 2.0 * x);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static byte ToByte(float value) => (byte)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);
}
