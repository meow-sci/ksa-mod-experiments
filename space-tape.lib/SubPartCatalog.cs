using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSA;

namespace MeowSci.SpaceTapeLib;

public sealed class SubPartCatalog
{
    private List<PartTemplate>? _subparts;

    // Thumbnail animation
    private double _animTimer;

    public IReadOnlyList<PartTemplate>? SubParts => _subparts;

    public string? SelectedSubPartId { get; private set; }

    /// <summary>Returns the currently selected SubPart ID and clears the selection, or null if nothing is selected.</summary>
    public string? TakeSelectedSubPartId()
    {
        var id = SelectedSubPartId;
        SelectedSubPartId = null;
        return id;
    }

    public void SetSelectedSubPartId(string id)
    {
        SelectedSubPartId = id;
    }

    public void LoadSubParts()
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        object? collection = field?.GetValue(null);
        MethodInfo? getList = collection?.GetType().GetMethod("GetList");
        var allParts = (List<PartTemplate>?)getList?.Invoke(collection, null);

        if (allParts == null)
        {
            Console.WriteLine("space-tape: SubPartCatalog.LoadSubParts - failed to get AllParts");
            _subparts = new List<PartTemplate>();
            return;
        }

        _subparts = allParts
            .Where(p => p.IsSubPart && !p.IsHidden)
            .OrderBy(p => p.Id)
            .ToList();

        Console.WriteLine($"space-tape: SubPartCatalog loaded {_subparts.Count} sub-parts");
    }

    public void Update(double dt)
    {
        _animTimer += dt;
    }
}
