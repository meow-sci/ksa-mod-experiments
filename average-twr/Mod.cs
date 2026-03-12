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

  // shared sample count
  private int _sampleCount = 0;

  // TWR accumulators
  private double _twrSum = 0.0;
  private double _twrSumSq = 0.0;
  private double _twrSumInv = 0.0;
  private double _twrSumInvSqrt = 0.0;

  // maxAccelMps2 accumulators
  private double _accelSum = 0.0;
  private double _accelSumSq = 0.0;
  private double _accelSumInv = 0.0;
  private double _accelSumInvSqrt = 0.0;

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
          var twr = vehicle.NavBallData.ThrustWeightRatio;
          _twrSum += twr;
          _twrSumSq += twr * twr;
          if (twr > 1e-30)
          {
            _twrSumInv += 1.0 / twr;
            _twrSumInvSqrt += 1.0 / Math.Sqrt(twr);
          }

          var fc = vehicle.FlightComputer;
          double gSurface = 6.6743e-11 * vehicle.Parent.Mass / (vehicle.Parent.MeanRadius * vehicle.Parent.MeanRadius);
          double maxThrustN = (double)fc.VehicleConfig.TotalEngineVacuumThrust;
          double totalMass = (double)vehicle.TotalMass;
          double accel = totalMass > 0.0 ? maxThrustN / totalMass : 0.0;
          _accelSum += accel;
          _accelSumSq += accel * accel;
          if (accel > 1e-30)
          {
            _accelSumInv += 1.0 / accel;
            _accelSumInvSqrt += 1.0 / Math.Sqrt(accel);
          }

          _sampleCount++;
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
    ImGui.SetNextWindowSize(new float2(420, 260), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Average TWR / Accel", ref _windowVisible))
    {
      int n = _sampleCount;
      ImGui.Text($"Samples: {n}");
      ImGui.Separator();

      if (n > 0)
      {
        double twrMean   = _twrSum / n;
        double twrVar    = _twrSumSq / n - twrMean * twrMean;
        double twrStdDev = twrVar > 0.0 ? Math.Sqrt(twrVar) : 0.0;
        double twrHarmonic   = _twrSumInv > 0.0 ? n / _twrSumInv : 0.0;
        double twrBrachi     = _twrSumInvSqrt > 0.0 ? Math.Pow(n / _twrSumInvSqrt, 2.0) : 0.0;

        double accelMean   = _accelSum / n;
        double accelVar    = _accelSumSq / n - accelMean * accelMean;
        double accelStdDev = accelVar > 0.0 ? Math.Sqrt(accelVar) : 0.0;
        double accelHarmonic = _accelSumInv > 0.0 ? n / _accelSumInv : 0.0;
        double accelBrachi   = _accelSumInvSqrt > 0.0 ? Math.Pow(n / _accelSumInvSqrt, 2.0) : 0.0;

        ImGui.Text("── TWR ──────────────────────────────");
        ImGui.Text($"  Mean:          {twrMean:F4}");
        ImGui.Text($"  Std Dev:       {twrStdDev:F4}  ({(twrMean > 0 ? twrStdDev / twrMean * 100 : 0):F1}%)");
        ImGui.Text($"  Harmonic mean: {twrHarmonic:F4}");
        ImGui.Text($"  Brachi eff:    {twrBrachi:F4}  (mean(1/√x))⁻²");
        ImGui.Separator();
        ImGui.Text("── maxAccelMps2 (m/s²) ──────────────");
        ImGui.Text($"  Mean:          {accelMean:F4}");
        ImGui.Text($"  Std Dev:       {accelStdDev:F4}  ({(accelMean > 0 ? accelStdDev / accelMean * 100 : 0):F1}%)");
        ImGui.Text($"  Harmonic mean: {accelHarmonic:F4}");
        ImGui.Text($"  Brachi eff:    {accelBrachi:F4}  (mean(1/√a))⁻²");
        ImGui.Separator();
      }
      else
      {
        ImGui.Text("No samples yet.");
        ImGui.Separator();
      }

      if (ImGui.Button(_isCollecting ? "Pause" : "Start"))
        _isCollecting = !_isCollecting;

      ImGui.SameLine();

      if (ImGui.Button("Reset"))
      {
        _twrSum = 0.0;
        _twrSumSq = 0.0;
        _twrSumInv = 0.0;
        _twrSumInvSqrt = 0.0;
        _accelSum = 0.0;
        _accelSumSq = 0.0;
        _accelSumInv = 0.0;
        _accelSumInvSqrt = 0.0;
        _sampleCount = 0;
      }
    }
    ImGui.End();
  }
}

