using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GraffitiLib;

/// <summary>Main panel: decal library + import, placement settings, arm button, placed-decals list.</summary>
public sealed partial class GraffitiSubmod
{
    private string[] _libraryNames = Array.Empty<string>();
    private int _selectedLibraryIndex = -1;
    private readonly ImInputString _decalFilter = new(128);
    private string _lastError = "";

    // Placement settings applied to the next placed decal.
    private float _width = 1f;
    private float _height = 1f;
    private float _depth; // 0 = auto (scales with the decal's larger side)
    private float _rollDeg;
    private float _range = 2000f;
    private float _alpha = 1f;
    private float _brightness = 1f;
    // Global render setting mirrored into DecalRenderer.MaxViewDistanceMetres on change.
    private float _maxDrawDistance = 50_000f;
    private bool _draftDebugBox;

    // Placed-list multi-select state.
    private readonly HashSet<int> _selectedIds = new();
    private int _lastClickedRow = -1;

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##graffiti_content");

        RenderPlaceSection();

        SubmodUI.EndContentArea();
    }

    // ---- place section ----

    private void RenderPlaceSection()
    {
        bool open = MeowSci.KsaAbstractions.WorkspaceUi.Header("Place Decal (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Pick a PNG from the decal library, press Place at Click...,\nthen click anywhere in the 3D world. The decal is projected onto\nthe vehicle, parachute, or terrain under the cursor.");
        if (!open) return;

        if (ImGui.Button(" Import PNG... ##graffiti_import"))
            _fileBrowser.Open();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Rescan ##graffiti_rescan"))
            RefreshLibrary();
        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"{_libraryNames.Length} decal(s) in library");

        if (_libraryNames.Length == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No decals yet — import a PNG, or drop .png files into:");
            ImGui.TextDisabled(DecalLibrary.DecalsDir);
        }

        ImGui.Spacing();
        if (MeowSci.KsaAbstractions.WorkspaceUi.Tree("Placement settings##graffiti_settings", ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            if (GraffitiUi.BeginParamGrid("##graffiti_settings_grid"))
            {
                ImGui.TableNextRow();
                GraffitiUi.GridDrag("Width (m)", "##graffiti_w", ref _width, 0.01f, 0.01f, 1000f, "%.2f");
                GraffitiUi.GridDrag("Height (m)", "##graffiti_h", ref _height, 0.01f, 0.01f, 1000f, "%.2f");
                ImGui.TableNextRow();
                GraffitiUi.GridDrag("Depth (m)", "##graffiti_d", ref _depth, 0.01f, 0f, 100f,
                    _depth > 0f ? "%.2f" : "auto");
                GraffitiUi.GridDrag("Roll (deg)", "##graffiti_roll", ref _rollDeg, 0.25f, -180f, 180f, "%.1f");
                ImGui.TableNextRow();
                GraffitiUi.GridDrag("Alpha", "##graffiti_alpha", ref _alpha, 0.01f, 0f, 1f, "%.2f");
                GraffitiUi.GridDrag("Brightness", "##graffiti_bright", ref _brightness, 0.01f, 0.01f, 8f, "%.2f");
                ImGui.TableNextRow();
                GraffitiUi.GridDrag("Range (m)", "##graffiti_range", ref _range, 10f, 10f, 100000f, "%.0f");
                GraffitiUi.GridDrag("Max draw dist (m)", "##graffiti_maxdraw", ref _maxDrawDistance,
                        500f, 1000f, 10_000_000f, "%.0f");
                GraffitiUi.EndParamGrid();
            }
            ImGui.Checkbox("Debug box (magenta checker instead of the image)##graffiti_debug", ref _draftDebugBox);
            if (ImGui.Button("Apply rendering settings", new float2(-1, 0))) { DebugBox = _draftDebugBox; DecalRenderer.MaxViewDistanceMetres = _maxDrawDistance; }
            ImGui.TextDisabled("Depth is how far the image projects through the surface; 0 = auto (half the larger\nside). Raise it if a big decal on a curved hull looks cropped/zoomed; lower it if the\nimage bleeds through to the far side of thin parts. Max draw dist applies to ALL decals\n(default 50 km); terrain decals auto-deepen with camera distance to survive terrain LOD.");
            ImGui.TreePop();
        }

        ImGui.Spacing();
        ImGui.Checkbox("Spray while holding mouse", ref _sprayMode);
        if (_sprayMode)
        {
            ImGui.InputInt(FormField.Label("Spray interval (ms)"), ref _sprayIntervalMs);
            _sprayIntervalMs = Math.Clamp(_sprayIntervalMs, 10, 60_000);
            ImGui.TextWrapped("Hold the left mouse button in the world to spray. Release to pause; Esc ends placement. At most one decal per frame.");
        }
        RenderArmControls();

        if (!string.IsNullOrEmpty(_placeStatus))
        {
            ImGui.Spacing();
            if (_placeStatusIsError)
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _placeStatus);
            else
                ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _placeStatus);
        }
        if (!string.IsNullOrEmpty(_lastError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _lastError);
        }
    }

    private void RenderArmControls()
    {
        if (_armed)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new float4(1f, 0.85f, 0.2f, 1f),
                $"{(_armedSpray ? "Hold to spray in the world" : "Waiting for a click in the world")} with '{_armedDecalName}'...  (Esc cancels)");
            if (ImGui.Button(" Cancel placement ##graffiti_cancel"))
                Disarm("Placement cancelled.");
            return;
        }

        bool hasSelection = _selectedLibraryIndex >= 0 && _selectedLibraryIndex < _libraryNames.Length;
        bool inEditor = Program.EditorFlag;
        bool canPlace = hasSelection && !inEditor && !_gpuFailed;
        if (!canPlace) ImGui.BeginDisabled();
        if (MeowSci.KsaAbstractions.WorkspaceUi.Button(_sprayMode ? "Spray at cursor...##graffiti_place" : "Place at Click...##graffiti_place", new float2(-1, 0)))
            Arm(_libraryNames[_selectedLibraryIndex]);
        if (!canPlace) ImGui.EndDisabled();

        if (inEditor)
        {
            ImGui.SameLine(0, 12);
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled("Decals are a flight-scene feature — leave the editor to place.");
        }
    }

    // ---- placed list ----

    private void RenderPlacedList()
    {
        // Drop selections whose decals are gone (e.g. removed via the public API).
        _selectedIds.RemoveWhere(id => _decals.All(d => d.Id != id));

        float rowH = ImGui.GetTextLineHeightWithSpacing();
        float listH = rowH * Math.Min(_decals.Count, 10) + ImGui.GetStyle().FramePadding.Y * 2f;
        if (ImGui.BeginListBox("##graffiti_list", new float2(-1f, listH)))
        {
            for (int i = 0; i < _decals.Count; i++)
                RenderListRow(i, _decals[i]);
            ImGui.EndListBox();
        }
        ImGui.TextDisabled("Click selects · Ctrl/Cmd+click toggles · Shift+click selects a range");

        ImGui.Spacing();
        bool noneSelected = _selectedIds.Count == 0;
        GraffitiUi.DangerButtonBegin();
        if (noneSelected) ImGui.BeginDisabled();
        if (ImGui.Button($" Delete Selected ( {_selectedIds.Count} ) ##graffiti_delete"))
        {
            RemoveDecals(_decals.Where(d => _selectedIds.Contains(d.Id)).ToList());
            _selectedIds.Clear();
            _lastClickedRow = -1;
        }
        if (noneSelected) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Clear All ##graffiti_clear"))
        {
            ClearDecals();
            _selectedIds.Clear();
            _lastClickedRow = -1;
        }
        GraffitiUi.DangerButtonEnd();
    }

    private void RenderListRow(int row, DecalEntry entry)
    {
        string suffix = entry.Live
            ? ""
            : entry.TextureState != DecalTextureState.Ready
                ? "  [image unavailable]"
                : "  [anchor gone]";
        string label = $"#{entry.Id}  {entry.ImageName}  →  {DescribeTarget(entry)}{suffix}"
                       + $"##graffiti_row_{entry.Id}";

        bool selected = _selectedIds.Contains(entry.Id);
        if (!ImGui.Selectable(label, selected))
            return;

        var io = ImGui.GetIO();
        if (io.KeyCtrl || io.KeySuper)
        {
            // Toggle just this row.
            if (!_selectedIds.Add(entry.Id))
                _selectedIds.Remove(entry.Id);
        }
        else if (io.KeyShift && _lastClickedRow >= 0 && _lastClickedRow < _decals.Count)
        {
            // Select the contiguous range from the last plain click to here.
            _selectedIds.Clear();
            var (from, to) = row < _lastClickedRow ? (row, _lastClickedRow) : (_lastClickedRow, row);
            for (int i = from; i <= to; i++)
                _selectedIds.Add(_decals[i].Id);
            return; // keep _lastClickedRow as the range pivot
        }
        else
        {
            _selectedIds.Clear();
            _selectedIds.Add(entry.Id);
        }
        _lastClickedRow = row;
    }
}
