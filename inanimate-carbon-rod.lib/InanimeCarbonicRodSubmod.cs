using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Thumbnails;
using MeowSci.KsaAbstractions;

namespace MeowSci.InanimateCarbonRodLib;

public sealed class InanimeCarbonicRodSubmod : ISubmod
{
    public string Name => "Inanimate Carbon Rod";

    private readonly SubpartThumbnailGenerator _generator = new();
    private int _thumbDisplaySize = 256;
    private readonly ImInputString _thumbFilter = new ImInputString(256);

    // Generation settings
    private int _viewCount = 32;
    private int _thumbImageSizeIndex = 2; // 256
    private static readonly int[] ThumbImageSizes = { 64, 128, 256, 512 };
    private static readonly string[] ThumbImageSizeLabels = { "64", "128", "256", "512" };

    // Display settings
    private int _animTickMs = 75;

    // Animation: global timer drives all animated previews in sync
    private double _animTimer;

    // Indices into the view array for the 4 static cardinal views (0°, 90°, 180°, 270°)
    private static readonly int[] CardinalIndices = { 0, 6, 12, 18 };

    // Virtual rendering: track which entries currently have ImGui descriptors registered
    private readonly HashSet<SubpartThumbnailEntry> _registeredEntries = new();
    // Filtered list rebuilt each frame to enable index-based virtual rendering
    private readonly List<KeyValuePair<string, SubpartThumbnailEntry>> _filteredEntries = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        _generator.Update();
        _animTimer += dt;
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
    }

    private void RenderContentInner()
    {

        ImGui.TextColored(new float4(1f, 0.85f, 0.1f, 1f), "Subpart Thumbnail Generator");
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
            ImGui.TextWrapped(
                "Generating subpart thumbnails is GPU-intensive. The number of views and image " +
                "resolution directly affect VRAM usage and generation time.\n\n" +
                "Reduce \"Views Per Subpart\" and \"Image Size\" on lower-end hardware or if you " +
                "experience long generation times or out-of-memory errors.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        ImGui.Spacing();

        // Status display
        string statusText = _generator.State switch
        {
            GenerationState.Idle => "Ready to generate",
            GenerationState.Generating => $"Generating... {_generator.ProgressCurrent}/{_generator.ProgressTotal}",
            GenerationState.Done => $"Done ({SubpartThumbnailCache.All.Count} subparts)",
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
        {
            _generator.ViewCount = _viewCount;
            _generator.ThumbnailImageSize = ThumbImageSizes[_thumbImageSizeIndex];
            _generator.GenerateAll();
        }
        if (!canGenerate) ImGui.EndDisabled();

        // Reset button if already done or failed
        if (_generator.State == GenerationState.Done || _generator.State == GenerationState.Failed)
        {
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                _registeredEntries.Clear();
                _generator.Reset();
            }
        }

        // Progress bar while generating
        if (_generator.State == GenerationState.Generating && _generator.ProgressTotal > 0)
        {
            float progress = (float)_generator.ProgressCurrent / _generator.ProgressTotal;
            ImGui.ProgressBar(progress, new float2(-1, 0),
                $"{_generator.ProgressCurrent}/{_generator.ProgressTotal}");
        }

        ImGui.SeparatorText("Generation Settings");
        bool isGenerating = _generator.State == GenerationState.Generating;
        if (isGenerating) ImGui.BeginDisabled();
        ImGui.DragInt("Views Per Subpart", ref _viewCount, 0.1f, 2, 32);
        ImGui.Combo("Image Size", ref _thumbImageSizeIndex, ThumbImageSizeLabels, ThumbImageSizeLabels.Length);
        if (isGenerating) ImGui.EndDisabled();

        ImGui.SeparatorText("Display Settings");
        ImGui.DragInt("Animation Tick (ms)", ref _animTickMs, 1, 25, 1000);
        ImGui.DragInt("Display Size", ref _thumbDisplaySize, 1, 32, 256);
        ImGui.InputText("##thumb_filter", _thumbFilter);
        RenderThumbnailGrid();
    }

    private void RenderThumbnailGrid()
    {
        if (!SubpartThumbnailCache.HasAny)
        {
            ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1f),
                "No subpart thumbnails generated yet.");
            return;
        }

        ImGui.Text($"Subparts: {SubpartThumbnailCache.All.Count}");
        ImGui.Spacing();

        float thumbSize = (float)_thumbDisplaySize;
        string filterText = _thumbFilter.ToString();

        // Rebuild filtered list
        _filteredEntries.Clear();
        foreach (var kvp in SubpartThumbnailCache.All)
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

        // Destroy ImGui descriptors for entries that scrolled out of view
        var visibleSet = new HashSet<SubpartThumbnailEntry>();
        for (int r = firstVisible; r <= lastVisible; r++)
            visibleSet.Add(_filteredEntries[r].Value);

        _registeredEntries.RemoveWhere(entry =>
        {
            if (visibleSet.Contains(entry)) return false;
            // Off-screen: free all descriptors
            for (int i = 0; i < entry.Views.Length; i++)
                entry.Views[i]?.DestroyImGuiThumbnail();
            return true;
        });

        // Compute which animation frame to show
        int animFrame = (int)(_animTimer / (_animTickMs / 1000.0));

        // Spacer for rows above visible range
        if (firstVisible > 0)
            ImGui.Dummy(new float2(0, firstVisible * rowHeight));

        // Render only visible rows
        for (int r = firstVisible; r <= lastVisible; r++)
        {
            var kvp = _filteredEntries[r];
            var entry = kvp.Value;
            int viewCount = entry.Views.Length;

            // Register descriptors for the 5 views we'll display:
            // the current animation frame + 4 cardinal statics
            bool viewsValid = true;
            int animIdx = animFrame % viewCount;
            if (entry.Views[animIdx] == null) { viewsValid = false; }
            else entry.Views[animIdx].CreateImGuiThumbnail(Program.LinearClampedSampler);

            if (viewsValid)
            {
                for (int c = 0; c < CardinalIndices.Length; c++)
                {
                    int ci = CardinalIndices[c] % viewCount;
                    if (entry.Views[ci] == null) { viewsValid = false; break; }
                    entry.Views[ci].CreateImGuiThumbnail(Program.LinearClampedSampler);
                }
            }
            if (!viewsValid) continue;
            _registeredEntries.Add(entry);

            ImGui.BeginGroup();

            // Animated preview (cycles through all views)
            ImGui.Image(entry.Views[animIdx].ImGuiImageRef, new float2(thumbSize));

            // 4 static cardinal views
            for (int c = 0; c < CardinalIndices.Length; c++)
            {
                ImGui.SameLine();
                int ci = CardinalIndices[c] % viewCount;
                ImGui.Image(entry.Views[ci].ImGuiImageRef, new float2(thumbSize));
            }

            ImGui.Text(kvp.Key);
            ImGui.EndGroup();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(kvp.Key);
        }

        // Spacer for rows below visible range
        int rowsBelow = totalRows - 1 - lastVisible;
        if (rowsBelow > 0)
            ImGui.Dummy(new float2(0, rowsBelow * rowHeight));

        ImGui.EndChild();
    }

    public void Dispose()
    {
        _generator.Dispose();
    }
}
