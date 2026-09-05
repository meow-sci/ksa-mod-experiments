using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.PartsNowLib;

public sealed partial class PartsNowSubmod
{
    public string FeatureId => "parts-now";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) { var apply = Draft.Prepare(state); return () => { apply(); _pastePanel.InvalidateDraftValidation(); }; }
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();

        _pastePanel.BindDraft(state);
        _modFolderPanel.BindDraft(state);
        _statusPanel.BindDraft(state);
        return state;
    }
}
