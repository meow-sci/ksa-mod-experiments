using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.ThugLifeLib;

public sealed partial class ThugLifeSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _manager!.Entries.ToArray())
            yield return new LiveStateItem<ThugLifeEntry>(entry.Id.ToString(), "Sunglasses " + entry.Id, entry.Vehicle.Id, entry, RenderLiveItem);
    }
    private void RenderLiveItem(ThugLifeEntry entry)
    {
        if (ImGui.Button(" Copy settings to form ")) { _pendingPosition = entry.Position; _pendingRotation = entry.Rotation; _pendingWidth = entry.Width; _pendingHeight = entry.Height; }
        ThugLifeEntry? remove = null;
        RenderEntrySection(entry, 0, ref remove);
        if (remove != null) _manager!.Remove(remove);
    }
}
