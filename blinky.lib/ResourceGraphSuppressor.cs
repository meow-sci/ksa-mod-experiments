namespace MeowSci.BlinkyLib;

/// <summary>
/// Tracks suppression state for the <c>PartTree.RecomputeAllDerivedData()</c> Harmony patch.
/// When <see cref="IsSuppressed"/> is true the Harmony prefix skips the original method,
/// allowing batch part creation without triggering N expensive recomputes.
/// Call <see cref="Suppress"/> / <see cref="Unsuppress"/> in matched pairs and then invoke
/// <c>vehicle.Parts.RecomputeAllDerivedData()</c> once after all parts are added.
/// </summary>
public static class ResourceGraphSuppressor
{
    private static int _depth = 0;

    /// <summary>Returns true when RecomputeAllDerivedData calls should be skipped.</summary>
    public static bool IsSuppressed => _depth > 0;

    /// <summary>Enter suppression scope. Supports nested calls.</summary>
    public static void Suppress() => _depth++;

    /// <summary>Exit suppression scope. Only lifts when all Suppress() calls are matched.</summary>
    public static void Unsuppress()
    {
        if (_depth > 0) _depth--;
    }
}
