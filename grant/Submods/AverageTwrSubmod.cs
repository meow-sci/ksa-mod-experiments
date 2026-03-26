using System;
using Brutal.ImGuiApi;
using MeowSci.AverageTwrLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Grant.Submods;

internal sealed class AverageTwrSubmod : IGrantSubmod
{
    public string Name => "Average TWR";

    private TwrSampleAccumulator _accumulator = null!;
    private double _timeSinceLastSample;
    private bool _isCollecting;
    private const double SampleInterval = 0.01;

    public void Initialize()
    {
        _accumulator = new TwrSampleAccumulator();
    }

    public void Update(double dt)
    {
        if (!_isCollecting) return;

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

    public void RenderContent()
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

        if (ImGui.Button(_isCollecting ? "Pause##atwr" : "Start##atwr"))
            _isCollecting = !_isCollecting;

        ImGui.SameLine();

        if (ImGui.Button("Reset##atwr"))
            _accumulator.Reset();
    }

    public void Dispose() { }
}
