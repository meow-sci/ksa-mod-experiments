using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.AverageTwrLib;

public sealed class AverageTwrSubmod : ISubmod
{
    public string Name => "Average TWR";
    public string Tooltip => "Records and analyzes thrust-to-weight ratio and acceleration statistics.";

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

        SubmodUI.BeginContentArea("##atwr_content");

        // Status table
        var statusFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##atwr_status", 2, statusFlags))
        {
            ImGui.TableSetupColumn("##atwr_status_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("##atwr_status_val", ImGuiTableColumnFlags.WidthStretch, 3f);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Status");
            ImGui.TableNextColumn();
            if (_isCollecting)
                ImGui.TextColored(new float4(0.4f, 1.0f, 0.4f, 1.0f), "● Recording");
            else
                ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "● Paused");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Samples");
            ImGui.TableNextColumn();
            ImGui.Text($"{n}");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding

        // TWR section
        if (ImGui.CollapsingHeader("TWR##atwr", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (n == 0)
            {
                ImGui.TextDisabled("No samples yet.");
            }
            else
            {
                double twrMean     = TwrStatistics.ComputeMean(_accumulator.TwrSum, n);
                double twrStdDev   = TwrStatistics.ComputeStdDev(_accumulator.TwrSum, _accumulator.TwrSumSq, n);
                double twrHarmonic = TwrStatistics.ComputeHarmonicMean(_accumulator.TwrSumInv, n);
                double twrBrachi   = TwrStatistics.ComputeBrachiMean(_accumulator.TwrSumInvSqrt, n);

                RenderStatTable("##atwr_twr", twrMean, twrStdDev, twrHarmonic, twrBrachi);
            }
        }

        // Max Acceleration section
        if (ImGui.CollapsingHeader("Max Acceleration (m/s²)##atwr", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (n == 0)
            {
                ImGui.TextDisabled("No samples yet.");
            }
            else
            {
                double accelMean     = TwrStatistics.ComputeMean(_accumulator.AccelSum, n);
                double accelStdDev   = TwrStatistics.ComputeStdDev(_accumulator.AccelSum, _accumulator.AccelSumSq, n);
                double accelHarmonic = TwrStatistics.ComputeHarmonicMean(_accumulator.AccelSumInv, n);
                double accelBrachi   = TwrStatistics.ComputeBrachiMean(_accumulator.AccelSumInvSqrt, n);

                RenderStatTable("##atwr_accel", accelMean, accelStdDev, accelHarmonic, accelBrachi);
            }
        }

        // Controls
        ImGui.SeparatorText("Controls");

        if (ImGui.Button(_isCollecting ? " ■ Pause ##atwr" : " ▶ Start ##atwr"))
            _isCollecting = !_isCollecting;

        ImGui.SameLine(0, 8);

        if (n == 0) ImGui.BeginDisabled();
        if (ImGui.Button(" Reset ##atwr"))
            _accumulator.Reset();
        if (n == 0) ImGui.EndDisabled();

        SubmodUI.EndContentArea();
    }

    private static void RenderStatTable(string tableId, double mean, double stdDev, double harmonic, double brachi)
    {
        var statFlags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX
                      | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 4f));
        if (ImGui.BeginTable(tableId, 2, statFlags))
        {
            ImGui.TableSetupColumn($"{tableId}_lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn($"{tableId}_val", ImGuiTableColumnFlags.WidthStretch, 3f);

            double pct = mean > 0 ? stdDev / mean * 100 : 0;
            RenderStatRow("Mean",          $"{mean:F4}");
            RenderStatRow("Std Dev",       $"{stdDev:F4}  ({pct:F1}%)");
            RenderStatRow("Harmonic mean", $"{harmonic:F4}");
            RenderStatRow("Brachi eff",    $"{brachi:F4}");

            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); // CellPadding
    }

    private static void RenderStatRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.TableNextColumn();
        ImGui.Text(value);
    }

    public void Dispose() { }
}
