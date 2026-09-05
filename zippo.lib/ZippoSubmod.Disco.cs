using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ZippoLib;

public sealed partial class ZippoSubmod
{
    private DiscoRecipe _disco = new();
    private bool _discoAllLights;
    private string? _discoError;
    private readonly Dictionary<Part, DiscoLight> _discoLights = new(ReferenceEqualityComparer.Instance);

    private void RenderDisco(Part? selected)
    {
        if (!WorkspaceUi.Header("Disco — party lights", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.Checkbox("All lights on selected craft", ref _discoAllLights);
        ImGui.TextWrapped("Each light gets an independent copy of this recipe. Color and spread affect only that instance. Actuation follows the light assembly's animation, shared by its moving subparts; unsupported channels are skipped. Light switches must be on.");
        ImGui.Checkbox("Animate color", ref _disco.Color);
        if (_disco.Color)
        {
            ImGui.Checkbox("Rainbow / random colors", ref _disco.RandomColors);
            if (!_disco.RandomColors)
            {
                int remove = -1;
                for (int i = 0; i < _disco.Palette.Count; i++)
                {
                    ImGui.PushID(i);
                    var color = _disco.Palette[i];
                    ImGui.ColorEdit3(FormField.Label($"Color {i + 1}"), ref color); _disco.Palette[i] = color;
                    if (_disco.Palette.Count > 1 && ImGui.Button("Remove color", new float2(-1, 0))) remove = i;
                    ImGui.PopID();
                }
                if (remove >= 0) _disco.Palette.RemoveAt(remove);
                ImGui.BeginDisabled(_disco.Palette.Count >= 32);
                if (ImGui.Button("Add color", new float2(-1, 0))) _disco.Palette.Add(new float3(1, 1, 1));
                ImGui.EndDisabled();
            }
            RenderDiscoTiming("Color", _disco.ColorTiming);
        }
        ImGui.Spacing();
        ImGui.Checkbox("Animate actuation", ref _disco.Actuation);
        if (_disco.Actuation)
        {
            using (new FormGrid("##disco-actuation"))
            {
                ImGui.SliderFloat(FormField.Label("Actuation minimum"), ref _disco.ActuationMin, 0, 1);
                ImGui.SliderFloat(FormField.Label("Actuation maximum"), ref _disco.ActuationMax, _disco.ActuationMin, 1);
            }
            RenderDiscoTiming("Actuation", _disco.ActuationTiming);
        }
        ImGui.Spacing();
        ImGui.Checkbox("Animate beam spread (spotlights)", ref _disco.Spread);
        if (_disco.Spread)
        {
            using (new FormGrid("##disco-spread"))
            {
                ImGui.SliderFloat(FormField.Label("Start inner half-angle (deg)"), ref _disco.InnerMin, .1f, 89);
                ImGui.SliderFloat(FormField.Label("Start outer half-angle (deg)"), ref _disco.OuterMin, _disco.InnerMin, 89);
                ImGui.SliderFloat(FormField.Label("End inner half-angle (deg)"), ref _disco.InnerMax, .1f, 89);
                ImGui.SliderFloat(FormField.Label("End outer half-angle (deg)"), ref _disco.OuterMax, _disco.InnerMax, 89);
            }
            RenderDiscoTiming("Spread", _disco.SpreadTiming);
        }
        var targets = _discoAllLights ? DraftLightParts() : selected == null ? new List<Part>() : new List<Part> { selected };
        ImGui.BeginDisabled(!Draft.SelectionsResolved || targets.Count == 0 || !(_disco.Color || _disco.Actuation || _disco.Spread));
        if (ImGui.Button(_discoAllLights ? "Start Disco on craft" : "Start Disco on light", new float2(-1, 0)))
        {
            try { _disco.Validate(); StartDisco(targets); _discoError = null; }
            catch (Exception ex) { _discoError = ex.Message; Console.WriteLine("zippo: Disco: " + ex); }
        }
        ImGui.EndDisabled();
        if (_discoError != null) ImGui.TextWrapped(_discoError);
    }

    private static void RenderDiscoTiming(string channel, DiscoTiming timing)
    {
        ImGui.PushID(channel);
        using (new FormGrid("##timing"))
        {
            ImGui.DragFloat(FormField.Label("Transition (s)"), ref timing.Transition, .05f, .01f, 3600);
            ImGui.DragFloat(FormField.Label("Hold (s)"), ref timing.Hold, .05f, 0, 3600);
            ImGui.Combo(FormField.Label("Easing"), ref timing.Easing, new[] { "Linear", "Ease in", "Ease out", "Smooth in-out" }, 4);
        }
        ImGui.PopID();
    }

    private void StartDisco(IEnumerable<Part> targets)
    {
        var claimed = new HashSet<KeyframeAnimationModule>();
        foreach (var part in targets)
        {
            StopDisco(part);
            _animationManager.CancelAll(Key(part));
            _managedLights.Remove(Key(part));
            var live = new DiscoLight(part, _disco);
            _discoLights[part] = live;
            if (_disco.Actuation)
            {
                // KeyframeAnimationModule applies transforms by subpart id within its full assembly.
                foreach (var module in part.FullPart.Modules.Get<KeyframeAnimationModule>())
                    if (module.Shared.Duration > 0 && module.Shared.PartLookup.ContainsKey(part.Id) && claimed.Add(module))
                    {
                        foreach (var other in _discoLights.Values) other.ReleaseActuator(module);
                        live.AddActuator(module);
                    }
            }
            live.Update(0);
        }
    }

    private void StopDisco(Part part)
    {
        if (_discoLights.Remove(part, out var live)) live.Dispose();
    }
    private void UpdateDisco(double dt)
    {
        if (_discoLights.Count == 0) return;
        var alive = new HashSet<Part>(VehicleProvider.GetAllVehicles(includeDebris: true).SelectMany(PartHelpers.GetAllParts), ReferenceEqualityComparer.Instance);
        foreach (var (part, live) in _discoLights.ToArray())
        {
            if (!alive.Contains(part)) { StopDisco(part); continue; }
            live.Update(dt);
        }
    }

    private IEnumerable<ILiveStateItem> GetDiscoItems()
    {
        foreach (var (part, live) in _discoLights.ToArray())
            yield return new LiveStateItem<DiscoLight>("disco/" + Key(part), "Disco light", part.DisplayName + " #" + part.InstanceId, live, state =>
            {
                ImGui.TextWrapped($"Elapsed {state.Elapsed:F1} s. Color: {state.Recipe.Color}; spread: {state.Recipe.Spread} ({state.SpotCount} spotlights); actuation drivers: {state.Actuators.Count}.");
                if (state.Recipe.Actuation && state.Actuators.Count == 0) ImGui.TextWrapped("No actuation driver owned here: unsupported, or shared with another Disco light. Applying a new recipe claims the assembly driver.");
                ImGui.Checkbox("Paused", ref state.Paused);
                var lightSwitch = part.LightSwitch ?? part.FullPart.LightSwitch;
                if (lightSwitch != null)
                {
                    bool enabled = lightSwitch.LightIsActive;
                    if (ImGui.Checkbox("Light switch (assembly)", ref enabled)) lightSwitch.LightIsActive = enabled;
                }
                if (ImGui.Button("Copy Disco recipe to workspace", new float2(-1, 0))) _disco = DraftJson.Clone(state.Recipe);
                if (ImGui.Button("Stop Disco and restore light", new float2(-1, 0))) StopDisco(part);
            }, !live.OwnsTemplates ? "Template replaced externally" : live.Paused ? "Paused" : "Animating");
    }
}
