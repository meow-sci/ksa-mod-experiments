using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.InanimateCarbonRodLib;

public sealed class InanimeCarbonicRodSubmod : ISubmod
{
    public string Name => "Inanimate Carbon Rod";

    private readonly SubpartThumbnailGenerator _generator = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        _generator.Update();
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##icr_content");

        ImGui.TextColored(new float4(1f, 0.85f, 0.1f, 1f), "Subpart Thumbnail Generator");
        ImGui.Spacing();

        // Status display
        string statusText = _generator.State switch
        {
            GenerationState.Idle => "Ready to generate",
            GenerationState.Generating => $"Generating... {_generator.ProgressCurrent}/{_generator.ProgressTotal}",
            GenerationState.Done => $"Done ({SubpartThumbnailCache.All.Count} thumbnails)",
            GenerationState.Failed => $"Failed: {_generator.LastError}",
            _ => "Unknown"
        };

        float4 statusColor = _generator.State switch
        {
            GenerationState.Idle => new float4(0.7f, 0.7f, 0.7f, 1f),
            GenerationState.Generating => new float4(0.3f, 0.8f, 1f, 1f),
            GenerationState.Done => new float4(0.3f, 1f, 0.3f, 1f),
            GenerationState.Failed => new float4(1f, 0.3f, 0.3f, 1f),
            _ => new float4(1f, 1f, 1f, 1f)
        };

        ImGui.TextColored(statusColor, statusText);
        ImGui.Spacing();

        // Generate button
        bool canGenerate = _generator.State == GenerationState.Idle;
        if (!canGenerate) ImGui.BeginDisabled();
        if (ImGui.Button("Generate Subpart Thumbnails"))
            _generator.GenerateAll();
        if (!canGenerate) ImGui.EndDisabled();

        // Reset button if already done or failed
        if (_generator.State == GenerationState.Done || _generator.State == GenerationState.Failed)
        {
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
                _generator.Reset();
        }

        // Progress bar while generating
        if (_generator.State == GenerationState.Generating && _generator.ProgressTotal > 0)
        {
            float progress = (float)_generator.ProgressCurrent / _generator.ProgressTotal;
            ImGui.ProgressBar(progress, new float2(-1, 0),
                $"{_generator.ProgressCurrent}/{_generator.ProgressTotal}");
        }

        ImGui.Separator();
        RenderThumbnailGrid();

        SubmodUI.EndContentArea();
    }

    private void RenderThumbnailGrid()
    {
        if (!SubpartThumbnailCache.HasAny)
        {
            ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1f),
                "No subpart thumbnails generated yet.");
            return;
        }

        ImGui.Text($"Thumbnails: {SubpartThumbnailCache.All.Count}");
        ImGui.Spacing();

        const float thumbSize = 256f;
        const float cellSize = thumbSize + 8f;
        float availWidth = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(availWidth / cellSize));

        if (ImGui.BeginChild("##thumb_scroll", new float2(0, 400),
                ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY))
        {
            int col = 0;
            foreach (var kvp in SubpartThumbnailCache.All)
            {
                kvp.Value.CreateImGuiThumbnail(Program.LinearClampedSampler);

                if (col > 0)
                    ImGui.SameLine();

                ImGui.Image(kvp.Value.ImGuiImageRef, new float2(thumbSize));

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(kvp.Key);

                col++;
                if (col >= cols)
                    col = 0;
            }
        }
        ImGui.EndChild();
    }

    public void Dispose()
    {
        _generator.Dispose();
    }
}
