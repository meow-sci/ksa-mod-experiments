using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.SteelyEyedMissileKittenLib.Missions;
using MeowSci.SteelyEyedMissileKittenLib.Telemetry;

namespace MeowSci.SteelyEyedMissileKittenLib.UI;

/// <summary>Renders the Missions tab: active missions, available definitions, and condition details.</summary>
public static class MissionUI
{
    private static string? _selectedMissionId = null;
    private static string? _activateForVehicleId = null;

    public static void Render(
        MissionManager missionManager,
        IReadOnlyDictionary<string, TelemetrySnapshot> currentSnapshots,
        double simTimeSec)
    {
        RenderActiveMissions(missionManager, simTimeSec);
        ImGui.Spacing();
        RenderAvailableMissions(missionManager, currentSnapshots, simTimeSec);
        ImGui.Spacing();
        RenderConditionDetails(missionManager);
    }

    // ── Active Missions ─────────────────────────────────────────────────────────

    private static void RenderActiveMissions(MissionManager missionManager, double simTimeSec)
    {
        if (!ImGui.CollapsingHeader("Active Missions##active", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var active = missionManager.ActiveMissions;

        if (active.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No active missions.");
            ImGui.Spacing();
            return;
        }

        List<(string missionId, string vehicleId)>? toAbandon = null;

        foreach (var ((missionId, vehicleId), state) in active)
        {
            var definition = missionManager.Definitions.FirstOrDefault(d => d.Id == missionId);
            string missionName = definition?.Name ?? missionId;

            double elapsed = simTimeSec - state.StartedAtSec;

            ImGui.BulletText($"{missionName}  [{vehicleId}]  {state.Status}  T+{elapsed:F0}s");

            ImGui.SameLine(0, 8);
            if (ImGui.Button($" Abandon ##{missionId}_{vehicleId}"))
            {
                toAbandon ??= new List<(string, string)>();
                toAbandon.Add((missionId, vehicleId));
            }
        }

        if (toAbandon != null)
        {
            foreach (var (mid, vid) in toAbandon)
                missionManager.AbandonMission(mid, vid);
        }

        ImGui.Spacing();
    }

    // ── Available Missions ───────────────────────────────────────────────────────

    private static void RenderAvailableMissions(
        MissionManager missionManager,
        IReadOnlyDictionary<string, TelemetrySnapshot> currentSnapshots,
        double simTimeSec)
    {
        if (!ImGui.CollapsingHeader("Available Missions##available", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var definitions = missionManager.Definitions;

        if (definitions.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("No mission definitions loaded.");
            ImGui.Spacing();
            return;
        }

        // Vehicle selection combo for activation
        var vehicleIds = currentSnapshots.Keys.ToArray();
        string vehiclePreview = _activateForVehicleId ?? (vehicleIds.Length > 0 ? "Select vehicle..." : "(no vehicles)");

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Activate for:");
        ImGui.SameLine(0, 6);
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo("##mission_vehicle_select", vehiclePreview))
        {
            foreach (var vid in vehicleIds)
            {
                bool sel = _activateForVehicleId == vid;
                if (ImGui.Selectable(vid + "##vsel", sel))
                    _activateForVehicleId = vid;
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (!ImGui.BeginTable("##mission_defs", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("Name",       ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Category",   ImGuiTableColumnFlags.WidthStretch, 1.5f);
        ImGui.TableSetupColumn("Difficulty", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Description",ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn("##actions",  ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableHeadersRow();

        foreach (var def in definitions)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            bool isSel = _selectedMissionId == def.Id;
            if (ImGui.Selectable($"{def.Name}##mdef_{def.Id}", isSel, ImGuiSelectableFlags.None))
                _selectedMissionId = isSel ? null : def.Id;

            ImGui.TableNextColumn();
            ImGui.TextDisabled(def.Category ?? "");

            ImGui.TableNextColumn();
            ImGui.Text(BuildDifficultyStars(def.Difficulty));

            ImGui.TableNextColumn();
            ImGui.TextDisabled(def.Description);

            ImGui.TableNextColumn();
            bool alreadyActive = IsAlreadyActive(missionManager, def.Id, _activateForVehicleId);
            bool canActivate = !string.IsNullOrEmpty(_activateForVehicleId) && vehicleIds.Length > 0 && !alreadyActive;

            if (!canActivate) ImGui.BeginDisabled();
            if (ImGui.Button($" Activate ##{def.Id}") && canActivate)
            {
                missionManager.ActivateMission(def.Id, _activateForVehicleId!, simTimeSec);
                Console.WriteLine($"[MissionUI] Activated '{def.Id}' for '{_activateForVehicleId}'");
            }
            if (!canActivate) ImGui.EndDisabled();
        }

        ImGui.EndTable();
    }

    // ── Condition Detail Panel ───────────────────────────────────────────────────

    private static void RenderConditionDetails(MissionManager missionManager)
    {
        if (_selectedMissionId == null) return;

        var def = missionManager.Definitions.FirstOrDefault(d => d.Id == _selectedMissionId);
        if (def == null) return;

        if (!ImGui.CollapsingHeader($"Objective: {def.Name}##cond_detail", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        if (def.Objective == null)
        {
            ImGui.TextDisabled("(no objective defined)");
            return;
        }

        ImGui.Indent(8);
        RenderCondition(def.Objective, 0);
        ImGui.Unindent(8);
    }

    private static void RenderCondition(MissionCondition condition, int depth)
    {
        string indent = new string(' ', depth * 3);
        string summary = BuildConditionSummary(condition);

        if (!string.IsNullOrEmpty(condition.Description))
        {
            ImGui.Text($"{indent}[{condition.Type}] {condition.Description}");
        }
        else
        {
            ImGui.Text($"{indent}[{condition.Type}] {summary}");
        }

        if (condition.SubConditions != null)
        {
            foreach (var sub in condition.SubConditions)
                RenderCondition(sub, depth + 1);
        }
    }

    private static string BuildConditionSummary(MissionCondition c) => c.Type switch
    {
        ConditionType.AltitudeAbove    => $"> {FormatDistance(c.Value.GetValueOrDefault())}",
        ConditionType.AltitudeBelow    => $"< {FormatDistance(c.Value.GetValueOrDefault())}",
        ConditionType.SpeedAbove       => $"> {FormatSpeed(c.Value.GetValueOrDefault())} ({c.SpeedFrame})",
        ConditionType.SpeedBelow       => $"< {FormatSpeed(c.Value.GetValueOrDefault())} ({c.SpeedFrame})",
        ConditionType.ApoapsisAbove    => $"Ap > {FormatDistance(c.Value.GetValueOrDefault())}",
        ConditionType.PeriapsisAbove   => $"Pe > {FormatDistance(c.Value.GetValueOrDefault())}",
        ConditionType.PeriapsisBelow   => $"Pe < {FormatDistance(c.Value.GetValueOrDefault())}",
        ConditionType.EccentricityBelow=> $"Ecc < {c.Value.GetValueOrDefault():F3}",
        ConditionType.InclinationBetween => $"Inc {c.MinValue.GetValueOrDefault():F1}\u00b0\u2013{c.MaxValue.GetValueOrDefault():F1}\u00b0",
        ConditionType.EventOccurred    => $"Event: {c.EventType}",
        ConditionType.InSoiOf          => $"SOI: {c.BodyId}",
        ConditionType.OnSurfaceOf      => $"Surface of {c.BodyId}",
        ConditionType.AllOf            => $"All of ({c.SubConditions?.Count ?? 0} conditions)",
        ConditionType.AnyOf            => $"Any of ({c.SubConditions?.Count ?? 0} conditions)",
        ConditionType.Sequence         => $"Sequence ({c.SubConditions?.Count ?? 0} steps)",
        _                              => c.Type.ToString(),
    };

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static bool IsAlreadyActive(MissionManager mgr, string missionId, string? vehicleId)
    {
        if (vehicleId == null) return false;
        return mgr.ActiveMissions.ContainsKey((missionId, vehicleId));
    }

    private static string BuildDifficultyStars(int difficulty)
    {
        int clamped = Math.Clamp(difficulty, 1, 5);
        return new string('*', clamped) + new string('.', 5 - clamped);
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
