using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.AverageTwrLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.AverageTwr;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private readonly TwrSampleAccumulator _accumulator = new();

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
        var vehicle = VehicleProvider.GetControlledVehicle();
        if (vehicle != null)
        {
          var twr = TwrDataReader.ReadTwr(vehicle);
          var accel = TwrDataReader.ComputeMaxAcceleration(vehicle);
          _accumulator.AddSample(twr, accel);
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
      int n = _accumulator.SampleCount;
      ImGui.Text($"Samples: {n}");
      ImGui.Separator();

      if (n > 0)
      {
        double twrMean     = TwrStatistics.ComputeMean(_accumulator.TwrSum, n);
        double twrStdDev   = TwrStatistics.ComputeStdDev(_accumulator.TwrSum, _accumulator.TwrSumSq, n);
        double twrHarmonic = TwrStatistics.ComputeHarmonicMean(_accumulator.TwrSumInv, n);
        double twrBrachi   = TwrStatistics.ComputeBrachiMean(_accumulator.TwrSumInvSqrt, n);

        double accelMean     = TwrStatistics.ComputeMean(_accumulator.AccelSum, n);
        double accelStdDev   = TwrStatistics.ComputeStdDev(_accumulator.AccelSum, _accumulator.AccelSumSq, n);
        double accelHarmonic = TwrStatistics.ComputeHarmonicMean(_accumulator.AccelSumInv, n);
        double accelBrachi   = TwrStatistics.ComputeBrachiMean(_accumulator.AccelSumInvSqrt, n);

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
        _accumulator.Reset();
    }
    ImGui.End();
  }
}

