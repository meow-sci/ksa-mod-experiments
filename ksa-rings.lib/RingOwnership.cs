using System;
using KSA;
namespace MeowSci.KsaRings;
/// <summary>Coordinates ring-reference replacement without dependencies between feature libraries.</summary>
public static class RingOwnership
{
    public static event Action<Celestial>? Replacing;
    public static void BeforeReplace(Celestial body) => Replacing?.Invoke(body);
}
