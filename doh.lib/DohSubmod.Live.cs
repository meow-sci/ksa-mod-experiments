using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.DohLib.Spawning;
namespace MeowSci.DohLib;

public sealed partial class DohSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (_registry == null) yield break;
        if (_registry.Count > 0)
            yield return new LiveStateItem<string>("all", "All spawned kittens", "Bulk controls", "registry", _ => RenderLiveRegistry());
        foreach (var entry in _registry.GetAll().ToArray())
            yield return new LiveStateItem<SpawnedKittenEntry>(entry.KittenId, "Kitten " + entry.CharacterId,
                entry.KittenId, entry, RenderMaterialDetails);
    }

    private void RenderLiveRegistry()
    {
        RenderStatus();
        if (ImGui.Button("Despawn all kittens", new Brutal.Numerics.float2(-1, 0))) _spawner?.DespawnAll();
    }
}
