using System;
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

  private double _twrSum = 0.0;
  private int _twrSampleCount = 0;
  private double _timeSinceLastSample = 0.0;
  private bool _isCollecting = false;
  private const double SampleInterval = 0.01; // 10ms = 100 times per second

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
      Console.WriteLine($"average-twr: Error during initialization: {ex.Message}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt)
  {
    if (!_isInitialized || _isDisposed) return;

    if (_isCollecting)
    {
      _timeSinceLastSample += dt;
      if (_timeSinceLastSample >= SampleInterval)
      {
        _timeSinceLastSample = 0.0;
        var vehicle = Program.ControlledVehicle;
        if (vehicle != null)
        {
          _twrSum += vehicle.NavBallData.ThrustWeightRatio;
          _twrSampleCount++;
        }
      }
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
      Console.WriteLine($"average-twr: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"average-twr: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(300, 120), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Average TWR", ref _windowVisible))
    {
      double averageTwr = _twrSampleCount > 0 ? _twrSum / _twrSampleCount : 0.0;
      ImGui.Text($"Average TWR: {averageTwr:F4}  (n={_twrSampleCount})");

      if (ImGui.Button(_isCollecting ? "Pause" : "Start"))
      {
        _isCollecting = !_isCollecting;
      }

      ImGui.SameLine();

      if (ImGui.Button("Reset"))
      {
        _twrSum = 0.0;
        _twrSampleCount = 0;
      }
    }
    ImGui.End();
  }
}

