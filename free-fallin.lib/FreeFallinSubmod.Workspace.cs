using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.FreeFallinLib;

public sealed partial class FreeFallinSubmod
{
    public string FeatureId => "free-fallin";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("textureMode", () => _textureMode, value => _textureMode = value);
        state.Value("tint", () => _tint, value => _tint = value);
        state.Value("brightness", () => _brightness, value => _brightness = value);
        state.Value("decalScale", () => _decalScale, value => _decalScale = value);
        state.Value("fullCanopyRotation", () => _fullCanopyRotation, value => _fullCanopyRotation = value);
        state.Value("useStockPbrMap", () => _useStockPbrMap, value => _useStockPbrMap = value);
        state.Value("ambientOcclusion", () => _ambientOcclusion, value => _ambientOcclusion = value);
        state.Value("roughness", () => _roughness, value => _roughness = value);
        state.Value("metallic", () => _metallic, value => _metallic = value);
        state.Choice("PNG", () => DraftOptions.Strings(_textures), () => _selectedTexture, v => _selectedTexture = v, target: false, required: () => _textureMode != (int)CanopyTextureMode.Stock);
        _browser.BindDraft(state);
        return state;
    }
}
