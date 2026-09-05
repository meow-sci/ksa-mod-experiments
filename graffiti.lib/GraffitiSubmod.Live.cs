using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.GraffitiLib;

public sealed partial class GraffitiSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        yield return new LiveStateItem<GraffitiSubmod>("render-policy", "Decal rendering", "Global", this, _ =>
        {
            float distance = (float)DecalRenderer.MaxViewDistanceMetres; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Max draw distance"), ref distance, 500, 1000, 10_000_000)) DecalRenderer.MaxViewDistanceMetres = distance;
            ImGui.Checkbox("Debug boxes", ref DebugBox);
        });
        foreach (var entry in _decals.ToArray())
            yield return new LiveStateItem<DecalEntry>(entry.Id.ToString(), "Decal " + entry.ImageName,
                DescribeTarget(entry), entry, RenderDecalInspector, entry.Live ? "Active" : "Dormant");
        if (_decals.Count > 0)
            yield return new LiveStateItem<string>("selection", "Decal multi-selection", "All decals", "selection", _ => RenderPlacedList());
    }
    public void CancelAuthoringGesture() => Disarm();
    private void RenderDecalInspector(DecalEntry entry)
    {
        ImGui.Text(entry.ImageName);
        ImGui.Checkbox("Visible", ref entry.Visible);
        var position = Brutal.Numerics.float3.Pack(entry.Position); if (ImGui.DragFloat3(MeowSci.KsaAbstractions.FormField.Label("Position offset"), ref position, .01f)) entry.Position = new Brutal.Numerics.double3(position.X, position.Y, position.Z);
        float depth = (float)entry.Depth, rotation = (float)entry.RotationDeg;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Depth"), ref depth, .01f, .01f, 1000)) entry.Depth = depth;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Roll"), ref rotation, .25f, -180, 180)) entry.RotationDeg = rotation;
        float width = (float)entry.Width, height = (float)entry.Height, alpha = (float)entry.Alpha, brightness = (float)entry.Brightness;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Width"), ref width, .01f, .01f, 1000f)) entry.Width = width;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Height"), ref height, .01f, .01f, 1000f)) entry.Height = height;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Alpha"), ref alpha, .01f, 0f, 1f)) entry.Alpha = alpha;
        if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("Brightness"), ref brightness, .01f, .01f, 8f)) entry.Brightness = brightness;
        if (ImGui.Button(" Copy settings to form "))
        { _width = width; _height = height; _alpha = alpha; _brightness = brightness; _depth = (float)entry.Depth; _rollDeg = (float)entry.RotationDeg; _selectedLibraryIndex = Array.IndexOf(_libraryNames, entry.ImageName); Draft.Select("Decal", entry.ImageName); }
        if (ImGui.Button(" Remove decal ")) RemoveDecals(new[] { entry });
    }
}
