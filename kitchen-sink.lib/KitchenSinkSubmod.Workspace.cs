using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.KitchenSinkLib;

public sealed partial class KitchenSinkSubmod
{
    public string FeatureId => "kitchen-sink";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        state.Value("ivaEnabled", () => _ivaEnabled, v => _ivaEnabled = v);
        return state;
    }
}
