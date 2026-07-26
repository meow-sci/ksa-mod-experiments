using System;
using System.Collections.Generic;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

/// <summary>
/// Enumerates the parts the paint UI can target, in flight and in the vehicle editor.
///
/// Flight parts come from the vehicles in the current system; editor parts come from the editing
/// space's part tree plus any unattached trees — the same two sources the game itself walks when
/// it submits part render data.
/// </summary>
public static class PaintTargets
{
    /// <summary>A named collection of paintable parts (one vehicle, or one editor part tree).</summary>
    public sealed class Group
    {
        public readonly string Label;
        public readonly List<Part> Parts;

        /// <summary>Display label per part, index-aligned with <see cref="Parts"/> and deduplicated.</summary>
        public readonly List<string> PartLabels;

        public Group(string label, List<Part> parts)
        {
            Label = label;
            Parts = parts;
            PartLabels = BuildLabels(parts);
        }

        private static List<string> BuildLabels(List<Part> parts)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                var name = BaseName(part);
                totals.TryGetValue(name, out int count);
                totals[name] = count + 1;
            }

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var labels = new List<string>(parts.Count);
            foreach (var part in parts)
            {
                var name = BaseName(part);
                if (totals[name] > 1)
                {
                    seen.TryGetValue(name, out int index);
                    seen[name] = index + 1;
                    labels.Add($"{name} #{index + 1}");
                }
                else
                {
                    labels.Add(name);
                }
            }
            return labels;
        }

        private static string BaseName(Part part) =>
            string.IsNullOrEmpty(part.DisplayName) ? part.Id : part.DisplayName;
    }

    /// <summary>Returns every paintable group for the current game state.</summary>
    public static List<Group> Enumerate()
    {
        var groups = new List<Group>();

        try
        {
            if (Program.Editor != null)
                CollectEditorGroups(Program.Editor, groups);
            else
                CollectFlightGroups(groups);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"humble-arteest: error enumerating paint targets: {ex.Message}");
        }

        return groups;
    }

    /// <summary>Counts how many parts of each template id are present across all groups.</summary>
    public static SortedDictionary<string, int> CountTemplates(List<Group> groups)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var part in group.Parts)
            {
                if (string.IsNullOrEmpty(part.Id)) continue;
                counts.TryGetValue(part.Id, out int count);
                counts[part.Id] = count + 1;
            }
        }
        return counts;
    }

    /// <summary>Flattens every group's parts into one set, for pruning stale paint entries.</summary>
    public static HashSet<Part> FlattenParts(List<Group> groups)
    {
        var all = new HashSet<Part>(ReferenceEqualityComparer.Instance);
        foreach (var group in groups)
            foreach (var part in group.Parts)
                all.Add(part);
        return all;
    }

    /// <summary>True when a part has at least one renderable model module (paint has an effect).</summary>
    public static bool IsPaintable(Part part) =>
        part.Modules.Get<PartModelModule>().Length > 0 ||
        part.Modules.Get<PartModelDynamicModule>().Length > 0;

    // ---- Sources ----

    private static void CollectFlightGroups(List<Group> groups)
    {
        foreach (var vehicle in VehicleProvider.GetAllVehicles())
        {
            var parts = PartHelpers.GetAllParts(vehicle);
            var paintable = FilterPaintable(parts);
            if (paintable.Count > 0)
                groups.Add(new Group(vehicle.Id, paintable));
        }
    }

    private static void CollectEditorGroups(VehicleEditor editor, List<Group> groups)
    {
        AddTree(groups, "Editor", editor.EditingSpace?.Parts);

        int index = 1;
        foreach (var tree in editor.UnattachedPartTrees)
            AddTree(groups, $"Editor — loose #{index++}", tree);
    }

    private static void AddTree(List<Group> groups, string label, PartTree? tree)
    {
        if (tree == null) return;

        var parts = new List<Part>();
        foreach (var part in tree.Parts)
            CollectRecursive(part, parts);

        var paintable = FilterPaintable(parts);
        if (paintable.Count > 0)
            groups.Add(new Group(label, paintable));
    }

    private static void CollectRecursive(Part part, List<Part> into)
    {
        into.Add(part);
        foreach (var sub in part.SubParts)
            CollectRecursive(sub, into);
    }

    private static List<Part> FilterPaintable(List<Part> parts)
    {
        var result = new List<Part>(parts.Count);
        foreach (var part in parts)
            if (IsPaintable(part))
                result.Add(part);
        return result;
    }
}
