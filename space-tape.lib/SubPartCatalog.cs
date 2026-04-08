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
    private float _thumbDisplaySize = 64f;

    // Thumbnail animation
    private double _animTimer;
    private int _animTickMs = 75;

    // Descriptor lifetime tracking to avoid DescriptorPoolOutOfMemoryException
    private readonly HashSet<SubpartThumbnailEntry> _registeredEntries = new();

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

    public void Render()
    {
        if (ImGui.Button("Load SubParts##st_cat"))
            LoadSubParts();

        if (_subparts != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({_subparts.Count} sub-parts)");
        }

        if (_subparts == null)
        {
            ImGui.TextDisabled("Click 'Load SubParts' to discover available sub-parts.");
            return;
        }

        if (_subparts.Count == 0)
        {
            ImGui.TextDisabled("No sub-parts found.");
            return;
        }

        // Filter input
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Filter");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##st_cat_filter", _filter);

        // Thumbnail size slider
        ImGui.SliderFloat("Thumb Size##st_cat", ref _thumbDisplaySize, 32f, 128f);

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

        // Free descriptors for entries that scrolled out of view
        var visibleEntries = new HashSet<SubpartThumbnailEntry>();
        for (int i = firstVisItem; i <= lastVisItem; i++)
        {
            var e = SubpartThumbnailCache.Get(_filtered[i].Id);
            if (e != null) visibleEntries.Add(e);
        }

        _registeredEntries.RemoveWhere(entry =>
        {
            if (visibleEntries.Contains(entry)) return false;
            foreach (var view in entry.Views)
                view?.DestroyImGuiThumbnail();
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
                    _registeredEntries.Add(cacheEntry!);
                    clicked = ImGui.ImageButton($"##st_cat_{template.Id}", animView.ImGuiImageRef, new float2(thumbSize));
                }
                else if (template.Thumbnail != null)
                {
                    template.Thumbnail.CreateImGuiThumbnail(Program.LinearClampedSampler);
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

        ImGui.Text($"Selected: {SelectedSubPartId ?? "(none)"}");
    }
}
