using System;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.PyroLib;

/// <summary>
/// Harmony surface for pyro. Postfixes <see cref="Vehicle.AddVolumetricExhaustInstances"/> — the per-frame,
/// per-visible-vehicle call where the game submits its own engine plumes — so pyro's plumes are added to the
/// same renderer batch with the same camera and delta time. Applied by both the standalone host and unscience.
/// </summary>
public static class PyroPatches
{
    private static MethodBase? Target() =>
        AccessTools.Method(typeof(Vehicle), nameof(Vehicle.AddVolumetricExhaustInstances));

    private static MethodInfo Postfix() =>
        AccessTools.Method(typeof(PyroPatches), nameof(AfterAddVolumetricExhaustInstances))!;

    public static void Apply(Harmony harmony)
    {
        var original = Target();
        if (original == null)
            throw new MissingMethodException(typeof(Vehicle).FullName, nameof(Vehicle.AddVolumetricExhaustInstances));
        harmony.Patch(original, postfix: new HarmonyMethod(Postfix()));
    }

    public static void Remove(Harmony harmony)
    {
        var original = Target();
        if (original != null) harmony.Unpatch(original, Postfix());
    }

    private static void AfterAddVolumetricExhaustInstances(Vehicle __instance, Camera camera,
        VolumetricExhaustRenderer renderer, double frameDeltaTime)
    {
        try
        {
            PyroSubmod.Instance?.SubmitPlumes(__instance, camera, renderer, frameDeltaTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"pyro: error submitting plumes: {ex.Message}");
        }
    }
}
