using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using KSA;

namespace MeowSci.KsaAbstractions;

public sealed record DraftOption(string Id, string Label);

/// <summary>Keeps the authored identity even while the current game's lookup has no match.</summary>
public sealed class DraftChoice
{
    private readonly Func<IReadOnlyList<DraftOption>> _options;
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly bool _vehicle;
    private readonly Func<bool> _required;
    private readonly ImInputString _filter = new(128);
    private string? _identity;
    private int _before;
    private bool _controlled;
    public string Label { get; }
    public ImInputString Filter => _filter;
    private bool _resolved = true;
    public bool Resolved => !_required() || _resolved;
    public DraftChoice(string label, Func<IReadOnlyList<DraftOption>> options, Func<int> get, Action<int> set, bool vehicle, Func<bool>? required = null)
    { Label = label; _options = options; _get = get; _set = set; _vehicle = vehicle; _required = required ?? (() => true); }
    public string Capture()
    {
        if (_identity == null)
        { var options = _options(); int index = _get(); _identity = index >= 0 && index < options.Count ? options[index].Id : ""; }
        return _controlled ? "$controlled" : _identity;
    }
    public void Restore(string identity) { _controlled = identity == "$controlled"; _identity = _controlled ? "" : identity; }
    public void Resolve()
    {
        Capture();
        string id = _controlled ? VehicleProvider.GetControlledVehicle()?.Id ?? "" : _identity!;
        var options = _options();
        _before = -1;
        for (int i = 0; i < options.Count; ++i) if (options[i].Id == id && id.Length > 0) { _before = i; break; }
        _resolved = _before >= 0;
        _set(_before);
    }
    public void ReadUserSelection()
    {
        int index = _get();
        if (index == _before) return;
        var options = _options();
        // A refreshed list or automatic fallback cannot replace an unresolved saved identity.
        if (!_resolved) { _set(_before); return; }
        if (index >= 0 && index < options.Count) { _identity = options[index].Id; _controlled = false; }
    }
    public void Render()
    {
        var options = _options();
        string id = Capture();
        var option = options.FirstOrDefault(o => o.Id == id);
        string preview = _controlled ? "Controlled vehicle" : option?.Label ?? (id.Length == 0 ? "Select…" : "Unresolved: " + id);
        ImGui.AlignTextToFramePadding(); ImGui.Text(Label);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo($"##choice-{Label}", preview))
        {
            ImGui.SetNextItemWidth(-1f); ImGui.InputTextWithHint($"##filter-{Label}", "Filter…", _filter);
            if (_vehicle && ImGui.Selectable("Controlled vehicle", _controlled)) { _controlled = true; _identity = ""; }
            foreach (var item in options)
            {
                if (!item.Label.Contains(_filter.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable($"{item.Label}##{item.Id}", !_controlled && item.Id == _identity)) { _identity = item.Id; _controlled = false; }
            }
            ImGui.EndCombo();
        }
        Resolve();
        if (!Resolved) ImGui.TextDisabled("Select an available target/asset to apply. The saved selection is retained.");
    }
}

public static class DraftOptions
{
    public static IReadOnlyList<DraftOption> Vehicles() => VehicleProvider.GetAllVehicles().Select(v => new DraftOption(v.Id, v.Id)).ToArray();
    public static IReadOnlyList<DraftOption> Strings(IEnumerable<string> values) => values.Select(v => new DraftOption(v, v)).ToArray();
    public static IReadOnlyList<DraftOption> Parts(IEnumerable<Part> parts) => parts.Select(p => new DraftOption(
        PartIdentity.Get(p), p.DisplayName + " #" + p.InstanceId)).ToArray();
}
