using Brutal.Numerics;
using KSA;

namespace MeowSci.KiwisMarblesLib;

/// <summary>Represents a single active weld that locks a celestial body's position relative to an orbiter.</summary>
public class CelestialWeldEntry
{
    /// <summary>The celestial body being repositioned.</summary>
    public Celestial Source = null!;

    /// <summary>The orbiter the source follows (can be Celestial or Vehicle).</summary>
    public IOrbiter Target = null!;

    /// <summary>
    /// Offset from the target's CCI position, in meters.
    /// Applied directly in the CCI frame (not rotated by any body frame).
    /// Use large values — planetary distances are typically millions to billions of meters.
    /// </summary>
    public double3 Offset;
}
