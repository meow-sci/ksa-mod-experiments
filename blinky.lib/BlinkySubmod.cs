using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.BlinkyLib;

public sealed class BlinkySubmod : ISubmod
{
    public string Name => "Blinky \u2014 Dynamic LCD Grid";

    // Per-grid UI state, keyed by (vehicleId, gridName)
    private readonly Dictionary<(string vehicleId, string gridName), GridUiState> _uiStates = new();

    // Grid name input for creating new grids
    private readonly ImInputString _newGridName = new ImInputString(64);

    // Build/scan status message (shown beside the create controls)
    private string _createMessage = "";
    private bool _createMessageIsError;

    // Grid configuration (global — applies to next build on any vehicle)
    private int _configWidth = 16;
    private int _configHeight = 8;
    private float _configSpacing = 5.0f;
    private float _configOffsetX = 0f;
    private float _configOffsetY = 5f;
    private float _configOffsetZ = 2f;
    private float _configPartScale = 0.010f;
    private string _enginePartId = "CorePropulsionA_Prefab_EngineA3";
    private int _configLayoutIndex = 0; // 0=Flat, 1=Cylinder
    private int _enginePresetIndex = 2;
    private ImGuiTextFilter _engineFilter = new();

    // Deferred action runner
    private readonly Queue<(double delayBefore, Action action)> _deferredActions = new();
    private double _deferredTimer = 0;

    // Known engine part IDs for quick-select
    private static readonly string[] EnginePresets = new[]
    {
        "CorePropulsionA_Prefab_EngineA1",
        "CorePropulsionA_Prefab_EngineA2",
        "CorePropulsionA_Prefab_EngineA3",
        "CorePropulsionA_Prefab_EngineA4",
        "CorePropulsionA_Prefab_EngineA5",
        "CorePropulsionA_Prefab_EngineA6",
    };

    public void Initialize() { }

    public void Update(double dt)
    {
        if (_deferredActions.Count > 0)
        {
            _deferredTimer -= dt;
            if (_deferredTimer <= 0)
            {
                var (_, action) = _deferredActions.Dequeue();
                action();
                _deferredTimer = _deferredActions.Count > 0 ? _deferredActions.Peek().delayBefore : 0;
            }
        }

        BlinkyGridManager.TickAll(dt);
    }

    private void ScheduleDeferred(double delaySeconds, Action action)
    {
        bool wasEmpty = _deferredActions.Count == 0;
        _deferredActions.Enqueue((delaySeconds, action));
        if (wasEmpty)
            _deferredTimer = delaySeconds;
    }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(0.2f, 1.0f, 0.5f, 1.0f), "blinky");
        ImGui.SameLine(0, 10);
        ImGui.TextDisabled("Dynamic LCD engine pixel grid");
        ImGui.Separator();

        var vehicle = VehicleProvider.GetControlledVehicle();

        if (vehicle == null)
        {
            ImGui.TextColored(new float4(1f, 0.4f, 0.2f, 1f), "No controlled vehicle");
            RenderActiveVehiclesSummary();
            return;
        }

        var vehicleId = vehicle.Id;
        var vehicleGrids = BlinkyGridManager.GetAllForVehicle(vehicleId).ToList();

        ImGui.Text($"Vehicle: {vehicleId}");
        ImGui.Text($"Grids: {vehicleGrids.Count} registered");
        ImGui.Separator();

        // ── Grid Configuration ──────────────────────────────────────────────────
        RenderGridConfiguration(vehicle, vehicleId);

        ImGui.Separator();

        // ── Per-Grid Controls ───────────────────────────────────────────────────
        foreach (var gs in vehicleGrids)
            RenderGridControls(vehicle, vehicleId, gs);

        // ── Active Vehicles Summary ─────────────────────────────────────────────
        ImGui.Separator();
        RenderActiveVehiclesSummary();
    }

    // ── Grid Configuration Section ───────────────────────────────────────────────

    private void RenderGridConfiguration(Vehicle vehicle, string vehicleId)
    {
        if (!ImGui.CollapsingHeader("Grid Configuration", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Indent();

        ImGui.DragInt("Width (cols)##blinky", ref _configWidth, 1, 1, 256);
        ImGui.DragInt("Height (rows)##blinky", ref _configHeight, 1, 1, 256);
        ImGui.Text($"Total parts: {_configWidth * _configHeight * 2}  (= {_configWidth} x {_configHeight} x 2 a/b pairs)");

        ImGui.Spacing();
        if (ImGui.RadioButton("Flat##blinky", _configLayoutIndex == 0)) _configLayoutIndex = 0;
        ImGui.SameLine(0, 8);
        if (ImGui.RadioButton("Cylinder##blinky", _configLayoutIndex == 1)) _configLayoutIndex = 1;
        if (_configLayoutIndex == 1)
        {
            double cylRadius = (_configWidth * _configSpacing) / (2.0 * System.Math.PI);
            ImGui.TextDisabled($"Cylinder radius: {cylRadius:F2} m  (circumference = {_configWidth} x {_configSpacing:F2} m)");
        }

        ImGui.Spacing();
        ImGui.DragFloat("Spacing (m)##blinky", ref _configSpacing, 0.01f, 0.0f, 10.0f);
        ImGui.DragFloat("Part scale##blinky", ref _configPartScale, 0.001f, 0.001f, 1.0f);
        ImGui.TextDisabled("(blinken uses 0.1 \u2014 full size engines visually overlap)");

        ImGui.Spacing();
        ImGui.Text("Offset from vehicle root (m):");
        ImGui.SetNextItemWidth(120);
        ImGui.DragFloat("X##blinkyOX", ref _configOffsetX, 0.1f);
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(120);
        ImGui.DragFloat("Y##blinkyOY", ref _configOffsetY, 0.1f);
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(120);
        ImGui.DragFloat("Z##blinkyOZ", ref _configOffsetZ, 0.1f);

        ImGui.Spacing();
        ImGui.Text("Engine template:");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##blinky_engine", _enginePartId))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _engineFilter.Clear();
            }
            _engineFilter.Draw("##blinky_engine_filter", -1f);
            for (int i = 0; i < EnginePresets.Length; i++)
            {
                if (_engineFilter.PassFilter(EnginePresets[i]))
                {
                    bool sel = _enginePresetIndex == i;
                    if (ImGui.Selectable(EnginePresets[i], sel))
                    {
                        _enginePresetIndex = i;
                        _enginePartId = EnginePresets[i];
                    }
                    if (sel) ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.SeparatorText("Create Grid");

        ImGui.Text("Grid name:");
        ImGui.SameLine(0, 8);
        ImGui.SetNextItemWidth(200);
        ImGui.InputText("##blinky_gridname", _newGridName);

        ImGui.Spacing();
        if (ImGui.Button("Build Grid##blinky_create"))
            DoBuildGrid(vehicle, vehicleId);

        ImGui.SameLine(0, 10);
        if (ImGui.Button("Scan Grid##blinky_scan"))
            DoScanGrid(vehicle, vehicleId);

        ImGui.SameLine(0, 10);
        if (ImGui.Button("Scan All Grids##blinky_scanall"))
            DoScanAllGrids(vehicle);

        if (!string.IsNullOrEmpty(_createMessage))
        {
            var msgColor = _createMessageIsError
                ? new float4(1f, 0.3f, 0.3f, 1f)
                : new float4(0.4f, 1f, 0.4f, 1f);
            ImGui.TextColored(msgColor, _createMessage);
        }

        ImGui.Spacing();
        bool showEngines = BlinkyPatchState.RenderPixelParts;
        if (ImGui.Checkbox("Show engine meshes##blinky", ref showEngines))
            BlinkyPatchState.RenderPixelParts = showEngines;
        ImGui.SameLine(0, 8);
        ImGui.TextDisabled("(off = better perf)");

        ImGui.Unindent();
    }

    // ── Per-Grid Controls ────────────────────────────────────────────────────────

    private void RenderGridControls(Vehicle vehicle, string vehicleId, GridState gs)
    {
        var gridName = gs.GridName;
        var gridId = $"{vehicleId}_{gridName}";
        var ui = GetOrCreateGridUiState(vehicleId, gridName);

        if (!ImGui.CollapsingHeader($"{gridName}##grid_{gridId}", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Indent();

        ImGui.Text($"Grid: {gs.BlinkyGrid.Grid.Cols}x{gs.BlinkyGrid.Grid.Rows} ({gs.BlinkyGrid.Grid.Count} pixel pairs)");

        // ── Patterns ─────────────────────────────────────────────────────────
        if (ImGui.Button($"All Off##{gridId}"))
            BlinkyGridManager.TurnOff(vehicleId, gridName);
        ImGui.SameLine(0, 8);
        if (ImGui.Button($"All On##{gridId}"))
            BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AllOn);
        ImGui.SameLine(0, 8);
        if (ImGui.Button($"Checkerboard##{gridId}"))
            BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.Checkerboard);
        ImGui.SameLine(0, 8);
        if (ImGui.Button($"Alt Rows##{gridId}"))
            BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AlternatingRows);
        ImGui.SameLine(0, 8);
        if (ImGui.Button($"Alt Cols##{gridId}"))
            BlinkyGridManager.ApplyPattern(vehicleId, gridName, PixelPatterns.AlternatingCols);

        // ── Scroll ───────────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.SeparatorText($"Scroll##{gridId}");

        bool isScrolling = gs.Scroll.IsActive;
        if (ImGui.Button(isScrolling ? $"Stop##{gridId}" : $"Start##{gridId}"))
        {
            if (isScrolling)
                BlinkyGridManager.TurnOff(vehicleId, gridName);
            else
                BlinkyGridManager.StartBuiltInScroll(vehicleId, gridName, ui.ScrollSpeed);
        }

        ImGui.SameLine(0, 10);
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat($"Speed##{gridId}", ref ui.ScrollSpeed, 0.5f, 30f))
        {
            if (gs.Scroll.IsActive)
                gs.Scroll.ScrollSpeed = ui.ScrollSpeed;
        }

        if (gs.Scroll.IsActive)
        {
            ImGui.TextColored(
                new float4(0.2f, 1f, 0.5f, 1f),
                $"Scrolling  offset={gs.Scroll.ScrollOffset:F1}  image {gs.Scroll.ImageWidth}x{gs.Scroll.ImageHeight}");
        }
        else
        {
            ImGui.TextDisabled("Scroll stopped");
        }

        // ── Grid Actions ─────────────────────────────────────────────────────
        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button($"Rescan Grid##{gridId}"))
            RescanGrid(gs);

        ImGui.SameLine(0, 10);
        if (ImGui.Button($"Destroy Grid##{gridId}"))
        {
            var capturedVehicleId = vehicleId;
            var capturedGridName = gridName;
            var capturedVehicle = vehicle;
            var capturedGrid = gs.BlinkyGrid;

            // Always shut down grid and wait 2s before destroying to prevent stuck-on engine sounds.
            ScheduleDeferred(0, () =>
            {
                Console.WriteLine($"blinky: destroy step 1 — shutting down grid '{capturedGridName}'");
                BlinkyGridManager.TurnOff(capturedVehicleId, capturedGridName);
            });
            ScheduleDeferred(2.0, () =>
            {
                try
                {
                    Console.WriteLine($"blinky: destroy step 2 — removing parts for grid '{capturedGridName}'");
                    LcdGridBuilder.DestroyGrid(capturedVehicle, capturedGrid);
                    BlinkyGridManager.Unregister(capturedVehicleId, capturedGridName);
                    _createMessage = $"Grid '{capturedGridName}' destroyed";
                    _createMessageIsError = false;
                    Console.WriteLine($"blinky: {_createMessage}");
                }
                catch (Exception ex)
                {
                    _createMessage = $"Destroy failed: {ex.Message}";
                    _createMessageIsError = true;
                    Console.WriteLine($"blinky: Destroy error: {ex}");
                }
            });
        }

        if (!string.IsNullOrEmpty(ui.Message))
        {
            var msgColor = ui.MessageIsError
                ? new float4(1f, 0.3f, 0.3f, 1f)
                : new float4(0.4f, 1f, 0.4f, 1f);
            ImGui.TextColored(msgColor, ui.Message);
        }

        ImGui.Unindent();
    }

    public void Dispose()
    {
        BlinkyGridManager.Clear();
        _uiStates.Clear();
    }

    // ── Per-Grid UI State ─────────────────────────────────────────────────────────

    private class GridUiState
    {
        public string Message = "";
        public bool MessageIsError = false;
        public float ScrollSpeed = 3f;
    }

    private GridUiState GetOrCreateGridUiState(string vehicleId, string gridName)
    {
        var key = (vehicleId, gridName);
        if (!_uiStates.TryGetValue(key, out var state))
        {
            state = new GridUiState();
            _uiStates[key] = state;
        }
        return state;
    }

    // ── Active Vehicles Summary ───────────────────────────────────────────────────

    private static void RenderActiveVehiclesSummary()
    {
        var grids = BlinkyGridManager.Grids;
        if (grids.Count == 0) return;

        int vehicleCount = grids.Values.Select(s => s.VehicleId).Distinct().Count();
        int scrollCount = 0;
        foreach (var state in grids.Values)
        {
            if (state.Scroll.IsActive) scrollCount++;
        }
        ImGui.TextDisabled($"Tracked: {vehicleCount} vehicle(s), {grids.Count} grid(s), {scrollCount} scrolling");
    }

    // ── Grid Build ────────────────────────────────────────────────────────────────

    private void DoBuildGrid(Vehicle vehicle, string vehicleId)
    {
        var gridName = _newGridName.ToString().Trim();

        if (!ValidateGridName(gridName, vehicleId))
            return;

        try
        {
            var config = new LcdGridConfig
            {
                Width = _configWidth,
                Height = _configHeight,
                Spacing = _configSpacing,
                OffsetX = _configOffsetX,
                OffsetY = _configOffsetY,
                OffsetZ = _configOffsetZ,
                PartScale = _configPartScale,
                EnginePartId = _enginePartId,
                Layout = _configLayoutIndex == 1 ? GridLayout.Cylinder : GridLayout.Flat,
            };

            var grid = LcdGridBuilder.BuildGrid(vehicle, gridName, config);
            if (grid != null)
            {
                BlinkyGridManager.Register(vehicle, gridName, grid);
                SetCreateMessage($"Built grid '{gridName}': {grid.Grid.Cols}x{grid.Grid.Rows} ({grid.OwnedParts.Count} parts)", false);
                _newGridName.Clear();
            }
            else
            {
                SetCreateMessage("Build failed \u2014 check console log", true);
            }
        }
        catch (Exception ex)
        {
            SetCreateMessage($"Build error: {ex.Message}", true);
            Console.WriteLine($"blinky: Build error: {ex}");
        }
    }

    // ── Scan Grid (single named grid) ─────────────────────────────────────────────

    private void DoScanGrid(Vehicle vehicle, string vehicleId)
    {
        var gridName = _newGridName.ToString().Trim();

        if (!ValidateGridName(gridName, vehicleId))
            return;

        try
        {
            Console.WriteLine($"blinky: scanning vehicle for grid '{gridName}'...");
            var pixelGrid = PixelGrid.ScanFromVehicle(vehicle, gridName);

            if (pixelGrid.Count > 0)
            {
                pixelGrid.RefreshEngineControllers();
                var blinkyGrid = new BlinkyPixelGrid(pixelGrid, new List<Part>());
                BlinkyGridManager.Register(vehicle, gridName, blinkyGrid);
                SetCreateMessage($"Scanned grid '{gridName}': {pixelGrid.Cols}x{pixelGrid.Rows} ({pixelGrid.Count} pixel pairs) [by ID]", false);
                _newGridName.Clear();
                return;
            }

            Console.WriteLine($"blinky: ID scan found nothing for '{gridName}', trying template-based scan...");
            var scannedGrid = LcdGridBuilder.ScanExistingGrid(vehicle, gridName, _enginePartId);

            if (scannedGrid != null)
            {
                BlinkyGridManager.Register(vehicle, gridName, scannedGrid);
                SetCreateMessage($"Scanned grid '{gridName}': {scannedGrid.Grid.Cols}x{scannedGrid.Grid.Rows} ({scannedGrid.Grid.Count} pixel pairs) [by template]", false);
                _newGridName.Clear();
            }
            else
            {
                SetCreateMessage($"No grid '{gridName}' found (tried ID + template '{_enginePartId}' scan)", true);
            }
        }
        catch (Exception ex)
        {
            SetCreateMessage($"Scan error: {ex.Message}", true);
            Console.WriteLine($"blinky: Scan error: {ex}");
        }
    }

    // ── Scan All Grids (auto-discovery) ───────────────────────────────────────────

    private void DoScanAllGrids(Vehicle vehicle)
    {
        try
        {
            Console.WriteLine("blinky: scanning vehicle for ALL pixel grids...");
            var discovered = PixelGrid.ScanAllFromVehicle(vehicle);

            if (discovered.Count == 0)
            {
                SetCreateMessage("No pixel grids found on vehicle", true);
                return;
            }

            var names = new List<string>();
            foreach (var (gridName, pixelGrid) in discovered)
            {
                pixelGrid.RefreshEngineControllers();
                var blinkyGrid = new BlinkyPixelGrid(pixelGrid, new List<Part>());
                BlinkyGridManager.Register(vehicle, gridName, blinkyGrid);
                names.Add(gridName);
            }

            SetCreateMessage($"Discovered {discovered.Count} grid(s): {string.Join(", ", names)}", false);
        }
        catch (Exception ex)
        {
            SetCreateMessage($"Scan all error: {ex.Message}", true);
            Console.WriteLine($"blinky: Scan all error: {ex}");
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────────

    private bool ValidateGridName(string gridName, string vehicleId)
    {
        if (string.IsNullOrWhiteSpace(gridName))
        {
            SetCreateMessage("Grid name is required", true);
            return false;
        }
        if (!PixelGrid.IsValidGridName(gridName))
        {
            SetCreateMessage("Grid name must contain only letters, digits, and hyphens (no underscores)", true);
            return false;
        }
        if (BlinkyGridManager.Get(vehicleId, gridName) != null)
        {
            SetCreateMessage($"Grid '{gridName}' already exists for this vehicle", true);
            return false;
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void RescanGrid(GridState gs)
    {
        gs.BlinkyGrid.Grid.RefreshEngineControllers();
        int total = 0;
        foreach (var engines in gs.BlinkyGrid.Grid.Engines.Values)
            total += engines.Length;
        Console.WriteLine($"blinky dbg: RescanGrid done \u2014 grid '{gs.GridName}' {gs.BlinkyGrid.Grid.Cols}x{gs.BlinkyGrid.Grid.Rows}, {total} cached engine controllers");
    }

    private void SetCreateMessage(string msg, bool isError)
    {
        _createMessage = msg;
        _createMessageIsError = isError;
        Console.WriteLine($"blinky: {msg}");
    }
}
