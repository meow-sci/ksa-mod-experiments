using System.Linq;
using MeowSci.KsaRings;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.RockyMcRockFaceLib;

public sealed partial class RockyMcRockFaceSubmod
{
    public string FeatureId => "rocky-mcrock-face";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("assetFilter", _assetFilter);
        state.Value("allLodsMeshId", () => _allLodsMeshId, value => _allLodsMeshId = value);
        state.Value("ring", () => _selection, value => _selection = value, validate: value => { if (value == null || value.LodMeshIds == null || value.LodMeshIds.Length != RingSelection.MaxLods) throw new InvalidOperationException("Invalid ring recipe."); });
        state.Choice("Body", () => DraftOptions.Strings(_controller.Bodies.Select(b => b.Id)), () => _selectedBodyIndex, v => _selectedBodyIndex = v, target: true);
        return state;
    }
}
