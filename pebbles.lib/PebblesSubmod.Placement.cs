using System;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.PebblesLib;

public sealed partial class PebblesSubmod
{
    private void PlacementControls(PlacementRecipe p)
    {
        using (new FormGrid("placement"))
        {
            p.Separation = PebblesUi.Number("Candidate separation (m)", p.Separation);
            p.Range = PebblesUi.Number("Generation range (m)", p.Range);
            p.MinScale = PebblesUi.Vector("Minimum instance scale XYZ", p.MinScale);
            p.MaxScale = PebblesUi.Vector("Maximum instance scale XYZ", p.MaxScale);
            p.Orientation = PebblesUi.Enum("Orientation", p.Orientation);
            ImGui.BeginDisabled(p.Orientation == OrientationMode.SurfaceNormalAndGradient);
            try
            {
                p.MinRotation = PebblesUi.Number("Minimum yaw (degrees)", p.MinRotation);
                p.MaxRotation = PebblesUi.Number("Maximum yaw (degrees)", p.MaxRotation);
            }
            finally { ImGui.EndDisabled(); }
            p.DistributionId = PebblesUi.Choice("Distribution texture", p.DistributionId, _assets.TextureIds, _assetFilter.ToString());
            p.DistributionTiling = PebblesUi.Number("Distribution tiling", p.DistributionTiling);
            p.SlopeStrength = PebblesUi.Number("Slope mask strength", p.SlopeStrength);
            p.SlopeContrast = PebblesUi.Number("Slope mask contrast", p.SlopeContrast);
            p.SlopeBias = PebblesUi.Number("Slope mask bias", p.SlopeBias);
            p.UseObjectTypeTexture = PebblesUi.Toggle("Use object type texture", p.UseObjectTypeTexture);
            p.ObjectTypeTextureId = PebblesUi.Choice("Object type texture", p.ObjectTypeTextureId, _assets.TextureIds, _assetFilter.ToString());
            p.ObjectTypeTiling = PebblesUi.Number("Object type tiling", p.ObjectTypeTiling);
            p.ObjectTypeJitter = PebblesUi.Number("Object type jitter", p.ObjectTypeJitter);
            p.AllBiomes = PebblesUi.Toggle("All biomes", p.AllBiomes);
        }
        if (!p.AllBiomes)
        {
            ImGui.TextWrapped("Exact biome aliases (one per entry). Unavailable aliases block Apply.");
            for (int i = 0; i < p.Biomes.Count; i++)
            {
                ImGui.PushID(i + 2000);
                try
                {
                    p.Biomes[i] = PebblesUi.Choice("Biome alias", p.Biomes[i], _controller.BiomeIds(_bodyId));
                    if (ImGui.Button("Remove biome")) { p.Biomes.RemoveAt(i); i--; }
                }
                finally { ImGui.PopID(); }
            }
            if (ImGui.Button("Add biome alias")) p.Biomes.Add("");
        }
        ImGui.TextWrapped("Collidable ecotypes require uniform XYZ instance scales. Gradient orientation ignores yaw; smooth normal orientation requires collision disabled.");
        if (!WorkspaceUi.Header("Altitude density curve")) return;
        for (int i = 0; i < p.AltitudeCurve.Count; i++)
        {
            ImGui.PushID(i + 3000);
            try
            {
                var point = p.AltitudeCurve[i];
                using (new FormGrid("point"))
                {
                    point.Altitude = PebblesUi.Number("Altitude (m)", point.Altitude);
                    point.Density = PebblesUi.Number("Density", point.Density);
                    point.InTangent = PebblesUi.Number("Incoming tangent", point.InTangent);
                    point.OutTangent = PebblesUi.Number("Outgoing tangent", point.OutTangent);
                }
                if (p.AltitudeCurve.Count > 2 && ImGui.Button("Remove curve point")) { p.AltitudeCurve.RemoveAt(i); i--; }
            }
            finally { ImGui.PopID(); }
        }
        if (ImGui.Button("Add curve point")) p.AltitudeCurve.Add(new() { Altitude = p.AltitudeCurve.Max(k => k.Altitude) + 1000 });
        if (ImGui.Button("Sort points by altitude")) p.AltitudeCurve.Sort((a, b) => a.Altitude.CompareTo(b.Altitude));
    }
}
