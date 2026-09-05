using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;
using MeowSci.Unscience.Contracts;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod
{
    private DraftBindings? _draft;
    public DraftBindings Draft => _draft ??= BindDraft();
    public DraftState CaptureDraft() => Draft.Capture();
    public Action PrepareRestore(DraftState state) => Draft.Prepare(state);
    private DraftBindings BindDraft()
    {
        var draft = new DraftBindings();
        _glbBrowser.Bind(draft);
        draft.Text("glbPath", _glbPath);
        draft.Value("glbSelection", () => _glbSelected, v => { _glbSelected = v; _glbOptions = []; _glbStatus = "Load file to browse its meshes, or explicitly preview/apply its saved selection."; },
            validate: v => { if (v.Length > 0) _ = GlbIdentity.Parse(v); });
        draft.Value("body", () => _bodyId, v => _bodyId = v, target: true);
        draft.Value("ecotype", () => _ecotypeName, v => _ecotypeName = v, target: true);
        draft.Value("object", () => _objectId, v => _objectId = v, target: true);
        draft.Value("recipe", () => _recipe, v => _recipe = v, validate: RecipeValidation.Validate);
        draft.Value("lod", () => _lod, v => _lod = v, validate: v => { if (v is < 0 or > 4) throw new InvalidOperationException("Invalid LOD."); });
        draft.Value("bulkScope", () => _bulkScope, v => _bulkScope = v, validate: v => { if (v is < 0 or > 2) throw new InvalidOperationException("Invalid bulk scope."); });
        draft.Value("bulkMaterial", () => _bulkMaterial, v => _bulkMaterial = v, validate: RecipeValidation.Material);
        draft.Value("bulkMesh", () => _bulkMesh, v => _bulkMesh = v);
        draft.Value("bulkTexture", () => _bulkTexture, v => _bulkTexture = v);
        draft.Value("workshopBody", () => _workshopBody, v => _workshopBody = v, target: true);
        draft.Value("workshopEcotype", () => _workshopEcotype, v => _workshopEcotype = v, target: true);
        draft.Value("workshopObject", () => _workshopObject, v => _workshopObject = v, target: true);
        draft.Value("workshop", () => _workshop.State, v => _workshop.State = v, validate: WorkshopValidation.Validate);
        draft.Text("assetFilter", _assetFilter);
        return draft;
    }
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (_assets.ImportedGlbCount > 0)
            yield return new LiveStateItem<ClutterAssets>("glb-imports", "Imported GLB assets", "Pebbles", _assets, assets =>
            {
                ImGui.TextWrapped($"{assets.ImportedGlbCount} imported content versions retained for previews and live clutter. Release all Pebbles state to reclaim imports safely.");
                if (ImGui.Button("Release all Pebbles state")) ReleaseLiveState();
            });
        foreach (var item in _controller.Live)
            yield return new LiveStateItem<ClutterLiveRecord>(item.BodyId, "Ground clutter override", item.BodyId, item.Status, item, live =>
            {
                ImGui.TextWrapped($"{live.Recipe.Ecotypes.Count} ecotypes on {live.BodyId}. Applied state is independent of the authoring recipe.");
                ImGui.TextDisabled($"{live.EcotypeCount} ecotypes · {live.VertexCount:N0} private vertices · {live.MaterialCount} material slots");
                foreach (var ecotype in live.Recipe.Ecotypes)
                    if (ImGui.Button($"Restore ecotype: {ecotype.Name}")) Try(() => _controller.RestoreEcotype(live.BodyId, ecotype.Name));
                if (ImGui.Button("Copy applied recipe to workspace")) { _bodyId = live.BodyId; _recipe = RecipeCopy.Clone(live.Recipe); }
                if (ImGui.Button("Restore original clutter")) Try(() => _controller.QueueRestore(live.BodyId));
            });
    }
}
