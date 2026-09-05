using System;
using Brutal.ImGuiApi;

namespace MeowSci.KsaAbstractions;

/// <summary>Persisted authoring disclosures. The host sets Current only around authoring content.</summary>
public static class WorkspaceUi
{
    public static DraftBindings? Current { get; set; }
    public static bool Header(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        if (Current == null) return ImGui.CollapsingHeader(label, flags);
        bool open = Current.Sections.TryGetValue(label, out var saved) ? saved : (flags & ImGuiTreeNodeFlags.DefaultOpen) != 0;
        ImGui.SetNextItemOpen(open, ImGuiCond.Always);
        open = ImGui.CollapsingHeader(label, flags);
        Current.Sections[label] = open;
        return open;
    }
    public static bool Tree(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        if (Current == null) return ImGui.TreeNodeEx(label, flags);
        bool open = Current.Sections.TryGetValue(label, out var saved) && saved;
        ImGui.SetNextItemOpen(open, ImGuiCond.Always);
        open = ImGui.TreeNodeEx(label, flags); Current.Sections[label] = open; return open;
    }
    public static bool Button(string label) => Button(label, default);
    public static bool Button(string label, Brutal.Numerics.float2 size)
    {
        Current?.ResolveChoices();
        bool disabled = Current != null && !Current.SelectionsResolved;
        ImGui.BeginDisabled(disabled);
        bool clicked = ImGui.Button(label, size);
        ImGui.EndDisabled();
        return clicked && !disabled;
    }
    public static void Error(Exception ex) => ImGui.TextColored(new Brutal.Numerics.float4(1f, .3f, .3f, 1f), ex.Message);
}
