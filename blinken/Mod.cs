using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.BlinkenLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Blinken;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private PixelGrid? _pixelGrid = null;
  private readonly LcdAnimation _lcdAnimation = new();
  private object? _lastVehicle = null;
  private bool _vehicleDebugDumped = false;
  private EngineController? _testEngineController = null;
  private bool _testEngineActive = false;

  private bool _lcdAnimationActive = false;


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
      if (_lcdAnimationActive && _pixelGrid != null && _pixelGrid.Cols > 0)
        _lcdAnimation.Update(dt);

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

  // private static void DumpComponentList(PartComponentList list, string listPath, string indent)
  // {
  //   var dictField = typeof(PartComponentList).GetField(
  //     "_typeListsByType", BindingFlags.Instance | BindingFlags.NonPublic);
  //   if (dictField == null)
  //   {
  //     Console.WriteLine($"{indent}{listPath}  = <_typeListsByType field not found>");
  //     return;
  //   }

  //   var dict = dictField.GetValue(list) as System.Collections.IDictionary;
  //   if (dict == null || dict.Count == 0)
  //   {
  //     Console.WriteLine($"{indent}{listPath}  = (empty — 0 types)");
  //     return;
  //   }

  //   Console.WriteLine($"{indent}{listPath}  ({dict.Count} type(s)):");
  //   foreach (System.Collections.DictionaryEntry entry in dict)
  //   {
  //     var componentType = (Type)entry.Key;
  //     var typeList = entry.Value;
  //     if (typeList == null) continue;

  //     var tlType = typeList.GetType();

  //     int count = -1;
  //     var countProp = tlType.GetProperty("Count", PubInst);
  //     if (countProp != null)
  //       count = (int)countProp.GetValue(typeList)!;

  //     Console.WriteLine($"{indent}  [{componentType.FullName}]  count={count}");

  //     if (count <= 0) continue;

  //     // Primary: extract from _components (List<T>)
  //     var componentsField = tlType.GetField("_components", AllInst);
  //     if (componentsField != null)
  //     {
  //       var listObj = componentsField.GetValue(typeList);
  //       if (listObj is System.Collections.IList ilist)
  //       {
  //         for (int i = 0; i < ilist.Count && i < count; i++)
  //         {
  //           var elem = ilist[i];
  //           if (elem == null) continue;
  //           string elemPath = $"{listPath}[{componentType.Name}][{i}]";
  //           string elemIndent = indent + "      ";
  //           DumpObject(elem, elemPath, elemIndent);
  //         }
  //         continue;
  //       }
  //     }

  //     // Fallback: any IEnumerable field
  //     bool extracted = false;
  //     foreach (var f in tlType.GetFields(AllInst))
  //     {
  //       var fVal = f.GetValue(typeList);
  //       if (fVal is System.Collections.IEnumerable enumerable && fVal is not string)
  //       {
  //         int i = 0;
  //         foreach (var elem in enumerable)
  //         {
  //           if (elem == null) continue;
  //           if (i >= count) break;
  //           string elemPath = $"{listPath}[{componentType.Name}][{i}]";
  //           string elemIndent = indent + "      ";
  //           DumpObject(elem, elemPath, elemIndent);
  //           i++;
  //         }
  //         if (i > 0) { extracted = true; break; }
  //       }
  //     }

  //     if (!extracted)
  //     {
  //       Console.WriteLine($"{indent}    (could not extract instances; type-list type: {tlType.FullName})");
  //       foreach (var f in tlType.GetFields(AllInst))
  //         Console.WriteLine($"{indent}      {f.Name} ({f.FieldType.Name}) = {f.GetValue(typeList)}");
  //     }
  //   }
  // }

  // ─── Debug: deep-dump a Part's component graph ───
  private void DebugDumpPart(Part part, string path = "part", int depth = 0)
  {
    string indent = new string(' ', depth * 2);

    Console.WriteLine($"{indent}{path}.GetType()          = {part.GetType().FullName}");
    Console.WriteLine($"{indent}{path}.Id                 = {part.Id}");
    Console.WriteLine($"{indent}{path}.DisplayName        = {part.DisplayName}");
    Console.WriteLine($"{indent}{path}.IsSubPart          = {part.IsSubPart}");
    Console.WriteLine($"{indent}{path}.PartParent?.Id     = {part.PartParent?.Id ?? "(null)"}");

    // DumpComponentList(part.Components, $"{path}.Components", indent);
    // DumpComponentList(part.SubtreeComponents, $"{path}.SubtreeComponents", indent);

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

  // ─── Engine helpers (work directly with Part/EngineController) ───

  // Set the Active state on an engine pair (both a and b) via SubtreeModules.
  private static void SetEngineActive(Part partA, Part partB, bool active)
  {
    foreach (var part in new[] { partA, partB })
    {
      var controllers = part.SubtreeModules.Get<EngineController>();
      for (int i = 0; i < controllers.Length; i++)
        controllers[i].SetIsActive(null, active);
    }
  }

  // Set active state using cached controllers (no per-call reflection).
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
      var controllers = part.SubtreeModules.Get<EngineController>();
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
    var engineControllers = vehicle.Parts.Modules.Get<EngineController>();

    foreach (var controller in engineControllers)
    {
      count++;
      controller.MinimumThrottle = minThrottle;
    }

    if (count > 0)
    {
      Console.WriteLine($"blinken: set MinimumThrottle={minThrottle} on {count} engine controllers");
      vehicle.Parts.RecomputeAllDerivedData();
    }
  }

  // Apply a pixel pattern:
  //   1. Set MinimumThrottle so engines can fire at any throttle level
  //   2. Set Active per pixel according to selector
  //   2. Set Active per pixel according to selector
  // Pass ignite=false to just shut everything down.
  private void ApplyPattern(bool ignite, System.Func<(int row, int col), bool> selector)
  {
    var vehicle = VehicleProvider.GetControlledVehicle();
    if (vehicle == null || _pixelGrid == null) return;

    SetupMinThrottle(vehicle, 0.0001f);

    foreach (var (key, (a, b)) in _pixelGrid.Grid)
      SetEngineActive(a, b, selector(key));
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(420, 200), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("blinken", ref _windowVisible))
    {
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "blinken — pixel engine grid");
      ImGui.Separator();

      var vehicle = VehicleProvider.GetControlledVehicle();
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
          _pixelGrid = PixelGrid.ScanFromVehicle(vehicle);

          // Debug dump — one time only
          if (!_vehicleDebugDumped)
            _vehicleDebugDumped = true;

          // Capture test EngineController for pixel_2_0_a — one time only
          if (_testEngineController == null)
          {
            _testEngineController = _pixelGrid.GetFirstController(2, 0);
            if (_testEngineController != null)
            {
              _testEngineActive = _testEngineController.IsActive;
              Console.WriteLine($"blinken: captured EngineController for pixel_2_0_a (IsActive={_testEngineActive})");
            }
          }
        }

        if (_pixelGrid == null || _pixelGrid.Count == 0)
        {
          ImGui.TextDisabled("No pixel_ engine parts found on vehicle");
        }
        else
        {
          ImGui.Text($"Pixel count: {_pixelGrid.Count}");
          ImGui.Separator();

          // --- On patterns ---
          if (ImGui.Button("All On"))
            ApplyPattern(ignite: true, PixelPatterns.AllOn);

          ImGui.SameLine();
          if (ImGui.Button("Every Other"))
            ApplyPattern(ignite: true, PixelPatterns.Checkerboard);

          ImGui.SameLine();
          if (ImGui.Button("Alt Rows"))
            ApplyPattern(ignite: true, PixelPatterns.AlternatingRows);

          ImGui.SameLine();
          if (ImGui.Button("Alt Cols"))
            ApplyPattern(ignite: true, PixelPatterns.AlternatingCols);

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
              var v = VehicleProvider.GetControlledVehicle();
              if (v != null) SetupMinThrottle(v, 0.0001f);
              _lcdAnimation.Init(_pixelGrid);
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
          var speed = _lcdAnimation.ScrollSpeed;
          ImGui.SliderFloat("Speed", ref speed, 0.5f, 20f);
          _lcdAnimation.ScrollSpeed = speed;

          if (_lcdAnimationActive)
          {
            ImGui.Text($"Grid: {_lcdAnimation.GridCols}x{_lcdAnimation.GridRows}  Image: {_lcdAnimation.ImageWidth}x{_lcdAnimation.ImageHeight}  Offset: {_lcdAnimation.ScrollOffset:F1}");
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

