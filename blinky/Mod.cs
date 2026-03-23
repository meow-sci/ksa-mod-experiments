using System;
using System.Reflection;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.BlinkenLib;
using MeowSci.BlinkyLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Blinky;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    // ── State ────────────────────────────────────────────────────────────────────

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;

    // Grid configuration
    private int _configWidth = 16;
    private int _configHeight = 8;
    private float _configSpacing = 0.5f;
    private float _configOffsetX = 0f;
    private float _configOffsetY = 5f;
    private float _configOffsetZ = 2f;
    private float _configPartScale = 0.1f;
    private string _enginePartId = "CorePropulsionA_Prefab_EngineA1";

    // Runtime state
    private BlinkyPixelGrid? _blinkyGrid = null;
    private readonly LcdAnimation _lcdAnimation = new();
    private bool _animActive = false;
    private string _buildMessage = "";
    private bool _buildMessageIsError = false;
    private object? _lastVehicle = null;

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

            // Advance LCD animation each frame when active
            if (_animActive && _blinkyGrid?.Grid != null && _blinkyGrid.Grid.Cols > 0)
                _lcdAnimation.Update(dt);

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
            _isDisposed = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"blinky: Error during unload: {ex.Message}");
        }
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

        // Detect vehicle change and clean up stale grid reference
        if (!ReferenceEquals(vehicle, _lastVehicle))
        {
            _lastVehicle = vehicle;
            _blinkyGrid = null;
            _animActive = false;
            _buildMessage = "";
        }

        // ── Vehicle status ──────────────────────────────────────────────────────
        if (vehicle == null)
        {
            ImGui.TextColored(new float4(1f, 0.4f, 0.2f, 1f), "No controlled vehicle");
            ImGui.End();
            return;
        }

        ImGui.Text($"Vehicle: {vehicle.Id}");
        ImGui.Text($"Grid: {(_blinkyGrid != null ? $"{_blinkyGrid.Grid.Cols}x{_blinkyGrid.Grid.Rows} ({_blinkyGrid.OwnedParts.Count} parts)" : "not built")}");
        ImGui.Separator();

        // ── Grid Configuration ──────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Grid Configuration", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();

            ImGui.SliderInt("Width (cols)##blinky", ref _configWidth, 1, 64);
            ImGui.SliderInt("Height (rows)##blinky", ref _configHeight, 1, 32);
            ImGui.Text($"Total parts: {_configWidth * _configHeight * 2}  (= {_configWidth} x {_configHeight} x 2 a/b pairs)");

            ImGui.Spacing();
            ImGui.SliderFloat("Spacing (m)##blinky", ref _configSpacing, 0.1f, 5.0f);
            ImGui.SliderFloat("Part scale##blinky", ref _configPartScale, 0.01f, 1.0f);
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

            bool hasGrid = _blinkyGrid != null;

            if (hasGrid)
            {
                ImGui.TextColored(new float4(0.2f, 1f, 0.5f, 1f), $"Grid active: {_blinkyGrid!.Grid.Cols}x{_blinkyGrid.Grid.Rows}");
                if (ImGui.Button("Destroy Grid##blinky"))
                {
                    try
                    {
                        _animActive = false;
                        if (_blinkyGrid.IsOwned)
                            LcdGridBuilder.DestroyGrid(vehicle, _blinkyGrid);
                        _blinkyGrid = null;
                        SetBuildMessage("Grid destroyed", false);
                    }
                    catch (Exception ex)
                    {
                        SetBuildMessage($"Destroy failed: {ex.Message}", true);
                        Console.WriteLine($"blinky: Destroy error: {ex}");
                    }
                }
            }
            else
            {
                if (ImGui.Button("Build Grid##blinky"))
                    DoBuildGrid(vehicle);

                ImGui.SameLine(0, 10);
                ImGui.TextDisabled($"Will create {_configWidth * _configHeight * 2} parts");
            }

            if (!string.IsNullOrEmpty(_buildMessage))
            {
                var msgColor = _buildMessageIsError
                    ? new float4(1f, 0.3f, 0.3f, 1f)
                    : new float4(0.4f, 1f, 0.4f, 1f);
                ImGui.TextColored(msgColor, _buildMessage);
            }

            ImGui.Unindent();
        }

        ImGui.Separator();

        // ── Pattern Control ─────────────────────────────────────────────────────
        if (_blinkyGrid != null)
        {
            if (ImGui.CollapsingHeader("Patterns", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ImGui.Button("All On##blinky"))
                    ApplyPattern(PixelPatterns.AllOn);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("All Off##blinky"))
                    ApplyPattern(_ => false);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Checkerboard##blinky"))
                    ApplyPattern(PixelPatterns.Checkerboard);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Rows##blinky"))
                    ApplyPattern(PixelPatterns.AlternatingRows);
                ImGui.SameLine(0, 8);
                if (ImGui.Button("Alt Cols##blinky"))
                    ApplyPattern(PixelPatterns.AlternatingCols);

                ImGui.Unindent();
            }

            ImGui.Separator();

            // ── Animation ──────────────────────────────────────────────────────
            if (ImGui.CollapsingHeader("Animation", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                if (ImGui.Button(_animActive ? "Stop##blinky" : "Start##blinky"))
                {
                    _animActive = !_animActive;
                    if (_animActive)
                        _lcdAnimation.Init(_blinkyGrid.Grid);
                }

                ImGui.SameLine(0, 10);
                float speed = _lcdAnimation.ScrollSpeed;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Speed##blinky", ref speed, 0.5f, 30f))
                    _lcdAnimation.ScrollSpeed = speed;

                if (_animActive)
                {
                    ImGui.TextColored(
                        new float4(0.2f, 1f, 0.5f, 1f),
                        $"Scrolling  offset={_lcdAnimation.ScrollOffset:F1}  image {_lcdAnimation.ImageWidth}x{_lcdAnimation.ImageHeight}");
                }
                else
                {
                    ImGui.TextDisabled("Animation stopped");
                }

                ImGui.Unindent();
            }
        }

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

            if (_blinkyGrid != null && ImGui.Button("Dump grid engines##blinky"))
                DumpGridEngines(_blinkyGrid.Grid);

            if (_blinkyGrid != null && ImGui.Button("Dump Engine Active States##blinky"))
                DumpEngineActiveStates(_blinkyGrid);

            if (ImGui.Button("Force SetIsActive All On##blinky"))
                ForceSetIsActiveAllOn(vehicle);

            if (_blinkyGrid != null && ImGui.Button("Rescan Grid##blinky"))
                RescanGrid(vehicle);

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

    // ── Grid Build ────────────────────────────────────────────────────────────────

    private void DoBuildGrid(Vehicle vehicle)
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
            };

            _blinkyGrid = LcdGridBuilder.BuildGrid(vehicle, config);
            if (_blinkyGrid != null)
                SetBuildMessage($"Built {_blinkyGrid.Grid.Cols}x{_blinkyGrid.Grid.Rows} grid ({_blinkyGrid.OwnedParts.Count} parts)", false);
            else
                SetBuildMessage("Build failed \u2014 check console log", true);
        }
        catch (Exception ex)
        {
            SetBuildMessage($"Build error: {ex.Message}", true);
            Console.WriteLine($"blinky: Build error: {ex}");
        }
    }

    // ── Pattern Helpers ───────────────────────────────────────────────────────────

    private void ApplyPattern(System.Func<(int row, int col), bool> selector)
    {
        if (_blinkyGrid == null) return;
        _animActive = false;

        // Use the PixelGrid's Engines dictionary: each key is one logical pixel (row,col)
        // and its value is the combined controller array for both the 'a' and 'b' engines.
        // This ensures a/b are always toggled together as a single pixel, matching blinken's model.
        var engines = _blinkyGrid.Grid.Engines;
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
            // ModLibrary.AllParts is internal; access via reflection
            var allPartsField = typeof(ModLibrary).GetField("AllParts",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (allPartsField == null)
            {
                Console.WriteLine("blinky dbg: could not find ModLibrary.AllParts field");
                return;
            }

            var allParts = allPartsField.GetValue(null);
            if (allParts == null) { Console.WriteLine("blinky dbg: AllParts is null"); return; }

            // SerializedCollection<PartTemplate> — iterate via IEnumerable or Values property
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

    // ── New Debug Helpers ─────────────────────────────────────────────────────────

    // Prints IsActive for up to 10 pixel engines from OwnedParts (always fresh, never cached).
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

    // Calls SetIsActive(null, true) on every EngineController in the vehicle's module list
    // (vehicle.Parts.Modules is the authoritative flat list of all merged parts' modules).
    // Also checks root.SubtreeModules as a diagnostic comparison.
    private static void ForceSetIsActiveAllOn(Vehicle vehicle)
    {
        // root.SubtreeModules only covers structural sub-parts of root, not TreeChildren.
        var root = vehicle.Parts.Root;
        int rootCount = root != null ? root.SubtreeModules.Get<EngineController>().Length : 0;
        Console.WriteLine($"blinky dbg: root.SubtreeModules engine controllers = {rootCount}");

        // vehicle.Parts.Modules is the true flat list of all modules from all merged parts.
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

    // Re-populates PixelGrid's cached engine controllers from the live part SubtreeModules.
    // Useful when the initial scan ran before controllers were fully initialized.
    private void RescanGrid(Vehicle vehicle)
    {
        if (_blinkyGrid == null) return;
        _blinkyGrid.Grid.RefreshEngineControllers();
        int total = 0;
        foreach (var engines in _blinkyGrid.Grid.Engines.Values)
            total += engines.Length;
        Console.WriteLine($"blinky dbg: RescanGrid done — grid {_blinkyGrid.Grid.Cols}x{_blinkyGrid.Grid.Rows}, {total} cached engine controllers");
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

    private void SetBuildMessage(string msg, bool isError)
    {
        _buildMessage = msg;
        _buildMessageIsError = isError;
        Console.WriteLine($"blinky: {msg}");
    }
}

