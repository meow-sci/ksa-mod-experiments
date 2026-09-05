using System.Linq;
using MeowSci.KsaRings;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.BloominOnionLib;

public sealed partial class BloominOnionSubmod
{
    public string FeatureId => "bloomin-onion";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("assetFilter", _assetFilter);
        state.Text("presetName", _presetName);
        state.Value("selectedPreset", () => _selectedPreset, value => _selectedPreset = value);
        state.Value("ring", () => _editor, value => _editor = value, validate: value =>
        {
            if (value.Stripes == null || value.Stripes.Any(s => s == null) || value.Lods == null || value.Lods.Count > RingDefinition.MaxLods || value.Lods.Any(l => l == null || l.MeshId == null) || value.Name == null || value.ObjectsName == null || value.BandTextureId == null || value.ControlTextureId == null || value.DiffuseId == null || value.NormalId == null || value.PbrId == null)
                throw new InvalidOperationException("Invalid ring recipe collections or asset identifiers.");
        });
        state.Choice("Body", () => DraftOptions.Strings(_bodies.Select(b => b.Id)), () => _selectedBodyIndex, v => _selectedBodyIndex = v, target: true);
        return state;
    }
}
