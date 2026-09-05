using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.PebblesLib;

internal static class PebblesUi
{
    public static string Choice(string label, string selected, IEnumerable<string> values, string filter = "")
    {
        if (!ImGui.BeginCombo(FormField.Label(label), selected.Length == 0 ? "Select…" : GlbIdentity.Label(selected))) return selected;
        try
        {
            foreach (var id in values)
                if ((filter.Length == 0 || (id.Contains(filter, StringComparison.OrdinalIgnoreCase) || GlbIdentity.Label(id).Contains(filter, StringComparison.OrdinalIgnoreCase))) && ImGui.Selectable(id.Length == 0 ? "(none / default)" : GlbIdentity.Label(id) + "##" + id, id == selected)) selected = id;
        }
        finally { ImGui.EndCombo(); }
        return selected;
    }
    public static T Enum<T>(string label, T value) where T : struct, Enum
    {
        string selected = Choice(label, value.ToString(), System.Enum.GetNames<T>());
        return System.Enum.Parse<T>(selected);
    }
    public static float Number(string label, float v) { ImGui.InputFloat(FormField.Label(label), ref v); return v; }
    public static double Number(string label, double v) { ImGui.InputDouble(FormField.Label(label), ref v); return v; }
    public static bool Toggle(string label, bool v) { ImGui.Checkbox(FormField.Label(label), ref v); return v; }
    public static Vec3 Vector(string label, Vec3 v)
    {
        var value = new float3(v.X, v.Y, v.Z); ImGui.InputFloat3(FormField.Label(label), ref value); return new(value.X, value.Y, value.Z);
    }
}
