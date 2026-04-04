using System.Collections.Generic;
using System.Linq;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Static helpers to get celestial bodies from the current system.</summary>
public static class CelestialProvider
{
    /// <summary>Returns all Celestial objects (planets, moons) in the current system, excluding stars.</summary>
    public static List<Celestial> GetAllCelestials() =>
        Universe.CurrentSystem?.All.UnsafeAsList().OfType<Celestial>().ToList() ?? new List<Celestial>();

    /// <summary>Returns all IOrbiter objects (celestials + vehicles) in the current system.</summary>
    public static List<IOrbiter> GetAllOrbiters() =>
        Universe.CurrentSystem?.All.UnsafeAsList().OfType<IOrbiter>().ToList() ?? new List<IOrbiter>();
}
