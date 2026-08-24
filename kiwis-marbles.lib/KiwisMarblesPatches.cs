using System;
using HarmonyLib;
using KSA;

namespace MeowSci.KiwisMarblesLib;

/// <summary>
/// Harmony hook that runs the weld update at the one safe point in the frame.
/// </summary>
/// <remarks>
/// <c>Program.PrepareFrame</c> waits on the orbit + vehicle job schedulers, applies their staged results
/// (<c>Universe.ApplyOrbitSolvers</c> → <c>Orbit.UpdatePosition</c>, <c>Universe.ApplyVehicleSolvers</c> →
/// <c>CelestialSystem.UpdatePerFrameData</c>), then calls <c>Universe.ExecuteNextVehicleSolvers</c> and
/// <c>Universe.ExecuteNextOrbitSolvers</c> to queue the next sim step on worker threads. A prefix on
/// <c>ExecuteNextVehicleSolvers</c> therefore runs with every position current, no worker in flight, and
/// before the next <c>CelestialUpdateTask</c> snapshots <c>Celestial.Orbit</c> — so the welded orbit is what
/// gets propagated instead of being overwritten. Same hook the other solver-timed submods use
/// (eternal-flame, kitchen-sink).
/// </remarks>
public static class KiwisMarblesPatches
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefixMethod = AccessTools.Method(typeof(KiwisMarblesPatches), nameof(BeforeVehicleSolvers));

        if (original == null)
            throw new MissingMethodException(typeof(Universe).FullName, nameof(Universe.ExecuteNextVehicleSolvers));
        if (prefixMethod == null)
            throw new MissingMethodException(typeof(KiwisMarblesPatches).FullName, nameof(BeforeVehicleSolvers));

        harmony.Patch(original, prefix: new HarmonyMethod(prefixMethod) { priority = Priority.First });
    }

    public static void Remove(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefixMethod = AccessTools.Method(typeof(KiwisMarblesPatches), nameof(BeforeVehicleSolvers));
        if (original != null && prefixMethod != null)
            harmony.Unpatch(original, prefixMethod);
    }

    private static void BeforeVehicleSolvers()
    {
        try
        {
            KiwisMarblesSubmod.Instance?.UpdateBeforeVehicleSolvers();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kiwis-marbles: Error in solver prefix: {ex.Message}\n{ex}");
        }
    }
}
