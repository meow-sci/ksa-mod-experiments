using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.HotPursuitLib;

public sealed partial class HotPursuitSubmod
{
    public string FeatureId => "hot-pursuit";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("placementRange", () => _placementRange, value => _placementRange = value);
        state.Value("nextFov", () => _nextFov, v => _nextFov = v);
        state.Value("nextWidth", () => _nextWidth, v => _nextWidth = v);
        state.Value("nextHeight", () => _nextHeight, v => _nextHeight = v);
        state.Value("nextTranslation", () => _nextTranslation, v => _nextTranslation = v);
        state.Value("nextRotation", () => _nextRotation, v => _nextRotation = v);
        return state;
    }
}
