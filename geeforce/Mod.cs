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

  private const double SampleIntervalSec = 0.025; // 25ms → 40 Hz
  private double _accumulator = 0.0;
  private GForceRecorder _recorder = null!;

  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch();
      int capacity = GForceUI.GetRequiredCapacity(SampleIntervalSec);
      _recorder = new GForceRecorder(capacity, SampleIntervalSec);
      _isInitialized = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"geeforce: Error during initialization: {ex.Message}");
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

      // Accumulate time and sample at fixed interval
      _accumulator += dt;
      while (_accumulator >= SampleIntervalSec)
      {
        _accumulator -= SampleIntervalSec;

        var vehicle = Program.ControlledVehicle;
        if (vehicle != null)
        {
          double simTime = Universe.GetElapsedSimTime().Seconds();
          _recorder.RecordSample(vehicle, simTime);
        }
      }

      if (_windowVisible)
        GForceUI.Render(ref _windowVisible, _recorder, SampleIntervalSec);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"geeforce: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"geeforce: Error during unload: {ex.Message}");
    }
  }
}

