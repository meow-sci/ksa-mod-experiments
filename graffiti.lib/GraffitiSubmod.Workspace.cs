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
        state.Value("SprayMode", () => _sprayMode, v => _sprayMode = v);
        state.Value("SprayIntervalMs", () => _sprayIntervalMs, v => _sprayIntervalMs = v,
            validate: v => { if (v < 10 || v > 60_000) throw new InvalidOperationException("Spray interval must be 10–60000 ms."); });
        state.Text("decalFilter", _decalFilter);
        state.Value("width", () => _width, value => _width = value, validate: v => DraftValueValidation.Range(v, 0.001, 100000.0, "width"));
        state.Value("height", () => _height, value => _height = value, validate: v => DraftValueValidation.Range(v, 0.001, 100000.0, "height"));
        state.Value("depth", () => _depth, value => _depth = value, validate: v => DraftValueValidation.Range(v, 0.001, 100000.0, "depth"));
        state.Value("rollDeg", () => _rollDeg, value => _rollDeg = value);
        state.Value("range", () => _range, value => _range = value, validate: v => DraftValueValidation.Range(v, 0.01, 100000000.0, "range"));
        state.Value("alpha", () => _alpha, value => _alpha = value, validate: v => DraftValueValidation.Range(v, 0, 1, "alpha"));
        state.Value("brightness", () => _brightness, value => _brightness = value, validate: v => DraftValueValidation.Range(v, 0, 100, "brightness"));
        state.Value("maxDrawDistance", () => _maxDrawDistance, value => _maxDrawDistance = value, validate: v => DraftValueValidation.Range(v, 0, 10000000000.0, "maxDrawDistance"));
        state.Choice("Decal", () => DraftOptions.Strings(_libraryNames), () => _selectedLibraryIndex, v => _selectedLibraryIndex = v, target: false);
        state.Value("DebugBox", () => _draftDebugBox, v => _draftDebugBox = v);
        _fileBrowser.BindDraft(state);
        return state;
    }
}
