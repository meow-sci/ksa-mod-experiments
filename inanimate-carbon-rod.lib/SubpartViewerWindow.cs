using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Separate ImGui window for inspecting a single subpart's thumbnails.
/// Supports an animated Viewer tab and a static Images grid tab.
/// Can generate hi-res images for the selected subpart on demand.
/// </summary>
public sealed class SubpartViewerWindow
{
    private string _subpartName = string.Empty;
    private bool _open;

    // Default data from the main cache (not owned, never disposed by viewer)
    private CpuThumbnailData? _defaultData;
    private int _defaultImageSize;

    // Hi-res generation
    private readonly SingleSubpartGenerator _hiResGen = new();
    private CpuThumbnailData? _hiResData;
    private int _hiResViewCount = 32;
    private int _hiResSizeIndex = 1; // default 512
    private static readonly int[] HiResSizes = { 256, 512, 1024, 1600, 2048 };
    private static readonly string[] HiResSizeLabels = { "256", "512", "1024", "1600", "2048" };

    // GPU pool for display (viewer has its own pool, separate from grid)
    private GpuThumbnailPool? _viewerPool;
    private static readonly int ViewerPoolMaxSlots = 64;

    // Viewer tab state
    private bool _playing = true;
    private int _frameIndex;
    private double _animTimer;
    private int _animTickMs = 75;
    private int _displaySize = 256;

    // Images tab state
    private int _imagesDisplaySize = 256;

    public bool IsOpen => _open;

    private CpuThumbnailData ActiveData =>
        _hiResData ?? _defaultData!;

    private int ActiveImageSize =>
        _hiResData != null ? _hiResGen.ThumbnailImageSize : _defaultImageSize;

    public void Open(string name, CpuThumbnailData data, int imageSize)
    {
        if (_open) DisposeHiRes();

        _subpartName = name;
        _defaultData = data;
        _defaultImageSize = imageSize;
        _open = true;
        _playing = true;
        _frameIndex = 0;
        _animTimer = 0;

        // Viewer pool will be created lazily in EnsureViewerPool
    }

    public void Close()
    {
        DisposeHiRes();
        _viewerPool?.Dispose();
        _viewerPool = null;
        _open = false;
        _defaultData = null;
    }

    public void Dispose()
    {
        Close();
        _hiResGen.Dispose();
    }

    private void DisposeHiRes()
    {
        _hiResGen.DestroyResult();
        _hiResData = null;
        // Evict viewer pool since hi-res data changed
        _viewerPool?.EvictAll();
    }

    public void Update(double dt)
    {
        if (!_open || _defaultData == null) return;

        _hiResGen.Update();

        // Capture hi-res result when generation completes
        if (_hiResGen.State == GenerationState.Done && _hiResGen.Result != null && _hiResData == null)
        {
            _hiResData = _hiResGen.DetachResult();
            // Recreate viewer pool at hi-res resolution
            _viewerPool?.Dispose();
            _viewerPool = null;
        }

        var active = ActiveData;
        if (_playing && active.Views.Length > 0)
        {
            _animTimer += dt;
            _frameIndex = (int)(_animTimer / (_animTickMs / 1000.0)) % active.Views.Length;
        }
    }

    public void Render()
    {
        if (!_open || _defaultData == null) return;

        ImGui.SetNextWindowSize(new float2(460, 560), ImGuiCond.FirstUseEver);
        bool open = _open;
        if (ImGui.Begin("Subpart Viewer##icr_viewer", ref open))
        {
            try
            {
                RenderContent();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Render error: {ex.Message}");
                Console.WriteLine($"inanimate-carbon-rod: SubpartViewerWindow error - {ex}");
            }
        }
        ImGui.End();

        if (!open) Close();
    }

    private void RenderContent()
    {
        var activeData = ActiveData;

        // Ensure GPU pool exists at the right resolution
        EnsureViewerPool(activeData.Size);
        if (_viewerPool == null) return;

        // Header: Copy Name button + part name with pixel size
        if (ImGui.Button(" Copy Name ##icr_v"))
            ImGui.SetClipboardText(_subpartName);
        ImGui.SameLine();
        ImGui.Text($"{_subpartName} ({ActiveImageSize}px)");

        ImGui.Spacing();
        RenderHiResSection();
        ImGui.Spacing();

        if (ImGui.BeginTabBar("##icr_viewer_tabs"))
        {
            if (ImGui.BeginTabItem("Viewer"))
            {
                RenderViewerTab(activeData);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Images"))
            {
                RenderImagesTab(activeData);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void RenderHiResSection()
    {
        bool isGenerating = _hiResGen.State == GenerationState.Generating;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##icr_hires", 2, tableFlags))
        {
            ImGui.TableSetupColumn("##hr_lbl", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##hr_input", ImGuiTableColumnFlags.WidthStretch);

            // ---- Images Count row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Images Count");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
                ImGui.TextWrapped(
                    "Number of rotation views to generate for this subpart. " +
                    "More views = smoother animation but more VRAM.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            if (isGenerating) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##hr_views", ref _hiResViewCount, 0.1f, 2, 32);
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
            ImGui.Combo("##hr_imgsize", ref _hiResSizeIndex, HiResSizeLabels, HiResSizeLabels.Length);
            if (isGenerating) ImGui.EndDisabled();

            // ---- Generate/Reset + Status row ----
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            bool hasResult = _hiResGen.State == GenerationState.Done || _hiResGen.State == GenerationState.Failed;
            if (hasResult)
            {
                if (ImGui.Button(" Reset ##hr"))
                    DisposeHiRes();
            }
            else
            {
                if (isGenerating) ImGui.BeginDisabled();
                if (ImGui.Button(" Generate Hi-Res ##hr"))
                {
                    _hiResGen.ViewCount = _hiResViewCount;
                    _hiResGen.ThumbnailImageSize = HiResSizes[_hiResSizeIndex];
                    _hiResGen.Generate(_subpartName);
                }
                if (isGenerating) ImGui.EndDisabled();
            }

            ImGui.TableNextColumn();
            if (isGenerating)
            {
                ImGui.ProgressBar(0f, new float2(-1, 0), "Generating...");
            }
            else if (_hiResGen.State == GenerationState.Done && _hiResGen.Result != null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(new float4(0.3f, 1f, 0.3f, 1f),
                    $"Done ({_hiResGen.Result.Views.Length} views, {_hiResGen.ThumbnailImageSize}px)");
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Error line below the table
        if (_hiResGen.State == GenerationState.Failed && !string.IsNullOrEmpty(_hiResGen.LastError))
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Error: {_hiResGen.LastError}");
    }

    private void RenderViewerTab(CpuThumbnailData data)
    {
        int viewCount = data.Views.Length;
        if (viewCount == 0)
        {
            ImGui.TextDisabled("No views available.");
            return;
        }

        ImGui.Spacing();

        // Settings table: 4 columns
        float availW = ImGui.GetContentRegionAvail().X;
        float colW = availW / 4f;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##icr_vt", 4, tableFlags))
        {
            ImGui.TableSetupColumn("##vt0", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##vt1", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##vt2", ImGuiTableColumnFlags.WidthFixed, colW);
            ImGui.TableSetupColumn("##vt3", ImGuiTableColumnFlags.WidthFixed, colW);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            float atW = ImGui.CalcTextSize("Anim tick").X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - atW);
            ImGui.Text("Anim tick");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##vt_anim", ref _animTickMs, 1, 25, 1000);
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            float dsW = ImGui.CalcTextSize("Display Size").X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - dsW);
            ImGui.Text("Display Size");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##vt_size", ref _displaySize, 1, 64, 2048);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Stop/Play button + frame slider
        if (ImGui.Button(_playing ? " Stop ##vt" : " Play ##vt"))
        {
            _playing = !_playing;
            if (_playing) _animTimer = 0;
        }
        ImGui.SameLine();
        if (_playing) ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderInt("##vt_frame", ref _frameIndex, 0, viewCount - 1, "");
        if (_playing) ImGui.EndDisabled();

        ImGui.Spacing();

        // Display the current frame, centered
        int idx = Math.Clamp(_frameIndex, 0, viewCount - 1);
        if (_viewerPool != null && idx < data.Views.Length)
        {
            string key = $"viewer:{idx}";
            var gpuRef = _viewerPool.TryGet(key) ?? _viewerPool.Upload(key, data.Views[idx]);
            float size = (float)_displaySize;
            float regionW = ImGui.GetContentRegionAvail().X;
            if (size < regionW)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (regionW - size) * 0.5f);
            ImGui.Image(gpuRef.ImGuiImageRef, new float2(size));
        }
    }

    private void RenderImagesTab(CpuThumbnailData data)
    {
        int viewCount = data.Views.Length;
        if (viewCount == 0)
        {
            ImGui.TextDisabled("No views available.");
            return;
        }

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Size");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.DragInt("##vt_img_size", ref _imagesDisplaySize, 1, 64, 2048);

        ImGui.Spacing();

        float thumbSize = (float)_imagesDisplaySize;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        float remainingH = ImGui.GetContentRegionAvail().Y;
        ImGui.BeginChild("##icr_img_scroll", new float2(0, remainingH),
            ImGuiChildFlags.Borders);

        if (_viewerPool != null)
        {
            float availW = ImGui.GetContentRegionAvail().X;

            for (int i = 0; i < viewCount; i++)
            {
                if (i >= data.Views.Length) continue;

                string key = $"img:{i}";
                var gpuRef = _viewerPool.TryGet(key) ?? _viewerPool.Upload(key, data.Views[i]);
                ImGui.Image(gpuRef.ImGuiImageRef, new float2(thumbSize));

                float nextX = ImGui.GetItemRectMax().X + spacing + thumbSize;
                if (i + 1 < viewCount && nextX <= availW + ImGui.GetWindowPos().X)
                    ImGui.SameLine();
            }
        }

        ImGui.EndChild();
    }

    private void EnsureViewerPool(int imageSize)
    {
        if (_viewerPool != null && _viewerPool.ImageSize == imageSize)
            return;

        _viewerPool?.Dispose();

        Renderer renderer = Program.GetRenderer();
        _viewerPool = new GpuThumbnailPool(
            renderer.Device, renderer, imageSize, ViewerPoolMaxSlots,
            Program.LinearClampedSampler);
    }
}
