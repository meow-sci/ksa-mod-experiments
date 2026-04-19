using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.KitchenSinkLib;

/// <summary>
/// Forces IVA (interior) parts to render even when not in IVA camera mode
/// by directly mutating Template.Internal on all loaded PartModel instances.
/// </summary>
public static class IvaForceRender
{
    private static bool _enabled;
    private static readonly List<PartModelModule.Template> _mutatedTemplates = new();

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
                ForceInternalVisible();
            else
                RestoreInternalHidden();
        }
    }

    /// <summary>
    /// Called by the constructor patch to handle parts created after the toggle is enabled.
    /// </summary>
    public static void TrackMutated(PartModelModule.Template template)
    {
        if (!_mutatedTemplates.Contains(template))
            _mutatedTemplates.Add(template);
    }

    private static void ForceInternalVisible()
    {
        _mutatedTemplates.Clear();
        foreach (var pm in PartModel.Instances)
        {
            if (pm.Template.Internal)
            {
                _mutatedTemplates.Add(pm.Template);
                pm.Template.Internal = false;
            }
        }
        Console.WriteLine($"kitchen-sink: Forced {_mutatedTemplates.Count} internal templates visible");
    }

    private static void RestoreInternalHidden()
    {
        foreach (var t in _mutatedTemplates)
            t.Internal = true;
        Console.WriteLine($"kitchen-sink: Restored {_mutatedTemplates.Count} internal templates");
        _mutatedTemplates.Clear();
    }
}
