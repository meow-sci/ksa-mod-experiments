using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.RockyMcRockFaceLib;

namespace MeowSci.BloominOnionLib;

public sealed partial class BloominOnionSubmod
{
    private const string StockTextureLabel = "(stock Saturn asset)";
    private const string StockMeshLabel = "(stock ring rock)";

    private void RenderGeometrySection(Celestial body)
    {
        bool open = ImGui.CollapsingHeader("Geometry (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Ring plane and radii. Equatorial = tilted relative to the body's spin axis\n" +
                             "(0 deg = around the equator); Ecliptic = relative to the parent's frame.");
        if (!open) return;

        double bodyRadiusKm = body.MeanRadius / 1000.0;
        int frame = _editor.UseEclipticFrame ? 1 : 0;
        if (RockyUi.BeginFormTable("##bloominonion_frame"))
        {
            RockyUi.FormLabel("Frame");
            if (ImGui.RadioButton("Equatorial##bloominonion_eq", ref frame, 0)) _editor.UseEclipticFrame = false;
            ImGui.SameLine(0, 12);
            bool hasParent = body.Parent != null;
            if (!hasParent) ImGui.BeginDisabled();
            if (ImGui.RadioButton("Ecliptic##bloominonion_ecl", ref frame, 1)) _editor.UseEclipticFrame = true;
            if (!hasParent) ImGui.EndDisabled();
            RockyUi.EndFormTable();
        }

        if (RockyUi.BeginParamGrid("##bloominonion_geo"))
        {
            ImGui.TableNextRow();
            RockyUi.GridDrag("Inclination (deg)", "##bloominonion_inc", ref _editor.InclinationDeg, 0.1f, -180f, 180f, "%.2f");
            RockyUi.GridDrag("Asc. node (deg)", "##bloominonion_lan", ref _editor.LongitudeOfAscendingNodeDeg, 0.5f, 0f, 360f, "%.1f");
            ImGui.TableNextRow();
            float radiusSpeed = (float)Math.Max(1.0, bodyRadiusKm * 0.002);
            RockyUi.GridDrag("Inner radius (km)", "##bloominonion_inner", ref _editor.InnerRadiusKm, radiusSpeed, 1f, 10000000f, "%.0f");
            RockyUi.GridDrag("Outer radius (km)", "##bloominonion_outer", ref _editor.OuterRadiusKm, radiusSpeed, 1f, 10000000f, "%.0f");
            ImGui.TableNextRow();
            RockyUi.GridDrag("Detail scale", "##bloominonion_detail", ref _editor.DetailScale, 1f, 1f, 5000f, "%.0f");
            ImGui.TableNextColumn(); ImGui.TableNextColumn();
            RockyUi.EndParamGrid();
        }

        if (ImGui.Button(" Fit to Body ##bloominonion_fit"))
        {
            _editor.InnerRadiusKm = Math.Round(bodyRadiusKm * 1.3);
            _editor.OuterRadiusKm = Math.Round(bodyRadiusKm * 2.8);
        }
        ImGui.SetItemTooltip("Inner = 1.3 x body radius, outer = 2.8 x body radius.");
        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"{body.Id} radius {bodyRadiusKm:N0} km -> ring spans {_editor.InnerRadiusKm / bodyRadiusKm:F2}R to {_editor.OuterRadiusKm / bodyRadiusKm:F2}R");
    }

    private void RenderBandSection()
    {
        bool open = ImGui.CollapsingHeader("Ring Band (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("The flat color/alpha strip seen from afar (it also casts the ring's shadow on the planet).\n" +
                             "Painted: built from stripes here. Texture: any game texture, plus a control strip\n" +
                             "(R = rocks allowed, G = dust thickness) that must be uncompressed RGBA.");
        if (!open) return;

        int source = _editor.BandSource == RingBandSource.Painted ? 0 : 1;
        if (RockyUi.BeginFormTable("##bloominonion_bandsrc"))
        {
            RockyUi.FormLabel("Source");
            bool canPaint = PaintedTextureReference.IsSupported;
            if (!canPaint) ImGui.BeginDisabled();
            if (ImGui.RadioButton("Painted##bloominonion_painted", ref source, 0)) _editor.BandSource = RingBandSource.Painted;
            if (!canPaint) ImGui.EndDisabled();
            ImGui.SameLine(0, 12);
            if (ImGui.RadioButton("Texture##bloominonion_texture", ref source, 1)) _editor.BandSource = RingBandSource.Texture;
            RockyUi.EndFormTable();
        }

        if (_editor.BandSource == RingBandSource.Texture)
        {
            if (!RockyUi.BeginFormTable("##bloominonion_bandtex")) return;
            RockyUi.FormLabel("Band texture");
            RockyUi.IdCombo("##bloominonion_band", _controller.Catalog.TextureIds, ref _editor.BandTextureId, _assetFilter, StockTextureLabel);
            RockyUi.FormLabel("Control strip");
            RockyUi.IdCombo("##bloominonion_control", _controlTextureIds, ref _editor.ControlTextureId, _assetFilter, StockTextureLabel);
            RockyUi.EndFormTable();
            return;
        }

        RenderBandPreview();
        if (RockyUi.BeginFormTable("##bloominonion_base"))
        {
            RockyUi.FormLabel("Base color");
            ImGui.ColorEdit4("##bloominonion_basecolor", ref _editor.BaseColor, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.AlphaBar);
            RockyUi.EndFormTable();
        }
        if (RockyUi.BeginParamGrid("##bloominonion_noise"))
        {
            ImGui.TableNextRow();
            RockyUi.GridDrag("Ringlet noise", "##bloominonion_noiseamt", ref _editor.NoiseAmount, 0.005f, 0f, 1f);
            RockyUi.GridDrag("Noise scale", "##bloominonion_noisescale", ref _editor.NoiseScale, 0.01f, 0.05f, 8f);
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Noise seed");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f); ImGui.DragInt("##bloominonion_seed", ref _editor.NoiseSeed, 1f, 0, 100000);
            RockyUi.GridDrag("Rocks above alpha", "##bloominonion_thr", ref _editor.MeshCoverageThreshold, 0.005f, 0f, 1f);
            RockyUi.EndParamGrid();
        }
        RenderStripes();
    }

    /// <summary>A live strip of the painted band, inner edge on the left.</summary>
    private void RenderBandPreview()
    {
        const int samples = 256;
        const float height = 22f;
        var drawList = ImGui.GetWindowDrawList();
        float2 origin = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        drawList.AddRectFilled(origin, origin + new float2(width, height), (ImColor8)new float4(0.05f, 0.05f, 0.08f, 1f), 3f);
        float step = width / samples;
        for (int i = 0; i < samples; i++)
        {
            float4 color = RingBandPainter.Evaluate(_editor, i * RingBandPainter.Width / samples);
            if (color.W <= 0.002f) continue;
            float2 a = origin + new float2(i * step, 0f);
            float2 b = origin + new float2((i + 1) * step + 0.5f, height);
            drawList.AddRectFilled(a, b, (ImColor8)color, 0f);
        }
        ImGui.Dummy(new float2(width, height));
        ImGui.TextDisabled("inner edge <- preview -> outer edge");
    }

    private void RenderStripes()
    {
        ImGui.SeparatorText("Stripes");
        if (ImGui.Button(" + Stripe ##bloominonion_addstripe"))
            _editor.Stripes.Add(new RingStripe(0.3, 0.5, new float4(0.85f, 0.8f, 0.68f, 0.9f)));
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Saturn-like ##bloominonion_saturnstripes")) _editor.ResetStripesToSaturnLike();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Clear ##bloominonion_clearstripes")) _editor.Stripes.Clear();
        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("start/end are 0..1 across the ring; alpha = opacity");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##bloominonion_stripes", 5, flags))
        {
            ImGui.TableSetupColumn("##c", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("##s", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("##e", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("##f", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("##x", ImGuiTableColumnFlags.WidthFixed, 28f);
            int remove = -1;
            for (int i = 0; i < _editor.Stripes.Count; i++)
            {
                var stripe = _editor.Stripes[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.ColorEdit4("##col", ref stripe.Color,
                    ImGuiColorEditFlags.Float | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                float start = (float)stripe.Start;
                if (ImGui.DragFloat("##start", ref start, 0.002f, 0f, 1f, "start %.3f")) stripe.Start = start;
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                float end = (float)stripe.End;
                if (ImGui.DragFloat("##end", ref end, 0.002f, 0f, 1f, "end %.3f")) stripe.End = end;
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                float feather = (float)stripe.Feather;
                if (ImGui.DragFloat("##feather", ref feather, 0.0005f, 0f, 0.25f, "soft %.3f")) stripe.Feather = feather;
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("x")) remove = i;
                ImGui.PopID();
            }
            ImGui.EndTable();
            if (remove >= 0) _editor.Stripes.RemoveAt(remove);
        }
        ImGui.PopStyleVar();
    }

    private void RenderVolumetricsSection()
    {
        bool open = ImGui.CollapsingHeader("Volumetric Dust (?)");
        ImGui.SetItemTooltip("The raymarched dust seen up close. Thickness, render distance and step size are\n" +
                             "interpolated between min/max by the control strip's G channel.");
        if (!open) return;

        if (!RockyUi.BeginParamGrid("##bloominonion_vol")) return;
        ImGui.TableNextRow();
        RockyUi.GridDrag("Min thickness (km)", "##bloominonion_vmint", ref _editor.VolumeMinThicknessKm, 0.05f, 0.01f, 100000f);
        RockyUi.GridDrag("Max thickness (km)", "##bloominonion_vmaxt", ref _editor.VolumeMaxThicknessKm, 5f, 0.01f, 100000f, "%.0f");
        ImGui.TableNextRow();
        RockyUi.GridDrag("Min draw dist (km)", "##bloominonion_vmind", ref _editor.VolumeMinRenderDistanceKm, 5f, 1f, 10000000f, "%.0f");
        RockyUi.GridDrag("Max draw dist (km)", "##bloominonion_vmaxd", ref _editor.VolumeMaxRenderDistanceKm, 50f, 1f, 10000000f, "%.0f");
        ImGui.TableNextRow();
        RockyUi.GridDrag("Min step (km)", "##bloominonion_smin", ref _editor.StepMinSizeKm, 0.01f, 0.001f, 10000f, "%.3f");
        RockyUi.GridDrag("Max step (km)", "##bloominonion_smax", ref _editor.StepMaxSizeKm, 1f, 0.001f, 100000f, "%.1f");
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Step scale");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat("##bloominonion_sscale", ref _editor.StepScale, 0.0005f, 0.0001f, 1f, "%.4f");
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Fade to rocks");
        ImGui.TableNextColumn(); ImGui.Checkbox("##bloominonion_fade", ref _editor.FadeToMeshes);
        RockyUi.EndParamGrid();
    }

    private void RenderRockFieldSection()
    {
        bool open = ImGui.CollapsingHeader("Rock Field (?)");
        ImGui.SetItemTooltip("The instanced rock meshes drawn within draw distance of the ring plane.\n" +
                             "High density x large draw distance costs VRAM and GPU time.");
        if (!open) return;

        if (RockyUi.BeginParamGrid("##bloominonion_rocks"))
        {
            ImGui.TableNextRow();
            RockyUi.GridDrag("Rock size (m)", "##bloominonion_rsize", ref _editor.ObjectSizeM, 0.1f, 0.1f, 5000f);
            RockyUi.GridDrag("Density (/km^3)", "##bloominonion_rdens", ref _editor.ObjectDensityPerKm3, 10f, 1f, 1000000f, "%.0f");
            ImGui.TableNextRow();
            RockyUi.GridDrag("Draw dist (km)", "##bloominonion_rdist", ref _editor.ObjectRenderDistanceKm, 0.5f, 1f, 500f, "%.1f");
            RockyUi.GridDrag("Thickness (km)", "##bloominonion_rthick", ref _editor.ObjectThicknessKm, 0.05f, 0.01f, 1000f);
            RockyUi.EndParamGrid();
        }
        RenderInstanceEstimate();

        ImGui.SeparatorText("LODs");
        if (_editor.Lods.Count >= RingDefinition.MaxLods) ImGui.BeginDisabled();
        if (ImGui.Button(" + LOD ##bloominonion_addlod"))
        {
            float last = _editor.Lods.Count > 0 ? _editor.Lods[^1].MinScreenSizePixels : 64f;
            _editor.Lods.Add(new RingLodDefinition(Math.Max(1f, last / 2f)));
        }
        if (_editor.Lods.Count >= RingDefinition.MaxLods) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        if (_editor.Lods.Count <= 1) ImGui.BeginDisabled();
        if (ImGui.Button(" - LOD ##bloominonion_dellod")) _editor.Lods.RemoveAt(_editor.Lods.Count - 1);
        if (_editor.Lods.Count <= 1) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Stock ladder ##bloominonion_stocklods")) _editor.ResetLodsToStock();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##bloominonion_lods", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##l", ImGuiTableColumnFlags.WidthFixed, 56f);
            ImGui.TableSetupColumn("##px", ImGuiTableColumnFlags.WidthFixed, 110f);
            ImGui.TableSetupColumn("##mesh", ImGuiTableColumnFlags.WidthStretch);
            for (int i = 0; i < _editor.Lods.Count; i++)
            {
                var lod = _editor.Lods[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text($"LOD {i}");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat("##px", ref lod.MinScreenSizePixels, 0.5f, 0.5f, 512f, ">= %.0f px");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
                RockyUi.IdCombo("##mesh", _controller.Catalog.MeshIds, ref lod.MeshId, _assetFilter, StockMeshLabel);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
        RenderMeshCostSummary();

        ImGui.SeparatorText("Rock material");
        if (!RockyUi.BeginFormTable("##bloominonion_material")) return;
        RockyUi.FormLabel("Diffuse");
        RockyUi.IdCombo("##bloominonion_diffuse", _controller.Catalog.TextureIds, ref _editor.DiffuseId, _assetFilter, StockTextureLabel);
        RockyUi.FormLabel("Normal");
        RockyUi.IdCombo("##bloominonion_normal", _controller.Catalog.NormalTextureIds, ref _editor.NormalId, _assetFilter, StockTextureLabel);
        RockyUi.FormLabel("AoRoughMetal");
        RockyUi.IdCombo("##bloominonion_pbr", _controller.Catalog.TextureIds, ref _editor.PbrId, _assetFilter, StockTextureLabel);
        RockyUi.EndFormTable();
    }

    /// <summary>Mirrors PlanetaryRingsRenderData's chunk sizing so the cost of density x distance is visible.</summary>
    private void RenderInstanceEstimate()
    {
        double areaKm = _editor.ObjectRenderDistanceKm * 2.0;
        double perChunk = _editor.ObjectDensityPerKm3 * areaKm * areaKm * areaKm / (40.0 * 40.0 * 40.0);
        const double stock = 3125.0;
        if (perChunk > stock * 8)
            ImGui.TextColored(ErrorColor, $"~{perChunk:N0} rocks per chunk (stock {stock:N0}) - very heavy");
        else if (perChunk > stock * 3)
            ImGui.TextColored(WarningColor, $"~{perChunk:N0} rocks per chunk (stock {stock:N0}) - heavy");
        else
            ImGui.TextDisabled($"~{perChunk:N0} rocks per chunk (stock {stock:N0})");
    }

    private void RenderMeshCostSummary()
    {
        for (int i = 0; i < _editor.Lods.Count; i++)
        {
            string id = _editor.Lods[i].MeshId;
            if (id.Length == 0) continue;
            int indexCount = _controller.Catalog.GetMeshIndexCount(id);
            if (indexCount == 0) indexCount = _controller.MeshFactory.GetConvertedIndexCount(id);
            if (indexCount == 0)
            {
                ImGui.TextDisabled($"LOD {i}: size unknown until first Apply");
                continue;
            }
            int triangles = indexCount / 3;
            if (triangles > 10000) ImGui.TextColored(ErrorColor, $"LOD {i}: {triangles:N0} tris - very heavy");
            else if (triangles > 3000) ImGui.TextColored(WarningColor, $"LOD {i}: {triangles:N0} tris - heavy");
            else ImGui.TextDisabled($"LOD {i}: {triangles:N0} tris");
        }
    }
}
