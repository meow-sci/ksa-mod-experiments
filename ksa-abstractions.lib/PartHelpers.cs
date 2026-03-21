using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>Static helpers for part tree traversal.</summary>
public static class PartHelpers
{
    /// <summary>Returns all parts in the vehicle's part tree, traversed recursively via SubParts.</summary>
    public static List<Part> GetAllParts(Vehicle vehicle)
    {
        var result = new List<Part>();
        foreach (var part in vehicle.Parts.Parts)
            CollectPartsRecursive(part, result);
        return result;
    }

    /// <summary>Returns all parts in the vehicle's part tree matching the given predicate.</summary>
    public static List<Part> GetPartsWhere(Vehicle vehicle, Func<Part, bool> predicate)
    {
        var result = new List<Part>();
        foreach (var part in GetAllParts(vehicle))
            if (predicate(part))
                result.Add(part);
        return result;
    }

    private static void CollectPartsRecursive(Part part, List<Part> result)
    {
        result.Add(part);
        foreach (var sub in part.SubParts)
            CollectPartsRecursive(sub, result);
    }
}
