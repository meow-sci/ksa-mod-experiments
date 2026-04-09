using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Thumbnails;
using MeowSci.InanimateCarbonRodLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.SpaceTapeLib;

public sealed class SubPartCatalog
{
    private List<PartTemplate>? _subparts;
    private readonly ImInputString _filter = new ImInputString(256);
    private readonly List<PartTemplate> _filtered = new();
    private float _thumbDisplaySize = 128f;

    // Thumbnail animation
    private double _animTimer;
    private int _animTickMs = 100;

    // Descriptor lifetime tracking — track individual ThumbnailReference views
    // so we only keep one descriptor per visible entry (the current anim frame).
    private readonly HashSet<ThumbnailReference> _registeredViews = new();

    public string? SelectedSubPartId { get; private set; }

    /// <summary>Returns the currently selected SubPart ID and clears the selection, or null if nothing is selected.</summary>
    public string? TakeSelectedSubPartId()
    {
        var id = SelectedSubPartId;
        SelectedSubPartId = null;
        return id;
    }

    public void LoadSubParts()
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        object? collection = field?.GetValue(null);
        MethodInfo? getList = collection?.GetType().GetMethod("GetList");
        var allParts = (List<PartTemplate>?)getList?.Invoke(collection, null);

        if (allParts == null)
        {
            Console.WriteLine("space-tape: SubPartCatalog.LoadSubParts - failed to get AllParts");
            _subparts = new List<PartTemplate>();
            return;
        }

        _subparts = allParts
            .Where(p => p.IsSubPart && !p.IsHidden)
            .OrderBy(p => p.Id)
            .ToList();

        Console.WriteLine($"space-tape: SubPartCatalog loaded {_subparts.Count} sub-parts");
    }

    public void Update(double dt)
    {
        _animTimer += dt;
    }

    public void Render(PartEditorScene? scene, ref bool editorWindowOpen)
    {
        // --- Control Panel (2-col, 5-row table) - ALWAYS VISIBLE ---
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var tableFlags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##st_cat_ctrl", 2, tableFlags))
        {
            // Row 1: [Open/Close Editor toggle] | [Editor Window]
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (scene == null) ImGui.BeginDisabled();
            if (scene != null && scene.IsActive)
            {
                if (ImGui.Button(" Close Editor ##st_cat_editor_close", new float2(-1, 0)))
                    scene.Exit();
            }
            else
            {
                if (ImGui.Button(" Open Editor ##st_cat_editor_open", new float2(-1, 0)))
                {
                    scene?.Enter();
                    editorWindowOpen = true;
                }
            }
            if (scene == null) ImGui.EndDisabled();

            ImGui.TableNextColumn();
            if (ImGui.Button(" Editor Window ##st_cat_editor_win", new float2(-1, 0)))
                editorWindowOpen = !editorWindowOpen;

            // Row 2: [Load SubParts] | (subpart count)
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (ImGui.Button(" Load SubParts##st_cat", new float2(-1, 0)))
                LoadSubParts();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(_subparts != null ? $"({_subparts.Count} sub-parts)" : "(0 sub-parts)");

            // Row 3: Thumb Size | [drag slider]
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Thumb Size");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat("##st_cat_thumbsize", ref _thumbDisplaySize, 1f, 32f, 256f, "%.0f");

            // Row 4: Animation Delay | [drag slider]
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Anim Delay");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.DragInt("##st_cat_animdelay", ref _animTickMs, 5, 16, 500, "%d ms");

            // Row 5: Filter | [text input]
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Filter");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##st_cat_filter", _filter);

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();

        // --- Grid of SubPart Thumbnails ---
        if (_subparts == null)
        {
            ImGui.TextDisabled("Click the 'Load SubParts' button above to discover available sub-parts.");
            return;
        }

        if (_subparts.Count == 0)
        {
            ImGui.TextDisabled("No sub-parts found.");
            return;
        }

        // Rebuild filtered list
        string filterText = _filter.ToString();
        _filtered.Clear();
        foreach (var p in _subparts)
        {
            if (filterText.Length > 0 && !p.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;
            _filtered.Add(p);
        }

        if (_filtered.Count == 0)
        {
            ImGui.TextDisabled("No matches.");
            return;
        }

        // Scrollable grid with virtual rendering — only register descriptors for visible items
        ImGui.BeginChild("##st_cat_scroll", new float2(0, 300),
            ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeY,
            ImGuiWindowFlags.None);

        float thumbSize = _thumbDisplaySize;
        float cellHeight = thumbSize + ImGui.GetStyle().ItemSpacing.Y;
        int ncols = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (thumbSize + 8f)));
        int totalItems = _filtered.Count;
        int totalRows = (totalItems + ncols - 1) / ncols;
        var selectedColor = new float4(0.2f, 0.6f, 1f, 0.8f);

        // Determine visible row range from scroll position
        float scrollY = ImGui.GetScrollY();
        float visibleHeight = ImGui.GetWindowHeight();
        int firstVisRow = Math.Max(0, (int)(scrollY / cellHeight) - 1);
        int lastVisRow = Math.Min(totalRows - 1, (int)((scrollY + visibleHeight) / cellHeight) + 1);
        int firstVisItem = firstVisRow * ncols;
        int lastVisItem = Math.Min(totalItems - 1, (lastVisRow + 1) * ncols - 1);

        // Collect the specific ThumbnailReference views we need this frame
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

        // Free descriptors for views no longer needed (scrolled away or different anim frame)
        _registeredViews.RemoveWhere(view =>
        {
            if (neededViews.Contains(view)) return false;
            view.DestroyImGuiThumbnail();
            return true;
        });

        // Spacer for rows above visible range
        if (firstVisRow > 0)
            ImGui.Dummy(new float2(0, firstVisRow * cellHeight));

        // Render only visible rows
        if (lastVisItem >= firstVisItem && ImGui.BeginTable("##st_cat_grid", ncols, ImGuiTableFlags.None))
        {
            for (int i = firstVisItem; i <= lastVisItem; i++)
            {
                ImGui.TableNextColumn();
                var template = _filtered[i];

                bool isSelected = SelectedSubPartId == template.Id;
                bool clicked;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Button, selectedColor);

                // Try animated thumbnail from ICR cache first, fall back to static/text
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
                    animView.CreateImGuiThumbnail(Program.LinearClampedSampler);
                    _registeredViews.Add(animView);
                    clicked = ImGui.ImageButton($"##st_cat_{template.Id}", animView.ImGuiImageRef, new float2(thumbSize));
                }
                else if (template.Thumbnail != null)
                {
                    template.Thumbnail.CreateImGuiThumbnail(Program.LinearClampedSampler);
                    _registeredViews.Add(template.Thumbnail);
                    clicked = ImGui.ImageButton($"##st_cat_{template.Id}", template.Thumbnail.ImGuiImageRef, new float2(thumbSize));
                }
                else
                {
                    string shortId = template.Id.Contains('.')
                        ? template.Id[(template.Id.LastIndexOf('.') + 1)..]
                        : template.Id;
                    clicked = ImGui.Button($"{shortId}##st_cat_{template.Id}", new float2(thumbSize, thumbSize));
                }

                if (isSelected)
                    ImGui.PopStyleColor();

                if (clicked)
                    SelectedSubPartId = template.Id;

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(template.Id);
            }

            ImGui.EndTable();
        }

        // Spacer for rows below visible range
        int rowsBelow = totalRows - lastVisRow - 1;
        if (rowsBelow > 0)
            ImGui.Dummy(new float2(0, rowsBelow * cellHeight));

        ImGui.EndChild();
    }
}
