using System;
using System.Collections;
using System.Collections.Generic;
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

  // Vehicle selection
  private List<Vehicle> _vehicles = new List<Vehicle>();
  private string[] _vehicleComboItems = new string[] { "(none)" };
  private int _vehicleComboIdx = 0;

  // Light part selection
  private List<Part> _lightParts = new List<Part>();
  private string[] _lightPartComboItems = new string[] { "(none)" };
  private int _lightPartComboIdx = 0;

  // Light state
  private float _intensity = 1.0f;
  private float _savedIntensity = 1.0f;
  private bool _lightEnabled = true;
  private int _colorComboIdx = 0;

  private static readonly string[] ColorComboItems = { "(none)", "Marine", "HotPink", "RadioactiveGreen", "BabyPurple" };

  // float3 values from KSAColor.Xkcd decompiled sources
  private static float3 GetPresetColor(int idx) => idx switch
  {
    1 => new float3(0.01568628f, 0.1803922f, 0.37647059f), // Marine
    2 => new float3(1f, 0.00784314f, 0.55294118f),          // HotPink
    3 => new float3(0.172549f, 0.9803922f, 0.1215686f),     // RadioactiveGreen
    4 => new float3(0.7921569f, 0.6078432f, 0.9686275f),    // BabyPurple
    _ => new float3(1f, 1f, 1f)
  };

  // ── Reflection helpers (PartTemplate.PointLights / SpotLights are not in StarMap API surface) ──

  private static readonly BindingFlags All =
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

  private static object? GF(object? o, string name) =>
    o?.GetType().GetField(name, All)?.GetValue(o);

  private static void SF(object? o, string name, object? val) =>
    o?.GetType().GetField(name, All)?.SetValue(o, val);

  // Finds all KSA.LightModule+TemplateData entries in the part template's Components list
  private static List<object> GetLightComponents(PartTemplate t)
  {
    var result = new List<object>();
    var comps = GF(t, "Components") as IList;
    if (comps == null) return result;
    for (int i = 0; i < comps.Count; i++)
    {
      var c = comps[i];
      if (c?.GetType().FullName == "KSA.LightModule+TemplateData")
        result.Add(c);
    }
    return result;
  }

  private static bool HasLights(PartTemplate t) => GetLightComponents(t).Count > 0;

  private static float ReadIntensity(PartTemplate t)
  {
    var lights = GetLightComponents(t);
    if (lights.Count == 0) return 1.0f;
    var intensityRef = GF(lights[0], "Intensity");
    var val = GF(intensityRef, "Value");
    return val is float f ? f : 1.0f;
  }

  private static void WriteIntensity(List<object> lights, float intensity)
  {
    foreach (var light in lights)
    {
      var intensityRef = GF(light, "Intensity");
      SF(intensityRef, "Value", intensity);
    }
  }

  private static void WriteColor(List<object> lights, float3 color)
  {
    foreach (var light in lights)
    {
      var colorRef = GF(light, "Color");
      if (colorRef == null) continue;
      SF(colorRef, "R", color.X);
      SF(colorRef, "G", color.Y);
      SF(colorRef, "B", color.Z);
      // OnDataLoad recomputes Value = new float3(R, G, B)
      try { colorRef.GetType().GetMethod("OnDataLoad", All)?.Invoke(colorRef, new object?[] { null }); }
      catch (Exception ex) { Console.WriteLine($"zippo: SetColor OnDataLoad error: {ex.Message}"); }
    }
  }

  // ── Debug dump ──

  private static void DumpPartsWithComponents(Part part, string indent = "")
  {
    var tmpl = part.Template;
    if (tmpl != null)
    {
      var compField = tmpl.GetType().GetField("Components", All);
      if (compField?.GetValue(tmpl) is System.Collections.IList comps && comps.Count > 0)
      {
        Console.WriteLine($"zippo: Part {part.Id} has Components[{comps.Count}]:");
        for (int i = 0; i < comps.Count; i++)
        {
          var c = comps[i];
          if (c == null) continue;
          Console.WriteLine($"zippo:   [{i}] {c.GetType().FullName}");
          var ctype = c.GetType();
          while (ctype != null && ctype != typeof(object))
          {
            foreach (var f in ctype.GetFields(All | BindingFlags.DeclaredOnly))
            {
              object? fv = null;
              try { fv = f.GetValue(c); } catch { fv = "<err>"; }
              string fvs = fv is System.Collections.ICollection col ? $"[Count={col.Count}]" : fv?.ToString() ?? "null";
              Console.WriteLine($"zippo:     .{f.Name} ({f.FieldType.Name}) = {fvs}");
            }
            ctype = ctype.BaseType;
          }
        }
      }
    }
    var subs = part.SubParts;
    for (int i = 0; i < subs.Length; i++)
      DumpPartsWithComponents(subs[i], indent + "  ");
  }

  // ── StarMap lifecycle ──

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
      Console.WriteLine($"zippo: Error during initialization: {ex.Message}");
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

      if (ImGui.IsKeyPressed(ImGuiKey.F11))
        _windowVisible = !_windowVisible;

      if (_windowVisible)
        RenderWindow();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"zippo: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"zippo: Error during unload: {ex.Message}");
    }
  }

  // ── State helpers ──

  private Vehicle? SelectedVehicle =>
    _vehicleComboIdx > 0 && (_vehicleComboIdx - 1) < _vehicles.Count
      ? _vehicles[_vehicleComboIdx - 1] : null;

  private Part? SelectedLightPart =>
    _lightPartComboIdx > 0 && (_lightPartComboIdx - 1) < _lightParts.Count
      ? _lightParts[_lightPartComboIdx - 1] : null;

  private void RefreshVehicles()
  {
    var list = Universe.CurrentSystem?.Vehicles?.GetList();
    _vehicles.Clear();
    if (list != null) _vehicles.AddRange(list);

    var names = new string[_vehicles.Count + 1];
    names[0] = "(none)";
    for (int i = 0; i < _vehicles.Count; i++)
      names[i + 1] = _vehicles[i].Id;
    _vehicleComboItems = names;

    if (_vehicleComboIdx > _vehicles.Count)
    {
      _vehicleComboIdx = 0;
      ClearLightParts();
    }
  }

  private void ClearLightParts()
  {
    _lightParts.Clear();
    _lightPartComboItems = new string[] { "(none)" };
    _lightPartComboIdx = 0;
  }

  private void RebuildLightParts(Vehicle vehicle)
  {
    _lightParts.Clear();
    var topLevel = vehicle.Parts.Parts;
    for (int i = 0; i < topLevel.Length; i++)
      CollectLightPartsRecursive(topLevel[i]);

    var names = new string[_lightParts.Count + 1];
    names[0] = "(none)";
    for (int i = 0; i < _lightParts.Count; i++)
      names[i + 1] = _lightParts[i].DisplayName ?? _lightParts[i].Id;
    _lightPartComboItems = names;
    _lightPartComboIdx = 0;
  }

  private void CollectLightPartsRecursive(Part part)
  {
    if (part.Template != null && HasLights(part.Template))
      _lightParts.Add(part);
    var subs = part.SubParts;
    for (int i = 0; i < subs.Length; i++)
      CollectLightPartsRecursive(subs[i]);
  }

  private void OnPartSelected(Part part)
  {
    _intensity = Math.Clamp(ReadIntensity(part.Template), 0f, 1f);
    _savedIntensity = _intensity;
    var ls = part.LightSwitch ?? part.FullPart.LightSwitch;
    _lightEnabled = ls == null || ls.LightIsActive;
    _colorComboIdx = 0;
  }

  private static void ApplyIntensity(Part part, float intensity) =>
    WriteIntensity(GetLightComponents(part.Template), intensity);

  private static void ApplyColor(Part part, float3 color) =>
    WriteColor(GetLightComponents(part.Template), color);

  // ── UI ──

  private void RenderWindow()
  {
    RefreshVehicles();

    ImGui.SetNextWindowSize(new float2(500, 380), ImGuiCond.FirstUseEver);
    if (ImGui.Begin("zippo - Light Control", ref _windowVisible))
    {
      ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "zippo  Light Control");
      ImGui.Separator();
      ImGui.Spacing();

      // Vehicle combobox
      int prevVehicleIdx = _vehicleComboIdx;
      if (ImGui.Combo("Vehicle##zippo", ref _vehicleComboIdx, _vehicleComboItems, _vehicleComboItems.Length))
      {
        if (_vehicleComboIdx != prevVehicleIdx)
        {
          ClearLightParts();
          var v = SelectedVehicle;
          if (v != null) RebuildLightParts(v);
        }
      }

      if (_vehicleComboIdx > 0)
      {
        ImGui.Spacing();

        // Light part combobox
        int prevPartIdx = _lightPartComboIdx;
        if (ImGui.Combo("Light Part##zippo", ref _lightPartComboIdx, _lightPartComboItems, _lightPartComboItems.Length))
        {
          if (_lightPartComboIdx != prevPartIdx)
          {
            var p = SelectedLightPart;
            if (p != null) OnPartSelected(p);
            else { _lightEnabled = true; _intensity = 1.0f; }
          }
        }

        // Debug dump button (helps diagnose template structure at runtime)
        ImGui.SameLine();
        if (ImGui.Button("Dbg##zippo"))
        {
          var v = SelectedVehicle;
          if (v != null)
          {
            Console.WriteLine("zippo: === debug dump (parts with Components > 0) ===");
            var parts = v.Parts.Parts;
            for (int i = 0; i < parts.Length; i++)
              DumpPartsWithComponents(parts[i]);
          }
        }
      }

      var selectedPart = SelectedLightPart;
      if (selectedPart != null)
      {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // On/Off toggle
        var ls = selectedPart.LightSwitch ?? selectedPart.FullPart.LightSwitch;
        if (ImGui.Button(_lightEnabled ? "Turn Off##zippo" : "Turn On##zippo"))
        {
          _lightEnabled = !_lightEnabled;
          if (ls != null)
            ls.LightIsActive = _lightEnabled;
          else
            ApplyIntensity(selectedPart, _lightEnabled ? _savedIntensity : 0f);
        }

        ImGui.Spacing();

        // Full-width intensity drag slider 0–1
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.DragFloat("##intensity_zippo", ref _intensity, 0.001f, 0f, 1f))
        {
          _savedIntensity = _intensity;
          ApplyIntensity(selectedPart, _intensity);
        }
        ImGui.Text("Emissive Intensity");

        ImGui.Spacing();

        // Color picker combobox
        if (ImGui.Combo("Color##zippo", ref _colorComboIdx, ColorComboItems, ColorComboItems.Length))
        {
          if (_colorComboIdx > 0)
            ApplyColor(selectedPart, GetPresetColor(_colorComboIdx));
        }
      }

      ImGui.Spacing();
      ImGui.Separator();
      ImGui.Spacing();
      if (ImGui.Button("Close##zippo"))
        _windowVisible = false;
    }
    ImGui.End();
  }
}

