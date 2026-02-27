using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;

namespace mod;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  // pixel grid: key = (row, col), value = (partA, partB) — the a/b engine pair for each pixel
  private readonly Dictionary<(int row, int col), (Part a, Part b)> _pixelGrid = new();
  // cached EngineControllers per pixel — resolved once at scan time, used every frame
  private readonly Dictionary<(int row, int col), EngineController[]> _pixelEngines = new();
  private object? _lastVehicle = null;
  private bool _vehicleDebugDumped = false;
  private EngineController? _testEngineController = null;
  private bool _testEngineActive = false;

  // ─── LCD scroll animation state ───
  private bool _lcdAnimationActive = false;
  private float _lcdScrollOffset = 0f;        // current scroll position (fractional columns)
  private float _lcdScrollSpeed = 3f;          // columns per second
  private int _lcdLastScrollCol = -1;          // last integer column applied (dirty check)
  private int _gridRows = 0;                   // physical grid height  (from scanned parts)
  private int _gridCols = 0;                   // physical grid width   (from scanned parts)
  private int _imageWidth = 0;                 // source image width    (from pixel data)
  private int _imageHeight = 0;                // source image height   (from pixel data)
  private HashSet<(int x, int y)> _lcdPixelSet = new(); // fast lookup for source pixels
  private int _lcdTotalScroll = 0;             // imageWidth + gap before repeat


  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch();
      _isInitialized = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"blinken: Error during initialization: {ex.Message}");
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

      // Advance LCD scroll animation
      if (_lcdAnimationActive && _gridCols > 0)
        UpdateLcdAnimation(dt);

      if (ImGui.IsKeyPressed(ImGuiKey.F11))
        _windowVisible = !_windowVisible;

      if (_windowVisible)
        RenderWindow();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"blinken: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"blinken: Error during unload: {ex.Message}");
    }
  }

  // ─── Shared debug helpers ───

  private const BindingFlags PubInst  = BindingFlags.Instance | BindingFlags.Public;
  private const BindingFlags AllInst  = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

  private static string FormatValue(object? val)
  {
    if (val == null) return "(null)";
    var vt = val.GetType();
    if (val is System.Collections.ICollection col) return $"[{col.Count} items] {vt.Name}";
    if (val is Array arr) return $"[{arr.Length} items] {vt.Name}";
    if (vt.Name.Contains("Span")) return $"{vt.Name}";
    return val.ToString() ?? "(null)";
  }

  private static void DumpObject(object obj, string objPath, string objIndent, bool includeNonPublic = true)
  {
    var type = obj.GetType();

    // Type hierarchy
    var hierarchy = new List<string>();
    var t = type;
    while (t != null && t != typeof(object))
    {
      hierarchy.Add(t.FullName ?? t.Name);
      t = t.BaseType;
    }
    Console.WriteLine($"{objIndent}{objPath}.GetType()  = {string.Join(" → ", hierarchy)}");

    // Interfaces
    var ifaces = type.GetInterfaces();
    if (ifaces.Length > 0)
      Console.WriteLine($"{objIndent}{objPath} implements: {string.Join(", ", Array.ConvertAll(ifaces, i => i.Name))}");

    // Public properties
    foreach (var prop in type.GetProperties(PubInst))
    {
      if (prop.GetIndexParameters().Length > 0) continue;
      try
      {
        var val = prop.GetValue(obj);
        Console.WriteLine($"{objIndent}{objPath}.{prop.Name}  = {FormatValue(val)}  ({prop.PropertyType.Name})");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"{objIndent}{objPath}.{prop.Name}  = <error: {ex.InnerException?.Message ?? ex.Message}>");
      }
    }

    // Public fields
    foreach (var field in type.GetFields(PubInst))
    {
      try
      {
        var val = field.GetValue(obj);
        Console.WriteLine($"{objIndent}{objPath}.{field.Name}  = {FormatValue(val)}  ({field.FieldType.Name})");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"{objIndent}{objPath}.{field.Name}  = <error: {ex.InnerException?.Message ?? ex.Message}>");
      }
    }

    // Non-public fields (walk hierarchy)
    if (includeNonPublic)
    {
      var walkType = type;
      while (walkType != null && walkType != typeof(object))
      {
        foreach (var field in walkType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
          if (field.Name.Contains("k__BackingField")) continue;
          try
          {
            var val = field.GetValue(obj);
            Console.WriteLine($"{objIndent}{objPath}.__{walkType.Name}.{field.Name}  = {FormatValue(val)}  ({field.FieldType.Name})");
          }
          catch (Exception ex)
          {
            Console.WriteLine($"{objIndent}{objPath}.__{walkType.Name}.{field.Name}  = <error: {ex.InnerException?.Message ?? ex.Message}>");
          }
        }
        walkType = walkType.BaseType;
      }
    }

    // Public methods (signatures only)
    foreach (var method in type.GetMethods(PubInst | BindingFlags.DeclaredOnly))
    {
      if (method.IsSpecialName) continue;
      var parms = string.Join(", ", Array.ConvertAll(method.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
      Console.WriteLine($"{objIndent}{objPath}.{method.Name}({parms})  → {method.ReturnType.Name}");
    }
  }

  private static void DumpComponentList(PartComponentList list, string listPath, string indent)
  {
    var dictField = typeof(PartComponentList).GetField(
      "_typeListsByType", BindingFlags.Instance | BindingFlags.NonPublic);
    if (dictField == null)
    {
      Console.WriteLine($"{indent}{listPath}  = <_typeListsByType field not found>");
      return;
    }

    var dict = dictField.GetValue(list) as System.Collections.IDictionary;
    if (dict == null || dict.Count == 0)
    {
      Console.WriteLine($"{indent}{listPath}  = (empty — 0 types)");
      return;
    }

    Console.WriteLine($"{indent}{listPath}  ({dict.Count} type(s)):");
    foreach (System.Collections.DictionaryEntry entry in dict)
    {
      var componentType = (Type)entry.Key;
      var typeList = entry.Value;
      if (typeList == null) continue;

      var tlType = typeList.GetType();

      int count = -1;
      var countProp = tlType.GetProperty("Count", PubInst);
      if (countProp != null)
        count = (int)countProp.GetValue(typeList)!;

      Console.WriteLine($"{indent}  [{componentType.FullName}]  count={count}");

      if (count <= 0) continue;

      // Primary: extract from _components (List<T>)
      var componentsField = tlType.GetField("_components", AllInst);
      if (componentsField != null)
      {
        var listObj = componentsField.GetValue(typeList);
        if (listObj is System.Collections.IList ilist)
        {
          for (int i = 0; i < ilist.Count && i < count; i++)
          {
            var elem = ilist[i];
            if (elem == null) continue;
            string elemPath = $"{listPath}[{componentType.Name}][{i}]";
            string elemIndent = indent + "      ";
            DumpObject(elem, elemPath, elemIndent);
          }
          continue;
        }
      }

      // Fallback: any IEnumerable field
      bool extracted = false;
      foreach (var f in tlType.GetFields(AllInst))
      {
        var fVal = f.GetValue(typeList);
        if (fVal is System.Collections.IEnumerable enumerable && fVal is not string)
        {
          int i = 0;
          foreach (var elem in enumerable)
          {
            if (elem == null) continue;
            if (i >= count) break;
            string elemPath = $"{listPath}[{componentType.Name}][{i}]";
            string elemIndent = indent + "      ";
            DumpObject(elem, elemPath, elemIndent);
            i++;
          }
          if (i > 0) { extracted = true; break; }
        }
      }

      if (!extracted)
      {
        Console.WriteLine($"{indent}    (could not extract instances; type-list type: {tlType.FullName})");
        foreach (var f in tlType.GetFields(AllInst))
          Console.WriteLine($"{indent}      {f.Name} ({f.FieldType.Name}) = {f.GetValue(typeList)}");
      }
    }
  }

  // ─── Debug: deep-dump a Part's component graph ───
  private void DebugDumpPart(Part part, string path = "part", int depth = 0)
  {
    string indent = new string(' ', depth * 2);

    Console.WriteLine($"{indent}{path}.GetType()          = {part.GetType().FullName}");
    Console.WriteLine($"{indent}{path}.Id                 = {part.Id}");
    Console.WriteLine($"{indent}{path}.DisplayName        = {part.DisplayName}");
    Console.WriteLine($"{indent}{path}.IsSubPart          = {part.IsSubPart}");
    Console.WriteLine($"{indent}{path}.PartParent?.Id     = {part.PartParent?.Id ?? "(null)"}");

    DumpComponentList(part.Components, $"{path}.Components", indent);
    DumpComponentList(part.SubtreeComponents, $"{path}.SubtreeComponents", indent);

    // SubParts — recurse
    int spIdx = 0;
    foreach (var sp in part.SubParts)
    {
      Console.WriteLine($"{indent}{path}.SubParts[{spIdx}] ───────────────────");
      DebugDumpPart(sp, $"{path}.SubParts[{spIdx}]", depth + 1);
      spIdx++;
    }
    if (spIdx == 0)
      Console.WriteLine($"{indent}{path}.SubParts = (none)");

    // TreeChildren (just list, don't recurse)
    if (part.TreeChildren.Count > 0)
    {
      Console.WriteLine($"{indent}{path}.TreeChildren.Count = {part.TreeChildren.Count}");
      for (int i = 0; i < part.TreeChildren.Count; i++)
        Console.WriteLine($"{indent}  {path}.TreeChildren[{i}].Id = {part.TreeChildren[i].Id}");
    }
    else
    {
      Console.WriteLine($"{indent}{path}.TreeChildren = (none)");
    }
  }

  // ─── Debug: dump Vehicle + PartTree component lists ───
  private void DebugDumpVehicle(Vehicle vehicle)
  {
    Console.WriteLine("═══════════ blinken VEHICLE DEBUG DUMP ═══════════");

    // Vehicle identity
    Console.WriteLine($"vehicle.GetType()     = {vehicle.GetType().FullName}");
    Console.WriteLine($"vehicle.Id            = {vehicle.Id}");

    // PartTree info
    var parts = vehicle.Parts;
    Console.WriteLine($"vehicle.Parts.GetType() = {parts.GetType().FullName}");
    Console.WriteLine($"vehicle.Parts.Count     = {parts.Count}");

    // PartTree public fields/properties via reflection
    var ptType = parts.GetType();
    Console.WriteLine($"vehicle.Parts type hierarchy:");
    var pt = ptType;
    while (pt != null && pt != typeof(object))
    {
      Console.WriteLine($"  {pt.FullName}");
      pt = pt.BaseType;
    }

    // Dump all public fields on PartTree (this is where Components, StageList, RocketCores, etc. live)
    Console.WriteLine("vehicle.Parts public fields:");
    foreach (var field in ptType.GetFields(PubInst))
    {
      var val = field.GetValue(parts);
      Console.WriteLine($"  vehicle.Parts.{field.Name}  = {FormatValue(val)}  ({field.FieldType.Name})");
    }

    Console.WriteLine("vehicle.Parts public properties:");
    foreach (var prop in ptType.GetProperties(PubInst))
    {
      if (prop.GetIndexParameters().Length > 0) continue;
      try
      {
        var val = prop.GetValue(parts);
        Console.WriteLine($"  vehicle.Parts.{prop.Name}  = {FormatValue(val)}  ({prop.PropertyType.Name})");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"  vehicle.Parts.{prop.Name}  = <error: {ex.InnerException?.Message ?? ex.Message}>");
      }
    }

    // PartTree.Components — the vehicle-wide component list (this is where EngineController/ThrusterController live)
    // Access via reflection since we don't know exactly what's public vs field
    PartComponentList? treeComponents = null;
    var compField = ptType.GetField("Components", PubInst);
    if (compField != null)
      treeComponents = compField.GetValue(parts) as PartComponentList;
    else
    {
      var compProp = ptType.GetProperty("Components", PubInst);
      if (compProp != null)
        treeComponents = compProp.GetValue(parts) as PartComponentList;
    }

    if (treeComponents != null)
    {
      DumpComponentList(treeComponents, "vehicle.Parts.Components", "");
    }
    else
    {
      Console.WriteLine("vehicle.Parts.Components  = <not found>");
    }

    // Also dump any other PartComponentList-type fields on PartTree
    foreach (var field in ptType.GetFields(AllInst))
    {
      if (field.FieldType == typeof(PartComponentList) && field.Name != "Components")
      {
        var pcl = field.GetValue(parts) as PartComponentList;
        if (pcl != null)
          DumpComponentList(pcl, $"vehicle.Parts.{field.Name}", "");
      }
    }

    // List all parts (just ids) for reference
    Console.WriteLine($"vehicle.Parts — part list ({parts.Count} parts):");
    foreach (var p in parts.Parts)
    {
      var compCount = 0;
      var dictField = typeof(PartComponentList).GetField("_typeListsByType", BindingFlags.Instance | BindingFlags.NonPublic);
      if (dictField != null)
      {
        var dict = dictField.GetValue(p.Components) as System.Collections.IDictionary;
        compCount = dict?.Count ?? 0;
      }
      Console.WriteLine($"  {p.Id}  components={compCount}  subparts={p.SubParts.Length}  treeChildren={p.TreeChildren.Count}");
    }

    Console.WriteLine("═══════════ END VEHICLE DEBUG DUMP ══════════════");
  }

  // Scans vehicle parts for pixel_ engine pairs and populates _pixelGrid.
  // Id format: pixel_{row}_{col}_a / pixel_{row}_{col}_b
  private void RefreshPixelGrid(object vehicle)
  {
    _pixelGrid.Clear();

    var partA = new Dictionary<(int row, int col), Part>();
    var partB = new Dictionary<(int row, int col), Part>();

    foreach (var part in Program.ControlledVehicle!.Parts.Parts)
    {
      // Debug dump vehicle-wide PartTree — one time only
      if (!_vehicleDebugDumped)
      {
        _vehicleDebugDumped = true;
        // DebugDumpVehicle(Program.ControlledVehicle);
      }

      // Capture EngineController for pixel_2_0_a — one time only
      if (_testEngineController == null && part.Id == "pixel_2_0_a")
      {
        var controllers = part.SubtreeComponents.Get<EngineController>();
        if (controllers.Length > 0)
        {
          _testEngineController = controllers[0];
          _testEngineActive = _testEngineController.IsActive;
          Console.WriteLine($"blinken: captured EngineController for pixel_2_0_a (IsActive={_testEngineActive})");
        }
      }

      if (!part.Id.StartsWith("pixel_")) continue;

      // Expected segments: ["pixel", row, col, "a"|"b"]
      var segments = part.Id.Split('_');
      if (segments.Length != 4) continue;
      if (!int.TryParse(segments[1], out int row)) continue;
      if (!int.TryParse(segments[2], out int col)) continue;

      var key = (row, col);
      if (segments[3] == "a") partA[key] = part;
      else if (segments[3] == "b") partB[key] = part;
    }

    foreach (var key in partA.Keys)
      if (partB.TryGetValue(key, out var pb))
        _pixelGrid[key] = (partA[key], pb);

    // Cache EngineControllers per pixel so we never call Get<T>() in the hot loop
    _pixelEngines.Clear();
    foreach (var (key, (a, b)) in _pixelGrid)
    {
      var list = new List<EngineController>();
      foreach (var part in new[] { a, b })
      {
        var controllers = part.SubtreeComponents.Get<EngineController>();
        for (int i = 0; i < controllers.Length; i++)
          list.Add(controllers[i]);
      }
      _pixelEngines[key] = list.ToArray();
    }

    Console.WriteLine($"blinken: found {_pixelGrid.Count} pixel pairs, cached {_pixelEngines.Values.Sum(e => e.Length)} engine controllers");
  }

  // Set the Active state on an engine pair (both a and b).
  private static void SetEngineActive(Part partA, Part partB, bool active)
  {
    foreach (var part in new[] { partA, partB })
    {
      var controllers = part.SubtreeComponents.Get<EngineController>();
      for (int i = 0; i < controllers.Length; i++)
        controllers[i].SetIsActive(null, active);
    }
  }

  // Set active state using cached controllers (no reflection per call)
  private static void SetEngineActiveCached(EngineController[] controllers, bool active)
  {
    for (int i = 0; i < controllers.Length; i++)
      controllers[i].SetIsActive(null, active);
  }

  // Deactivate every EngineController on the entire vehicle.
  private static void DeactivateAllEngines(Vehicle vehicle)
  {
    int count = 0;
    foreach (var part in vehicle.Parts.Parts)
    {
      var controllers = part.SubtreeComponents.Get<EngineController>();
      for (int i = 0; i < controllers.Length; i++)
      {
        controllers[i].SetIsActive(null, false);
        count++;
      }
    }
    Console.WriteLine($"blinken: deactivated {count} engine controllers");
  }
  
  private void SetupMinThrottle(Vehicle vehicle, float minThrottle)
  {
    int count = 0;
    var engineControllers = vehicle.Parts.Components.Get<EngineController>();
    
    foreach (var controller in engineControllers)
    {
      count++;
      controller.MinimumThrottle = minThrottle;
    }
    
    //
    // foreach (var part in vehicle.Parts.Parts)
    // {
    //   var controllers = part.SubtreeComponents.Get<EngineController>();
    //   for (int i = 0; i < controllers.Length; i++)
    //   {
    //     controllers[i].MinimumThrottle = minThrottle;
    //     count++;
    //   }
    // }
    if (count > 0)
    {
      Console.WriteLine($"blinken: set MinimumThrottle={minThrottle} on {count} engine controllers");
      vehicle.Parts.RecomputeAllDerivedData();
    }
  }

  // ─── LCD scroll animation logic ───

  private void InitLcdAnimation()
  {
    // Compute physical grid dimensions from scanned pixel parts
    _gridRows = _pixelGrid.Keys.Max(k => k.row) + 1;
    _gridCols = _pixelGrid.Keys.Max(k => k.col) + 1;

    // Compute source image dimensions from pixel data
    var pixels = LcdAnimationPixels.Pixels;
    if (pixels.Length == 0)
    {
      _imageWidth = 0;
      _imageHeight = 0;
      Console.WriteLine("blinken: LCD animation has no pixel data");
      return;
    }

    _imageWidth  = pixels.Max(p => p.x) + 1;
    _imageHeight = pixels.Max(p => p.y) + 1;

    // Build fast lookup set
    _lcdPixelSet = new HashSet<(int x, int y)>(pixels);

    // Total scroll distance: image width + half-grid-width gap
    _lcdTotalScroll = _imageWidth + (_gridCols / 2);

    _lcdScrollOffset = 0f;
    _lcdLastScrollCol = -1; // force first frame to apply

    // One-time throttle setup so we don't do it every frame
    var vehicle = Program.ControlledVehicle;
    if (vehicle != null)
      SetupMinThrottle(vehicle, 0.0001f);

    Console.WriteLine($"blinken: LCD init — grid {_gridCols}x{_gridRows}, image {_imageWidth}x{_imageHeight}, totalScroll {_lcdTotalScroll}");
  }

  private void UpdateLcdAnimation(double dt)
  {
    _lcdScrollOffset += _lcdScrollSpeed * (float)dt;

    // Wrap around when we've scrolled the full cycle
    if (_lcdScrollOffset >= _lcdTotalScroll)
      _lcdScrollOffset -= _lcdTotalScroll;

    int scrollCol = (int)_lcdScrollOffset;

    // Skip update if we haven't moved to a new integer column
    if (scrollCol == _lcdLastScrollCol) return;
    _lcdLastScrollCol = scrollCol;

    // Directly update cached engine controllers — no reflection, no lambda, no SetupMinThrottle
    foreach (var (key, engines) in _pixelEngines)
    {
      int srcX = scrollCol + key.col;
      int srcY = key.row;

      bool on = srcX >= 0 && srcX < _imageWidth
             && srcY >= 0 && srcY < _imageHeight
             && _lcdPixelSet.Contains((srcX, srcY));

      SetEngineActiveCached(engines, on);
    }
  }

  // Apply a pixel pattern:
  //   1. Vehicle-wide shutdown (resets all running engines)
  //   2. Set Active per pixel according to selector
  //   3. Vehicle-wide ignite (only Active engines will fire)
  // Pass ignite=false to just shut everything down.
  private void ApplyPattern(bool ignite, System.Func<(int row, int col), bool> selector)
  {
    var vehicle = Program.ControlledVehicle;
    if (vehicle == null) return;

    SetupMinThrottle(vehicle, 0.0001f);

    // Step 1: shutdown all engines
    // vehicle.SetEnum(VehicleEngine.MainShutdown);

    // Step 2: set active state per pixel
    foreach (var (key, (a, b)) in _pixelGrid)
      SetEngineActive(a, b, selector(key));

    // Step 3: re-ignite so active engines fire
    if (ignite)
    {
      // vehicle.SetEnum(VehicleEngine.MainIgnite);
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(420, 200), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("blinken", ref _windowVisible))
    {
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "blinken — pixel engine grid");
      ImGui.Separator();

      var vehicle = Program.ControlledVehicle;
      if (vehicle == null)
      {
        ImGui.TextDisabled("No controlled vehicle");
      }
      else
      {
        // Refresh grid whenever the vehicle instance changes
        if (!ReferenceEquals(vehicle, _lastVehicle))
        {
          _lastVehicle = vehicle;
          RefreshPixelGrid(vehicle);
        }

        if (_pixelGrid.Count == 0)
        {
          ImGui.TextDisabled("No pixel_ engine parts found on vehicle");
        }
        else
        {
          ImGui.Text($"Pixel count: {_pixelGrid.Count}");
          ImGui.Separator();

          // --- On patterns ---
          if (ImGui.Button("All On"))
            ApplyPattern(ignite: true, _ => true);

          ImGui.SameLine();
          if (ImGui.Button("Every Other"))
          {
            // Odd rows are offset by 1 so the checkerboard staggers
            ApplyPattern(ignite: true, key =>
            {
              bool rowOffset = (key.row % 2) == 1;
              return ((key.col + (rowOffset ? 1 : 0)) % 2) == 0;
            });
          }

          ImGui.SameLine();
          if (ImGui.Button("Alt Rows"))
            ApplyPattern(ignite: true, key => (key.row % 2) == 0);

          ImGui.SameLine();
          if (ImGui.Button("Alt Cols"))
            ApplyPattern(ignite: true, key => (key.col % 2) == 0);

          // --- Off ---
          ImGui.Separator();
          if (ImGui.Button("All Off"))
            ApplyPattern(ignite: false, _ => false);

          ImGui.SameLine();
          if (ImGui.Button("Deactivate All Engines"))
            DeactivateAllEngines(vehicle);

          // --- LCD scroll animation ---
          ImGui.Separator();
          ImGui.TextColored(new float4(0.3f, 0.8f, 1.0f, 1.0f), "LCD Animation");

          if (ImGui.Button(_lcdAnimationActive ? "Stop Scroll" : "Start Scroll"))
          {
            _lcdAnimationActive = !_lcdAnimationActive;
            if (_lcdAnimationActive)
            {
              InitLcdAnimation();
              Console.WriteLine("blinken: LCD scroll animation started");
            }
            else
            {
              // Turn everything off when stopping
              ApplyPattern(ignite: false, _ => false);
              Console.WriteLine("blinken: LCD scroll animation stopped");
            }
          }

          ImGui.SameLine();
          ImGui.SetNextItemWidth(120);
          ImGui.SliderFloat("Speed", ref _lcdScrollSpeed, 0.5f, 20f);

          if (_lcdAnimationActive)
          {
            ImGui.Text($"Grid: {_gridCols}x{_gridRows}  Image: {_imageWidth}x{_imageHeight}  Offset: {_lcdScrollOffset:F1}");
          }

          // --- Test: toggle pixel_2_0_a engine ---
          ImGui.Separator();
          if (_testEngineController != null)
          {
            _testEngineActive = _testEngineController.IsActive;
            if (ImGui.Button(_testEngineActive ? "pixel_2_0_a: ON  → turn OFF" : "pixel_2_0_a: OFF → turn ON"))
            {
              _testEngineActive = !_testEngineActive;
              _testEngineController.SetIsActive(null, _testEngineActive);
              Console.WriteLine($"blinken: SetIsActive({_testEngineActive}) on pixel_2_0_a");
            }
            ImGui.Text($"pixel_2_0_a IsActive = {_testEngineController.IsActive}");
          }
          else
          {
            ImGui.TextDisabled("pixel_2_0_a EngineController not found");
          }
        }
      }

      ImGui.Separator();
      if (ImGui.Button("Close"))
        _windowVisible = false;
    }
    ImGui.End();
  }
}

