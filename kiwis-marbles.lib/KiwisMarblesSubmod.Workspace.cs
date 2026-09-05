using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.KiwisMarblesLib;

public sealed partial class KiwisMarblesSubmod
{
    public string FeatureId => "kiwis-marbles";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("pendingOffset", () => _pendingOffset, value => _pendingOffset = value);
        state.Value("pendingOffsetScaleIndex", () => _pendingOffsetScaleIndex, value => _pendingOffsetScaleIndex = value);
        state.Text("sourceFilter", _sourceFilter);
        state.Text("targetFilter", _targetFilter);
        state.Choice("Source body", () => DraftOptions.Strings(CelestialProvider.GetAllCelestials().Select(b => b.Id)), () => _pendingSourceIndex, v => _pendingSourceIndex = v, target: true);
        state.Choice("Target orbiter", () => DraftOptions.Strings(CelestialProvider.GetAllOrbiters().Select(b => b.Id)), () => _pendingTargetIndex, v => _pendingTargetIndex = v, target: true);
        return state;
    }
}
