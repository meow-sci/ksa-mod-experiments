using System;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
using MeowSci.KsaLights;
namespace MeowSci.ZippoLib;
public sealed partial class ZippoSubmod
{
    private readonly System.Collections.Generic.Dictionary<string, Part> _managedLights = new();
    private int _draftVehicle = -1;
    private int _draftPart = -1;
    private System.Collections.Generic.List<Part> DraftLightParts()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        return _draftVehicle >= 0 && _draftVehicle < vehicles.Count ? LightController.GetLightParts(vehicles[_draftVehicle]) : new();
    }
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##zippo-draft");
        ImGui.Checkbox("Light enabled", ref _lightEnabled);
        ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Intensity"), ref _intensity, .01f, 0, 100);
        ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label("Color"), ref _currentColor);
        if (WorkspaceUi.Header("Named colors"))
        {
            ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##light-colors", "Filter colors…", _animStartColorFilter);
            if (ImGui.BeginListBox("##light-color-list", new float2(-1, 160)))
            { foreach (var (name, color) in XkcdColorHelper.GetAll())
                if (MatchesFilter(_animStartColorFilter, name) && ImGui.Selectable(name)) _currentColor = color;
              ImGui.EndListBox(); }
        }
        var parts = DraftLightParts();
        Part? part = _draftPart >= 0 && _draftPart < parts.Count ? parts[_draftPart] : null;
        ImGui.BeginDisabled(part == null || !Draft.SelectionsResolved);
        if (ImGui.Button("Apply light settings", new float2(-1, 0)) && part != null)
        {
            StopDisco(part);
            _managedLights[Key(part)] = part;
            LightController.ApplyColor(part, new float3(_currentColor.X, _currentColor.Y, _currentColor.Z));
            LightController.ApplyIntensity(part, _intensity);
            var lightSwitch = part.LightSwitch ?? part.FullPart.LightSwitch;
            if (lightSwitch != null) lightSwitch.LightIsActive = _lightEnabled;
            else if (!_lightEnabled) LightController.ApplyIntensity(part, 0);
        }
        ImGui.EndDisabled();
        if (WorkspaceUi.Header("Animation recipe", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label("Start color"), ref _animStartColor4);
            ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label("End color"), ref _animEndColor4);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Start intensity"), ref _animStartIntensity, .01f, 0, 100);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("End intensity"), ref _animEndIntensity, .01f, 0, 100);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Duration (s)"), ref _animDuration, .1f, .1f, 3600);
            ImGui.Combo(MeowSci.KsaAbstractions.FormField.Label("Easing"), ref _animEasingIdx, new[] { "Linear", "Ease In", "Ease Out", "Ease In-Out" }, 4);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Start power"), ref _animPowerStart, .1f, .1f, 20);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("End power"), ref _animPowerEnd, .1f, .1f, 20);
            ImGui.BeginDisabled(part == null || !Draft.SelectionsResolved);
            if (ImGui.Button("Queue animation", new float2(-1, 0)) && part != null)
            {
                StopDisco(part);
                _managedLights[Key(part)] = part;
                var animation = new LightAnimation(new float3(_animStartColor4.X, _animStartColor4.Y, _animStartColor4.Z),
                    new float3(_animEndColor4.X, _animEndColor4.Y, _animEndColor4.Z), _animStartIntensity, _animEndIntensity,
                    Math.Max(.1f, _animDuration), (EasingType)_animEasingIdx, _animPowerStart, _animPowerEnd);
                if (!_animationManager.Enqueue(Key(part), animation)) _animQueueError = "Animation queue is full.";
            }
            ImGui.EndDisabled();
            if (_animQueueError != null) ImGui.TextDisabled(_animQueueError);
        }
        RenderDisco(part);
        SubmodUI.EndContentArea();
    }
    private static string Key(Part part) => LiveIdentity.Get(part);
    private Part? ResolveManagedPart(string key)
    {
        if (!_managedLights.TryGetValue(key, out var part)) return null;
        foreach (var vehicle in VehicleProvider.GetAllVehicles()) if (LightController.GetLightParts(vehicle).Contains(part)) return part;
        return null;
    }
}
