using System;
using System.Linq;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod
{
    private readonly ImInputString _assetFilter = new(256);
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##pebbles");
        try
        {
            ImGui.TextDisabled($"Game settings: clutter {(KSA.GameSettings.GenerateGroundClutter() ? "on" : "off")}, collisions {(KSA.GameSettings.GetGroundClutterCollisions() ? "on" : "off")}, shadows {(KSA.GameSettings.GetGroundClutterShadowCasting() ? "on" : "off")}");
            _bodyId = PebblesUi.Choice("Celestial", _bodyId, _controller.BodyIds);
            ImGui.TextWrapped("Capture the body's current clutter to start. Editing and Workshop Done change only the recipe; Apply replaces this body's live override.");
            if (ImGui.Button("Refresh available bodies and assets", new float2(-1, 0))) Try(() => { _controller.Refresh(); _assets.Refresh(); });
            if (ImGui.Button("Capture target clutter into draft", new float2(-1, 0))) Try(() =>
            {
                _recipe = _controller.Capture(_bodyId);
                _ecotypeName = _recipe.Ecotypes.FirstOrDefault()?.Name ?? "";
                _objectId = SelectedEcotype?.Objects.FirstOrDefault()?.SourceId ?? "";
            });
            if (ImGui.Button("Capture original target clutter into draft", new float2(-1, 0))) Try(() =>
            {
                _recipe = _controller.CaptureOriginal(_bodyId);
                _ecotypeName = _recipe.Ecotypes.FirstOrDefault()?.Name ?? "";
                _objectId = SelectedEcotype?.Objects.FirstOrDefault()?.SourceId ?? "";
            });
            if (_message.Length > 0) ImGui.TextWrapped(_message);
            ImGui.TextWrapped(_controller.Status);
            foreach (var fault in _controller.Faults) ImGui.TextWrapped(fault);
            ImportControls();
            if (_recipe.Ecotypes.Count == 0) return;
            ImGui.InputText(FormField.Label("Filter mesh / texture assets"), _assetFilter);
            BulkControls();
            ImGui.TextDisabled($"Apply affects {_recipe.Ecotypes.Count} ecotypes, {_recipe.Ecotypes.Sum(e => e.Objects.Count)} variant slots and {_recipe.Ecotypes.Sum(e => e.Objects.Count) * 5} LOD slots on {_bodyId}.");
            _ecotypeName = PebblesUi.Choice("Ecotype", _ecotypeName, _recipe.Ecotypes.Select(e => e.Name));
            if (SelectedEcotype is { } ecotype)
            {
                ecotype.Enabled = PebblesUi.Toggle("Enabled", ecotype.Enabled);
                ecotype.CollisionMode = PebblesUi.Enum("Ecotype collision mode", ecotype.CollisionMode);
                if (WorkspaceUi.Header("Placement")) PlacementControls(ecotype.Placement);
                _objectId = PebblesUi.Choice("Object variant", _objectId, ecotype.Objects.Select(o => o.SourceId));
                if (SelectedObject is { } item) ObjectControls(item);
            }
            if (WorkspaceUi.Header("Resource budgets"))
            {
                _recipe.CandidateBudget = (long)PebblesUi.Number("Maximum generated candidates", (double)_recipe.CandidateBudget);
                _recipe.MeshVertexBudget = (long)PebblesUi.Number("Maximum private mesh vertices", (double)_recipe.MeshVertexBudget);
            }
            if (ImGui.Button("Validate recipe", new float2(-1, 0))) Try(() => { RecipeValidation.Validate(_recipe); _message = "Recipe structure is valid. Apply also checks assets and the target."; });
            if (ImGui.Button("Apply to selected celestial", new float2(-1, 0))) Try(() => { RecipeValidation.Validate(_recipe); _controller.QueueApply(_bodyId, _recipe); });
        }
        finally { SubmodUI.EndContentArea(); }
    }

    private void BulkControls()
    {
        if (!WorkspaceUi.Header("Make everything the same")) return;
        string[] scopes = ["Whole body", "Selected ecotype", "Selected variant"];
        _bulkScope = Array.IndexOf(scopes, PebblesUi.Choice("Bulk scope", scopes[_bulkScope], scopes));
        var targets = BulkTargets().ToArray();
        ImGui.TextDisabled($"{targets.Length} variants / {targets.Length * 5} LOD slots selected");
        _bulkMesh = PebblesUi.Choice("Replacement mesh", _bulkMesh, _assets.MeshIds, _assetFilter.ToString());
        if (ImGui.Button("Use mesh in every variant and all five LODs"))
            foreach (var item in targets) FillMesh(item, _bulkMesh);
        _bulkTexture = PebblesUi.Choice("Replacement diffuse texture", _bulkTexture, _assets.TextureIds, _assetFilter.ToString());
        if (ImGui.Button("Use diffuse texture in every material") && _bulkTexture.Length > 0)
            foreach (var m in targets.SelectMany(o => o.Lods).SelectMany(l => l.Materials)) { m.DiffuseId = _bulkTexture; m.SourceColors = true; }
        if (WorkspaceUi.Header("One material for selected slots"))
        {
            MaterialControls(_bulkMaterial);
            if (ImGui.Button("Fill selected LODs with this material"))
                foreach (var lod in targets.SelectMany(o => o.Lods)) lod.Materials = [RecipeCopy.Clone(_bulkMaterial)];
        }
        if (ImGui.Button("Copy selected variant appearance to bulk targets") && SelectedObject is { } source)
            foreach (var item in targets.Where(o => !ReferenceEquals(o, source)))
            { item.Lods = RecipeCopy.Clone(source.Lods); item.Transform = RecipeCopy.Clone(source.Transform); item.Collision = CollisionPolicy.None; }
        if (ImGui.Button("Fix each ecotype's size and yaw to its minimum"))
            foreach (var e in _recipe.Ecotypes.Where(e => _bulkScope == 0 || e.Name == _ecotypeName))
            { e.Placement.MaxScale = e.Placement.MinScale; e.Placement.MaxRotation = e.Placement.MinRotation; }
        if (ImGui.Button("Disable all ecotypes in draft")) foreach (var e in _recipe.Ecotypes) e.Enabled = false;
        if (ImGui.Button("Enable all ecotypes in draft")) foreach (var e in _recipe.Ecotypes) e.Enabled = true;
        ImGui.TextWrapped("Mesh replacement keeps variant identities and defaults to no collision. Choose KeepOriginal explicitly or author colliders in the Workshop before Apply.");
    }
    private IEnumerable<ObjectRecipe> BulkTargets() => _bulkScope switch
    {
        1 => SelectedEcotype?.Objects ?? [],
        2 => SelectedObject is { } o ? [o] : [],
        _ => _recipe.Ecotypes.SelectMany(e => e.Objects)
    };
    private static void FillMesh(ObjectRecipe item, string mesh)
    {
        if (mesh.Length == 0) return;
        item.Collision = CollisionPolicy.None;
        foreach (var lod in item.Lods)
        {
            lod.MeshIds = [mesh];
            if (lod.Materials.Count > 1) lod.Materials = [lod.Materials[0]];
        }
    }
    private void ObjectControls(ObjectRecipe item)
    {
        if (!WorkspaceUi.Header("Meshes, materials and collision", ImGuiTreeNodeFlags.DefaultOpen)) return;
        item.Collision = PebblesUi.Enum("Collision recipe", item.Collision);
        if (item.Collision == CollisionPolicy.KeepOriginal) ImGui.TextWrapped("Original colliders are retained independently of visual mesh transforms. They may extend beyond a replacement mesh.");
        item.MassKg = PebblesUi.Number("Mass (kg)", item.MassKg);
        if (ImGui.Button("Open mesh and collider Workshop", new float2(-1, 0)))
        {
            _workshopBody = _bodyId; _workshopEcotype = _ecotypeName; _workshopObject = _objectId;
            _workshop.Open(item, CompleteWorkshop);
        }
        if (ImGui.Button("Use bulk mesh for this variant's five LODs")) FillMesh(item, _bulkMesh);
        using (new FormGrid("objectTransform"))
        {
            item.Transform.Position = PebblesUi.Vector("Mesh translation (m)", item.Transform.Position);
            item.Transform.RotationDegrees = PebblesUi.Vector("Mesh rotation XYZ (degrees)", item.Transform.RotationDegrees);
            item.Transform.Scale = PebblesUi.Vector("Mesh scale XYZ", item.Transform.Scale);
        }
        var selected = PebblesUi.Choice("LOD (0 is closest)", _lod.ToString(), ["0", "1", "2", "3", "4"]);
        _lod = int.Parse(selected);
        var lod = item.Lods[_lod];
        lod.MinScreenSize = PebblesUi.Number("Minimum projected size (pixels)", lod.MinScreenSize);
        lod.CastShadows = PebblesUi.Toggle("LOD casts shadows", lod.CastShadows);
        for (int i = 0; i < lod.MeshIds.Count; i++)
        {
            ImGui.PushID(i);
            try
            {
                string mesh = PebblesUi.Choice("Mesh component", lod.MeshIds[i], _assets.MeshIds, _assetFilter.ToString());
                if (mesh != lod.MeshIds[i]) { lod.MeshIds[i] = mesh; item.Collision = CollisionPolicy.None; }
                if (ImGui.Button("Remove component") && lod.MeshIds.Count > 1) { lod.MeshIds.RemoveAt(i); i--; }
            }
            finally { ImGui.PopID(); }
        }
        if (ImGui.Button("Add bulk mesh as another component") && _bulkMesh.Length > 0) lod.MeshIds.Add(_bulkMesh);
        ImGui.TextWrapped("Every component and every primitive in this LOD is rendered together. Material slots map to the atlas's ordered source material indices.");
        for (int i = 0; i < lod.Materials.Count; i++)
        {
            ImGui.PushID(i + 1000);
            try { if (WorkspaceUi.Header($"Material slot {i}: {lod.Materials[i].SourceId}")) MaterialControls(lod.Materials[i]); }
            finally { ImGui.PopID(); }
        }
        if (ImGui.Button("Add material slot")) lod.Materials.Add(lod.Materials.Count > 0 ? RecipeCopy.Clone(lod.Materials[^1]) : new MaterialRecipe());
        if (lod.Materials.Count > 1 && ImGui.Button("Remove last material slot")) lod.Materials.RemoveAt(lod.Materials.Count - 1);
    }
    private void MaterialControls(MaterialRecipe m)
    {
        using var grid = new FormGrid("material");
        string filter = _assetFilter.ToString();
        m.DiffuseId = PebblesUi.Choice("Diffuse", m.DiffuseId, _assets.TextureIds.Prepend(""), filter);
        m.NormalId = PebblesUi.Choice("Normal", m.NormalId, _assets.TextureIds.Prepend(""), filter);
        m.PbrId = PebblesUi.Choice("AO / roughness / metal", m.PbrId, _assets.TextureIds.Prepend(""), filter);
        m.OpacityId = PebblesUi.Choice("Opacity", m.OpacityId, _assets.TextureIds.Prepend(""), filter);
        m.ThicknessId = PebblesUi.Choice("Thickness / transmission", m.ThicknessId, _assets.TextureIds.Prepend(""), filter);
        m.SourceColors = PebblesUi.Toggle("Preserve diffuse source colors", m.SourceColors);
        m.UseTerrainMask = PebblesUi.Toggle("Terrain mask", m.UseTerrainMask);
        m.DoubleSided = PebblesUi.Toggle("Double sided", m.DoubleSided);
        m.CastShadows = PebblesUi.Toggle("Material casts shadows", m.CastShadows);
        m.ReceiveShadows = PebblesUi.Toggle("Receive shadows", m.ReceiveShadows);
        m.BiasNormalsUp = PebblesUi.Toggle("Bias normals up", m.BiasNormalsUp);
        m.ApplyExtraSpec = PebblesUi.Toggle("Extra specular", m.ApplyExtraSpec);
        m.DistanceFadeDither = PebblesUi.Toggle("Distance fade dithering", m.DistanceFadeDither);
    }
}
