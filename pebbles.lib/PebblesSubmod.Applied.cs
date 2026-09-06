using Brutal.ImGuiApi;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod
{
    private void RenderAppliedClutter()
    {
        ImGui.Spacing();
        ImGui.SeparatorText("Applied clutter"u8);
        if (_controller.Live.Count == 0) ImGui.TextDisabled("No planet overrides applied.");
        foreach (var live in _controller.Live)
        {
            ImGui.PushID(live.BodyId);
            try
            {
                ImGui.SeparatorText(live.BodyId);
                ImGui.TextWrapped(live.Status);
                ImGui.TextDisabled($"{live.EcotypeCount} clutter types · {live.VertexCount:N0} private vertices · {live.MaterialCount} materials");
                foreach (var ecotype in live.Recipe.Ecotypes)
                    if (ImGui.Button($"Restore type: {ecotype.Name}"))
                        Try(() => _controller.RestoreEcotype(live.BodyId, ecotype.Name));
                if (ImGui.Button("Select this planet"))
                {
                    _bodyId = live.BodyId;
                    _recipeBody = live.BodyId;
                    _recipe = RecipeCopy.Clone(live.Recipe);
                    _targetTypes.Clear();
                    _allTypes = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Restore original clutter"))
                    Try(() => _controller.QueueRestore(live.BodyId));
            }
            finally { ImGui.PopID(); }
        }

        if (_assets.ImportedGlbCount > 0)
            ImGui.TextWrapped($"{_assets.ImportedGlbCount} imported GLB versions retained for previews and applied clutter.");
        if (ImGui.Button("Restore all and release resources")) Try(ReleaseAll);
    }
}
