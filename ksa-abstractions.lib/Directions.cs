using Brutal.Numerics;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Named unit-axis vectors in KSA's right-handed, Y-up, -Z-forward convention.
///
/// These replace <c>KSA.Double3Ex.Up/Down/Left/Right/Forward/Backward</c>, which the game removed in
/// build <c>2026.8.3.5117</c> (rev 5067: "Removed Double3Ex Up/Forward/etc. vectors as they were
/// misleading and often misused"). The game kept view-frame equivalents on <see cref="KSA.Camera"/>
/// (<c>ForwardView</c>/<c>RightView</c>/<c>UpView</c>) for genuine camera-space use; this class
/// carries the same values for the frame-agnostic cases, so the axis a caller means stays explicit
/// at the call site rather than implied by a game type.
///
/// Values are identical to the removed properties, so behavior is unchanged.
/// </summary>
public static class Directions
{
    /// <summary>+Y.</summary>
    public static double3 Up => double3.UnitY;

    /// <summary>-Y.</summary>
    public static double3 Down => -double3.UnitY;

    /// <summary>+X.</summary>
    public static double3 Right => double3.UnitX;

    /// <summary>-X.</summary>
    public static double3 Left => -double3.UnitX;

    /// <summary>-Z.</summary>
    public static double3 Forward => -double3.UnitZ;

    /// <summary>+Z.</summary>
    public static double3 Backward => double3.UnitZ;
}
