using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.IFeelSeenLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.IFeelSeen;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private int _pendingVehicleIndex = 0;

  private readonly VehicleTracker _tracker = new();

  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch(_tracker);
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
      _tracker.Clear();
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
      var tracked = _tracker.Tracked;
      for (int i = 0; i < tracked.Count; i++)
      {
        var entry = tracked[i];
        ImGui.PushID(i);

        bool seeMe = entry.SeeMe;
        if (ImGui.Checkbox($"{entry.Vehicle.Id}", ref seeMe))
          entry.SeeMe = seeMe;

        ImGui.SameLine();
        if (ImGui.Button("Remove"))
          toRemove = entry;

        ImGui.PopID();
      }

      if (toRemove != null)
        _tracker.RemoveVehicle(toRemove.Vehicle);

      ImGui.Separator();

      // Add vehicle
      var vehicles = VehicleProvider.GetAllVehicles();
      if (vehicles.Count > 0)
      {
        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++)
          vehicleIds[i] = vehicles[i].Id;

        _pendingVehicleIndex = Math.Clamp(_pendingVehicleIndex, 0, vehicles.Count - 1);
        ImGui.Combo("Vehicle", ref _pendingVehicleIndex, vehicleIds, vehicleIds.Length);

        if (ImGui.Button("Add Vehicle"))
          _tracker.AddVehicle(vehicles[_pendingVehicleIndex]);
      }
      else
      {
        ImGui.Text("No vehicles available.");
      }
    }
    ImGui.End();
  }
}

