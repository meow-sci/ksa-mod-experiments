using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.HumbleArteestLib;

public sealed partial class HumbleArteestSubmod
{
    public string FeatureId => "humble-arteest";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        state.Value("Brush", () => _settings, v => _settings = v, validate: v =>
        {
            if (v.Materials == null || v.Templates == null || v.Scope < 0 || v.Scope > 2 || v.Blend < 0 || v.Blend > 2)
                throw new InvalidOperationException("Invalid paint recipe.");
        });
        state.Value("ClickScope", () => _clickScope, v => _clickScope = v,
            validate: v => { if (v < 0 || v > 2) throw new InvalidOperationException("Invalid click paint scope."); });
        state.Value("ClickRange", () => _clickRange, v => _clickRange = v,
            validate: v => { if (!float.IsFinite(v) || v < 1 || v > 100_000) throw new InvalidOperationException("Invalid paint range."); });
        state.Text("Filter", _paintFilter);
        state.Value("SelectedParts", () => _settings.Parts, v => _settings.Parts = v, target: true);
        state.Value("SelectedEngines", () => _settings.Engines, v => _settings.Engines = v, target: true);
        return state;
    }
}
