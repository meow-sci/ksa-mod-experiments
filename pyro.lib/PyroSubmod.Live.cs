using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.PyroLib;

public sealed partial class PyroSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        foreach (var (id, entry) in _templateOverrides.ToArray())
            yield return new LiveStateItem<TemplateOverride>("template/" + id, "Exhaust template", id + " (all engines and plumes)", entry, item =>
            {
                item.Editor.Render();
                if (ImGui.Button("Apply live edits")) ApplyTemplateRecipe(id, item.Editor);
                if (ImGui.Button("Copy settings to workspace")) { _templateDraftId = id; _templateDraft = DraftJson.Clone(item.Editor); }
                if (ImGui.Button("Restore original template")) { item.Original.Apply(item.Template); TemplateRefresher.NotifyTemplateChanged(item.Template, this); _templateOverrides.Remove(id); }
            });
        if (_plumes.Count > 0) yield return new LiveStateItem<PyroSubmod>("all", "All plumes", "Bulk controls", this, _ => RenderBulkToggles());
        foreach (var entry in _plumes.ToArray())
            yield return new LiveStateItem<PlumeEntry>(entry.Id.ToString(), "Plume " + entry.Id, entry.Vehicle.Id + "/" + entry.Part.Id, entry, RenderLiveItem);
    }
    private void RenderLiveItem(PlumeEntry entry)
    {
        if (ImGui.Button(" Copy settings to form ")) { _pendingPreset = PlumePreset.FromPlume(entry); _pendingPosition = entry.Position; _pendingRotation = entry.Rotation; _pendingTemplateIndex = Array.IndexOf(PlumeTemplates.GetTemplateIds(), entry.TemplateId); Draft.Select("Template", entry.TemplateId); }
        PlumeEntry? remove = null;
        RenderPlumeSection(entry, _plumes.IndexOf(entry), ref remove);
        RenderPresetModals();
        if (remove != null) RemovePlume(remove);
    }
}
