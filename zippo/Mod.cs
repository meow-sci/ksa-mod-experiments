using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.ZippoLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Zippo;

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
  private float4 _currentColor = new float4(1.0f, 1.0f, 1.0f, 1.0f);

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
    var list = VehicleProvider.GetAllVehicles();
    _vehicles.Clear();
    _vehicles.AddRange(list);

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
    _lightParts = LightController.GetLightParts(vehicle);

    var names = new string[_lightParts.Count + 1];
    names[0] = "(none)";
    for (int i = 0; i < _lightParts.Count; i++)
      names[i + 1] = _lightParts[i].DisplayName ?? _lightParts[i].Id;
    _lightPartComboItems = names;
    _lightPartComboIdx = 0;
  }

  private void OnPartSelected(Part part)
  {
    _intensity = Math.Clamp(LightController.ReadIntensity(part.Template), 0f, 1f);
    _savedIntensity = _intensity;
    var ls = part.LightSwitch ?? part.FullPart.LightSwitch;
    _lightEnabled = ls == null || ls.LightIsActive;
    _colorComboIdx = 0;
    var color3 = LightController.ReadColor(part.Template);
    _currentColor = new float4(color3.X, color3.Y, color3.Z, 1.0f);
  }

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
              LightController.DumpPartsWithComponents(parts[i]);
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
            LightController.ApplyIntensity(selectedPart, _lightEnabled ? _savedIntensity : 0f);
        }

        ImGui.Spacing();

        // Full-width intensity drag slider 0–1
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.DragFloat("##intensity_zippo", ref _intensity, 0.001f, 0f, 1f))
        {
          _savedIntensity = _intensity;
          LightController.ApplyIntensity(selectedPart, _intensity);
        }
        ImGui.Text("Emissive Intensity");

        ImGui.Spacing();

        // Color picker combobox
        var colorItems = LightController.ColorPresetNames;
        if (ImGui.Combo("Color##zippo", ref _colorComboIdx, colorItems, colorItems.Length))
        {
          if (_colorComboIdx > 0)
          {
            var presetColor = LightController.GetPresetColor(_colorComboIdx);
            _currentColor = new float4(presetColor.X, presetColor.Y, presetColor.Z, 1.0f);
            LightController.ApplyColor(selectedPart, presetColor);
          }
        }

        // Manual color picker - synced with current light color
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.ColorEdit4("##color_picker_zippo", ref _currentColor, ImGuiColorEditFlags.NoLabel))
        {
          var color3 = new float3(_currentColor.X, _currentColor.Y, _currentColor.Z);
          LightController.ApplyColor(selectedPart, color3);
          _colorComboIdx = 0; // Clear preset selection when manually editing
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

