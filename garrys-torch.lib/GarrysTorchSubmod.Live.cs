using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarrysTorchLib;

public sealed partial class GarrysTorchSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _welds.ToArray())
            yield return new LiveStateItem<WeldEntry>(entry.Source.Id, "Vehicle weld", entry.Source.Id + " → " + entry.Target.Id, entry, RenderLiveItem);
    }
    private void RenderLiveItem(WeldEntry entry)
    {
        if (ImGui.Button(" Copy settings to form ")) { _pendingPosition = entry.Position; _pendingRotation = entry.Rotation; _pendingScale = entry.Scale; _pendingLockRotation = entry.LockRotation; }
        WeldEntry? remove = null;
        RenderWeldSection(entry, _welds.IndexOf(entry), ref remove);
        if (_openSaveModal) { ImGui.OpenPopup("Save as preset##gt"); _openSaveModal = false; }
        RenderSavePresetModal();
        if (remove != null) RemoveWeld(remove);
    }
}
