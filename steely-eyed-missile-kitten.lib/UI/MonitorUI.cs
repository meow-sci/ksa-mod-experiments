using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.SteelyEyedMissileKittenLib.Monitoring;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.UI;

/// <summary>Renders the Live Telemetry tab showing all monitored vehicles with live data.</summary>
public static class MonitorUI
{
    private static string? _selectedVehicleId;

    public static void Render(MonitoringLoop loop, MonitoringConfig config)
    {
        float intervalF = (float)config.SampleIntervalSec;
        if (ImGui.DragFloat("Sample Interval (s)##monitor_interval", ref intervalF, 0.01f,
            (float)MonitoringConfig.MinIntervalSec, (float)MonitoringConfig.MaxIntervalSec, "%.2f s"))
        {
            config.SampleIntervalSec = intervalF;
        }

        ImGui.Spacing();

        var snapshots = loop.CurrentSnapshots;
        var tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        var tableHeight = ImGui.GetContentRegionAvail().Y - 80;

        ImGui.BeginChild("##monitor_scroll", new float2(0, tableHeight),
            ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        if (ImGui.BeginTable("##monitor_vehicles", 10, tableFlags))
        {
            ImGui.TableSetupColumn("Name",          ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("SOI Parent",    ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Altitude",      ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Orb. Speed",    ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Surf. Speed",   ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Ap",            ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Pe",            ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Mass",          ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("G-Force",       ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Situation",     ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableHeadersRow();

            foreach (var snap in snapshots.Values)
            {
                bool isSelected = snap.VehicleId == _selectedVehicleId;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{snap.VehicleName}##row_{snap.VehicleId}", isSelected,
                    ImGuiSelectableFlags.None))
                {
                    _selectedVehicleId = isSelected ? null : snap.VehicleId;
                }

                ImGui.TableNextColumn();
                ImGui.Text(snap.ParentBodyName);

                ImGui.TableNextColumn();
                ImGui.Text(FormatDistance(snap.BarometricAltitudeM));

                ImGui.TableNextColumn();
                ImGui.Text(FormatSpeed(snap.OrbitalSpeedMps));

                ImGui.TableNextColumn();
                ImGui.Text(FormatSpeed(snap.SurfaceSpeedMps));

                ImGui.TableNextColumn();
                ImGui.Text(FormatDistance(snap.ApoapsisAltitudeM));

                ImGui.TableNextColumn();
                ImGui.Text(FormatDistance(snap.PeriapsisAltitudeM));

                ImGui.TableNextColumn();
                ImGui.Text($"{snap.TotalMassKg / 1000.0:F2} t");

                ImGui.TableNextColumn();
                RenderGForceCell(snap.GForceMagnitude);

                ImGui.TableNextColumn();
                RenderSituationCell(snap.Situation);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();

        if (_selectedVehicleId != null && snapshots.TryGetValue(_selectedVehicleId, out var selected))
        {
            ImGui.Spacing();
            if (ImGui.CollapsingHeader($"Details: {selected.VehicleName}##monitor_detail",
                ImGuiTreeNodeFlags.DefaultOpen))
            {
                RenderVehicleDetail(selected);
            }
        }
    }

    private static void RenderGForceCell(double gForce)
    {
        float4 color = gForce < 3.0
            ? new float4(0.4f, 1.0f, 0.4f, 1f)
            : gForce < 6.0
                ? new float4(1.0f, 0.85f, 0.2f, 1f)
                : new float4(1.0f, 0.3f, 0.3f, 1f);
        ImGui.TextColored(color, $"{gForce:F2} g");
    }

    private static void RenderSituationCell(string situation)
    {
        float4 color = situation switch
        {
            "Landed" or "Floating" or "Sailing" => new float4(0.4f, 1.0f, 0.4f, 1f),
            "Freefall" or "Maneuvering"          => new float4(0.4f, 0.9f, 1.0f, 1f),
            _                                    => new float4(1.0f, 1.0f, 1.0f, 1f),
        };
        ImGui.TextColored(color, situation);
    }

    private static void RenderVehicleDetail(TelemetrySnapshot snap)
    {
        if (!ImGui.BeginTable("##monitor_detail_props", 2,
            ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Value",    ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        DetailRow("Vehicle ID",       snap.VehicleId);
        DetailRow("Parent Body",      snap.ParentBodyName);
        DetailRow("Situation",        snap.Situation);
        DetailRow("Altitude (baro)",  FormatDistance(snap.BarometricAltitudeM));
        DetailRow("Altitude (radar)", FormatDistance(snap.RadarAltitudeM));
        DetailRow("Orbital Speed",    FormatSpeed(snap.OrbitalSpeedMps));
        DetailRow("Surface Speed",    FormatSpeed(snap.SurfaceSpeedMps));
        DetailRow("Inertial Speed",   FormatSpeed(snap.InertialSpeedMps));
        DetailRow("Apoapsis Alt",     FormatDistance(snap.ApoapsisAltitudeM));
        DetailRow("Periapsis Alt",    FormatDistance(snap.PeriapsisAltitudeM));
        DetailRow("Eccentricity",     $"{snap.Eccentricity:F4}");
        DetailRow("Inclination",      $"{snap.Inclination:F2}\u00b0");
        DetailRow("Orbital Period",   $"{snap.OrbitalPeriodSec:F0} s");
        DetailRow("Total Mass",       $"{snap.TotalMassKg / 1000.0:F3} t");
        DetailRow("Propellant",       $"{snap.PropellantMassKg / 1000.0:F3} t");
        DetailRow("G-Force",          $"{snap.GForceMagnitude:F3} g");
        DetailRow("In Atmosphere",    snap.IsInAtmosphere ? "Yes" : "No");
        DetailRow("Atm Pressure",     $"{snap.AtmosphericPressurePa:F1} Pa");

        ImGui.EndTable();
    }

    private static void DetailRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TableNextColumn();
        ImGui.Text(value);
    }

    private static string FormatDistance(double meters)
    {
        if (meters >= 1e9) return $"{meters / 1e9:F2} Gm";
        if (meters >= 1e6) return $"{meters / 1e6:F2} Mm";
        if (meters >= 1e3) return $"{meters / 1e3:F1} km";
        return $"{meters:F0} m";
    }

    private static string FormatSpeed(double mps)
    {
        if (mps >= 1000) return $"{mps / 1000:F2} km/s";
        return $"{mps:F1} m/s";
    }
}
