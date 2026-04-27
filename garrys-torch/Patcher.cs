using System;
using HarmonyLib;
using KSA;
using MeowSci.GarrysTorchLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorch;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("garrys-torch");
            GarrysTorchSolverPatch.Apply(_harmony);
            HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"garrys-torch: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("garrys-torch");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"garrys-torch: Error removing patches: {ex.Message}");
        }
    }

}

internal static class GarrysTorchSolverPatch
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefix = new HarmonyMethod(typeof(GarrysTorchSolverPatch), nameof(BeforeVehicleSolvers))
        {
            priority = Priority.First
        };
        harmony.Patch(original, prefix: prefix);
    }

    private static void BeforeVehicleSolvers(double dtPlayer)
    {
        try
        {
            GarrysTorchSubmod.Instance?.UpdateBeforeVehicleSolvers(dtPlayer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"garrys-torch: Error updating welds before vehicle solvers: {ex.Message}");
        }
    }
}
