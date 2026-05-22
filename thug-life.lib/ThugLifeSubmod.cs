using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// ImGui surface for the thug-life mod: create new anchored sunglasses entries on a
/// selected vehicle/part/subpart and tune position, rotation, and size per entry.
/// </summary>
public sealed class ThugLifeSubmod : ISubmod
{
    public string Name => "Thug Life - Sunglasses Anchor";
    public string Tooltip => "Apply the thug-life sunglasses meme as a 2D quad anchored to any part/subpart on a vehicle.";

    public static ThugLifeSubmod? Instance { get; private set; }

    private ThugLifeRenderManager? _manager;

    // Create-form state
    private int _pendingVehicleIndex = -1;
    private int _pendingPartIndex = -1;
    private int _pendingSubPartIndex = -1;
    private int _prevVehicleIndex = -2;
    private int _prevPartIndex = -2;
    private float3 _pendingPosition = new(0f, 0f, 0f);
    private float3 _pendingRotation = new(0f, 0f, 0f);
    private float _pendingWidth = 0.6f;
    private float _pendingHeight = 0.16f;
    private string? _createError;

    // Cached lists for the selected target
    private readonly List<Part> _topLevelParts = new();
    private readonly List<Part> _subParts = new();

    // Combo filters
    private readonly ImInputString _vehicleFilter = new(128);
    private readonly ImInputString _partFilter = new(128);
    private readonly ImInputString _subPartFilter = new(128);

    public void Initialize()
    {
        Instance = this;
        _manager = new ThugLifeRenderManager();
    }

    public void Update(double dt)
    {
    }

    public void Dispose()
    {
        _manager?.Dispose();
        _manager = null;
        Instance = null;
    }

    public ThugLifeRenderManager? Manager => _manager;

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##tl_content");

        if (_manager == null || !_manager.IsReady)
        {
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f),
                _manager?.LastError ?? "thug-life renderer not initialized.");
            SubmodUI.EndContentArea();
            return;
        }

        RenderDebugSection();
        RenderCreateSection();

        var entries = _manager.Entries;
        if (entries.Count > 0)
        {
            ImGui.Spacing();
            ImGui.SeparatorText($"Active Sunglasses ( {entries.Count} )");

            ThugLifeEntry? toRemove = null;
            for (int i = 0; i < entries.Count; i++)
                RenderEntrySection(entries[i], i, ref toRemove);
            if (toRemove != null)
                _manager.Remove(toRemove);
        }

        SubmodUI.EndContentArea();
    }

    // ---- Debug Section ----

    private void RenderDebugSection()
    {
        if (_manager == null) return;

        bool open = ImGui.CollapsingHeader("Debug (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Render a quad directly in camera (ego) space, independent of any\nvehicle/part anchor. Use this to verify the render pipeline is alive.\nIf the debug quad doesn't show, try flipping the sign on the Z offset —\nthe forward axis depends on the camera convention.");
        if (!open) return;

        bool debug = _manager.DebugCameraMode;
        if (ImGui.Checkbox("Debug camera-space quad##tl_dbg_on", ref debug))
            _manager.DebugCameraMode = debug;

        ImGui.SameLine(0, 12);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"frames drawn: {_manager.FramesDrawn}");

        ImGui.Spacing();
        ImGui.Text("Ego-space offset (x, y, z) in meters");
        ImGui.SetNextItemWidth(-1f);
        float3 offset = _manager.DebugEgoOffset;
        if (ImGui.DragFloat3("##tl_dbg_offset", ref offset, 0.05f, -20f, 20f))
            _manager.DebugEgoOffset = offset;

        ImGui.Spacing();
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##tl_dbg_size", 4, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Width");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            float w = _manager.DebugWidth;
            if (ImGui.DragFloat("##tl_dbg_w", ref w, 0.01f, 0.01f, 50f))
                _manager.DebugWidth = w;
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Height");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            float h = _manager.DebugHeight;
            if (ImGui.DragFloat("##tl_dbg_h", ref h, 0.01f, 0.01f, 50f))
                _manager.DebugHeight = h;
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();
        if (ImGui.Button(" -Z 3m ##tl_dbg_negz"))
            _manager.DebugEgoOffset = new float3(0f, 0f, -3f);
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" +Z 3m ##tl_dbg_posz"))
            _manager.DebugEgoOffset = new float3(0f, 0f, 3f);
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Reset ##tl_dbg_reset"))
        {
            _manager.DebugEgoOffset = new float3(0f, 0f, -3f);
            _manager.DebugWidth = 1.5f;
            _manager.DebugHeight = 0.4f;
        }
    }

    // ---- Create Section ----

    private void RenderCreateSection()
    {
        bool open = ImGui.CollapsingHeader("Anchor New Sunglasses (?)", ImGuiTreeNodeFlags.DefaultOpen);
        ImGui.SetItemTooltip("Pick a vehicle, then a part, then optionally a subpart to anchor to.\nThe quad is positioned in that part's local frame using\nthe offset and rotation below.");
        if (!open) return;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
        {
            ImGui.Text("No vehicles available.");
            return;
        }

        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++) vehicleIds[i] = vehicles[i].Id;
        if (_pendingVehicleIndex >= vehicles.Count) _pendingVehicleIndex = -1;

        if (_pendingVehicleIndex != _prevVehicleIndex)
        {
            _prevVehicleIndex = _pendingVehicleIndex;
            _topLevelParts.Clear();
            _pendingPartIndex = -1;
            _subParts.Clear();
            _pendingSubPartIndex = -1;
            if (_pendingVehicleIndex >= 0)
                foreach (var p in vehicles[_pendingVehicleIndex].Parts.Parts)
                    _topLevelParts.Add(p);
        }

        var partLabels = new string[_topLevelParts.Count];
        for (int i = 0; i < _topLevelParts.Count; i++)
            partLabels[i] = $"{_topLevelParts[i].Template.Id}  [{_topLevelParts[i].Id}]";
        if (_pendingPartIndex >= _topLevelParts.Count) _pendingPartIndex = -1;

        if (_pendingPartIndex != _prevPartIndex)
        {
            _prevPartIndex = _pendingPartIndex;
            _subParts.Clear();
            _pendingSubPartIndex = -1;
            if (_pendingPartIndex >= 0)
                foreach (var sp in _topLevelParts[_pendingPartIndex].SubParts)
                    _subParts.Add(sp);
        }

        // The subpart combo always offers "(use this part)" at index 0, then the actual subparts.
        var subPartLabels = new string[_subParts.Count + 1];
        subPartLabels[0] = "(use this part)";
        for (int i = 0; i < _subParts.Count; i++)
            subPartLabels[i + 1] = $"{_subParts[i].Template.Id}  [{_subParts[i].Id}]";
        if (_pendingSubPartIndex < 0) _pendingSubPartIndex = 0;
        if (_pendingSubPartIndex >= subPartLabels.Length) _pendingSubPartIndex = 0;

        var style = ImGui.GetStyle();
        float labelW = ImGui.CalcTextSize("SubPart").X + style.ItemSpacing.X + 8f;

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var formFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##tl_form", 2, formFlags))
        {
            ImGui.TableSetupColumn("##tl_lbl", ImGuiTableColumnFlags.WidthFixed, labelW);
            ImGui.TableSetupColumn("##tl_widget", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            RenderFilteredCombo("##tl_veh", vehicleIds, ref _pendingVehicleIndex, _vehicleFilter);

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Part");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            bool noVehicle = _pendingVehicleIndex < 0 || _topLevelParts.Count == 0;
            if (noVehicle) ImGui.BeginDisabled();
            RenderFilteredCombo("##tl_part", partLabels, ref _pendingPartIndex, _partFilter);
            if (noVehicle) ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("SubPart");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            bool noPart = _pendingPartIndex < 0;
            if (noPart) ImGui.BeginDisabled();
            RenderFilteredCombo("##tl_sp", subPartLabels, ref _pendingSubPartIndex, _subPartFilter);
            if (noPart) ImGui.EndDisabled();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        RenderTransformFields("##tl_create",
            ref _pendingPosition, ref _pendingRotation,
            ref _pendingWidth, ref _pendingHeight);

        ImGui.Spacing();
        bool canCreate = _pendingVehicleIndex >= 0 && _pendingPartIndex >= 0;
        if (!canCreate) ImGui.BeginDisabled();
        if (ImGui.Button(" Add Sunglasses ##tl_add"))
        {
            CreateEntry(vehicles[_pendingVehicleIndex]);
        }
        if (!canCreate) ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(_createError))
        {
            ImGui.Spacing();
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _createError);
        }
    }

    private void CreateEntry(Vehicle vehicle)
    {
        if (_pendingPartIndex < 0 || _pendingPartIndex >= _topLevelParts.Count)
        {
            _createError = "Select a part first.";
            return;
        }
        Part topPart = _topLevelParts[_pendingPartIndex];
        Part anchor = topPart;
        if (_pendingSubPartIndex > 0 && _pendingSubPartIndex - 1 < _subParts.Count)
            anchor = _subParts[_pendingSubPartIndex - 1];

        if (_manager == null) { _createError = "Renderer not ready."; return; }

        var entry = new ThugLifeEntry
        {
            Vehicle = vehicle,
            Part = anchor,
            Position = _pendingPosition,
            Rotation = _pendingRotation,
            Width = _pendingWidth,
            Height = _pendingHeight,
            Visible = true,
        };
        _manager.Add(entry);
        _createError = null;
        Console.WriteLine($"thug-life: anchored sunglasses to {vehicle.Id} / {anchor.Id}");

        // Reset offsets but keep dropdowns where they are so the user can iterate.
        _pendingPosition = new float3(0f, 0f, 0f);
        _pendingRotation = new float3(0f, 0f, 0f);
    }

    // ---- Entry Section ----

    private void RenderEntrySection(ThugLifeEntry entry, int index, ref ThugLifeEntry? toRemove)
    {
        string label = $"Sunglasses: {entry.Vehicle.Id} / {entry.Part.Id}##tl_e{index}";
        if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var wpadX = ImGui.GetStyle().WindowPadding.X;
        float childW = ImGui.GetContentRegionAvail().X + wpadX * 2;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - wpadX);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 10f));
        ImGui.BeginChild($"tl_child_{index}", new float2(childW, 0),
            ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.PopStyleVar();

        ImGui.Text($"{entry.Vehicle.Id}  →  {entry.Part.Id}");

        RenderTransformFields($"##tl_e{index}",
            ref entry.Position, ref entry.Rotation,
            ref entry.Width, ref entry.Height);

        ImGui.Spacing();
        ImGui.Checkbox($"Visible##tl_e{index}_vis", ref entry.Visible);

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
        if (ImGui.Button($" Remove ##tl_e{index}_rm"))
            toRemove = entry;
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.EndChild();
    }

    // ---- Shared Fields ----

    private static void RenderTransformFields(string idPrefix,
        ref float3 position, ref float3 rotation,
        ref float width, ref float height)
    {
        ImGui.Spacing();
        ImGui.Text("Position (x, y, z) in meters — anchor part local frame");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_pos", ref position, 0.001f, 0f, 0f);

        ImGui.Spacing();
        ImGui.Text("Rotation (pitch, yaw, roll) in degrees");
        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat3($"{idPrefix}_rot", ref rotation, 0.25f, -180f, 180f);

        ImGui.Spacing();
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable($"{idPrefix}_size", 4, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Width");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.DragFloat($"{idPrefix}_w", ref width, 0.001f, 0.001f, 50f);
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Height");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1f);
            ImGui.DragFloat($"{idPrefix}_h", ref height, 0.001f, 0.001f, 50f);
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private static void RenderFilteredCombo(string id, string[] items, ref int selectedIndex,
        ImInputString filter)
    {
        string preview = selectedIndex >= 0 && selectedIndex < items.Length
            ? items[selectedIndex] : "Select...";

        if (!ImGui.BeginCombo(id, preview)) return;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            filter.Clear();
        }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint($"{id}_filter", "filter..."u8, filter);
        string filterText = filter.ToString().Trim();

        for (int i = 0; i < items.Length; i++)
        {
            if (filterText.Length > 0 && !items[i].Contains(filterText, StringComparison.OrdinalIgnoreCase)) continue;
            bool sel = selectedIndex == i;
            ImGui.PushID(i);
            if (ImGui.Selectable(items[i], sel))
                selectedIndex = i;
            ImGui.PopID();
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }
}
