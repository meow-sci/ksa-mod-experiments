using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.KittenAnimationsLib;

public sealed partial class KittenAnimationsSubmod
{
    public string FeatureId => "kitten-animations";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        state.Value("Recipe", () => _recipe, v => _recipe = v, validate: v => v.Validate());
        state.Value("Kitten", () => _kittenTarget, v => _kittenTarget = v, target: true);
        return state;
    }
}
