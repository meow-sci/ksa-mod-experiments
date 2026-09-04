using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KSA;

namespace MeowSci.DontStifleMeLib;

/// <summary>
/// Expands individual vehicle-editor value ranges while preserving each instance's authored
/// limits so disabling the feature (or unloading the mod) restores stock behavior.
/// </summary>
public static class EditorValueLimitPatches
{
    private const string DrawParachuteSectionName = "DrawParachuteSection";
    private const float ParachuteMinimumDiameterM = 2f;
    private const float ParachuteMaximumDiameterM = 1000f;

    private static readonly Dictionary<Parachute, (float Min, float Max)> OriginalParachuteBounds = new();
    private static MethodInfo? _drawParachuteSection;
    private static MethodInfo? _setParachuteDiameter;

    public static bool IsApplied { get; private set; }

    public static void Apply(Harmony harmony)
    {
        if (IsApplied) return;

        _drawParachuteSection = AccessTools.Method(typeof(VehicleEditor), DrawParachuteSectionName)
            ?? throw new MissingMethodException(nameof(VehicleEditor), DrawParachuteSectionName);
        _setParachuteDiameter = AccessTools.Method(
            typeof(Parachute), nameof(Parachute.SetDiameter), new[] { typeof(float) })
            ?? throw new MissingMethodException(nameof(Parachute), nameof(Parachute.SetDiameter));
        harmony.Patch(_drawParachuteSection,
            prefix: new HarmonyMethod(typeof(EditorValueLimitPatches), nameof(DrawParachuteSectionPrefix)));
        harmony.Patch(_setParachuteDiameter,
            prefix: new HarmonyMethod(typeof(EditorValueLimitPatches), nameof(SetParachuteDiameterPrefix)));

        IsApplied = true;
        Console.WriteLine("dont-stifle-me: editor value-limit patches applied");
    }

    public static void Remove(Harmony harmony)
    {
        RestoreTrackedBounds();
        if (_drawParachuteSection != null)
        {
            harmony.Unpatch(_drawParachuteSection, HarmonyPatchType.Prefix, harmony.Id);
        }
        if (_setParachuteDiameter != null)
        {
            harmony.Unpatch(_setParachuteDiameter, HarmonyPatchType.Prefix, harmony.Id);
        }
        _drawParachuteSection = null;
        _setParachuteDiameter = null;
        IsApplied = false;
    }

    /// <summary>
    /// Immediately restores every live chute touched by this feature. The draw prefix will apply
    /// the expanded bounds again on the next editor frame if the checkbox remains enabled.
    /// </summary>
    public static void RestoreTrackedBounds()
    {
        foreach ((Parachute parachute, (float min, float max)) in OriginalParachuteBounds)
        {
            parachute.Tuning.MinDiameterM = min;
            parachute.Tuning.MaxDiameterM = max;
        }
        OriginalParachuteBounds.Clear();
    }

    private static void DrawParachuteSectionPrefix(Part part)
    {
        if (!EditorLimitSettings.JplSaidNoClamps)
        {
            RestoreTrackedBounds();
            return;
        }

        Span<Parachute> parachutes = part.SubtreeModules.Get<Parachute>();
        ExpandParachuteBounds(parachutes);
    }

    private static void SetParachuteDiameterPrefix(Parachute __instance)
    {
        if (!EditorLimitSettings.JplSaidNoClamps) return;

        // SetDiameter updates every chute module in this part. Expand the whole group first so
        // symmetry counterparts and multi-canopy parts do not silently clamp back to stock.
        ExpandParachuteBounds(__instance.Parent.Modules.Get<Parachute>());
    }

    private static void ExpandParachuteBounds(Span<Parachute> parachutes)
    {
        foreach (Parachute parachute in parachutes)
        {
            if (!OriginalParachuteBounds.ContainsKey(parachute))
            {
                OriginalParachuteBounds.Add(parachute,
                    (parachute.Tuning.MinDiameterM, parachute.Tuning.MaxDiameterM));
            }

            parachute.Tuning.MinDiameterM = ParachuteMinimumDiameterM;
            parachute.Tuning.MaxDiameterM = ParachuteMaximumDiameterM;
        }
    }
}
