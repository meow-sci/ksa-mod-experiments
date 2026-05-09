using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Brutal.Numerics;
using KSA;

namespace MeowSci.RedAlertLib;

/// <summary>
/// Helpers for KSA LightModule operations (color + on/off).
///
/// <para>Per-instance color requires cloning <see cref="LightModule.TemplateData"/>: by default
/// every <see cref="LightModule"/> instance shares one TemplateData per <see cref="PartTemplate"/>,
/// so writing to <c>Template.Color</c> changes every part using that template. We clone the
/// TemplateData (and its <c>ColorReference</c>) on first write per LightModule and remember which
/// modules have been "unshared" so subsequent edits target only the per-instance copy.</para>
/// </summary>
internal static class LightActions
{
    private const BindingFlags AllInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Tracks which LightModule instances already have a per-instance (cloned) Template ref.</summary>
    private static readonly ConditionalWeakTable<LightModule, object> _unsharedTemplates = new();

    /// <summary>
    /// Walks the top-level part's subtree, finds every per-instance LightModule, ensures each one
    /// has a private cloned Template, and writes the color into that private Template's ColorReference.
    /// </summary>
    public static int ApplyColorToSubtree(Part topPart, float3 color)
    {
        int written = 0;
        WriteColorRecursive(topPart, color, ref written);
        return written;
    }

    /// <summary>On/off lives on the top-level part's PowerConsumer (LightSwitch). No-op if missing.</summary>
    public static void SetEnabled(Part topPart, bool enabled)
    {
        var ls = topPart.LightSwitch;
        if (ls != null) ls.LightIsActive = enabled;
    }

    public static bool? GetEnabled(Part topPart) => topPart.LightSwitch?.LightIsActive;

    // ── internals ─────────────────────────────────────────────────────────────

    private static void WriteColorRecursive(Part part, float3 color, ref int written)
    {
        var lights = part.Modules.Get<LightModule>();
        for (int i = 0; i < lights.Length; i++)
        {
            var lm = lights[i];
            EnsurePerInstanceTemplate(lm);
            WriteColorReference(lm.Template?.Color, color);
            written++;
        }
        foreach (var sub in part.SubParts)
            WriteColorRecursive(sub, color, ref written);
    }

    private static void EnsurePerInstanceTemplate(LightModule lm)
    {
        if (_unsharedTemplates.TryGetValue(lm, out _)) return;
        var orig = lm.Template;
        if (orig == null) return;

        var cloneTd = ShallowClone(orig);
        if (orig.Color != null)
            cloneTd.Color = ShallowClone(orig.Color);

        lm.Template = cloneTd;
        _unsharedTemplates.Add(lm, _marker);
    }

    private static readonly object _marker = new();

    private static void WriteColorReference(object? colorRef, float3 color)
    {
        if (colorRef == null) return;
        var t = colorRef.GetType();
        t.GetField("R", AllInstance)?.SetValue(colorRef, color.X);
        t.GetField("G", AllInstance)?.SetValue(colorRef, color.Y);
        t.GetField("B", AllInstance)?.SetValue(colorRef, color.Z);
        try { t.GetMethod("OnDataLoad", AllInstance)?.Invoke(colorRef, new object?[] { null }); }
        catch (Exception ex) { Console.WriteLine($"red-alert: ColorReference OnDataLoad error: {ex.Message}"); }
    }

    /// <summary>Reflection-based shallow clone of any reference type. Copies all instance fields,
    /// public and non-public, from <paramref name="source"/> into a new uninitialised instance.</summary>
    private static T ShallowClone<T>(T source) where T : class
    {
        var type = source.GetType();
        var clone = (T)RuntimeHelpers.GetUninitializedObject(type);
        var current = type;
        while (current != null && current != typeof(object))
        {
            foreach (var field in current.GetFields(AllInstance | BindingFlags.DeclaredOnly))
                field.SetValue(clone, field.GetValue(source));
            current = current.BaseType;
        }
        return clone;
    }
}
