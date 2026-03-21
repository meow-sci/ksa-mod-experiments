using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Wrapper for universe simulation time.</summary>
public static class SimTimeProvider
{
    /// <summary>Returns the current elapsed simulation time as a SimTime value.</summary>
    public static SimTime GetElapsedTime() => Universe.GetElapsedSimTime();
}
