using System;
using HarmonyLib;
using KSA;
using MeowSci.BlinkyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.EternalFlameLib;
using MeowSci.GlassLib;
using MeowSci.GarrysTorchLib;
using MeowSci.IFeelSeenLib;
using MeowSci.HumbleArteestLib;
using MeowSci.ItsSoShinyLib;
using MeowSci.KsaAbstractions;
using MeowSci.FlexoLib;
using MeowSci.ThugLifeLib;

namespace MeowSci.Unscience;

internal static class Patcher
{
    private static Harmony? _harmony;

    public static VehicleTracker? IFeelSeenTracker { private get; set; }
    public static KeyframeSequencePlayer? CameraSequencePlayer { private get; set; }
    public static Action? MenuBarToggle { get; set; }

    public static void Patch()
    {
        try
        {
            _harmony = new Harmony("MeowSci.Unscience");
            HotkeyGuard.Patch(_harmony);
            // Apply our render postfix FIRST so a failure further down the chain
            // doesn't prevent thug-life from registering.
            ThugLifeRenderPatches.Apply(_harmony);
            MenuBarPatch.ToggleWindow = MenuBarToggle;
            MenuBarPatch.Apply(_harmony);
            BlinkyPatches.Apply(_harmony);
            ShinyPatches.Apply(_harmony);
            CameraControllerOverridePatches.SequencePlayer = CameraSequencePlayer;
            CameraControllerOverridePatches.Apply(_harmony);
            EternalFlamePatches.Apply(_harmony);
            // garrys-torch no longer registers a Harmony patch — its weld physics
            // runs from unscience/Mod.cs OnAfterUi via GarrysTorchSubmod.UpdateWelds,
            // which internally calls JobSystems.VehicleSolvers.Wait() to avoid the
            // worker-thread races that any other timing produces.
            GlassPatches.Apply(_harmony);
            IFeelSeenPatches.Apply(_harmony, IFeelSeenTracker!);
            VehiclePaintPatches.Apply(_harmony);
            EngineEmissivePatches.Apply(_harmony);
            FlexoPatches.Apply(_harmony);
            Console.WriteLine("unscience: Harmony patches applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null)
            {
                HotkeyGuard.Unpatch(_harmony);
                MenuBarPatch.Remove(_harmony);
                BlinkyPatches.Remove(_harmony);
                ShinyPatches.Remove(_harmony);
                CameraControllerOverridePatches.Remove(_harmony);
                EternalFlamePatches.Remove(_harmony);
                GlassPatches.Remove(_harmony);
                IFeelSeenPatches.Remove(_harmony);
                EngineEmissivePatches.Remove(_harmony);
                FlexoPatches.Remove(_harmony);
                VehiclePaintPatches.Remove(_harmony);
                ThugLifeRenderPatches.Remove(_harmony);
            }
            VehiclePaint.Cleanup();
            EngineEmissive.Cleanup();
            _harmony = null;
            IFeelSeenTracker = null;
            CameraSequencePlayer = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience: Error removing patches: {ex.Message}");
        }
    }
}

internal static class EternalFlamePatches
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefixMethod = AccessTools.Method(typeof(EternalFlamePatches), nameof(BeforeVehicleSolvers));

        if (original == null)
            throw new MissingMethodException(typeof(Universe).FullName, nameof(Universe.ExecuteNextVehicleSolvers));
        if (prefixMethod == null)
            throw new MissingMethodException(typeof(EternalFlamePatches).FullName, nameof(BeforeVehicleSolvers));

        harmony.Patch(original, prefix: new HarmonyMethod(prefixMethod) { priority = Priority.First });
    }

    public static void Remove(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefixMethod = AccessTools.Method(typeof(EternalFlamePatches), nameof(BeforeVehicleSolvers));
        if (original != null && prefixMethod != null)
            harmony.Unpatch(original, prefixMethod);
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

