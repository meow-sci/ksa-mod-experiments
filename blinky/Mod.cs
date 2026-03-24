using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.BlinkenLib;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Blinky;

/// <summary>
/// Per-vehicle state container. Each vehicle with an active blinky grid gets its own instance.
/// </summary>
internal class VehicleState
{
    public readonly Vehicle Vehicle;
    public BlinkyPixelGrid? Grid;
    public readonly LcdAnimation Animation = new();
    public bool AnimActive;
    public string BuildMessage = "";
    public bool BuildMessageIsError;

    public VehicleState(Vehicle vehicle) => Vehicle = vehicle;
}

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    // ── Global State ─────────────────────────────────────────────────────────────

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    // Per-vehicle state, keyed by Vehicle reference
    private readonly Dictionary<Vehicle, VehicleState> _vehicleStates = new();

    // Grid configuration (global — applies to next build on any vehicle)
    private int _configWidth = 16;
    private int _configHeight = 8;
    private float _configSpacing = 5.0f;
    private float _configOffsetX = 0f;
    private float _configOffsetY = 5f;
    private float _configOffsetZ = 2f;
    private float _configPartScale = 0.010f;
    private string _enginePartId = "CorePropulsionA_Prefab_EngineA1";
    private int _configLayoutIndex = 0; // 0=Flat, 1=Cylinder

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

    // ── StarMap Lifecycle ─────────────────────────────────────────────────────────

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _isInitialized = true;
            Console.WriteLine("blinky: initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error during initialization: {ex.Message}");
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

            // Tick animations on ALL vehicles, not just the focused one
            TickAllAnimations(dt);

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            Patcher.Unload();
            _vehicleStates.Clear();
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error during unload: {ex.Message}");
        }
    }

    // ── Animation Tick ────────────────────────────────────────────────────────────

    private void TickAllAnimations(double dt)
    {
        foreach (var state in _vehicleStates.Values)
        {
            if (state.AnimActive && state.Grid?.Grid != null && state.Grid.Grid.Cols > 0)
                state.Animation.Update(dt);
        }
    }

    // ── Per-Vehicle State Lookup ──────────────────────────────────────────────────

    private VehicleState GetOrCreateState(Vehicle vehicle)
    {
        if (!_vehicleStates.TryGetValue(vehicle, out var state))
        {
            state = new VehicleState(vehicle);
            _vehicleStates[vehicle] = state;
        }
        return state;
    }

    // ── ImGui Window ──────────────────────────────────────────────────────────────

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(480, 640), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("blinky \u2014 Dynamic LCD Grid", ref _windowVisible))
        {
            ImGui.End();
            return;
        }

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
            ImGui.End();
            return;
        }

        var vs = GetOrCreateState(vehicle);

        ImGui.Text($"Vehicle: {vehicle.Id}");
        ImGui.Text($"Grid: {(vs.Grid != null ? $"{vs.Grid.Grid.Cols}x{vs.Grid.Grid.Rows} ({vs.Grid.OwnedParts.Count} parts)" : "not built")}");
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
            ImGui.TextDisabled("(blinken uses 0.1 — full size engines visually overlap)");

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
            ImGui.Text($"Engine template: {_enginePartId}");
            ImGui.Text("Quick select:");
            for (int i = 0; i < EnginePresets.Length; i++)
            {
                if (i > 0) ImGui.SameLine(0, 4);
                string label = $"A{i + 1}";
                if (ImGui.SmallButton(label))
                    _enginePartId = EnginePresets[i];
            }

            ImGui.Unindent();
        }

        ImGui.Separator();

        // ── Build / Destroy ─────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Build Control", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();

            bool hasGrid = vs.Grid != null;

            if (hasGrid)
            {
                ImGui.TextColored(new float4(0.2f, 1f, 0.5f, 1f), $"Grid active: {vs.Grid!.Grid.Cols}x{vs.Grid.Grid.Rows}");
                if (ImGui.Button("Destroy Grid##blinky"))
                {
                    try
                    {
                        vs.AnimActive = false;
                        if (vs.Grid.IsOwned)
                            LcdGridBuilder.DestroyGrid(vehicle, vs.Grid);
                        vs.Grid = null;
                        SetBuildMessage(vs, "Grid destroyed", false);
                    }
                    catch (Exception ex)
                    {
                        SetBuildMessage(vs, $"Destroy failed: {ex.Message}", true);
                        Console.WriteLine($"blinky: Destroy error: {ex}");
                    }
                }
            }
            else
            {
                if (ImGui.Button("Build Grid##blinky"))
                    DoBuildGrid(vehicle, vs);

                ImGui.SameLine(0, 10);
                ImGui.TextDisabled($"Will create {_configWidth * _configHeight * 2} parts");
            }

            if (!string.IsNullOrEmpty(vs.BuildMessage))
            {
                var msgColor = vs.BuildMessageIsError
                    ? new float4(1f, 0.3f, 0.3f, 1f)
                    : new float4(0.4f, 1f, 0.4f, 1f);
                ImGui.TextColored(msgColor, vs.BuildMessage);
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
        if (vs.Grid != null)
        {
            if (ImGui.CollapsingHeader("Patterns", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ImGui.Button("All On##blinky"))
                    ApplyPattern(vs, PixelPatterns.AllOn);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("All Off##blinky"))
                    ApplyPattern(vs, _ => false);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Checkerboard##blinky"))
                    ApplyPattern(vs, PixelPatterns.Checkerboard);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Rows##blinky"))
                    ApplyPattern(vs, PixelPatterns.AlternatingRows);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Cols##blinky"))
                    ApplyPattern(vs, PixelPatterns.AlternatingCols);

                ImGui.Unindent();
            }

            ImGui.Separator();

            // ── Animation ──────────────────────────────────────────────────────
            if (ImGui.CollapsingHeader("Animation", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ImGui.Button(vs.AnimActive ? "Stop##blinky" : "Start##blinky"))
                {
                    vs.AnimActive = !vs.AnimActive;
                    if (vs.AnimActive)
                        vs.Animation.Init(vs.Grid.Grid);
                }

                ImGui.SameLine(0, 10);
                float speed = vs.Animation.ScrollSpeed;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Speed##blinky", ref speed, 0.5f, 30f))
                    vs.Animation.ScrollSpeed = speed;

                if (vs.AnimActive)
                {
                    ImGui.TextColored(
                        new float4(0.2f, 1f, 0.5f, 1f),
                        $"Scrolling  offset={vs.Animation.ScrollOffset:F1}  image {vs.Animation.ImageWidth}x{vs.Animation.ImageHeight}");
                }
                else
                {
                    ImGui.TextDisabled("Animation stopped");
                }

                ImGui.Unindent();
            }
        }

        ImGui.Separator();

        // ── Active Vehicles Summary ─────────────────────────────────────────────
        RenderActiveVehiclesSummary();

        ImGui.Separator();

        // ── Debug ───────────────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Debug"))
        {
            ImGui.Indent();

            if (ImGui.Button("Dump vehicle.Parts type##blinky"))
                DumpVehiclePartsType(vehicle);

            ImGui.SameLine(0, 8);
            if (ImGui.Button("Dump root part##blinky"))
                DumpRootPart(vehicle);

            if (ImGui.Button("List engine templates##blinky"))
                ListEngineTemplates();

            if (vs.Grid != null && ImGui.Button("Dump grid engines##blinky"))
                DumpGridEngines(vs.Grid.Grid);

            if (vs.Grid != null && ImGui.Button("Dump Engine Active States##blinky"))
                DumpEngineActiveStates(vs.Grid);

            if (ImGui.Button("Force SetIsActive All On##blinky"))
                ForceSetIsActiveAllOn(vehicle);

            if (vs.Grid != null && ImGui.Button("Rescan Grid##blinky"))
                RescanGrid(vs);

            if (ImGui.Button("Compare Engines##blinky"))
            {
                try { DumpEngineComparison(vehicle); }
                catch (Exception ex) { Console.WriteLine($"blinky dbg compare error: {ex}"); }
            }

            ImGui.Unindent();
        }

        ImGui.Separator();
        if (ImGui.Button("Close##blinky"))
            _windowVisible = false;

        ImGui.End();
    }

    // ── Active Vehicles Summary ───────────────────────────────────────────────────

    private void RenderActiveVehiclesSummary()
    {
        // Count vehicles with grids or active animations
        int withGrids = 0;
        int withAnims = 0;
        foreach (var state in _vehicleStates.Values)
        {
            if (state.Grid != null) withGrids++;
            if (state.AnimActive) withAnims++;
        }
        if (withGrids > 0)
        {
            ImGui.TextDisabled($"Tracked: {withGrids} vehicle(s) with grids, {withAnims} animating");
        }
    }

    // ── Grid Build ────────────────────────────────────────────────────────────────

    private void DoBuildGrid(Vehicle vehicle, VehicleState vs)
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

            vs.Grid = LcdGridBuilder.BuildGrid(vehicle, config);
            if (vs.Grid != null)
                SetBuildMessage(vs, $"Built {vs.Grid.Grid.Cols}x{vs.Grid.Grid.Rows} grid ({vs.Grid.OwnedParts.Count} parts)", false);
            else
                SetBuildMessage(vs, "Build failed \u2014 check console log", true);
        }
        catch (Exception ex)
        {
            SetBuildMessage(vs, $"Build error: {ex.Message}", true);
            Console.WriteLine($"blinky: Build error: {ex}");
        }
    }

    // ── Pattern Helpers ───────────────────────────────────────────────────────────

    private static void ApplyPattern(VehicleState vs, System.Func<(int row, int col), bool> selector)
    {
        if (vs.Grid == null) return;
        vs.AnimActive = false;

        var engines = vs.Grid.Grid.Engines;
        if (engines.Count == 0)
        {
            Console.WriteLine("blinky: ApplyPattern — no engines cached (try Rescan Grid)");
            return;
        }

        int setOn = 0, setOff = 0;
        foreach (var (key, controllers) in engines)
        {
            bool on = selector(key);
            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i].SetIsActive(null, on);
                if (on) setOn++; else setOff++;
            }
        }
        Console.WriteLine($"blinky: ApplyPattern -> {setOn} on, {setOff} off across {engines.Count} pixels");
    }

    // ── Debug Helpers ─────────────────────────────────────────────────────────────

    private static void DumpVehiclePartsType(Vehicle vehicle)
    {
        Console.WriteLine($"blinky dbg: vehicle.Parts type = {vehicle.Parts.GetType().FullName}");
        Console.WriteLine($"blinky dbg: vehicle.Parts.Root = {vehicle.Parts.Root?.Id ?? "(null)"}");
        Console.WriteLine($"blinky dbg: vehicle.Parts.Count = {vehicle.Parts.Count}");

        var method = vehicle.Parts.GetType().GetMethod("RecomputeAllDerivedData",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Console.WriteLine($"blinky dbg: RecomputeAllDerivedData declaring type = {method?.DeclaringType?.FullName ?? "(not found)"}");
    }

    private static void DumpRootPart(Vehicle vehicle)
    {
        var root = vehicle.Parts.Root;
        if (root == null) { Console.WriteLine("blinky dbg: no root part"); return; }
        Console.WriteLine($"blinky dbg: root.Id = {root.Id}");
        Console.WriteLine($"blinky dbg: root.DisplayName = {root.DisplayName}");
        Console.WriteLine($"blinky dbg: root.IsSubPart = {root.IsSubPart}");
        Console.WriteLine($"blinky dbg: root.TreeChildren.Count = {root.TreeChildren.Count}");
    }

    private static void ListEngineTemplates()
    {
        Console.WriteLine("blinky dbg: listing engine-related PartTemplates via reflection...");
        try
        {
            var allPartsField = typeof(ModLibrary).GetField("AllParts",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (allPartsField == null)
            {
                Console.WriteLine("blinky dbg: could not find ModLibrary.AllParts field");
                return;
            }

            var allParts = allPartsField.GetValue(null);
            if (allParts == null) { Console.WriteLine("blinky dbg: AllParts is null"); return; }

            var valuesField = allParts.GetType().GetField("_collection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (valuesField == null) valuesField = allParts.GetType().GetField("_items",
                BindingFlags.Instance | BindingFlags.NonPublic);

            int count = 0;
            if (valuesField?.GetValue(allParts) is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var t = entry.Value;
                    if (t == null) continue;
                    var idProp = t.GetType().GetProperty("Id");
                    string? id = idProp?.GetValue(t) as string;
                    if (id?.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        id?.IndexOf("Propulsion", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"  template: {id}");
                        count++;
                    }
                }
            }
            else
            {
                Console.WriteLine($"blinky dbg: AllParts backing type = {allParts.GetType().FullName}");
                Console.WriteLine("blinky dbg: try AllParts.GetType() fields:");
                foreach (var f in allParts.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    Console.WriteLine($"  field: {f.Name} ({f.FieldType.Name})");
            }
            Console.WriteLine($"blinky dbg: found {count} engine-related templates");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky dbg: ListEngineTemplates error: {ex.Message}");
        }
    }

    private static void DumpGridEngines(PixelGrid grid)
    {
        Console.WriteLine($"blinky dbg: grid {grid.Cols}x{grid.Rows}, {grid.Count} cells");
        int total = 0;
        foreach (var (key, engines) in grid.Engines)
        {
            total += engines.Length;
            if (total <= 10)
                Console.WriteLine($"  ({key.row},{key.col}) -> {engines.Length} controllers, MinThrottle={engines[0].MinimumThrottle}");
        }
        Console.WriteLine($"blinky dbg: {total} total engine controllers");
    }

    private static void DumpEngineActiveStates(BlinkyPixelGrid grid)
    {
        Console.WriteLine("blinky dbg: DumpEngineActiveStates (first 10 pixel parts):");
        int shown = 0;
        int total = 0;
        foreach (var part in grid.OwnedParts)
        {
            var controllers = part.SubtreeModules.Get<EngineController>();
            total += controllers.Length;
            if (shown < 10)
            {
                for (int i = 0; i < controllers.Length; i++)
                    Console.WriteLine($"  {part.Id}[{i}]: IsActive={controllers[i].IsActive}, MinThrottle={controllers[i].MinimumThrottle}");
                shown++;
            }
        }
        Console.WriteLine($"blinky dbg: saw {total} total controllers across {grid.OwnedParts.Count} owned parts");
    }

    private static void ForceSetIsActiveAllOn(Vehicle vehicle)
    {
        var root = vehicle.Parts.Root;
        int rootCount = root != null ? root.SubtreeModules.Get<EngineController>().Length : 0;
        Console.WriteLine($"blinky dbg: root.SubtreeModules engine controllers = {rootCount}");

        var allControllers = vehicle.Parts.Modules.Get<EngineController>();
        Console.WriteLine($"blinky dbg: vehicle.Parts.Modules engine controllers = {allControllers.Length}");

        int count = 0;
        for (int i = 0; i < allControllers.Length; i++)
        {
            allControllers[i].SetIsActive(null, true);
            count++;
        }
        Console.WriteLine($"blinky dbg: Force SetIsActive All On: set {count} engines active");
    }

    private static void RescanGrid(VehicleState vs)
    {
        if (vs.Grid == null) return;
        vs.Grid.Grid.RefreshEngineControllers();
        int total = 0;
        foreach (var engines in vs.Grid.Grid.Engines.Values)
            total += engines.Length;
        Console.WriteLine($"blinky dbg: RescanGrid done — grid {vs.Grid.Grid.Cols}x{vs.Grid.Grid.Rows}, {total} cached engine controllers");
    }

    private static readonly BindingFlags AllFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static void DumpAllFields(object obj, string label, string indent = "")
    {
        var type = obj.GetType();
        Console.WriteLine($"blinky dbg: === {label} ({type.FullName}) ===");
        while (type != null && type != typeof(object))
        {
            foreach (var f in type.GetFields(AllFlags | BindingFlags.DeclaredOnly))
            {
                object? val = null;
                try { val = f.GetValue(obj); } catch { val = "<error>"; }
                string valStr = val is System.Collections.ICollection col
                    ? $"[Count={col.Count}]" : val?.ToString() ?? "null";
                Console.WriteLine($"blinky dbg: {indent}[{type.Name}] {f.Name} ({f.FieldType.Name}) = {valStr}");
            }
            type = type.BaseType;
        }
    }

    private static void DumpEngineComparison(Vehicle vehicle)
    {
        var all = vehicle.Parts.Modules.Get<EngineController>();
        Console.WriteLine($"blinky dbg: compare — total controllers: {all.Length}");

        EngineController? builtIn = null;
        EngineController? pixel = null;
        foreach (var ec in all)
        {
            if (pixel == null && ec.Parent.Id.StartsWith("pixel_"))
                pixel = ec;
            else if (builtIn == null && !ec.Parent.Id.StartsWith("pixel_"))
                builtIn = ec;
            if (pixel != null && builtIn != null) break;
        }

        if (builtIn != null)
            DumpSingleEngine(builtIn, "BUILT-IN");
        else
            Console.WriteLine("blinky dbg compare: no built-in engine found");

        if (pixel != null)
            DumpSingleEngine(pixel, "PIXEL");
        else
            Console.WriteLine("blinky dbg compare: no pixel engine found");
    }

    private static void DumpSingleEngine(EngineController ec, string label)
    {
        Console.WriteLine($"blinky dbg: ===== {label} ENGINE =====");
        DumpAllFields(ec, $"{label} EngineController");

        if (ec.Cores != null)
        {
            for (int i = 0; i < ec.Cores.Length; i++)
            {
                var core = ec.Cores[i];
                DumpAllFields(core, $"{label} RocketCore[{i}]");
                if (core.Rocket != null)
                    DumpAllFields(core.Rocket, $"{label} RocketCore[{i}].Rocket");
            }
        }
        else
        {
            Console.WriteLine($"blinky dbg: [{label}] Cores is null");
        }

        var part = ec.Parent;
        Console.WriteLine($"blinky dbg: [{label}] Part.Id                  = {part.Id}");
        Console.WriteLine($"blinky dbg: [{label}] Part.Stage               = {part.Stage}");
        Console.WriteLine($"blinky dbg: [{label}] Part.IsSubPart           = {part.IsSubPart}");
        Console.WriteLine($"blinky dbg: [{label}] Part.Template.Id         = {part.Template?.Id ?? "(null)"}");
        Console.WriteLine($"blinky dbg: [{label}] Part.TreeChildren.Count  = {part.TreeChildren?.Count ?? -1}");
        var subPartsField = typeof(Part).GetField("_subParts", BindingFlags.Instance | BindingFlags.NonPublic);
        var subParts = subPartsField?.GetValue(part) as System.Collections.ICollection;
        Console.WriteLine($"blinky dbg: [{label}] Part._subParts.Count     = {subParts?.Count ?? -1}");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────────

    private static void SetBuildMessage(VehicleState vs, string msg, bool isError)
    {
        vs.BuildMessage = msg;
        vs.BuildMessageIsError = isError;
        Console.WriteLine($"blinky: {msg}");
    }
}

