using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.IFeelSeenLib;

public sealed class IFeelSeenSubmod : ISubmod
{
    public string Name => "I Feel Seen";

    private VehicleTracker _tracker = null!;
    private int _pendingVehicleIndex;

    /// <summary>Exposed so Harmony patches can reference it for vehicle render distance overrides.</summary>
    public VehicleTracker Tracker => _tracker;

    public void Initialize()
    {
        _tracker = new VehicleTracker();
    }

    public void Update(double dt) { }

    public void RenderContent()
    {
        ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "Vehicle Render Distance Override");
        ImGui.Separator();

        TrackedVehicle? toRemove = null;
        var tracked = _tracker.Tracked;
        for (int i = 0; i < tracked.Count; i++)
        {
            var entry = tracked[i];
            ImGui.PushID(i + 10000); // offset to avoid ID collision with other submods

            bool seeMe = entry.SeeMe;
            if (ImGui.Checkbox($"{entry.Vehicle.Id}##ifs", ref seeMe))
                entry.SeeMe = seeMe;

            ImGui.SameLine();
            if (ImGui.Button("Remove##ifs"))
                toRemove = entry;

            ImGui.PopID();
        }

        if (toRemove != null)
            _tracker.RemoveVehicle(toRemove.Vehicle);

        ImGui.Separator();

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count > 0)
        {
            var vehicleIds = new string[vehicles.Count];
            for (int i = 0; i < vehicles.Count; i++)
                vehicleIds[i] = vehicles[i].Id;

            _pendingVehicleIndex = Math.Clamp(_pendingVehicleIndex, 0, vehicles.Count - 1);
            ImGui.Combo("Vehicle##ifs", ref _pendingVehicleIndex, vehicleIds, vehicleIds.Length);

            if (ImGui.Button("Add Vehicle##ifs"))
                _tracker.AddVehicle(vehicles[_pendingVehicleIndex]);
        }
        else
        {
            ImGui.Text("No vehicles available.");
        }
    }

    public void Dispose()
    {
        _tracker.Clear();
    }
}
