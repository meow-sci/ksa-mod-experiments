using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.KitchenSinkLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.KitchenSink;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("kitchen-sink");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null)
            {
                HotkeyGuard.Patch(_harmony);
                IvaForceRender.Patch(_harmony);
                KitchenSinkSolverPatch.Apply(_harmony);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitchen-sink: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                IvaForceRender.Unpatch(_harmony);
                HotkeyGuard.Unpatch(_harmony);
            }
            _harmony?.UnpatchAll("kitchen-sink");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitchen-sink: Error removing patches: {ex.Message}");
        }
    }
}

internal static class KitchenSinkSolverPatch
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefix = new HarmonyMethod(typeof(KitchenSinkSolverPatch), nameof(BeforeVehicleSolvers))
        {
            priority = Priority.First
        };
        harmony.Patch(original, prefix: prefix);
    }

    private static void BeforeVehicleSolvers(double dtPlayer)
    {
        try
        {
            KitchenSinkSubmod.Instance?.UpdateBeforeVehicleSolvers(dtPlayer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitchen-sink: Error in solver prefix: {ex.Message}");
        }
    }
}
