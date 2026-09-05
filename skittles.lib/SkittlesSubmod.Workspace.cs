using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.SkittlesLib;

public sealed partial class SkittlesSubmod
{
    public string FeatureId => "skittles";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("Theme", () => _themeDraft, v => _themeDraft = v, validate: v => v?.Validate());
        state.Value("Template", () => _templateName, v => _templateName = v);
        return state;
    }
}
