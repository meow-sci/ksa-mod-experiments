using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.DohLib.Materials;
using MeowSci.DohLib.Spawning;
using MeowSci.KsaAbstractions;

namespace MeowSci.DohLib;

/// <summary>
/// ISubmod implementation for DOH (Dynamically Originating Hominids).
/// Provides kitten spawning controls and management UI.
/// Used by grant supermod and standalone doh mod.
/// </summary>
public sealed class DohSubmod : ISubmod
{
    public string Name => "DOH";
    public string Tooltip => "Programmatic kitten spawning with per-kitten material customization.";

    // Core systems
    private MaterialFactory? _materialFactory;
    private SpawnedKittenRegistry? _registry;
    private KittenSpawner? _spawner;

    // UI state — vehicle selection
    private int _selectedVehicleIndex = -1;
    private ImGuiTextFilter _vehicleFilter = new();

    // UI state — character selection
    private string[] _availableCharacters = Array.Empty<string>();
    private int _selectedCharacterIndex = -1;
    private ImGuiTextFilter _characterFilter = new();

    // UI state — spawn parameters
    private float3 _offset = new float3(0f, 0f, 10f);
    private int _spawnCount = 1;
    private bool _useCustomColor;
    private float4 _tintColor = new float4(1f, 1f, 1f, 1f);
    private bool _uniquePerKitten;

    // UI state — XKCD color picker
    private string _selectedXkcdName = "";
    private readonly ImInputString _xkcdFilterText = new(64);

    // UI state — feedback
    private string? _statusMessage;
    private bool _statusIsError;

    // Cached XKCD color palette (built once via reflection)
    private static (string Name, float4 Color)[]? _xkcdColors;

    public void Initialize()
    {
        _materialFactory = new MaterialFactory();
        _registry = new SpawnedKittenRegistry();
        _spawner = new KittenSpawner(_materialFactory, _registry);

        _availableCharacters = _spawner.GetAvailableCharacters();
        Console.WriteLine("doh: DohSubmod initialized");
    }

    public void Update(double dt) { }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##doh_content");

        RenderStatus();
        RenderSpawnControls();
        ImGui.SeparatorText("Spawned Kittens");
        RenderKittenList();

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        _spawner?.DespawnAll();
        _materialFactory?.Cleanup();
        MaterialSystemAccessor.Cleanup();
        Console.WriteLine("doh: DohSubmod disposed");
    }

    // ---- Status ----

    private void RenderStatus()
    {
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            if (_statusIsError)
                ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), _statusMessage);
            else
                ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), _statusMessage);
            ImGui.Spacing();
        }

        if (!MaterialSystemAccessor.IsInitialized)
        {
            ImGui.TextColored(new float4(1f, 0.8f, 0.3f, 1f),
                "MaterialSystem not yet initialized (will init on first spawn).");
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Init Now"))
            {
                if (MaterialSystemAccessor.Initialize())
                    SetStatus("MaterialSystem initialized.", false);
                else
                    SetStatus($"Init failed: {MaterialSystemAccessor.LastError}", true);
            }
            ImGui.Spacing();
        }

        ImGui.TextDisabled($"Spawned: {_registry?.Count ?? 0} kittens  |  Materials: {_materialFactory?.CreatedSets.Count ?? 0}");
        ImGui.Spacing();
    }

    // ---- Spawn Controls ----

    private void RenderSpawnControls()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##doh_spawn_params", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            RenderVehicleRow();
            RenderCharacterRow();

            // Offset
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Offset");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.DragFloat3("##doh_offset", ref _offset, 1f, -1000f, 1000f);

            // Count
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Count");
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            ImGui.SliderInt("##doh_count", ref _spawnCount, 1, 20);

            // Custom color toggle
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Custom Color");
            ImGui.TableNextColumn();
            ImGui.Checkbox("##doh_usecolor", ref _useCustomColor);

            if (_useCustomColor)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Tint");
                ImGui.TableNextColumn();
                var prevColor = _tintColor;
                ImGui.ColorEdit4("##doh_tint", ref _tintColor,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
                if (_tintColor.X != prevColor.X || _tintColor.Y != prevColor.Y
                    || _tintColor.Z != prevColor.Z || _tintColor.W != prevColor.W)
                    _selectedXkcdName = "";

                // XKCD color combo
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("XKCD Color");
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                RenderXkcdCombo();

                if (_spawnCount > 1)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Unique Each");
                    ImGui.TableNextColumn();
                    ImGui.Checkbox("##doh_unique", ref _uniquePerKitten);
                }
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();

        // Spawn button
        bool canSpawn = _selectedVehicleIndex >= 0 && _spawner != null;
        if (!canSpawn) ImGui.BeginDisabled();
        if (ImGui.Button(" Spawn Kitten(s) ##doh"))
            DoSpawn();
        ImGui.SameLine(0, 8);
        ImGui.PushStyleColor(ImGuiCol.Button, (float4)KSAColor.Xkcd.NeonPurple);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, (float4)KSAColor.Xkcd.BrightMagenta);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, (float4)KSAColor.Xkcd.HotPink);
        ImGui.PushStyleColor(ImGuiCol.Text, (float4)KSAColor.Xkcd.PaleYellow);
        if (ImGui.Button(" I'm Feeling Lucky ##doh"))
            DoFeelingLucky();
        ImGui.PopStyleColor(4);
        if (!canSpawn) ImGui.EndDisabled();

        ImGui.SameLine(0, 12);
        if (ImGui.Button(" Refresh Characters ##doh"))
        {
            _availableCharacters = _spawner?.GetAvailableCharacters() ?? Array.Empty<string>();
            SetStatus($"Found {_availableCharacters.Length} characters.", false);
        }

        ImGui.Spacing();
    }

    private void RenderVehicleRow()
    {
        var vehicles = VehicleProvider.GetAllVehicles();
        var vehicleNames = vehicles.Select(v => v.Id).ToArray();

        if (_selectedVehicleIndex >= vehicleNames.Length)
            _selectedVehicleIndex = -1;

        string preview = _selectedVehicleIndex >= 0 ? vehicleNames[_selectedVehicleIndex] : "Select vehicle...";

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Vehicle");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##doh_vehicle", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _vehicleFilter.Clear();
            }
            _vehicleFilter.Draw("##doh_vfilter", -1f);

            for (int i = 0; i < vehicleNames.Length; i++)
            {
                if (!_vehicleFilter.PassFilter(vehicleNames[i])) continue;
                bool sel = _selectedVehicleIndex == i;
                if (ImGui.Selectable(vehicleNames[i] + "##doh_v", sel))
                    _selectedVehicleIndex = i;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void RenderCharacterRow()
    {
        if (_selectedCharacterIndex >= _availableCharacters.Length)
            _selectedCharacterIndex = -1;

        string charPreview = _selectedCharacterIndex >= 0
            ? _availableCharacters[_selectedCharacterIndex]
            : "(random)";

        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Character");
        ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##doh_char", charPreview))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _characterFilter.Clear();
            }
            _characterFilter.Draw("##doh_cfilter", -1f);

            if (_characterFilter.PassFilter("(random)"))
            {
                bool noSel = _selectedCharacterIndex < 0;
                if (ImGui.Selectable("(random)##doh_c", noSel))
                    _selectedCharacterIndex = -1;
                if (noSel) ImGui.SetItemDefaultFocus();
            }

            for (int i = 0; i < _availableCharacters.Length; i++)
            {
                if (!_characterFilter.PassFilter(_availableCharacters[i])) continue;
                bool sel = _selectedCharacterIndex == i;
                if (ImGui.Selectable(_availableCharacters[i] + "##doh_c", sel))
                    _selectedCharacterIndex = i;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    // ---- Kitten List ----

    private void RenderKittenList()
    {
        if (_registry == null || _registry.Count == 0)
        {
            ImGui.TextDisabled("No spawned kittens.");
            return;
        }

        var kittens = _registry.GetAll();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        var tableFlags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX
                       | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                       | ImGuiTableFlags.ScrollY;

        float maxHeight = ImGui.GetTextLineHeightWithSpacing() * 10;
        if (ImGui.BeginTable("##doh_kittens", 4, tableFlags, new float2(0, maxHeight)))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 80f);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 50f);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 80f);
            ImGui.TableHeadersRow();

            for (int i = 0; i < kittens.Count; i++)
            {
                var entry = kittens[i];
                ImGui.PushID(i);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(entry.KittenId);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled(entry.CharacterId);

                ImGui.TableNextColumn();
                if (entry.MaterialSet != null)
                {
                    var color = entry.MaterialSet.TintColor;
                    if (ImGui.ColorEdit4("##clr", ref color,
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                    {
                        _spawner?.RecolorKitten(entry.KittenId, color);
                    }
                }
                else
                {
                    ImGui.TextDisabled("—");
                }

                ImGui.TableNextColumn();
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
                if (ImGui.Button(" X ##despawn"))
                {
                    _spawner?.Despawn(entry.KittenId);
                    SetStatus($"Despawned '{entry.KittenId}'.", false);
                }
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
        if (ImGui.Button(" Despawn All ##doh"))
        {
            _spawner?.DespawnAll();
            SetStatus("All kittens despawned.", false);
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }

    // ---- Actions ----

    private void DoSpawn()
    {
        if (_spawner == null) return;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (_selectedVehicleIndex < 0 || _selectedVehicleIndex >= vehicles.Count)
        {
            SetStatus("No vehicle selected.", true);
            return;
        }

        string? characterId = _selectedCharacterIndex >= 0 && _selectedCharacterIndex < _availableCharacters.Length
            ? _availableCharacters[_selectedCharacterIndex]
            : null;

        var request = new SpawnRequest
        {
            ReferenceVehicleId = vehicles[_selectedVehicleIndex].Id,
            OffsetBodyFrame = new double3(_offset.X, _offset.Y, _offset.Z),
            Count = _spawnCount,
            CharacterId = characterId,
            TintColor = _useCustomColor ? _tintColor : null,
            UniqueMaterialsPerKitten = _uniquePerKitten,
        };

        var result = _spawner.Spawn(request);
        if (result.Success)
            SetStatus($"Spawned {result.Count} kitten(s).", false);
        else
            SetStatus(result.Error ?? "Spawn failed.", true);
    }

    private void DoFeelingLucky()
    {
        if (_spawner == null) return;

        var vehicles = VehicleProvider.GetAllVehicles();
        if (_selectedVehicleIndex < 0 || _selectedVehicleIndex >= vehicles.Count)
        {
            SetStatus("No vehicle selected.", true);
            return;
        }

        string? characterId = _selectedCharacterIndex >= 0 && _selectedCharacterIndex < _availableCharacters.Length
            ? _availableCharacters[_selectedCharacterIndex]
            : null;

        var colors = GetXkcdColors();
        var perKittenColors = PickRandomUniqueColors(colors, _spawnCount);

        var request = new SpawnRequest
        {
            ReferenceVehicleId = vehicles[_selectedVehicleIndex].Id,
            OffsetBodyFrame = new double3(_offset.X, _offset.Y, _offset.Z),
            Count = _spawnCount,
            CharacterId = characterId,
            TintColor = perKittenColors[0],
            PerKittenColors = perKittenColors,
            UniqueMaterialsPerKitten = true,
        };

        var result = _spawner.Spawn(request);
        if (result.Success)
            SetStatus($"Feeling lucky! Spawned {result.Count} rainbow kitten(s).", false);
        else
            SetStatus(result.Error ?? "Spawn failed.", true);
    }

    private static float4[] PickRandomUniqueColors((string Name, float4 Color)[] palette, int count)
    {
        if (count >= palette.Length)
        {
            // More kittens than colors — shuffle the whole palette and take what we need
            var shuffled = palette.OrderBy(_ => Random.Shared.Next()).ToArray();
            var result = new float4[count];
            for (int i = 0; i < count; i++)
                result[i] = shuffled[i % shuffled.Length].Color;
            return result;
        }

        // Fisher-Yates partial shuffle to pick `count` unique indices
        var indices = Enumerable.Range(0, palette.Length).ToArray();
        for (int i = 0; i < count; i++)
        {
            int j = Random.Shared.Next(i, indices.Length);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var colors = new float4[count];
        for (int i = 0; i < count; i++)
            colors[i] = palette[indices[i]].Color;
        return colors;
    }

    // ---- XKCD Color Combo ----

    private void RenderXkcdCombo()
    {
        string previewText = _selectedXkcdName.Length > 0 ? _selectedXkcdName : "Pick XKCD color...";
        if (ImGui.BeginCombo("##xkcd_combo", previewText))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##xkcd_filter", _xkcdFilterText);

            string filterStr = _xkcdFilterText.ToString();
            var colors = GetXkcdColors();
            foreach (var (name, color) in colors)
            {
                if (filterStr.Length > 0
                    && !name.Contains(filterStr, StringComparison.OrdinalIgnoreCase))
                    continue;

                ImGui.ColorButton($"##swatch_{name}", color,
                    ImGuiColorEditFlags.NoTooltip, new float2(14, 14));
                ImGui.SameLine();

                if (ImGui.Selectable(name, name == _selectedXkcdName))
                {
                    _selectedXkcdName = name;
                    _tintColor = color;
                    _useCustomColor = true;
                }
            }
            ImGui.EndCombo();
        }
    }

    private static (string Name, float4 Color)[] GetXkcdColors()
    {
        if (_xkcdColors != null) return _xkcdColors;

        var props = typeof(KSAColor.Xkcd).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var list = new List<(string, float4)>();
        foreach (var prop in props)
        {
            try
            {
                float4 val = (Color.Preset)prop.GetValue(null)!;
                list.Add((prop.Name, val));
            }
            catch { }
        }
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
        _xkcdColors = list.ToArray();
        Console.WriteLine($"doh: Cached {_xkcdColors.Length} XKCD colors");
        return _xkcdColors;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }
}
