using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.EternalFlameLib;

namespace MeowSci.EternalFlame;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private readonly FuelManager _fuelManager = new();
  private ImGuiTextFilter _vehicleFilter = new ImGuiTextFilter();
  private int _selectedVehicleIndex = -1;
  private int _refillIntervalMs = 500;

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
      Console.WriteLine($"eternal-flame: Error during initialization: {ex.Message}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt)
  {
    try
    {
      if (!_isInitialized || _isDisposed) return;
      _fuelManager.Update(dt);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"eternal-flame: Error in OnBeforeUi: {ex.Message}");
    }
  }

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
      Console.WriteLine($"eternal-flame: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"eternal-flame: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(500, 450), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Eternal Flame - Infinite Fuel", ref _windowVisible))
    {
      ImGui.TextColored(new float4(1.0f, 0.6f, 0.0f, 1.0f), "Eternal Flame");
      ImGui.SameLine(0, 10);
      ImGui.TextDisabled($"(refill every {_fuelManager.RefillIntervalMs}ms)");
      ImGui.Separator();

      RenderVehicleSelector();
      ImGui.Spacing();
      RenderRefillIntervalSlider();
      ImGui.Spacing();
      ImGui.SeparatorText("Monitored Vehicles");
      RenderMonitoredTable();
    }
    ImGui.End();
  }

  private void RenderVehicleSelector()
  {
    var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
    if (vehicles == null || vehicles.Count == 0)
    {
      ImGui.TextDisabled("No vehicles available");
      return;
    }

    var vehicleNames = vehicles.Select(v => v.Id).ToArray();

    if (_selectedVehicleIndex >= vehicleNames.Length)
      _selectedVehicleIndex = -1;

    string preview = _selectedVehicleIndex >= 0 ? vehicleNames[_selectedVehicleIndex] : "Select a vehicle...";

    if (ImGui.BeginCombo("Vehicle##selector", preview))
    {
      if (ImGui.IsWindowAppearing())
      {
        ImGui.SetKeyboardFocusHere();
        _vehicleFilter.Clear();
      }
      _vehicleFilter.Draw("##VehicleFilter", -float.MaxValue);

      for (int i = 0; i < vehicleNames.Length; i++)
      {
        if (!_vehicleFilter.PassFilter(vehicleNames[i]))
          continue;

        bool isSelected = _selectedVehicleIndex == i;
        if (ImGui.Selectable(vehicleNames[i], isSelected))
          _selectedVehicleIndex = i;
      }
      ImGui.EndCombo();
    }

    ImGui.SameLine();

    bool canAdd = _selectedVehicleIndex >= 0;
    if (!canAdd) ImGui.BeginDisabled();
    if (ImGui.Button("Add"))
    {
      var v = vehicles[_selectedVehicleIndex];
      _fuelManager.AddVehicle(v.Id, v.Id);
      _selectedVehicleIndex = -1;
    }
    if (!canAdd) ImGui.EndDisabled();
  }

  private void RenderRefillIntervalSlider()
  {
    if (ImGui.DragInt("Refill Interval (ms)", ref _refillIntervalMs, 1, 0, 1000))
    {
      _fuelManager.RefillIntervalMs = _refillIntervalMs;
    }
  }

  private void RenderMonitoredTable()
  {
    var monitored = _fuelManager.MonitoredVehicles;
    if (monitored.Count == 0)
    {
      ImGui.TextDisabled("No vehicles being monitored. Add one above.");
      return;
    }

    if (ImGui.BeginTable("##MonitoredVehicles", 3,
      ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
    {
      ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 50);
      ImGui.TableSetupColumn("Vehicle", ImGuiTableColumnFlags.WidthStretch);
      ImGui.TableSetupColumn("##Remove", ImGuiTableColumnFlags.WidthFixed, 30);
      ImGui.TableHeadersRow();

      string? toRemove = null;
      for (int i = 0; i < monitored.Count; i++)
      {
        var entry = monitored[i];
        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        bool active = entry.Active;
        if (ImGui.Checkbox($"##active_{i}", ref active))
          entry.Active = active;

        ImGui.TableSetColumnIndex(1);
        ImGui.Text(entry.DisplayName);

        ImGui.TableSetColumnIndex(2);
        if (ImGui.SmallButton($"X##{i}"))
          toRemove = entry.VehicleId;
      }

      ImGui.EndTable();

      if (toRemove != null)
        _fuelManager.RemoveVehicle(toRemove);
    }
  }
}

