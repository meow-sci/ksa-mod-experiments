using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using KSA.Rendering.Thumbnails;

namespace MeowSci.SpaceTapeLib;

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
    private SubpartThumbnailEntry? _defaultEntry;
    private int _defaultImageSize;

    // Hi-res generation
    private readonly SingleSubpartGenerator _hiResGen = new();
    private SubpartThumbnailEntry? _pendingDispose;
    private int _hiResViewCount = 60;
    private int _hiResSizeIndex = 2; // default 1024
    private static readonly int[] HiResSizes = { 256, 512, 1024, 1600, 2048 };
    private static readonly string[] HiResSizeLabels = { "256", "512", "1024", "1600", "2048" };

    private bool _autoGenerateHiRes = false;

    public Action<string>? OnAddSubPartRequested { get; set; }

    // Viewer tab state
    private bool _playing = true;
    private int _frameIndex;
    private double _animTimer;
    private int _animTickMs = 120;
    private int _displaySize = 1024;

    // Images tab state
    private int _imagesDisplaySize = 1024;

    public bool IsOpen => _open;

    private SubpartThumbnailEntry ActiveEntry =>
        (_hiResGen.State == GenerationState.Done && _hiResGen.Result != null)
            ? _hiResGen.Result
            : _defaultEntry!;

    private int ActiveImageSize =>
        (_hiResGen.State == GenerationState.Done && _hiResGen.Result != null)
            ? _hiResGen.ThumbnailImageSize
            : _defaultImageSize;

    public void Open(string name, SubpartThumbnailEntry entry, int imageSize)
    {
        if (_open) DisposeHiRes();

        _subpartName = name;
        _defaultEntry = entry;
        _defaultImageSize = imageSize;
        _open = true;
        _playing = true;
        _frameIndex = 0;
        _animTimer = 0;

        if (_autoGenerateHiRes)
        {
            _hiResGen.ViewCount = _hiResViewCount;
            _hiResGen.ThumbnailImageSize = HiResSizes[_hiResSizeIndex];
            _hiResGen.Generate(_subpartName);
        }
    }

    public void Close()
    {
        DisposeHiRes();
        _open = false;
        _defaultEntry = null;
    }

    public void Dispose()
    {
        Close();
        DisposePending();
        _hiResGen.Dispose();
    }

    private void DisposeHiRes()
    {
        _pendingDispose = _hiResGen.DetachResult();
    }

    private void DisposePending()
    {
        if (_pendingDispose == null) return;
        Program.GetRenderer().Device.WaitIdle();
        foreach (var view in _pendingDispose.Views)
        {
            view?.DestroyImGuiThumbnail();
            view?.Dispose();
        }
        _pendingDispose = null;
    }

    public void Update(double dt)
    {
        DisposePending();

        if (!_open || _defaultEntry == null) return;

        _hiResGen.Update();

        var active = ActiveEntry;
        if (_playing && active.Views.Length > 0)
        {
            _animTimer += dt;
            _frameIndex = (int)(_animTimer / (_animTickMs / 1000.0)) % active.Views.Length;
        }
    }

    public void Render()
    {
        if (!_open || _defaultEntry == null) return;

        ImGui.SetNextWindowSize(new float2(1050, 1550), ImGuiCond.FirstUseEver);
        bool open = _open;
        if (ImGui.Begin("SubPart Viewer##icr_viewer", ref open))
        {
            try
            {
                RenderContent();
            }
            catch (Exception ex)
            {
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), $"Render error: {ex.Message}");
                Console.WriteLine($"space-tape: SubpartViewerWindow error - {ex}");
            }
        }
        ImGui.End();

        if (!open) Close();
    }

    private void RenderContent()
    {
        var activeEntry = ActiveEntry;

        // Header: Add SubPart | Copy Name | part name with pixel size
        if (ImGui.Button(" Add SubPart ##icr_add"))
        {
            OnAddSubPartRequested?.Invoke(_subpartName);
            Close();
        }
        ImGui.SameLine();
        if (ImGui.Button(" Copy Name ##icr_v"))
            ImGui.SetClipboardText(_subpartName);
        ImGui.SameLine();
        ImGui.Text($"{_subpartName} ({ActiveImageSize}px)");

        ImGui.Checkbox(" Auto generate Hi-Res on Window Open ##icr_autogen", ref _autoGenerateHiRes);

        ImGui.Spacing();
        RenderHiResSection();
        ImGui.Spacing();

        if (ImGui.BeginTabBar("##icr_viewer_tabs"))
        {
            if (ImGui.BeginTabItem("Viewer"))
            {
                RenderViewerTab(activeEntry);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Images"))
            {
                RenderImagesTab(activeEntry);
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

    private void RenderViewerTab(SubpartThumbnailEntry entry)
    {
        int viewCount = entry.Views.Length;
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
            ImGui.Text("Anim tick (ms)");
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
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##vt_frame", ref _frameIndex, 0, viewCount - 1, "") && _playing)
        {
            // User dragged the slider while playing — stop animation so they can scrub manually
            _playing = false;
        }

        ImGui.Spacing();

        // Display the current frame, centered
        int idx = Math.Clamp(_frameIndex, 0, viewCount - 1);
        var view = entry.Views[idx];
        if (view != null)
        {
            view.CreateImGuiThumbnail(Program.LinearClampedSampler);
            float size = (float)_displaySize;
            float regionW = ImGui.GetContentRegionAvail().X;
            if (size < regionW)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (regionW - size) * 0.5f);
            ImGui.Image(view.ImGuiImageRef, new float2(size));
        }
    }

    private void RenderImagesTab(SubpartThumbnailEntry entry)
    {
        int viewCount = entry.Views.Length;
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

        float availW = ImGui.GetContentRegionAvail().X;

        for (int i = 0; i < viewCount; i++)
        {
            var view = entry.Views[i];
            if (view == null) continue;

            view.CreateImGuiThumbnail(Program.LinearClampedSampler);
            ImGui.Image(view.ImGuiImageRef, new float2(thumbSize));

            float nextX = ImGui.GetItemRectMax().X + spacing + thumbSize;
            if (i + 1 < viewCount && nextX <= availW + ImGui.GetWindowPos().X)
                ImGui.SameLine();
        }

        ImGui.EndChild();
    }
}
