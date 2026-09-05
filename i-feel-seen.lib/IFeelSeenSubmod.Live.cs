using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.IFeelSeenLib;

public sealed partial class IFeelSeenSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _tracker.Tracked.ToArray())
            yield return new LiveStateItem<TrackedVehicle>(entry.Vehicle.Id, "Render visibility", entry.Vehicle.Id, entry, item =>
            {
                bool visible = item.SeeMe;
                if (ImGui.Checkbox("Force visible", ref visible)) item.SeeMe = visible;
                if (ImGui.Button(" Remove override ")) _tracker.RemoveVehicle(item.Vehicle);
            });
    }

}
