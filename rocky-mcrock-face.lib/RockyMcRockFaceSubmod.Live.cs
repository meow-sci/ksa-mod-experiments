using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.KsaRings;
namespace MeowSci.RockyMcRockFaceLib;

public sealed partial class RockyMcRockFaceSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
foreach (var body in _controller.Bodies.ToArray())
            if (_appliedSelections.TryGetValue(body.Id, out var selection))
                yield return new LiveStateItem<RingedBody>(body.Id, "Ring mesh/material override", body.Id, "Applied", body, b =>
                {
                    RenderMeshSection(b, selection); RenderTextureSection(selection); RenderFieldSection(selection);
                    if (ImGui.Button("Apply live edits", new float2(-1, 0))) ApplySelection(b, selection);
                    if (ImGui.Button("Copy settings to workspace", new float2(-1, 0))) _selection = DraftJson.Clone(selection);
                    if (ImGui.Button("Restore defaults", new float2(-1, 0))) RestoreDefaults(b, selection);
                });
    }

}
