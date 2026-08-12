using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Wrapper for universe simulation time.</summary>
public static class SimTimeProvider
{
    /// <summary>Returns the current elapsed simulation time as a UniverseTime value.</summary>
    /// <remarks>
    /// KSA build 2026.8.19.5261 (rev 5211) replaced <c>SimTime</c> with <c>UniverseTime</c>,
    /// backed by 128-bit nanoseconds instead of double seconds, and renamed
    /// <c>Universe.GetElapsedSimTime()</c> to <c>Universe.GetElapsedTime()</c>.
    /// <c>Seconds()</c> still returns a double on the new type, so callers are unaffected.
    /// </remarks>
    public static UniverseTime GetElapsedTime() => Universe.GetElapsedTime();
}
