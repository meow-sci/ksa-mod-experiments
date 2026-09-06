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
        draft.Value("replacement", () => _replacement, v => _replacement = v, validate: RecipeValidation.Object);
        draft.Value("targetTypes", () => _targetTypes, v => _targetTypes = v, target: true,
            validate: v => { if (v == null || v.Count > 128 || v.Exists(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Invalid clutter target selection."); });
        draft.Value("allTypes", () => _allTypes, v => _allTypes = v, target: true);
        draft.Value("recipeBody", () => _recipeBody, v => _recipeBody = v, target: true);
        _glbBrowser.Bind(draft);
        draft.Text("glbPath", _glbPath);
        draft.Value("glbSelection", () => _glbSelected, v => { _glbSelected = v; _glbOptions = []; _glbStatus = "Load file to browse its meshes, or explicitly preview/apply its saved selection."; },
            validate: v => { if (v.Length > 0) _ = GlbIdentity.Parse(v); });
        draft.Value("body", () => _bodyId, v => _bodyId = v, target: true);
        draft.Value("targetRecipe", () => _recipe, v => _recipe = v, target: true, validate: RecipeValidation.Validate);
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
                if (ImGui.Button("Select this planet in authoring form")) { _bodyId = live.BodyId; _recipeBody = live.BodyId; _recipe = RecipeCopy.Clone(live.Recipe); _targetTypes.Clear(); _allTypes = false; }
                if (ImGui.Button("Restore original clutter")) Try(() => _controller.QueueRestore(live.BodyId));
            });
    }
}
