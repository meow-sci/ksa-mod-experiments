using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarblesLib;

public sealed partial class KiwisMarblesSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _welds.ToArray())
            yield return new LiveStateItem<CelestialWeldEntry>(entry.Source.Id, "Celestial weld", entry.Source.Id + " → " + entry.Target.Id, entry, RenderLiveItem);
    }
    private void RenderLiveItem(CelestialWeldEntry entry)
    {
        if (ImGui.Button(" Copy settings to form ")) { _pendingOffset = Brutal.Numerics.float3.Pack(entry.Offset); _pendingOffsetScaleIndex = 0; }
        CelestialWeldEntry? remove = null;
        RenderWeldSection(entry, _welds.IndexOf(entry), ref remove);
        if (remove != null) RemoveWeld(remove);
    }
}
