using System;
using HarmonyLib;
using KSA;
using MeowSci.BlinkyLib;
using MeowSci.CameraControllerOverrideLib;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.GlassLib;
using MeowSci.GarrysTorchLib;
using MeowSci.IFeelSeenLib;
using MeowSci.HumbleArteestLib;
using MeowSci.KsaAbstractions;
using MeowSci.FlexoLib;

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
            MenuBarPatch.ToggleWindow = MenuBarToggle;
            MenuBarPatch.Apply(_harmony);
            BlinkyPatches.Apply(_harmony);
            CameraControllerOverridePatches.SequencePlayer = CameraSequencePlayer;
            CameraControllerOverridePatches.Apply(_harmony);
            GarrysTorchPatches.Apply(_harmony);
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
                CameraControllerOverridePatches.Remove(_harmony);
                GarrysTorchPatches.Remove(_harmony);
                GlassPatches.Remove(_harmony);
                IFeelSeenPatches.Remove(_harmony);
                EngineEmissivePatches.Remove(_harmony);
                FlexoPatches.Remove(_harmony);
                VehiclePaintPatches.Remove(_harmony);
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

internal static class GarrysTorchPatches
{
    public static void Apply(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        var prefix = new HarmonyMethod(typeof(GarrysTorchPatches), nameof(BeforeVehicleSolvers))
        {
            priority = Priority.First
        };
        harmony.Patch(original, prefix: prefix);
    }

    public static void Remove(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
        harmony.Unpatch(original, AccessTools.Method(typeof(GarrysTorchPatches), nameof(BeforeVehicleSolvers)));
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
