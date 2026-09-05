using MeowSci.KsaLights;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.ZippoLib;

public sealed partial class ZippoSubmod
{
    public string FeatureId => "zippo";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("intensity", () => _intensity, value => _intensity = value);
        state.Value("lightEnabled", () => _lightEnabled, value => _lightEnabled = value);
        state.Value("currentColor", () => _currentColor, value => _currentColor = value);
        state.Value("animStartColor4", () => _animStartColor4, value => _animStartColor4 = value);
        state.Value("animEndColor4", () => _animEndColor4, value => _animEndColor4 = value);
        state.Value("animStartIntensity", () => _animStartIntensity, value => _animStartIntensity = value);
        state.Value("animEndIntensity", () => _animEndIntensity, value => _animEndIntensity = value);
        state.Value("animDuration", () => _animDuration, value => _animDuration = value);
        state.Value("animEasingIdx", () => _animEasingIdx, value => _animEasingIdx = value);
        state.Value("animPowerStart", () => _animPowerStart, value => _animPowerStart = value);
        state.Value("animPowerEnd", () => _animPowerEnd, value => _animPowerEnd = value);
        state.Text("animStartColorFilter", _animStartColorFilter);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _draftVehicle, v => _draftVehicle = v, target: true, vehicle: true);
        state.Choice("Light part", () => DraftOptions.Parts(DraftLightParts()), () => _draftPart, v => _draftPart = v, target: true);
        return state;
    }
}
