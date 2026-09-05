using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using KSA;
using MeowSci.KsaLights;
namespace MeowSci.ZippoLib;

public sealed partial class ZippoSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
foreach (var (id, part) in _managedLights.ToArray())
            yield return new LiveStateItem<Part>(id, part.DisplayName, part.Id + " (color/intensity shared by template)", part, p =>
            {
                bool alive = ResolveManagedPart(id) != null;
                ImGui.BeginDisabled(!alive);
                var color = LightController.ReadColor(p.Template); if (ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("Color"), ref color)) LightController.ApplyColor(p, color);
                float intensity = LightController.ReadIntensity(p.Template); if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Intensity"), ref intensity, .01f, 0, 100)) LightController.ApplyIntensity(p, intensity);
                var lightSwitch = p.LightSwitch ?? p.FullPart.LightSwitch;
                if (lightSwitch != null) { bool enabled = lightSwitch.LightIsActive; if (ImGui.Checkbox("Enabled", ref enabled)) lightSwitch.LightIsActive = enabled; }
                if (ImGui.Button("Copy settings to workspace", new float2(-1, 0))) { _currentColor = new float4(color.X, color.Y, color.Z, 1); _intensity = intensity; }
                ImGui.EndDisabled();
                var animation = _animationManager.GetActiveAnimation(id);
                if (animation != null) ImGui.ProgressBar((float)(animation.ElapsedSeconds / animation.DurationSeconds), new float2(-1, 0));
                ImGui.Text($"Queued: {_animationManager.GetQueueCount(id)}");
                if (ImGui.Button("Stop and clear animation queue", new float2(-1, 0))) _animationManager.CancelAll(id);
                if (ImGui.Button("Stop managing this light", new float2(-1, 0))) { _animationManager.CancelAll(id); _managedLights.Remove(id); }
            }, ResolveManagedPart(id) == null ? "Target missing" : _animationManager.IsAnimating(id) ? "Animating" : "Applied");
    }

}
