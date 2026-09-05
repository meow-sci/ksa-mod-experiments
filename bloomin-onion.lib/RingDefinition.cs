using MeowSci.KsaRings;
using System.Collections.Generic;
using Brutal.Numerics;

namespace MeowSci.BloominOnionLib;

/// <summary>Where the 2D ring band (and its control strip) comes from.</summary>
public enum RingBandSource
{
    /// <summary>Generated at runtime from <see cref="RingDefinition.Stripes"/> and the noise settings.</summary>
    Painted,
    /// <summary>An existing game texture picked from the asset catalog.</summary>
    Texture,
}

/// <summary>One colored stripe of a painted ring band. Positions are fractions of inner→outer radius.</summary>
public sealed class RingStripe
{
    public double Start;
    public double End;
    /// <summary>RGB = stripe color, A = opacity of the stripe (0 = invisible gap, 1 = opaque).</summary>
    public float4 Color;
    /// <summary>Edge softness as a fraction of the ring width (0 = hard edge).</summary>
    public double Feather;

    public RingStripe(double start, double end, float4 color, double feather = 0.01)
    {
        Start = start;
        End = end;
        Color = color;
        Feather = feather;
    }

    public RingStripe Clone() => new(Start, End, Color, Feather);
}

/// <summary>One instanced rock-field LOD: the screen size it switches in at and its mesh.</summary>
public sealed class RingLodDefinition
{
    public float MinScreenSizePixels;
    /// <summary>Catalog mesh id; "" uses the stock ring rock mesh for this LOD slot.</summary>
    public string MeshId;

    public RingLodDefinition(float minScreenSizePixels, string meshId = "")
    {
        MinScreenSizePixels = minScreenSizePixels;
        MeshId = meshId;
    }

    public RingLodDefinition Clone() => new(MinScreenSizePixels, MeshId);
}

/// <summary>
/// Everything KSA lets XML data say about a planetary ring, as a plain editable model.
/// Defaults are Saturn's stock values so a fresh definition renders sensibly anywhere.
/// Empty asset ids mean "use the stock Saturn asset for that slot".
/// </summary>
public sealed class RingDefinition
{
    public const int MaxLods = 5;
    public const int PainterWidth = 2048;

    public string Name = "New Ring";

    // --- Geometry ---
    /// <summary>False = Equatorial frame (relative to the body's spin axis). True = Ecliptic frame (needs a parent body).</summary>
    public bool UseEclipticFrame;
    public double InclinationDeg = 0.0001;
    public double LongitudeOfAscendingNodeDeg;
    public double InnerRadiusKm = 69000.0;
    public double OuterRadiusKm = 313900.0;
    /// <summary>Scale of the procedural noise bands the shader overlays on the band texture.</summary>
    public double DetailScale = 300.0;

    // --- Band (2D strip) ---
    public RingBandSource BandSource = RingBandSource.Painted;
    public string BandTextureId = "";
    public string ControlTextureId = "";
    /// <summary>Painted mode: color/opacity of the ring where no stripe covers it.</summary>
    public float4 BaseColor = new(0.55f, 0.5f, 0.42f, 0.0f);
    public List<RingStripe> Stripes = new();
    /// <summary>Painted mode: strength of fine ringlet noise multiplied into the opacity (0 = none).</summary>
    public double NoiseAmount = 0.35;
    /// <summary>Painted mode: ringlet frequency multiplier.</summary>
    public double NoiseScale = 1.0;
    public int NoiseSeed = 1;
    /// <summary>Painted mode: opacity above which the rock field is allowed to draw (control texture R).</summary>
    public double MeshCoverageThreshold = 0.15;

    // --- Volumetric dust ---
    public double VolumeMinThicknessKm = 1.25;
    public double VolumeMaxThicknessKm = 4000.0;
    public double VolumeMinRenderDistanceKm = 1000.0;
    public double VolumeMaxRenderDistanceKm = 100000.0;
    public float StepScale = 0.006f;
    public double StepMinSizeKm = 0.1;
    public double StepMaxSizeKm = 1000.0;
    public bool FadeToMeshes = true;

    // --- Rock field (ring objects) ---
    public string ObjectsName = "BloominOnionRocks";
    public double ObjectSizeM = 10.0;
    public double ObjectThicknessKm = 1.0;
    public double ObjectRenderDistanceKm = 20.0;
    public double ObjectDensityPerKm3 = 3125.0;
    public List<RingLodDefinition> Lods = new();
    public string DiffuseId = "";
    public string NormalId = "";
    public string PbrId = "";

    /// <summary>A Saturn-like painted ring with the stock LOD ladder.</summary>
    public static RingDefinition CreateDefault()
    {
        var definition = new RingDefinition();
        definition.ResetStripesToSaturnLike();
        definition.ResetLodsToStock();
        return definition;
    }

    public void ResetStripesToSaturnLike()
    {
        Stripes.Clear();
        Stripes.Add(new RingStripe(0.00, 0.17, new float4(0.62f, 0.58f, 0.50f, 0.30f), 0.02)); // C ring - faint
        Stripes.Add(new RingStripe(0.17, 0.45, new float4(0.86f, 0.80f, 0.66f, 0.95f), 0.01)); // B ring - bright
        Stripes.Add(new RingStripe(0.50, 0.75, new float4(0.80f, 0.74f, 0.62f, 0.80f), 0.01)); // A ring
        Stripes.Add(new RingStripe(0.66, 0.67, new float4(0.30f, 0.28f, 0.25f, 0.20f), 0.003)); // Encke-ish gap
        Stripes.Add(new RingStripe(0.78, 0.79, new float4(0.90f, 0.88f, 0.80f, 0.55f), 0.004)); // F ring - thin
    }

    public void ResetLodsToStock()
    {
        Lods.Clear();
        Lods.Add(new RingLodDefinition(32f));
        Lods.Add(new RingLodDefinition(16f));
        Lods.Add(new RingLodDefinition(8f));
        Lods.Add(new RingLodDefinition(4f));
        Lods.Add(new RingLodDefinition(2f));
    }

    public RingDefinition Clone()
    {
        var clone = (RingDefinition)MemberwiseClone();
        clone.Stripes = new List<RingStripe>(Stripes.Count);
        foreach (var stripe in Stripes) clone.Stripes.Add(stripe.Clone());
        clone.Lods = new List<RingLodDefinition>(Lods.Count);
        foreach (var lod in Lods) clone.Lods.Add(lod.Clone());
        return clone;
    }
}
