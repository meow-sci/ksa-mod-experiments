using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
namespace MeowSci.HumbleArteestLib;
public sealed partial class HumbleArteestSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (VehiclePaint.Active || VehiclePaint.HasAnyPaint)
            yield return new LiveStateItem<VehiclePaintSubmod>("paint-policy", "Paint shader and global color", "Global policy", _vehiclePaint, _ => RenderPaintPolicy());
        foreach (var part in VehiclePaint.PaintedParts.ToArray())
            yield return new LiveStateItem<Part>("paint/" + part.InstanceId, "Part paint", part.DisplayName + " #" + part.InstanceId, part, p =>
            {
                if (VehiclePaint.TryGetPartColor(p, out var color))
                { if (ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("Color"), ref color)) VehiclePaint.SetPart(p, color);
                  if (ImGui.Button("Copy brush to workspace")) _settings.Color = color; }
                if (ImGui.Button("Remove paint")) VehiclePaint.ClearPart(p);
            });
        foreach (var id in VehiclePaint.PaintedTemplates.ToArray())
            yield return new LiveStateItem<string>("paint-type/" + id, "Part-type paint", id, id, type =>
            {
                if (VehiclePaint.TryGetTemplateColor(type, out var color))
                { if (ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("Color"), ref color)) VehiclePaint.SetTemplate(type, color);
                  if (ImGui.Button("Copy brush to workspace")) _settings.Color = color; }
                if (ImGui.Button("Remove paint")) VehiclePaint.ClearTemplate(type);
            });
        foreach (var (handle, applied) in KittenColor.Overrides.ToArray())
        {
            string name = KittenColor.GetMaterials().FirstOrDefault(m => m.Handle == handle).Name ?? "Material " + handle;
            yield return new LiveStateItem<int>("material/" + handle, "Kitten material", name, handle, h =>
            { var color = applied; if (ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label("Color"), ref color)) KittenColor.ApplyToMaterial(h, color);
              if (ImGui.Button("Copy color to workspace")) _settings.KittenColor = color;
              if (ImGui.Button("Reset material")) KittenColor.ResetMaterial(h); });
        }
        if (EngineEmissive.GlobalEnabled)
            yield return new LiveStateItem<EngineEmissiveSubmod>("engine-policy", "Global engine emissive", "All dynamic parts", _engineEmissive, _ =>
            {
                float temperature = EngineEmissive.GlobalTemperature, tfi = EngineEmissive.GlobalTfi;
                if (ImGui.SliderFloat(MeowSci.KsaAbstractions.FormField.Label("Temperature"), ref temperature, 0, 1)) EngineEmissive.GlobalTemperature = temperature;
                if (ImGui.SliderFloat(MeowSci.KsaAbstractions.FormField.Label("TFI"), ref tfi, 0, 1)) EngineEmissive.GlobalTfi = tfi;
                if (ImGui.Button("Copy settings to workspace")) { _settings.Temperature = temperature; _settings.Tfi = tfi; }
                if (ImGui.Button("Remove global override")) EngineEmissive.GlobalEnabled = false;
            });
        foreach (var (model, settings) in EngineEmissive.Overrides.ToArray())
            yield return new LiveStateItem<PartModelDynamic>("engine/" + LiveIdentity.Get(model), "Engine emissive", "Dynamic engine instance", model, m =>
            {
                float temperature = settings.Temperature, tfi = settings.Tfi;
                ImGui.SetNextItemWidth(-1); bool changed = ImGui.SliderFloat("Temperature", ref temperature, 0, 1);
                ImGui.SetNextItemWidth(-1); changed |= ImGui.SliderFloat("TFI", ref tfi, 0, 1);
                if (changed) EngineEmissive.SetEngine(m, temperature, tfi);
                if (ImGui.Button("Copy settings to workspace")) { _settings.Temperature = temperature; _settings.Tfi = tfi; }
                if (ImGui.Button("Remove override")) EngineEmissive.ClearEngine(m);
            });
    }
    private void RenderPaintPolicy()
    {
        bool enabled = VehiclePaint.Active;
        if (ImGui.Checkbox("Enable paint shader", ref enabled)) { if (enabled) VehiclePaint.Enable(); else VehiclePaint.Disable(); }
        int blend = (int)VehiclePaint.BlendMode; if (ImGui.Combo(MeowSci.KsaAbstractions.FormField.Label("Blend"), ref blend, new[] { "Multiply", "Tint", "Replace" }, 3)) VehiclePaint.BlendMode = (PaintBlendMode)blend;
        bool all = VehiclePaint.GlobalEnabled; if (ImGui.Checkbox("Paint all parts", ref all)) VehiclePaint.GlobalEnabled = all;
        var color = VehiclePaint.GlobalColor; if (ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("Color"), ref color)) VehiclePaint.GlobalColor = color;
        if (ImGui.Button("Clear all paint")) VehiclePaint.ClearAllPaint();
    }
}
