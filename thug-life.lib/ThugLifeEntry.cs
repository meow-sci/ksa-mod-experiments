using Brutal.Numerics;
using KSA;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// A single anchored thug-life quad. Position/Rotation are interpreted in the
/// anchor part's local frame. Width/Height set the quad's size in meters.
/// </summary>
public sealed class ThugLifeEntry
{
    public Vehicle Vehicle = null!;
    public Part Part = null!;

    /// <summary>Offset in the part's local frame (meters).</summary>
    public float3 Position = new(0f, 0f, 0f);

    /// <summary>Pitch/Yaw/Roll in degrees, applied in the part's local frame.</summary>
    public float3 Rotation = new(0f, 0f, 0f);

    /// <summary>Quad width in meters.</summary>
    public float Width = 0.6f;

    /// <summary>Quad height in meters. Default keeps the 15:4 aspect ratio of the texture.</summary>
    public float Height = 0.16f;

    /// <summary>If false the entry stays in the list but is skipped during rendering.</summary>
    public bool Visible = true;
}
