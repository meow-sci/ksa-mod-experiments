using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.ThugLifeLib;

public sealed partial class ThugLifeSubmod
{
    public string FeatureId => "thug-life";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("pendingPosition", () => _pendingPosition, value => _pendingPosition = value);
        state.Value("pendingRotation", () => _pendingRotation, value => _pendingRotation = value);
        state.Value("pendingWidth", () => _pendingWidth, value => _pendingWidth = value);
        state.Value("pendingHeight", () => _pendingHeight, value => _pendingHeight = value);
        state.Text("vehicleFilter", _vehicleFilter);
        state.Text("partFilter", _partFilter);
        state.Text("subPartFilter", _subPartFilter);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _pendingVehicleIndex, v => _pendingVehicleIndex = v, target: true, vehicle: true);
        state.Choice("Part", DraftPartOptions, () => _pendingPartIndex, v => _pendingPartIndex = v, target: true);
        state.Choice("Sub-part", DraftSubPartOptions, () => _pendingSubPartIndex, v => _pendingSubPartIndex = v, target: true);
        return state;
    }
    private IReadOnlyList<DraftOption> DraftPartOptions()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        _topLevelParts.Clear();
        if (_pendingVehicleIndex >= 0 && _pendingVehicleIndex < vehicles.Count)
            foreach (var part in vehicles[_pendingVehicleIndex].Parts.Parts) _topLevelParts.Add(part);
        _prevVehicleIndex = _pendingVehicleIndex;
        return DraftOptions.Parts(_topLevelParts);
    }
    private IReadOnlyList<DraftOption> DraftSubPartOptions()
    {
        _subParts.Clear();
        if (_pendingPartIndex >= 0 && _pendingPartIndex < _topLevelParts.Count)
            foreach (var part in _topLevelParts[_pendingPartIndex].SubParts) _subParts.Add(part);
        _prevPartIndex = _pendingPartIndex;
        return new[] { new DraftOption("$self", "Part itself") }.Concat(DraftOptions.Parts(_subParts)).ToArray();
    }
}
