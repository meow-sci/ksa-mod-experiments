using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.DontStifleMeLib;

public sealed partial class DontStifleMeSubmod
{
    public string FeatureId => "dont-stifle-me";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        state.Value("enabled", () => _enabled, v => _enabled = v);
        state.Value("snap", () => _snap, v => _snap = v);
        state.Value("expandedLimits", () => _expandedLimits, v => _expandedLimits = v);
        return state;
    }
}
