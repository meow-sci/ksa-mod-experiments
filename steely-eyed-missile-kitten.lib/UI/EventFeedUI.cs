using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using MeowSci.SteelyEyedMissileKittenLib.Events;

namespace MeowSci.SteelyEyedMissileKittenLib.UI;

/// <summary>Renders the Event Feed tab showing a scrollable log of flight events with type filtering.</summary>
public static class EventFeedUI
{
    private static FlightEventType? _filterType = null;
    private static bool _autoScroll = true;
    private static int _lastEventCount = 0;

    /// <summary>Renders the event feed. Returns true if the caller should clear the event list.</summary>
    public static bool Render(IReadOnlyList<FlightEvent> events)
    {
        bool clearRequested = false;

        // Toolbar
        if (ImGui.Button(" Clear ##feed"))
            clearRequested = true;

        ImGui.SameLine(0, 8);

        string filterLabel = "Filter: " + (_filterType?.ToString() ?? "All");
        ImGui.SetNextItemWidth(160);
        if (ImGui.BeginCombo("##feed_filter", filterLabel))
        {
            if (ImGui.Selectable("All##feed_filter_all", _filterType == null))
                _filterType = null;

            foreach (FlightEventType evtType in Enum.GetValues<FlightEventType>())
            {
                bool selected = _filterType == evtType;
                if (ImGui.Selectable(evtType.ToString() + "##feed_filter_" + evtType, selected))
                    _filterType = evtType;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine(0, 8);
        ImGui.Checkbox("Auto-scroll##feed", ref _autoScroll);

        ImGui.Spacing();

        bool hasNewEvents = events.Count > _lastEventCount;
        _lastEventCount = events.Count;

        ImGui.BeginChild("##events_feed", new float2(0, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);

        // Render newest first (reverse order)
        for (int i = events.Count - 1; i >= 0; i--)
        {
            var evt = events[i];

            if (_filterType.HasValue && evt.Type != _filterType.Value)
                continue;

            float4 color = GetEventColor(evt.Type);
            ImGui.TextColored(color, $"[T+{evt.TimestampSec:F0}s] [{evt.Type}] {evt.VehicleName}: {evt.Description}");

            if (evt.Details.Count > 0)
            {
                ImGui.Indent(12);
                foreach (var (key, value) in evt.Details)
                    ImGui.TextDisabled($"{key}: {value}");
                ImGui.Unindent(12);
            }
        }

        if (_autoScroll && hasNewEvents)
            ImGui.SetScrollHereY(0.0f); // newest first = scroll to top

        ImGui.EndChild();

        return clearRequested;
    }

    private static float4 GetEventColor(FlightEventType type) => type switch
    {
        FlightEventType.Liftoff or FlightEventType.StableOrbitAchieved
            => new float4(0.4f, 1.0f, 0.4f, 1f),

        FlightEventType.AtmosphereEntered or FlightEventType.AtmosphereExited
        or FlightEventType.SoiChanged or FlightEventType.OrbitEscaped
            => new float4(1f, 0.85f, 0.2f, 1f),

        FlightEventType.Landed or FlightEventType.SplashDown
            => new float4(0.4f, 0.8f, 1.0f, 1f),

        _ => new float4(1f, 1f, 1f, 0.8f),
    };
}
