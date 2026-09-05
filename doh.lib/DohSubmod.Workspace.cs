using System.Linq;
using System;
using System.Collections.Generic;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.DohLib;

public sealed partial class DohSubmod
{
    public string FeatureId => "doh";
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= CreateDraftBindings();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings CreateDraftBindings()
    {
        var state = new DraftBindings();
        state.Text("vehicleFilter", _vehicleFilter);
        state.Text("characterFilter", _characterFilter);
        state.Value("offset", () => _offset, value => _offset = value);
        state.Value("spawnCount", () => _spawnCount, value => _spawnCount = value);
        state.Value("useCustomColor", () => _useCustomColor, value => _useCustomColor = value);
        state.Value("tintColor", () => _tintColor, value => _tintColor = value);
        state.Value("uniquePerKitten", () => _uniquePerKitten, value => _uniquePerKitten = value);
        state.Value("selectedXkcdName", () => _selectedXkcdName, value => _selectedXkcdName = value);
        state.Text("xkcdFilterText", _xkcdFilterText);
        state.Choice("Vehicle", DraftOptions.Vehicles, () => _selectedVehicleIndex, v => _selectedVehicleIndex = v, target: true, vehicle: true);
        state.Choice("Character", () => new[] { new DraftOption("$random", "Random character") }.Concat(DraftOptions.Strings(_availableCharacters)).ToArray(), () => _selectedCharacterIndex + 1, v => _selectedCharacterIndex = v - 1, target: false);
        return state;
    }
}
