using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarblesLib;

/// <summary>Stateless engine for celestial body weld computation.</summary>
public static class CelestialWeldEngine
{
    /// <summary>
    /// Repositions the source celestial body to maintain its weld relative to the target.
    /// Returns false if the weld should be removed (source or target is null/invalid).
    /// </summary>
    public static bool UpdateWeld(CelestialWeldEntry entry)
    {
        if (entry.Source == null || entry.Target == null)
            return false;

        if (entry.Target.Orbit == null || entry.Target.Parent == null)
            return false;

        double3 tgtPosCci = entry.Target.GetPositionCci();
        double3 tgtVelCci = entry.Target.GetVelocityCci();
        IParentBody parent = entry.Target.Parent;

        double3 newSrcPosCci = tgtPosCci + entry.Offset;
        double3 newSrcVelCci = tgtVelCci;

        Orbit newOrbit = Orbit.CreateFromStateCci(
            parent,
            SimTimeProvider.GetElapsedTime(),
            newSrcPosCci,
            newSrcVelCci,
            entry.Source.OrbitColor
        );

        entry.Source.SetOrbit(newOrbit);
        entry.Source.UpdatePerFrameData();
        return true;
    }

    /// <summary>
    /// Returns welds sorted so that a target celestial is always processed before
    /// any source that depends on it. Uses Kahn's topological sort (Kahn 1962).
    /// If a cycle is detected, the original order is returned unchanged.
    /// </summary>
    public static List<CelestialWeldEntry> TopologicalSort(List<CelestialWeldEntry> welds)
    {
        var inDegree = new Dictionary<CelestialWeldEntry, int>();
        var adj = new Dictionary<CelestialWeldEntry, List<CelestialWeldEntry>>();

        foreach (var w in welds)
        {
            inDegree[w] = 0;
            adj[w] = new List<CelestialWeldEntry>();
        }

        // Build edges: if x's source is the same body as y's target,
        // x must be processed before y (x moves the body; y then follows it).
        foreach (var x in welds)
        {
            foreach (var y in welds)
            {
                if ((IOrbiter)x.Source == y.Target)
                {
                    adj[x].Add(y);
                    inDegree[y]++;
                }
            }
        }

        var queue = new Queue<CelestialWeldEntry>();
        foreach (var w in welds)
            if (inDegree[w] == 0)
                queue.Enqueue(w);

        var sorted = new List<CelestialWeldEntry>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);
            foreach (var neighbor in adj[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count == welds.Count)
            return sorted;

        Console.WriteLine("kiwis-marbles: TopologicalSort: cycle detected, leaving order as-is.");
        return new List<CelestialWeldEntry>(welds);
    }
}
