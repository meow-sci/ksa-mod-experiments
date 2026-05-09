using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.RedAlertLib;

/// <summary>Reflection helpers for KSA LightModule operations (color + intensity).</summary>
internal static class LightActions
{
    private static readonly BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static List<object> GetLightComponents(PartTemplate t)
    {
        var result = new List<object>();
        if (ReflectionHelpers.GetFieldValue(t, "Components") is not IList comps) return result;
        for (int i = 0; i < comps.Count; i++)
        {
            var c = comps[i];
            if (c?.GetType().FullName == "KSA.LightModule+TemplateData")
                result.Add(c);
        }
        return result;
    }

    public static void ApplyColor(Part part, float3 color)
    {
        if (part.Template == null) return;
        foreach (var light in GetLightComponents(part.Template))
        {
            var colorRef = ReflectionHelpers.GetFieldValue(light, "Color");
            if (colorRef == null) continue;
            ReflectionHelpers.SetFieldValue(colorRef, "R", color.X);
            ReflectionHelpers.SetFieldValue(colorRef, "G", color.Y);
            ReflectionHelpers.SetFieldValue(colorRef, "B", color.Z);
            try { colorRef.GetType().GetMethod("OnDataLoad", All)?.Invoke(colorRef, new object?[] { null }); }
            catch (Exception ex) { Console.WriteLine($"red-alert: SetColor OnDataLoad error: {ex.Message}"); }
        }
    }

    public static void SetEnabled(Part part, bool enabled)
    {
        var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
        if (ls != null) ls.LightIsActive = enabled;
    }

    public static bool? GetEnabled(Part part)
    {
        var ls = part.LightSwitch ?? part.FullPart?.LightSwitch;
        return ls?.LightIsActive;
    }
}
