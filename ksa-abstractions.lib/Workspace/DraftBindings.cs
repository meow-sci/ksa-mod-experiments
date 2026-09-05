using System;
using System.Collections.Generic;
using System.Text.Json;
using Brutal.ImGuiApi;
using MeowSci.Unscience.Contracts;

namespace MeowSci.KsaAbstractions;

/// <summary>Explicit, typed field bindings. No field discovery or runtime object graph serialization.</summary>
public sealed class DraftBindings
{
    private interface IField
    {
        bool Target { get; }
        JsonElement Capture();
        Action Prepare(JsonElement? data);
    }
    private sealed class Field<T>(Func<T> get, Action<T> set, bool target, Action<T>? validate) : IField
    {
        private readonly JsonElement _initial = DraftJson.Encode(get());
        public bool Target => target;
        public JsonElement Capture() => DraftJson.Encode(get());
        public Action Prepare(JsonElement? data)
        {
            var encoded = data ?? _initial;
            DraftValueValidation.Json(encoded);
            DraftValueValidation.RequiredShape(encoded, _initial);
            T value = DraftJson.Decode<T>(encoded);
            if (value is float f && !float.IsFinite(f) || value is double d && !double.IsFinite(d)) throw new JsonException("Non-finite authoring value.");
            if (typeof(T).IsEnum && value != null && !Enum.IsDefined(typeof(T), value)) throw new JsonException("Invalid authoring enum.");
            if (value == null && _initial.ValueKind != JsonValueKind.Null) throw new JsonException("Required authoring field cannot be null.");
            validate?.Invoke(value);
            return () => set(value);
        }
    }
    public float ScrollY { get; set; }
    public bool RestoreScroll { get; set; }
    private readonly List<DraftChoice> _choices = new();
    public bool SelectionsResolved => _choices.TrueForAll(c => c.Resolved);
    public DraftBindings Choice(string key, Func<IReadOnlyList<DraftOption>> options, Func<int> get, Action<int> set, bool target = false, bool vehicle = false, Func<bool>? required = null)
    {
        var choice = new DraftChoice(key, options, get, set, vehicle, required);
        _choices.Add(choice);
        Text("__choiceFilter/" + key, choice.Filter);
        return Value(key, choice.Capture, choice.Restore, target);
    }
    public void Select(string key, string id) => _choices.Find(c => c.Label == key)?.Restore(id);
    public void RenderChoices() { foreach (var choice in _choices) choice.Render(); }
    public void ResolveChoices() { foreach (var choice in _choices) choice.Resolve(); }
    public void ReadChoices() { foreach (var choice in _choices) choice.ReadUserSelection(); }
    private readonly Dictionary<string, IField> _fields = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Sections { get; private set; } = new(StringComparer.Ordinal);
    public DraftBindings Value<T>(string key, Func<T> get, Action<T> set, bool target = false, Action<T>? validate = null)
    { _fields.Add(key, new Field<T>(get, set, target, validate)); return this; }
    public DraftBindings Text(string key, ImInputString input) => Value(key, input.ToString, v => input.Value16 = v);
    public DraftState Capture()
    {
        var state = new DraftState { Sections = new(Sections, StringComparer.Ordinal) };
        foreach (var (key, field) in _fields)
            (field.Target ? state.Targets : state.Fields)[key] = field.Capture();
        state.Fields["__scrollY"] = DraftJson.Encode(ScrollY);
        return state;
    }
    public Action Prepare(DraftState state)
    {
        if (state.Version != 1) throw new InvalidOperationException("Unsupported feature schema.");
        var actions = new List<Action>();
        foreach (var (key, field) in _fields)
        {
            var source = field.Target ? state.Targets : state.Fields;
            actions.Add(field.Prepare(source.TryGetValue(key, out var value) ? value : null));
        }
        float scroll = state.Fields.TryGetValue("__scrollY", out var scrollData) ? scrollData.GetSingle() : 0;
        if (!float.IsFinite(scroll)) throw new JsonException("Invalid scroll offset.");
        var sections = new Dictionary<string, bool>(state.Sections, StringComparer.Ordinal);
        return () =>
        {
            foreach (var action in actions) action();
            Sections = sections; ScrollY = Math.Max(0, scroll); RestoreScroll = true;
        };
    }
}
