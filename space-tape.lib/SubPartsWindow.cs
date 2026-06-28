using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Thumbnails;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

public sealed class SubPartsWindow
{
    public bool IsOpen { get; set; }
    public bool ViewSubPartsMode { get; private set; }

    private float _thumbDisplaySize = 96f;
    private int _animTickMs = 100;
    private double _animTimer;
    private readonly ImInputString _filter = new(256);
    private readonly List<PartTemplate> _filtered = new();
    private readonly HashSet<ThumbnailReference> _registeredViews = new();
    private string? _altClickedSubPartId;

    public void Update(double dt)
    {
        _animTimer += dt;
    }

    /// <summary>
    /// Returns and clears the ID of a subpart that was alt-clicked (force-open viewer mode).
    /// Returns null if no alt-click occurred since the last call.
    /// </summary>
    public string? TakeAltClickedSubPartId()
    {
        var val = _altClickedSubPartId;
        _altClickedSubPartId = null;
        return val;
    }

    public void Render(SubPartCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (!IsOpen)
        {
            CleanupRegisteredViews();
            return;
        }

        ImGui.SetNextWindowPos(new float2(10, 50), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new float2(550, 1000), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin("SubParts##st_subparts_window", ref open))
        {
            RenderControls();
            ImGui.Spacing();
            RenderGrid(catalog);
        }
        ImGui.End();

        IsOpen = open;
        if (!IsOpen)
            CleanupRegisteredViews();
    }

    private void RenderControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_sp_ctrl", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##st_sp_c0", ImGuiTableColumnFlags.WidthFixed, 225f);
            ImGui.TableSetupColumn("##st_sp_c1", ImGuiTableColumnFlags.WidthStretch, 1f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Thumb Size");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat("##st_sp_thumbsize", ref _thumbDisplaySize, 1f, 32f, 256f, "%.0f");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Anim Delay");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##st_sp_animdelay", ref _animTickMs, 5, 16, 500, "%d ms");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Filter");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##st_sp_filter", _filter);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        var ivaEnabled = IvaForceRender.Enabled;
        if (ImGui.Checkbox(" Render IVA SubParts ##st_sp_iva", ref ivaEnabled))
            IvaForceRender.Enabled = ivaEnabled;
        if (ImGui.IsItemHovered())
            ImGui.SetItemTooltip("Force interior (IVA) parts to render even when not in IVA camera mode.");

        bool viewMode = ViewSubPartsMode;
        if (ImGui.Checkbox(" Open SubPart Viewer ##st_sp_view", ref viewMode))
            ViewSubPartsMode = viewMode;
        if (ImGui.IsItemHovered())
            ImGui.SetItemTooltip("When checked, clicking a thumbnail opens the full viewer instead of adding the part to the editor.");
    }

    private void RenderGrid(SubPartCatalog catalog)
    {
        var subparts = catalog.SubParts;
        if (subparts == null)
        {
            ImGui.TextDisabled("Click the 'Load SubParts' button above to discover available sub-parts.");
            CleanupRegisteredViews();
            return;
        }

        if (subparts.Count == 0)
        {
            ImGui.TextDisabled("No sub-parts found.");
            CleanupRegisteredViews();
            return;
        }

        string filterText = _filter.ToString();
        _filtered.Clear();
        foreach (var p in subparts)
        {
            if (filterText.Length > 0 && !p.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;
            _filtered.Add(p);
        }

        ImGui.SeparatorText($"SubParts ({_filtered.Count})");

        if (_filtered.Count == 0)
        {
            ImGui.TextDisabled("No matches.");
            CleanupRegisteredViews();
            return;
        }

        ImGui.BeginChild("##st_sp_scroll", new float2(0, 0),
            ImGuiChildFlags.Borders,
            ImGuiWindowFlags.None);

        float thumbSize = _thumbDisplaySize;
        float cellHeight = thumbSize + ImGui.GetStyle().ItemSpacing.Y;
        int ncols = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (thumbSize + 8f)));
        int totalItems = _filtered.Count;
        int totalRows = (totalItems + ncols - 1) / ncols;
        var selectedColor = new float4(0.2f, 0.6f, 1f, 0.8f);

        float scrollY = ImGui.GetScrollY();
        float visibleHeight = ImGui.GetWindowHeight();
        int firstVisRow = Math.Max(0, (int)(scrollY / cellHeight) - 1);
        int lastVisRow = Math.Min(totalRows - 1, (int)((scrollY + visibleHeight) / cellHeight) + 1);
        int firstVisItem = firstVisRow * ncols;
        int lastVisItem = Math.Min(totalItems - 1, (lastVisRow + 1) * ncols - 1);

        var neededViews = new HashSet<ThumbnailReference>();
        for (int i = firstVisItem; i <= lastVisItem; i++)
        {
            var template = _filtered[i];
            var e = SubpartThumbnailCache.Get(template.Id);
            if (e != null && e.Views.Length > 1)
            {
                int animIdx = (int)(_animTimer / (_animTickMs / 1000.0)) % e.Views.Length;
                var view = e.Views[animIdx];
                if (view != null)
                    neededViews.Add(view);
            }
            else if (template.Thumbnail != null)
            {
                neededViews.Add(template.Thumbnail);
            }
        }

        _registeredViews.RemoveWhere(view =>
        {
            if (neededViews.Contains(view)) return false;
            view.DestroyImGuiThumbnail();
            return true;
        });

        if (firstVisRow > 0)
            ImGui.Dummy(new float2(0, firstVisRow * cellHeight));

        if (lastVisItem >= firstVisItem && ImGui.BeginTable("##st_sp_grid", ncols, ImGuiTableFlags.None))
        {
            for (int i = firstVisItem; i <= lastVisItem; i++)
            {
                ImGui.TableNextColumn();
                var template = _filtered[i];

                bool isSelected = catalog.SelectedSubPartId == template.Id;
                bool clicked;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Button, selectedColor);

                ThumbnailReference? animView = null;
                var cacheEntry = SubpartThumbnailCache.Get(template.Id);
                if (cacheEntry != null && cacheEntry.Views.Length > 1)
                {
                    int animIdx = (int)(_animTimer / (_animTickMs / 1000.0)) % cacheEntry.Views.Length;
                    var candidate = cacheEntry.Views[animIdx];
                    if (candidate != null)
                        animView = candidate;
                }

                if (animView != null)
                {
                    animView.GetOrCreateImGuiTexture(Program.LinearClampedSampler);
                    _registeredViews.Add(animView);
                    clicked = ImGui.ImageButton($"##st_sp_{template.Id}", animView.ImGuiImageRef, new float2(thumbSize));
                }
                else if (template.Thumbnail != null)
                {
                    template.Thumbnail.GetOrCreateImGuiTexture(Program.LinearClampedSampler);
                    _registeredViews.Add(template.Thumbnail);
                    clicked = ImGui.ImageButton($"##st_sp_{template.Id}", template.Thumbnail.ImGuiImageRef, new float2(thumbSize));
                }
                else
                {
                    string shortId = template.Id.Contains('.')
                        ? template.Id[(template.Id.LastIndexOf('.') + 1)..]
                        : template.Id;
                    clicked = ImGui.Button($"{shortId}##st_sp_{template.Id}", new float2(thumbSize, thumbSize));
                }

                if (isSelected)
                    ImGui.PopStyleColor();

                if (clicked)
                {
                    if (ImGui.GetIO().KeyAlt)
                        _altClickedSubPartId = template.Id;
                    else
                        catalog.SetSelectedSubPartId(template.Id);
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(template.Id);
            }

            ImGui.EndTable();
        }

        int rowsBelow = totalRows - lastVisRow - 1;
        if (rowsBelow > 0)
            ImGui.Dummy(new float2(0, rowsBelow * cellHeight));

        ImGui.EndChild();
    }

    private void CleanupRegisteredViews()
    {
        foreach (var view in _registeredViews)
            view.DestroyImGuiThumbnail();
        _registeredViews.Clear();
    }
}