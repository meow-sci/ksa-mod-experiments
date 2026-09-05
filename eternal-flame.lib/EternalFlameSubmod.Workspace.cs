using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.EternalFlameLib;

public sealed partial class EternalFlameSubmod
{
    public string FeatureId => "eternal-flame";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("vehicleFilter", _vehicleFilter);
        state.Value("refillIntervalMs", () => _refillIntervalMs, value => _refillIntervalMs = value);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _selectedVehicleIndex, v => _selectedVehicleIndex = v, target: true, vehicle: true);
        state.Value("Fuel", () => _refillFuel, v => _refillFuel = v);
        state.Value("Electricity", () => _refillElectricity, v => _refillElectricity = v);
        return state;
    }
}
