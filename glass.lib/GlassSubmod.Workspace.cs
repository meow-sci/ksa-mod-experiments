using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.GlassLib;

public sealed partial class GlassSubmod
{
    public string FeatureId => "glass";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("fov", () => _fov, value => _fov = value, validate: v => DraftValueValidation.Range(v, 1, 179, "fov"));
        state.Value("selectedPresetIndex", () => _selectedPresetIndex, value => _selectedPresetIndex = value, validate: v => DraftValueValidation.Range(v, -1, Presets.Length - 1, "selectedPresetIndex"));
        return state;
    }
}
