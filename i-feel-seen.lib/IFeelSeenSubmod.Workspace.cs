using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.IFeelSeenLib;

public sealed partial class IFeelSeenSubmod
{
    public string FeatureId => "i-feel-seen";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("vehicleFilter", _vehicleFilter);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _pendingVehicleIndex, v => _pendingVehicleIndex = v, target: true, vehicle: true);
        return state;
    }
}
