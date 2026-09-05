using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.PyroLib;

public sealed partial class PyroSubmod
{
    public string FeatureId => "pyro";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("plumeTemplateFilter", _plumeTemplateFilter);
        state.Value("pendingPosition", () => _pendingPosition, value => _pendingPosition = value);
        state.Value("pendingRotation", () => _pendingRotation, value => _pendingRotation = value);
        state.Text("vehicleFilter", _vehicleFilter);
        state.Text("partFilter", _partFilter);
        state.Text("subPartFilter", _subPartFilter);
        state.Text("templateFilter", _templateFilter);
        state.Text("presetFilter", _presetFilter);
        state.Value("plumeSettings", () => _pendingPreset, value => _pendingPreset = value, validate: value => { if (value != null && value.Nozzle == null) throw new InvalidOperationException("Missing nozzle settings."); });
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _pendingVehicleIndex, v => _pendingVehicleIndex = v, target: true, vehicle: true);
        state.Choice("Template", () => DraftOptions.Strings(PlumeTemplates.GetTemplateIds()), () => _pendingTemplateIndex, v => _pendingTemplateIndex = v, target: false);
        state.Choice("Part", DraftPartOptions, () => _pendingPartIndex, v => _pendingPartIndex = v, target: true);
        state.Choice("Sub-part", DraftSubPartOptions, () => _pendingSubPartIndex, v => _pendingSubPartIndex = v, target: true);
        state.Value("SharedTemplateId", () => _templateDraftId, v => _templateDraftId = v);
        state.Value("SharedTemplate", () => _templateDraft, v => _templateDraft = v);
        state.Value("LegacyPreset", () => { var names = _presetManager.GetPresetNames(); return _selectedPresetIndex >= 0 && _selectedPresetIndex < names.Length ? names[_selectedPresetIndex] : ""; }, value => _selectedPresetIndex = Array.IndexOf(_presetManager.GetPresetNames(), value));
        return state;
    }
    private IReadOnlyList<DraftOption> DraftPartOptions()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        _topParts.Clear();
        if (_pendingVehicleIndex >= 0 && _pendingVehicleIndex < vehicles.Count)
            foreach (var part in vehicles[_pendingVehicleIndex].Parts.Parts) _topParts.Add(part);
        _partsVehicle = _pendingVehicleIndex >= 0 && _pendingVehicleIndex < vehicles.Count ? vehicles[_pendingVehicleIndex] : null;
        _topPartLabels = _topParts.Select(PyroUi.PartLabel).ToArray();
        return DraftOptions.Parts(_topParts);
    }
    private IReadOnlyList<DraftOption> DraftSubPartOptions()
    {
        _subParts.Clear();
        if (_pendingPartIndex >= 0 && _pendingPartIndex < _topParts.Count)
            foreach (var part in _topParts[_pendingPartIndex].SubParts) _subParts.Add(part);
        _subPartsOwner = _pendingPartIndex >= 0 && _pendingPartIndex < _topParts.Count ? _topParts[_pendingPartIndex] : null;
        _subPartLabels = new[] { "(part itself)" }.Concat(_subParts.Select(PyroUi.PartLabel)).ToArray();
        return new[] { new DraftOption("$self", "Part itself") }.Concat(DraftOptions.Parts(_subParts)).ToArray();
    }
}
