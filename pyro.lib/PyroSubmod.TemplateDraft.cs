using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
namespace MeowSci.PyroLib;
public sealed partial class PyroSubmod
{
    private string _templateDraftId = "";
    private ExhaustTemplateRecipe? _templateDraft;
    private sealed record TemplateOverride(VolumetricExhaustTemplate Template, ExhaustTemplateRecipe Original, ExhaustTemplateRecipe Editor);
    private readonly Dictionary<string, TemplateOverride> _templateOverrides = new();
    private void RenderTemplateEditorSection()
    {
        if (!WorkspaceUi.Header("Shared exhaust template")) return;
        if (ImGui.BeginCombo(MeowSci.KsaAbstractions.FormField.Label("Template to edit"), _templateDraftId.Length == 0 ? "Select…" : _templateDraftId))
        { foreach (var id in PlumeTemplates.GetTemplateIds()) if (ImGui.Selectable(id, id == _templateDraftId)) _templateDraftId = id; ImGui.EndCombo(); }
        if (ImGui.Button("Copy template values into editor", new float2(-1, 0)))
        { var template = PlumeTemplates.Get(_templateDraftId); if (template != null) _templateDraft = ExhaustTemplateRecipe.Capture(template); }
        if (_templateDraft == null) return;
        _templateDraft.Render();
        if (ImGui.Button("Apply shared template", new float2(-1, 0))) ApplyTemplateRecipe(_templateDraftId, _templateDraft);
    }
    private void ApplyTemplateRecipe(string id, ExhaustTemplateRecipe recipe)
    {
        var template = PlumeTemplates.Get(id); if (template == null) return;
        var original = _templateOverrides.TryGetValue(id, out var prior) ? prior.Original : ExhaustTemplateRecipe.Capture(template);
        var copy = DraftJson.Clone(recipe); copy.Apply(template);
        _templateOverrides[id] = new(template, original, copy);
        TemplateRefresher.NotifyTemplateChanged(template, this);
    }
}
