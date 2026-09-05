using MeowSci.KsaLights;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.ItsSoShinyLib;

public sealed partial class ItsSoShinySubmod
{
    public string FeatureId => "its-so-shiny";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("newGridName", _newGridName);
        state.Text("vehicleFilter", _vehicleFilter);
        state.Value("configCols", () => _configCols, value => _configCols = value, validate: v => DraftValueValidation.Range(v, 1, 128, "configCols"));
        state.Value("configRows", () => _configRows, value => _configRows = value, validate: v => DraftValueValidation.Range(v, 1, 128, "configRows"));
        state.Value("configSpacing", () => _configSpacing, value => _configSpacing = value, validate: v => DraftValueValidation.Range(v, 0.001, 1000, "configSpacing"));
        state.Value("configOffsetX", () => _configOffsetX, value => _configOffsetX = value);
        state.Value("configOffsetY", () => _configOffsetY, value => _configOffsetY = value);
        state.Value("configOffsetZ", () => _configOffsetZ, value => _configOffsetZ = value);
        state.Value("configLightScale", () => _configLightScale, value => _configLightScale = value, validate: v => DraftValueValidation.Range(v, 0.001, 100, "configLightScale"));
        state.Value("configLayoutIndex", () => _configLayoutIndex, value => _configLayoutIndex = value, validate: v => DraftValueValidation.Range(v, 0, 1, "configLayoutIndex"));
        state.Value("configIntensity", () => _configIntensity, value => _configIntensity = value, validate: v => DraftValueValidation.Range(v, 0, 25, "configIntensity"));
        state.Value("configColor", () => _configColor, value => _configColor = value);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _selectedVehicleIndex, v => _selectedVehicleIndex = v, target: true, vehicle: true);
        return state;
    }
}
