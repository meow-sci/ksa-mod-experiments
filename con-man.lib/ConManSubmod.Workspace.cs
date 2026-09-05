using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.ConManLib;

public sealed partial class ConManSubmod
{
    public string FeatureId => "con-man";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("Gauges", () => _layoutDraft, v => _layoutDraft = v);
        state.Value("LayoutName", () => _layoutName, v => _layoutName = v);
        return state;
    }
}
