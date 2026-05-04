using System;
using HarmonyLib;
using KSA;
using MeowSci.EternalFlameLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.EternalFlame;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony;

    public static void Patch()
    {
        try
        {
            _harmony ??= new Harmony("eternal-flame");
            EternalFlameSolverPatch.Apply(_harmony);
            HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error applying patches: {ex.Message}\n{ex}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("eternal-flame");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error removing patches: {ex.Message}");
        }
    }
}

internal static class EternalFlameSolverPatch
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefixMethod = AccessTools.Method(typeof(EternalFlameSolverPatch), nameof(BeforeVehicleSolvers));

        if (original == null)
            throw new MissingMethodException(typeof(Universe).FullName, nameof(Universe.ExecuteNextVehicleSolvers));
        if (prefixMethod == null)
            throw new MissingMethodException(typeof(EternalFlameSolverPatch).FullName, nameof(BeforeVehicleSolvers));

        harmony.Patch(original, prefix: new HarmonyMethod(prefixMethod) { priority = Priority.First });
    }

    private static void BeforeVehicleSolvers()
    {
        try
        {
            EternalFlameSubmod.Instance?.UpdateBeforeVehicleSolvers();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"eternal-flame: Error in solver prefix: {ex.Message}\n{ex}");
        }
    }
}

