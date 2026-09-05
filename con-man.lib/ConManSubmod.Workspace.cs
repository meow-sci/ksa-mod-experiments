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
        state.Value("Gauges", () => _layoutDraft, v => _layoutDraft = v, validate: gauges =>
        {
            foreach (var gauge in gauges.Values)
            {
                if (gauge == null) throw new InvalidOperationException("Missing gauge settings.");
                DraftValueValidation.Range(gauge.ScaleX, .01, 100, "Gauge scale X");
                DraftValueValidation.Range(gauge.ScaleY, .01, 100, "Gauge scale Y");
                DraftValueValidation.Range(gauge.OffsetX, -100000, 100000, "Gauge offset X");
                DraftValueValidation.Range(gauge.OffsetY, -100000, 100000, "Gauge offset Y");
            }
        });
        state.Value("LayoutName", () => _layoutName, v => _layoutName = v);
        return state;
    }
}
