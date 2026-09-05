using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.GeeForceLib;

public sealed partial class GeeForceSubmod
{
    public string FeatureId => "geeforce";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        state.Value("threshold", () => _threshold, v => _threshold = v);
        state.Value("axes", () => _axes, v => _axes = v);
        state.Value("jerk", () => _jerk, v => _jerk = v);
        state.Value("window", () => _viewWindow, v => _viewWindow = v);
        return state;
    }
}
