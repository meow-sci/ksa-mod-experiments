using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Separate ImGui window for inspecting a single subpart's thumbnails.
/// Supports an animated Viewer tab and a static Images grid tab.
/// </summary>
public sealed class SubpartViewerWindow
{
    private string _subpartName = string.Empty;
    private SubpartThumbnailEntry? _entry;
    private bool _open;

    // Viewer tab state
    private bool _playing = true;
    private int _frameIndex;
    private double _animTimer;
    private int _animTickMs = 75;
    private int _displaySize = 256;

    // Images tab state
    private int _imagesDisplaySize = 256;

    public bool IsOpen => _open;

    public void Open(string name, SubpartThumbnailEntry entry)
    {
        _subpartName = name;
        _entry = entry;
        _open = true;
        _playing = true;
        _frameIndex = 0;
        _animTimer = 0;
    }

    public void Close()
    {
        _open = false;
        _entry = null;
    }

    public void Update(double dt)
    {
        if (!_open || _entry == null) return;

        if (_playing)
        {
            _animTimer += dt;
            int viewCount = _entry.Views.Length;
            if (viewCount > 0)
                _frameIndex = (int)(_animTimer / (_animTickMs / 1000.0)) % viewCount;
        }
    }

    public void Render()
    {
        if (!_open || _entry == null) return;

        ImGui.SetNextWindowSize(new float2(420, 480), ImGuiCond.FirstUseEver);
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
        var entry = _entry!;

        // Header: part name + Copy Name button
        ImGui.Text(_subpartName);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(" Copy Name ").X
            - ImGui.GetStyle().FramePadding.X * 2f + ImGui.GetCursorPosX());
        if (ImGui.Button(" Copy Name ##icr_v"))
            ImGui.SetClipboardText(_subpartName);

        ImGui.Spacing();

        if (ImGui.BeginTabBar("##icr_viewer_tabs"))
        {
            if (ImGui.BeginTabItem("Viewer"))
            {
                RenderViewerTab(entry);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Images"))
            {
                RenderImagesTab(entry);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
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

        // Controls table: 4 columns for settings row
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
            ImGui.DragInt("##vt_size", ref _displaySize, 1, 32, 512);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // Stop/Play button + frame slider on a single line
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

        // Display the current frame, centered horizontally
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

        // Size control
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Size");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.DragInt("##vt_img_size", ref _imagesDisplaySize, 1, 32, 512);

        ImGui.Spacing();

        float thumbSize = (float)_imagesDisplaySize;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        // Use all remaining window height for the image area
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

            // Wrap to next line when the next image wouldn't fit
            float nextX = ImGui.GetItemRectMax().X + spacing + thumbSize;
            if (i + 1 < viewCount && nextX <= availW + ImGui.GetWindowPos().X)
                ImGui.SameLine();
        }

        ImGui.EndChild();
    }
}
