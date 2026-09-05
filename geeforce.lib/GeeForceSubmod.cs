using System;
using KSA;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.GeeForceLib;

public sealed partial class GeeForceSubmod : IWorkspaceFeature
{
    public string Name => "GeeForce";
    public string Tooltip => "Monitors vehicle G's in real-time with configurable sampling and peak tracking.";

    private const double SampleIntervalSec = 0.025; // 25ms → 40 Hz
    private double _accumulator;
    private readonly GForceUI _liveView = new();
    private float _threshold = 9f;
    private bool _axes, _jerk;
    private int _viewWindow;
    private GForceRecorder _recorder = null!;

    public void Initialize()
    {
        // Always allocate up to 1 hour of samples so all recorded data is retained.
        // The view window selection in the UI controls how much is displayed.
        int maxCapacity = (int)(3600.0 / SampleIntervalSec);
        _recorder = new GForceRecorder(maxCapacity, SampleIntervalSec);
    }

    public void Update(double dt)
    {
        if (!_recorder.IsRecording) { _accumulator = 0; return; }
        _accumulator += dt;
        while (_accumulator >= SampleIntervalSec)
        {
            _accumulator -= SampleIntervalSec;
            var vehicle = VehicleProvider.GetControlledVehicle();
            if (vehicle != null)
            {
                double simTime = SimTimeProvider.GetElapsedTime().Seconds();
                _recorder.RecordSample(vehicle, simTime);
                _recorder.CheckKillGeesBreaches(_liveView.Threshold);
                _recorder.CheckJerkBreaches(_liveView.Threshold);
            }
        }
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##gf_content");
        Brutal.ImGuiApi.ImGui.SetNextItemWidth(-1f);
        Brutal.ImGuiApi.ImGui.DragFloat("Threshold (g)", ref _threshold, .1f, 1f, 250f);
        Brutal.ImGuiApi.ImGui.Checkbox("Show axes", ref _axes);
        Brutal.ImGuiApi.ImGui.Checkbox("Show jerk", ref _jerk);
        Brutal.ImGuiApi.ImGui.Combo("History window", ref _viewWindow, new[] { "30s", "1m", "2m", "5m", "10m", "30m", "1h" });
        if (Brutal.ImGuiApi.ImGui.Button(" Apply recorder settings "))
        { _liveView.Threshold = _threshold; _liveView.ShowAxes = _axes; _liveView.ShowJerk = _jerk; _liveView.WindowIndex = _viewWindow; }
        SubmodUI.EndContentArea();
    }

    public void Dispose()
    {
        ReleaseLiveState();
    }
}
