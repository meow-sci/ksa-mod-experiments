using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.ItsSoShinyLib;

public sealed partial class ItsSoShinySubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in ShinyGridManager.Grids.Values.ToArray())
            yield return new LiveStateItem<ShinyGridState>(entry.VehicleId + "/" + entry.GridName,
                "Light grid " + entry.GridName, entry.VehicleId, entry, RenderGridSection);
        yield return new LiveStateItem<string>("mesh-policy", "Light mesh visibility", "Global", "policy", _ =>
        {
            if (ImGui.Button("Scan all vehicles for existing grids", new Brutal.Numerics.float2(-1, 0))) DoGlobalScan();
            bool enabled = ShinyPatchState.RenderShinyParts;
            if (ImGui.Checkbox("Always render light meshes", ref enabled)) ShinyPatchState.RenderShinyParts = enabled;
        });
    }

}
