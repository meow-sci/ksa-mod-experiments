using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.DohLib.Materials;
using MeowSci.DohLib.Spawning;
using MeowSci.KsaAbstractions;

namespace MeowSci.Doh;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;

    // Core systems (from doh.lib)
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

    // UI state — feedback
    private string? _statusMessage;
    private bool _statusIsError;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();

            if (!MaterialSystemAccessor.Initialize())
                Console.WriteLine($"doh: MaterialSystem init failed: {MaterialSystemAccessor.LastError}");

            _materialFactory = new MaterialFactory();
            _registry = new SpawnedKittenRegistry();
            _spawner = new KittenSpawner(_materialFactory, _registry);

            _availableCharacters = _spawner.GetAvailableCharacters();

            _isInitialized = true;
            Console.WriteLine("doh: Initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F8))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            _spawner?.DespawnAll();
            _materialFactory?.Cleanup();
            MaterialSystemAccessor.Cleanup();
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("doh: Unloaded");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(480, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("DOH — Kitten Spawner###doh-window", ref _windowVisible))
        {
            ImGui.End();
            return;
        }

        SubmodUI.BeginContentArea("##doh_content");

        RenderStatus();
        RenderSpawnControls();
        ImGui.SeparatorText("Spawned Kittens");
        RenderKittenList();

        SubmodUI.EndContentArea();
        ImGui.End();
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
            ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f),
                $"MaterialSystem not initialized: {MaterialSystemAccessor.LastError ?? "unknown"}");
            ImGui.Spacing();
        }

        ImGui.TextDisabled($"Spawned: {_registry?.Count ?? 0} kittens  |  Materials: {_materialFactory?.CreatedSets.Count ?? 0}");
        ImGui.Spacing();
    }

    // ---- Spawn Controls ----

    private void RenderSpawnControls()
    {
        if (!ImGui.CollapsingHeader("Spawn Controls (?)", ImGuiTreeNodeFlags.DefaultOpen))
            return;
        ImGui.SetItemTooltip("Configure and spawn kittens near a vehicle or at an absolute position.");

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        if (ImGui.BeginTable("##doh_spawn_params", 2, flags))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

            // Vehicle selector
            RenderVehicleRow();

            // Character selector
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

            // Color picker (shown when custom color enabled)
            if (_useCustomColor)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Tint");
                ImGui.TableNextColumn();
                ImGui.ColorEdit4("##doh_tint", ref _tintColor,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);

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

            // "(random)" option
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

                // Name
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text(entry.KittenId);

                // Character
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled(entry.CharacterId);

                // Color (editable if has material set)
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

                // Actions
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

        // Despawn All
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

    private void SetStatus(string message, bool isError)
    {
        _statusMessage = message;
        _statusIsError = isError;
    }
}
