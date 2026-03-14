using System;
using System.Collections.Generic;
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

  private int _pendingVehicleIndex = 0;

  private readonly List<TrackedVehicle> _tracked = new();

  private class TrackedVehicle
  {
    public Vehicle Vehicle = null!;
    public bool SeeMe = true;
  }

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
      Console.WriteLine($"i-feel-seen: Error during initialization: {ex.Message}");
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
      Console.WriteLine($"i-feel-seen: Error in OnAfterUi: {ex.Message}");
    }
  }

  [StarMapUnload]
  public void Unload()
  {
    try
    {
      foreach (var entry in _tracked)
        Patcher.UntrackVehicle(entry.Vehicle);
      _tracked.Clear();
      Patcher.Unload();
      _isDisposed = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"i-feel-seen: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(400, 350), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("I Feel Seen###i-feel-seen", ref _windowVisible))
    {
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "Vehicle Render Distance Override");
      ImGui.Separator();

      // Tracked vehicles list
      TrackedVehicle? toRemove = null;
      for (int i = 0; i < _tracked.Count; i++)
      {
        var entry = _tracked[i];
        ImGui.PushID(i);

        bool seeMe = entry.SeeMe;
        if (ImGui.Checkbox($"{entry.Vehicle.Id}", ref seeMe))
        {
          entry.SeeMe = seeMe;
          if (seeMe)
            Patcher.TrackVehicle(entry.Vehicle);
          else
            Patcher.UntrackVehicle(entry.Vehicle);
        }

        ImGui.SameLine();
        if (ImGui.Button("Remove"))
          toRemove = entry;

        ImGui.PopID();
      }

      if (toRemove != null)
      {
        Patcher.UntrackVehicle(toRemove.Vehicle);
        _tracked.Remove(toRemove);
      }

      ImGui.Separator();

      // Add vehicle
      var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
      if (vehicles != null && vehicles.Count > 0)
      {
        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++)
          vehicleIds[i] = vehicles[i].Id;

        _pendingVehicleIndex = Math.Clamp(_pendingVehicleIndex, 0, vehicles.Count - 1);
        ImGui.Combo("Vehicle", ref _pendingVehicleIndex, vehicleIds, vehicleIds.Length);

        if (ImGui.Button("Add Vehicle"))
        {
          var vehicle = vehicles[_pendingVehicleIndex];
          bool alreadyTracked = false;
          foreach (var entry in _tracked)
          {
            if (entry.Vehicle == vehicle)
            {
              alreadyTracked = true;
              break;
            }
          }

          if (!alreadyTracked)
          {
            var newEntry = new TrackedVehicle { Vehicle = vehicle, SeeMe = true };
            _tracked.Add(newEntry);
            Patcher.TrackVehicle(vehicle);
          }
        }
      }
      else
      {
        ImGui.Text("No vehicles available.");
      }
    }
    ImGui.End();
  }
}

