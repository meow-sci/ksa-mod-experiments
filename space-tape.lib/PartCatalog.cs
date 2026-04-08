using System;
using System.Collections.Generic;
using System.Reflection;
using KSA;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Loads all non-SubPart, non-Hidden parts from ModLibrary for the "Import From Game" feature.
/// </summary>
public sealed class PartCatalog
{
    public List<(string id, string displayName)> Parts { get; } = new();
    public bool IsLoaded { get; private set; }

    public void Load()
    {
        Parts.Clear();

        var allPartsField = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (allPartsField?.GetValue(null) is not SerializedCollection<PartTemplate> allParts)
        {
            Console.WriteLine("space-tape: PartCatalog.Load — failed to get AllParts");
            return;
        }

        var list = allParts.GetList();
        foreach (var pt in list)
        {
            if (pt.IsSubPart || pt.IsHidden) continue;
            Parts.Add((pt.Id, pt.DisplayName ?? pt.Id));
        }

        Parts.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        IsLoaded = true;
        Console.WriteLine($"space-tape: PartCatalog loaded {Parts.Count} parts");
    }
}
