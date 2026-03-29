using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;
using MeowSci.KsaAbstractions;

namespace MeowSci.InanimateCarbonRodLib;

public sealed class InanimeCarbonicRodSubmod : ISubmod
{
    public string Name => "Inanimate Carbon Rod";

    private readonly SubpartThumbnailGenerator _generator = new();
    private int _thumbDisplaySize = 128;
    private readonly ImInputString _thumbFilter = new ImInputString(256);

    // Generation settings
    private int _viewCount = 32;
    private int _thumbImageSizeIndex = 1; // 128
    private static readonly int[] ThumbImageSizes = { 64, 128, 256, 512, 1024 };
    private static readonly string[] ThumbImageSizeLabels = { "64", "128", "256", "512", "1024" };

    // Display settings
    private int _animTickMs = 75;

    // Animation: global timer drives all animated previews in sync
    private double _animTimer;

    // Indices into the view array for the 4 static cardinal views (0°, 90°, 180°, 270°)
    private static readonly int[] CardinalIndices = { 0, 6, 12, 18 };

    // GPU thumbnail pool for on-demand upload from CPU cache
    private GpuThumbnailPool? _gpuPool;
    private static readonly int GpuPoolMaxSlots = 256;

    // Filtered list rebuilt each frame to enable index-based virtual rendering
    private readonly List<KeyValuePair<string, CpuThumbnailData>> _filteredEntries = new();

    // Subpart detail viewer window
    private readonly SubpartViewerWindow _viewerWindow = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        _generator.Update();
        _animTimer += dt;
        _viewerWindow.Update(dt);
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##icr_content");

        try
        {
            RenderContentInner();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Render error: {ex.Message}");
            Console.WriteLine($"inanimate-carbon-rod: RenderContent error - {ex}");
        }

        SubmodUI.EndContentArea();

        _viewerWindow.Render();
    }

    private void RenderContentInner()
    {
        RenderGeneratorSection();

        int subpartCount = CpuThumbnailCache.HasAny ? CpuThumbnailCache.All.Count : 0;
        ImGui.SeparatorText($"Subparts ({subpartCount})");

        RenderDisplaySettings();
        RenderThumbnailGrid();
    }

    private void RenderGeneratorSection()
    {
        if (!ImGui.CollapsingHeader("Generator##icr", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Spacing();

        bool isGenerating = _generator.State == GenerationState.Generating;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##icr_gen", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##icr_lbl", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##icr_input", ImGuiTableColumnFlags.WidthStretch);

            // ---- Images per Subpart row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Images per Subpart");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
                ImGui.TextWrapped(
                    "Generating subpart thumbnails is GPU-intensive. The number of views and image " +
                    "resolution directly affect VRAM usage and generation time.\n\n" +
                    "Reduce this value and \"Image Size\" on lower-end hardware or if you " +
                    "experience long generation times or out-of-memory errors.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            if (isGenerating) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##icr_views", ref _viewCount, 0.1f, 2, 32);
            if (isGenerating) ImGui.EndDisabled();

            // ---- Image Size row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Image Size");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
                ImGui.TextWrapped("Higher resolution produces sharper thumbnails but uses more VRAM.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            if (isGenerating) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(-1);
            ImGui.Combo("##icr_imgsize", ref _thumbImageSizeIndex, ThumbImageSizeLabels, ThumbImageSizeLabels.Length);
            if (isGenerating) ImGui.EndDisabled();

            // ---- Generate/Reset + Progress row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            bool isDone = _generator.State == GenerationState.Done || _generator.State == GenerationState.Failed;
            if (isDone)
            {
                if (ImGui.Button(" Reset ##icr"))
                {
                    _viewerWindow.Close();
                    _gpuPool?.EvictAll();
                    _generator.Reset();
                }
            }
            else
            {
                if (isGenerating) ImGui.BeginDisabled();
                if (ImGui.Button(" Generate ##icr"))
                {
                    _generator.ViewCount = _viewCount;
                    _generator.ThumbnailImageSize = ThumbImageSizes[_thumbImageSizeIndex];
                    _generator.GenerateAll();
                }
                if (isGenerating) ImGui.EndDisabled();
            }

            ImGui.TableNextColumn();
            if (isGenerating && _generator.ProgressTotal > 0)
            {
                float progress = (float)_generator.ProgressCurrent / _generator.ProgressTotal;
                ImGui.ProgressBar(progress, new float2(-1, 0),
                    $"{_generator.ProgressCurrent}/{_generator.ProgressTotal}");
            }
            else
            {
                // Status text
                string statusText = _generator.State switch
                {
                    GenerationState.Idle => "Ready to generate",
                    GenerationState.Done => $"Done ({CpuThumbnailCache.All.Count} subparts)",
                    GenerationState.Failed => $"Failed: {_generator.LastError}",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(statusText))
                {
                    ImGui.AlignTextToFramePadding();
                    float4 statusColor = _generator.State switch
                    {
                        GenerationState.Idle => new float4(0.7f, 0.7f, 0.7f, 1f),
                        GenerationState.Done => new float4(0.3f, 1f, 0.3f, 1f),
                        GenerationState.Failed => new float4(1f, 0.3f, 0.3f, 1f),
                        _ => new float4(1f, 1f, 1f, 1f)
                    };
                    ImGui.TextColored(statusColor, statusText);
                }
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
        ImGui.Spacing();
    }

    private void RenderDisplaySettings()
    {
        float availW = ImGui.GetContentRegionAvail().X;
        float colW = availW / 4f;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##icr_display", 4, tableFlags))
        {
            ImGui.TableSetupColumn("##icr_d0", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##icr_d1", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##icr_d2", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##icr_d3", ImGuiTableColumnFlags.WidthFixed, colW);

            // Row 1: Anim tick (right-aligned) | input | Display Size (right-aligned) | input
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            float animLabelW = ImGui.CalcTextSize("Anim tick").X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - animLabelW);
            ImGui.Text("Anim tick");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##icr_anim", ref _animTickMs, 1, 25, 1000);
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            float sizeLabelW = ImGui.CalcTextSize("Display Size").X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - sizeLabelW);
            ImGui.Text("Display Size");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##icr_size", ref _thumbDisplaySize, 1, 32, 256);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Filter row: label naturally sized, input takes remainder of width
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Filter");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##icr_filter", _thumbFilter);
        ImGui.Spacing();
    }

    private void RenderThumbnailGrid()
    {
        if (!CpuThumbnailCache.HasAny)
        {
            ImGui.TextDisabled("No subpart thumbnails generated yet.");
            return;
        }

        // Ensure GPU pool exists at the right resolution
        EnsureGpuPool();
        if (_gpuPool == null) return;

        float thumbSize = (float)_thumbDisplaySize;
        string filterText = _thumbFilter.ToString();

        // Rebuild filtered list from CPU cache
        _filteredEntries.Clear();
        foreach (var kvp in CpuThumbnailCache.All)
        {
            if (filterText.Length > 0 && !kvp.Key.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;
            _filteredEntries.Add(kvp);
        }

        int totalRows = _filteredEntries.Count;
        if (totalRows == 0)
        {
            ImGui.TextDisabled("No matches.");
            return;
        }

        // Row height: thumbnail + text line + spacing
        float rowHeight = thumbSize + ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

        ImGui.BeginChild("##thumb_scroll", new float2(0, 400),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY,
            ImGuiWindowFlags.HorizontalScrollbar);

        // Determine visible row range from scroll position
        float scrollY = ImGui.GetScrollY();
        float visibleHeight = ImGui.GetWindowHeight();
        int firstVisible = Math.Max(0, (int)(scrollY / rowHeight) - 1);
        int lastVisible = Math.Min(totalRows - 1, (int)((scrollY + visibleHeight) / rowHeight) + 1);

        // Compute which animation frame to show
        int animFrame = (int)(_animTimer / (_animTickMs / 1000.0));

        // Spacer for rows above visible range
        if (firstVisible > 0)
            ImGui.Dummy(new float2(0, firstVisible * rowHeight));

        // Render only visible rows
        for (int r = firstVisible; r <= lastVisible; r++)
        {
            var kvp = _filteredEntries[r];
            var cpuData = kvp.Value;
            int viewCount = cpuData.Views.Length;
            if (viewCount == 0) continue;

            int animIdx = animFrame % viewCount;

            ImGui.BeginGroup();

            // Animated preview (cycles through all views)
            string animKey = $"{kvp.Key}:{animIdx}";
            var animRef = _gpuPool.TryGet(animKey) ?? _gpuPool.Upload(animKey, cpuData.Views[animIdx]);
            ImGui.Image(animRef.ImGuiImageRef, new float2(thumbSize));

            // 4 static cardinal views
            for (int c = 0; c < CardinalIndices.Length; c++)
            {
                ImGui.SameLine();
                int ci = CardinalIndices[c] % viewCount;
                string cardKey = $"{kvp.Key}:{ci}";
                var cardRef = _gpuPool.TryGet(cardKey) ?? _gpuPool.Upload(cardKey, cpuData.Views[ci]);
                ImGui.Image(cardRef.ImGuiImageRef, new float2(thumbSize));
            }

            ImGui.Text(kvp.Key);
            ImGui.EndGroup();

            if (ImGui.IsItemClicked())
                _viewerWindow.Open(kvp.Key, cpuData, _generator.ThumbnailImageSize);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(kvp.Key);
        }

        // Spacer for rows below visible range
        int rowsBelow = totalRows - 1 - lastVisible;
        if (rowsBelow > 0)
            ImGui.Dummy(new float2(0, rowsBelow * rowHeight));

        ImGui.EndChild();
    }

    /// <summary>
    /// Ensures the GPU pool exists and matches the current generation resolution.
    /// Recreates the pool if resolution changed since last generation.
    /// </summary>
    private void EnsureGpuPool()
    {
        // Determine resolution from the first CPU cache entry
        int imageSize = 0;
        foreach (var kvp in CpuThumbnailCache.All)
        {
            imageSize = kvp.Value.Size;
            break;
        }
        if (imageSize == 0) return;

        if (_gpuPool != null && _gpuPool.ImageSize == imageSize)
            return;

        // Dispose old pool if resolution changed
        _gpuPool?.Dispose();

        Renderer renderer = Program.GetRenderer();
        _gpuPool = new GpuThumbnailPool(
            renderer.Device, renderer, imageSize, GpuPoolMaxSlots,
            Program.LinearClampedSampler);
    }

    public void Dispose()
    {
        _viewerWindow.Dispose();
        _gpuPool?.Dispose();
        _generator.Dispose();
    }
}
