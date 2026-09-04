using System;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.HotPursuitLib;

/// <summary>Same-frame pose seam shared by the standalone host and unscience.</summary>
public static class HotPursuitPatches
{
    private static MethodBase? Target() =>
        AccessTools.Method(typeof(FixedController), nameof(FixedController.OnFrame),
            new[] { typeof(IViewport), typeof(double) });

    public static void Apply(Harmony harmony)
    {
        var original = Target();
        var prefix = AccessTools.Method(typeof(HotPursuitPatches), nameof(BeforeFixedFrame));
        if (original == null || prefix == null)
            throw new MissingMethodException(typeof(FixedController).FullName, nameof(FixedController.OnFrame));
        harmony.Patch(original, prefix: new HarmonyMethod(prefix));
    }

    public static void Remove(Harmony harmony)
    {
        var original = Target();
        var prefix = AccessTools.Method(typeof(HotPursuitPatches), nameof(BeforeFixedFrame));
        if (original != null && prefix != null)
            harmony.Unpatch(original, prefix);
    }

    private static bool BeforeFixedFrame(IViewport inViewport)
    {
        try
        {
            // False skips only the stock controller update for a Hot Pursuit-owned viewport.
            // GameViewport still calls Camera.OnFrame immediately afterward to bake the view.
            return !(HotPursuitSubmod.Instance?.ApplyFixedPose(inViewport) ?? false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"hot-pursuit: fixed-camera pose error: {ex.Message}");
            // ApplyFixedPose can throw only after matching one of our entries. Keep the camera's
            // last valid pose instead of running stock FixedController with its zero direction.
            return false;
        }
    }
}
