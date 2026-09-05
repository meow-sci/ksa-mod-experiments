using KSA;
using System.Collections.Generic;
using System;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;
namespace MeowSci.HumbleArteestLib;
public sealed partial class HumbleArteestSubmod
{
    private PaintDraft _settings = new();
    private readonly ImInputString _paintFilter = new(128);
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##paint-draft");
        if (WorkspaceUi.Header("Vehicle paint", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("Brush"), ref _settings.Color);
            ImGui.Combo(MeowSci.KsaAbstractions.FormField.Label("Blend"), ref _settings.Blend, new[] { "Multiply", "Tint", "Replace" }, 3);
            ImGui.Combo(MeowSci.KsaAbstractions.FormField.Label("Scope"), ref _settings.Scope, new[] { "Selected parts", "Selected part types", "All parts" }, 3);
            var parts = PaintTargets.FlattenParts(PaintTargets.Enumerate()).ToList();
            if (_settings.Scope != 2)
            {
                ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##paint-filter", "Filter targets…", _paintFilter);
                var choices = _settings.Scope == 0 ? DraftOptions.Parts(parts) : DraftOptions.Strings(parts.Select(p => p.Id).Distinct());
                var selected = _settings.Scope == 0 ? _settings.Parts : _settings.Templates;
                if (ImGui.BeginListBox("##paint-targets", new float2(-1, 220)))
                {
                    foreach (var option in choices)
                        if (option.Label.Contains(_paintFilter.ToString(), StringComparison.OrdinalIgnoreCase))
                        { bool check = selected.Contains(option.Id); if (ImGui.Checkbox($"{option.Label}##{option.Id}", ref check)) { if (check) selected.Add(option.Id); else selected.Remove(option.Id); } }
                    ImGui.EndListBox();
                }
                var missing = selected.Except(choices.Select(c => c.Id)).ToArray();
                if (missing.Length > 0) ImGui.TextDisabled($"{missing.Length} unresolved selections. Re-select targets to apply.");
                ImGui.BeginDisabled(missing.Length > 0 || selected.Count == 0);
                if (ImGui.Button("Apply paint", new float2(-1, 0)))
                {
                    VehiclePaint.Enable(); VehiclePaint.BlendMode = (PaintBlendMode)_settings.Blend;
                    if (_settings.Scope == 1) foreach (var id in selected) VehiclePaint.SetTemplate(id, _settings.Color);
                    else { var ids = DraftOptions.Parts(parts); for (int i = 0; i < parts.Count; i++) if (selected.Contains(ids[i].Id)) VehiclePaint.SetPart(parts[i], _settings.Color); }
                }
                ImGui.EndDisabled();
            }
            else if (ImGui.Button("Apply global paint", new float2(-1, 0)))
            { VehiclePaint.Enable(); VehiclePaint.BlendMode = (PaintBlendMode)_settings.Blend; VehiclePaint.GlobalColor = _settings.Color; VehiclePaint.GlobalEnabled = true; }
        }
        if (WorkspaceUi.Header("Kitten materials", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.ColorEdit4(MeowSci.KsaAbstractions.FormField.Label("Color"), ref _settings.KittenColor);
            ImGui.Checkbox("All kitten materials", ref _settings.AllMaterials);
            if (!_settings.AllMaterials)
            {
                if (ImGui.Button("Discover materials")) KittenColor.Initialize();
                var materials = KittenColor.GetMaterials();
                if (ImGui.BeginListBox("##material-targets", new float2(-1, 180)))
                { foreach (var (name, handle) in materials)
                    { bool selected = _settings.Materials.Contains(name); if (ImGui.Checkbox($"{name}##material-{handle}", ref selected)) { if (selected) _settings.Materials.Add(name); else _settings.Materials.Remove(name); } }
                  ImGui.EndListBox(); }
                bool missing = _settings.Materials.Any(name => !materials.Any(m => m.Name == name));
                ImGui.BeginDisabled(missing || _settings.Materials.Count == 0);
                if (ImGui.Button("Apply to selected materials", new float2(-1, 0)))
                    foreach (var (name, handle) in materials) if (_settings.Materials.Contains(name)) KittenColor.ApplyToMaterial(handle, _settings.KittenColor);
                ImGui.EndDisabled();
                if (missing) ImGui.TextDisabled("Some saved material names are unresolved.");
            }
            else if (ImGui.Button("Apply to all kitten materials", new float2(-1, 0)))
            { if (KittenColor.IsInitialized || KittenColor.Initialize()) KittenColor.ApplyToAll(_settings.KittenColor); }
        }
        if (WorkspaceUi.Header("Engine emissive", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.SliderFloat(MeowSci.KsaAbstractions.FormField.Label("Temperature"), ref _settings.Temperature, 0, 1);
            ImGui.SliderFloat(MeowSci.KsaAbstractions.FormField.Label("TFI"), ref _settings.Tfi, 0, 1);
            ImGui.Checkbox("All engines", ref _settings.AllEngines);
            if (!_settings.AllEngines)
            {
                var engines = EngineTargets().ToArray();
                if (ImGui.BeginListBox("##engine-targets", new float2(-1, 180)))
                { foreach (var entry in engines)
                    { bool selected = _settings.Engines.Contains(entry.Id); if (ImGui.Checkbox($"{entry.Label}##{entry.Id}", ref selected)) { if (selected) _settings.Engines.Add(entry.Id); else _settings.Engines.Remove(entry.Id); } }
                  ImGui.EndListBox(); }
                bool missing = _settings.Engines.Any(id => !engines.Any(e => e.Id == id));
                ImGui.BeginDisabled(missing || _settings.Engines.Count == 0);
                if (ImGui.Button("Apply to selected engines", new float2(-1, 0)))
                    foreach (var entry in engines) if (_settings.Engines.Contains(entry.Id)) EngineEmissive.SetEngine(entry.Model, _settings.Temperature, _settings.Tfi);
                ImGui.EndDisabled();
                if (missing) ImGui.TextDisabled("Some saved engine targets are unresolved.");
            }
            else if (ImGui.Button("Apply to all engines", new float2(-1, 0)))
            { EngineEmissive.GlobalTemperature = _settings.Temperature; EngineEmissive.GlobalTfi = _settings.Tfi; EngineEmissive.GlobalEnabled = true; }
        }
        SubmodUI.EndContentArea();
    }
    private static IEnumerable<(string Id, string Label, PartModelDynamic Model)> EngineTargets()
    {
        foreach (var vehicle in VehicleProvider.GetAllVehicles())
            foreach (var part in PartHelpers.GetAllParts(vehicle))
            {
                var modules = part.Modules.Get<PartModelDynamicModule>().ToArray();
                for (int i = 0; i < modules.Length; i++)
                    yield return (PartIdentity.Get(part) + "/model/" + i, vehicle.Id + "/" + part.DisplayName + " #" + part.InstanceId + " / " + i, modules[i].PartModelDynamic);
            }
    }
}
