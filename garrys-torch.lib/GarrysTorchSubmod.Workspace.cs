using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.GarrysTorchLib;

public sealed partial class GarrysTorchSubmod
{
    public string FeatureId => "garrys-torch";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Value("pendingPosition", () => _pendingPosition, value => _pendingPosition = value);
        state.Value("pendingRotation", () => _pendingRotation, value => _pendingRotation = value);
        state.Value("pendingScale", () => _pendingScale, value => _pendingScale = value, validate: v => DraftValueValidation.Range(v, 0.001, 1000, "pendingScale"));
        state.Value("pendingLockRotation", () => _pendingLockRotation, value => _pendingLockRotation = value);
        state.Text("sourceFilter", _sourceFilter);
        state.Text("targetFilter", _targetFilter);
        state.Text("presetFilter", _presetFilter);
        state.Text("targetPartFilter", _targetPartFilter);
        state.Choice("Source vehicle", DraftOptions.Vehicles, () => _pendingSourceIndex, v => _pendingSourceIndex = v, target: true, vehicle: true);
        state.Choice("Target vehicle", DraftOptions.Vehicles, () => _pendingTargetIndex, v => _pendingTargetIndex = v, target: true, vehicle: true);
        state.Choice("Anchor part", DraftPartOptions, () => _targetPartIndex, v => _targetPartIndex = v, target: true);
        state.Value("LegacyPreset", () => { var names = _presetManager.GetPresetNames(); return _selectedPresetIndex >= 0 && _selectedPresetIndex < names.Length ? names[_selectedPresetIndex] : ""; }, value => _selectedPresetIndex = Array.IndexOf(_presetManager.GetPresetNames(), value));
        return state;
    }
    private IReadOnlyList<DraftOption> DraftPartOptions()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        _targetParts.Clear();
        if (_pendingTargetIndex >= 0 && _pendingTargetIndex < vehicles.Count)
            foreach (var part in vehicles[_pendingTargetIndex].Parts.Parts) _targetParts.Add(part);
        _prevTargetIndex = _pendingTargetIndex;
        return DraftOptions.Parts(_targetParts);
    }
}
