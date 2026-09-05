using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
namespace MeowSci.BloominOnionLib;

public sealed partial class BloominOnionSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var entry in _controller.Applied.ToArray())
            yield return new LiveStateItem<AppliedRing>(entry.BodyId, entry.Definition.Name, entry.BodyId, "Applied ring", entry, ring =>
            {
                ImGui.Text($"Radius: {ring.Definition.InnerRadiusKm:N0}–{ring.Definition.OuterRadiusKm:N0} km");
                if (ImGui.Button("Copy settings to workspace", new float2(-1, 0))) { _editor = ring.Definition.Clone(); _presetName.Value16 = _editor.Name; }
                if (ImGui.Button("Remove ring", new float2(-1, 0))) { _controller.Remove(ring.Celestial, out var message); SetStatus(message, false); }
                if (ImGui.Button("Remove all custom rings", new float2(-1, 0))) { _controller.RemoveAll(out var message); SetStatus(message, false); }
            });
    }

}
