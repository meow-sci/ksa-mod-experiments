using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.IFeelSeenLib;

public sealed partial class IFeelSeenSubmod : IWorkspaceFeature
{
    public string Name => "I Feel Seen - Always Visible Vehicles";
    public string Tooltip => "Makes vehicles visible from infinite distance.";

    private VehicleTracker _tracker = null!;
    private int _pendingVehicleIndex;
    private readonly ImInputString _vehicleFilter = new(128);

    /// <summary>Exposed so Harmony patches can reference it for vehicle render distance overrides.</summary>
    public VehicleTracker Tracker => _tracker;

    public void Initialize()
    {
        _tracker = new VehicleTracker();
    }

    public void Update(double dt) { }

    public void RenderContent()
    {
        var vehicles = VehicleProvider.GetAllVehicles();

        SubmodUI.BeginContentArea("##ifs_content");

        // Vehicle selector — 2-column proportional table (label 1fr, widget 3fr)
        ImGui.Spacing();
        if (MeowSci.KsaAbstractions.WorkspaceUi.Button(" Add Vehicle ##ifs") && vehicles.Count > 0 && _pendingVehicleIndex >= 0 && _pendingVehicleIndex < vehicles.Count)
            _tracker.AddVehicle(vehicles[_pendingVehicleIndex]);

        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        ReleaseLiveState();
    }
}
