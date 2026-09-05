using System;
using System.Collections.Generic;
using Brutal.Numerics;

namespace MeowSci.ZippoLib;

/// <summary>Detached authoring recipe. Each running light receives its own deep copy.</summary>
public sealed class DiscoRecipe
{
    public bool Color = true;
    public bool Actuation;
    public bool Spread;
    public bool RandomColors;
    public List<float3> Palette = new() { new(1, 0, .2f), new(0, .8f, 1), new(.6f, 0, 1) };
    public DiscoTiming ColorTiming = new();
    public DiscoTiming ActuationTiming = new();
    public DiscoTiming SpreadTiming = new();
    public float ActuationMin;
    public float ActuationMax = 1;
    // Cone half-angles, in degrees. Always inner <= outer at both endpoints.
    public float InnerMin = 5, OuterMin = 15, InnerMax = 25, OuterMax = 45;

    public void Validate()
    {
        if (Palette == null || Palette.Count < 1 || Palette.Count > 32
            || Palette.Exists(c => !Unit(c.X) || !Unit(c.Y) || !Unit(c.Z))
            || ColorTiming == null || ActuationTiming == null || SpreadTiming == null
            || !Unit(ActuationMin) || !Unit(ActuationMax) || ActuationMin > ActuationMax
            || !Angle(InnerMin) || !Angle(InnerMax) || !Angle(OuterMin) || !Angle(OuterMax)
            || InnerMin > OuterMin || InnerMax > OuterMax)
            throw new InvalidOperationException("Invalid Disco palette, actuation range, or cone angles.");
        ColorTiming.Validate(); ActuationTiming.Validate(); SpreadTiming.Validate();
    }
    private static bool Unit(float v) => float.IsFinite(v) && v >= 0 && v <= 1;
    private static bool Angle(float v) => float.IsFinite(v) && v >= .1f && v <= 89;
}
