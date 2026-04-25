using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

public sealed class LoadSubPartsModal
{
    public const string PopupId = "Load SubParts##st_load_popup";

    public void Render(SubpartGenerationController gen)
    {
        ArgumentNullException.ThrowIfNull(gen);

        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        bool busy = gen.IsBusy;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_load_tbl", 2,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, 160f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Images per SubPart");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
                ImGui.TextWrapped("Generating subpart thumbnails is GPU-intensive. " +
                                 "Reduce this and Image Size on lower-end hardware.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            if (busy) ImGui.BeginDisabled();
            int views = gen.ViewCount;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("##st_load_views", ref views, 0.1f, 2, 32))
                gen.ViewCount = views;
            if (busy) ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Image Size");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetItemTooltip("Higher resolution = sharper thumbs, more VRAM.");
            ImGui.TableNextColumn();
            if (busy) ImGui.BeginDisabled();
            int sizeIdx = gen.ImageSizeIndex;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##st_load_imgsize", ref sizeIdx,
                           SubpartGenerationController.ImageSizeLabels,
                           SubpartGenerationController.ImageSizeLabels.Length))
                gen.ImageSizeIndex = sizeIdx;
            if (busy) ImGui.EndDisabled();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        if (busy && gen.ProgressTotal > 0)
        {
            float progress = (float)gen.ProgressCurrent / gen.ProgressTotal;
            ImGui.ProgressBar(progress, new float2(-1, 0),
                              $"{gen.ProgressCurrent}/{gen.ProgressTotal}");
        }
        else
        {
            string status = gen.State switch
            {
                GenerationState.Done => $"Done ({SubpartThumbnailCache.All.Count} subparts)",
                GenerationState.Failed => $"Failed: {gen.LastError}",
                _ => "Ready to generate"
            };

            float4 color = gen.State switch
            {
                GenerationState.Done => new float4(0.3f, 1f, 0.3f, 1f),
                GenerationState.Failed => new float4(1f, 0.3f, 0.3f, 1f),
                _ => new float4(0.7f, 0.7f, 0.7f, 1f)
            };

            ImGui.TextColored(color, status);
        }

        ImGui.Spacing();

        string genLabel = gen.HasGeneratedAtLeastOnce ? " Re-generate ##st_load_gen" : " Generate ##st_load_gen";
        if (busy) ImGui.BeginDisabled();
        if (ImGui.Button(genLabel))
        {
            if (gen.HasGeneratedAtLeastOnce)
                gen.Reset();
            gen.Generate();
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Close ##st_load_close"))
            ImGui.CloseCurrentPopup();
        if (busy) ImGui.EndDisabled();

        ImGui.EndPopup();
    }
}