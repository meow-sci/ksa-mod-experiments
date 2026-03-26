using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Grant.Submods;

internal sealed class BlinkySubmod : IGrantSubmod
{
    public string Name => "Blinky \u2014 Dynamic LCD Grid";

    // Per-vehicle UI state, keyed by vehicle ID
    private readonly Dictionary<string, VehicleUiState> _uiStates = new();

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

    // Known engine part IDs for quick-select buttons
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
        BlinkyGridManager.TickAll(dt);
    }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(0.2f, 1.0f, 0.5f, 1.0f), "blinky");
        ImGui.SameLine(0, 10);
        ImGui.TextDisabled("Dynamic LCD engine pixel grid");
        ImGui.Separator();

        var vehicle = VehicleProvider.GetControlledVehicle();

        // ── Vehicle status ──────────────────────────────────────────────────────
        if (vehicle == null)
        {
            ImGui.TextColored(new float4(1f, 0.4f, 0.2f, 1f), "No controlled vehicle");
            RenderActiveVehiclesSummary();
            return;
        }

        var vehicleId = vehicle.Id;
        var ui = GetOrCreateUiState(vehicleId);
        var gridState = BlinkyGridManager.Get(vehicleId);

        ImGui.Text($"Vehicle: {vehicleId}");
        ImGui.Text($"Grid: {(gridState != null ? $"{gridState.BlinkyGrid.Grid.Cols}x{gridState.BlinkyGrid.Grid.Rows} ({gridState.BlinkyGrid.OwnedParts.Count} parts)" : "not built")}");
        ImGui.Separator();

        // ── Grid Configuration ──────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Grid Configuration", ImGuiTreeNodeFlags.DefaultOpen))
        {
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

            ImGui.Unindent();
        }

        ImGui.Separator();

        // ── Build / Destroy ─────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Build Control", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();

            bool hasGrid = gridState != null;

            if (hasGrid)
            {
                ImGui.TextColored(new float4(0.2f, 1f, 0.5f, 1f), $"Grid active: {gridState!.BlinkyGrid.Grid.Cols}x{gridState.BlinkyGrid.Grid.Rows}");
                if (ImGui.Button("Destroy Grid##blinky"))
                {
                    try
                    {
                        BlinkyGridManager.TurnOff(vehicleId);
                        LcdGridBuilder.DestroyGrid(vehicle, gridState.BlinkyGrid);
                        BlinkyGridManager.Unregister(vehicleId);
                        SetBuildMessage(ui, "Grid destroyed", false);
                    }
                    catch (Exception ex)
                    {
                        SetBuildMessage(ui, $"Destroy failed: {ex.Message}", true);
                        Console.WriteLine($"blinky: Destroy error: {ex}");
                    }
                }
            }
            else
            {
                if (ImGui.Button("Build Grid##blinky"))
                    DoBuildGrid(vehicle, ui);

                ImGui.SameLine(0, 10);
                if (ImGui.Button("Scan Vehicle##blinky"))
                    DoScanVehicle(vehicle, ui);

                ImGui.SameLine(0, 10);
                ImGui.TextDisabled($"Build: {_configWidth * _configHeight * 2} parts | Scan: find existing");
            }

            if (!string.IsNullOrEmpty(ui.BuildMessage))
            {
                var msgColor = ui.BuildMessageIsError
                    ? new float4(1f, 0.3f, 0.3f, 1f)
                    : new float4(0.4f, 1f, 0.4f, 1f);
                ImGui.TextColored(msgColor, ui.BuildMessage);
            }

            ImGui.Spacing();
            bool showEngines = Patcher.RenderPixelParts;
            if (ImGui.Checkbox("Show engine meshes##blinky", ref showEngines))
                Patcher.RenderPixelParts = showEngines;
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("(off = better perf)");

            ImGui.Unindent();
        }

        ImGui.Separator();

        // ── Pattern Control ─────────────────────────────────────────────────────
        if (gridState != null)
        {
            if (ImGui.CollapsingHeader("Patterns", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ImGui.Button("All Off##blinky"))
                    BlinkyGridManager.TurnOff(vehicleId);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("All On##blinky"))
                    BlinkyGridManager.ApplyPattern(vehicleId, PixelPatterns.AllOn);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Checkerboard##blinky"))
                    BlinkyGridManager.ApplyPattern(vehicleId, PixelPatterns.Checkerboard);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Rows##blinky"))
                    BlinkyGridManager.ApplyPattern(vehicleId, PixelPatterns.AlternatingRows);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Cols##blinky"))
                    BlinkyGridManager.ApplyPattern(vehicleId, PixelPatterns.AlternatingCols);

                ImGui.Unindent();
            }

            ImGui.Separator();

            // ── Scroll Animation ───────────────────────────────────────────────
            if (ImGui.CollapsingHeader("Scroll", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                bool isScrolling = gridState.Scroll.IsActive;
                if (ImGui.Button(isScrolling ? "Stop##blinky" : "Start##blinky"))
                {
                    if (isScrolling)
                    {
                        BlinkyGridManager.TurnOff(vehicleId);
                    }
                    else
                    {
                        BlinkyGridManager.StartBuiltInScroll(vehicleId, ui.ScrollSpeed);
                    }
                }

                ImGui.SameLine(0, 10);
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Speed##blinky", ref ui.ScrollSpeed, 0.5f, 30f))
                {
                    if (gridState.Scroll.IsActive)
                        gridState.Scroll.ScrollSpeed = ui.ScrollSpeed;
                }

                if (gridState.Scroll.IsActive)
                {
                    ImGui.TextColored(
                        new float4(0.2f, 1f, 0.5f, 1f),
                        $"Scrolling  offset={gridState.Scroll.ScrollOffset:F1}  image {gridState.Scroll.ImageWidth}x{gridState.Scroll.ImageHeight}");
                }
                else
                {
                    ImGui.TextDisabled("Scroll stopped");
                }

                ImGui.Unindent();
            }
        }

        ImGui.Separator();

        // ── Active Vehicles Summary ─────────────────────────────────────────────
        RenderActiveVehiclesSummary();

        ImGui.Separator();

        if (gridState != null && ImGui.Button("Rescan Grid##blinky"))
            RescanGrid(gridState);
    }

    public void Dispose()
    {
        BlinkyGridManager.Clear();
        _uiStates.Clear();
    }

    // ── Per-Vehicle UI State ──────────────────────────────────────────────────────

    private class VehicleUiState
    {
        public string BuildMessage = "";
        public bool BuildMessageIsError;
        public float ScrollSpeed = 3f;
    }

    private VehicleUiState GetOrCreateUiState(string vehicleId)
    {
        if (!_uiStates.TryGetValue(vehicleId, out var state))
        {
            state = new VehicleUiState();
            _uiStates[vehicleId] = state;
        }
        return state;
    }

    // ── Active Vehicles Summary ───────────────────────────────────────────────────

    private static void RenderActiveVehiclesSummary()
    {
        var grids = BlinkyGridManager.Grids;
        if (grids.Count > 0)
        {
            int withScrolls = 0;
            foreach (var state in grids.Values)
            {
                if (state.Scroll.IsActive) withScrolls++;
            }
            ImGui.TextDisabled($"Tracked: {grids.Count} vehicle(s) with grids, {withScrolls} scrolling");
        }
    }

    // ── Grid Build ────────────────────────────────────────────────────────────────

    private void DoBuildGrid(Vehicle vehicle, VehicleUiState ui)
    {
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

            var grid = LcdGridBuilder.BuildGrid(vehicle, config);
            if (grid != null)
            {
                BlinkyGridManager.Register(vehicle, grid);
                SetBuildMessage(ui, $"Built {grid.Grid.Cols}x{grid.Grid.Rows} grid ({grid.OwnedParts.Count} parts)", false);
            }
            else
            {
                SetBuildMessage(ui, "Build failed \u2014 check console log", true);
            }
        }
        catch (Exception ex)
        {
            SetBuildMessage(ui, $"Build error: {ex.Message}", true);
            Console.WriteLine($"blinky: Build error: {ex}");
        }
    }

    // ── Scan Vehicle ──────────────────────────────────────────────────────────────

    private void DoScanVehicle(Vehicle vehicle, VehicleUiState ui)
    {
        try
        {
            Console.WriteLine("blinky: scanning vehicle for existing pixel engine grid...");
            var pixelGrid = PixelGrid.ScanFromVehicle(vehicle);

            if (pixelGrid.Count > 0)
            {
                pixelGrid.RefreshEngineControllers();
                var blinkyGrid = new BlinkyPixelGrid(pixelGrid, new List<Part>());
                BlinkyGridManager.Register(vehicle, blinkyGrid);
                SetBuildMessage(ui, $"Scanned {pixelGrid.Cols}x{pixelGrid.Rows} grid ({pixelGrid.Count} pixel pairs) [by ID]", false);
                return;
            }

            Console.WriteLine("blinky: ID scan found nothing, trying template-based scan...");
            var scannedGrid = LcdGridBuilder.ScanExistingGrid(vehicle, _enginePartId);

            if (scannedGrid != null)
            {
                BlinkyGridManager.Register(vehicle, scannedGrid);
                SetBuildMessage(ui, $"Scanned {scannedGrid.Grid.Cols}x{scannedGrid.Grid.Rows} grid ({scannedGrid.Grid.Count} pixel pairs) [by template]", false);
            }
            else
            {
                SetBuildMessage(ui, $"No pixel grid found (tried ID + template '{_enginePartId}' scan)", true);
            }
        }
        catch (Exception ex)
        {
            SetBuildMessage(ui, $"Scan error: {ex.Message}", true);
            Console.WriteLine($"blinky: Scan error: {ex}");
        }
    }

    private static void RescanGrid(GridState gs)
    {
        gs.BlinkyGrid.Grid.RefreshEngineControllers();
        int total = 0;
        foreach (var engines in gs.BlinkyGrid.Grid.Engines.Values)
            total += engines.Length;
        Console.WriteLine($"blinky dbg: RescanGrid done \u2014 grid {gs.BlinkyGrid.Grid.Cols}x{gs.BlinkyGrid.Grid.Rows}, {total} cached engine controllers");
    }

    private static void SetBuildMessage(VehicleUiState ui, string msg, bool isError)
    {
        ui.BuildMessage = msg;
        ui.BuildMessageIsError = isError;
        Console.WriteLine($"blinky: {msg}");
    }
}
