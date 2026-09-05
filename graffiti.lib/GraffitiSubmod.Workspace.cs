using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.GraffitiLib;

public sealed partial class GraffitiSubmod
{
    public string FeatureId => "graffiti";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("decalFilter", _decalFilter);
        state.Value("width", () => _width, value => _width = value);
        state.Value("height", () => _height, value => _height = value);
        state.Value("depth", () => _depth, value => _depth = value);
        state.Value("rollDeg", () => _rollDeg, value => _rollDeg = value);
        state.Value("range", () => _range, value => _range = value);
        state.Value("alpha", () => _alpha, value => _alpha = value);
        state.Value("brightness", () => _brightness, value => _brightness = value);
        state.Value("maxDrawDistance", () => _maxDrawDistance, value => _maxDrawDistance = value);
        state.Choice("Decal", () => DraftOptions.Strings(_libraryNames), () => _selectedLibraryIndex, v => _selectedLibraryIndex = v, target: false);
        state.Value("DebugBox", () => _draftDebugBox, v => _draftDebugBox = v);
        _fileBrowser.BindDraft(state);
        return state;
    }
}
