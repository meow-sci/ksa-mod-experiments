using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarblesLib;

/// <summary>
/// Stateless engine for celestial body weld computation.
/// </summary>
/// <remarks>
/// Timing contract (KSA 2026.8.x sim-step model): every method that mutates a celestial MUST run on the
/// main thread inside the window opened by <c>Program.PrepareFrame</c> after
/// <c>JobSystems.OrbitSolvers.Wait()</c> / <c>Universe.ApplyOrbitSolvers()</c> and before
/// <c>Universe.ExecuteNextOrbitSolvers()</c> queues the next <c>CelestialUpdateTask</c>s. Outside that
/// window a worker thread may be reading <c>Celestial.Orbit</c>, and the results it stages are written
/// back over whatever orbit is current, undoing the weld. <see cref="KiwisMarblesPatches"/> supplies
/// that window via a prefix on <c>Universe.ExecuteNextVehicleSolvers</c>.
/// </remarks>
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

        ApplyOrbit(entry.Source, newOrbit);
        return true;
    }

    /// <summary>Puts the source back on the orbit captured at weld time (no-op if none was captured).</summary>
    public static void RestoreOrbit(CelestialWeldEntry entry)
    {
        if (entry.Source == null || entry.OriginalOrbit == null)
            return;

        ApplyOrbit(entry.Source, entry.OriginalOrbit);
    }

    /// <summary>
    /// Swaps a celestial onto <paramref name="newOrbit"/>, fixing up the parent/child tree and refreshing
    /// the cached per-frame transforms of the body and everything orbiting it.
    /// </summary>
    /// <remarks>
    /// <c>Celestial.SetOrbit</c> is a bare property assignment: <c>Celestial.Parent</c> follows
    /// <c>Orbit.Parent</c> automatically, but the parents' <c>Children</c> lists (which drive
    /// <c>IParentBody.UpdatePerFrameDataTree</c> ordering and the orbit-tree UI) are not touched, so a
    /// cross-parent weld has to move the body between lists itself.
    /// </remarks>
    private static void ApplyOrbit(Celestial body, Orbit newOrbit)
    {
        IParentBody? oldParent = body.Parent;
        IParentBody? newParent = newOrbit.Parent;

        body.SetOrbit(newOrbit);

        if (!ReferenceEquals(oldParent, newParent))
            Reparent(body, oldParent, newParent);

        // Refresh the body and its whole subtree (moons, vehicles) so anything sampling positions later
        // this frame sees the welded state rather than the propagated one.
        ((IParentBody)body).UpdatePerFrameDataTree();
    }

    private static void Reparent(Celestial body, IParentBody? oldParent, IParentBody? newParent)
    {
        oldParent?.Children.Remove(body);
        if (newParent != null && !newParent.Children.Contains(body))
            newParent.Children.Add(body);

        Console.WriteLine($"kiwis-marbles: re-parented {body.Id}: {oldParent?.Id ?? "none"} -> {newParent?.Id ?? "none"}");
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
