using System;
using KSA;
using Brutal.Numerics;
using MeowSci.KsaAbstractions;

namespace MeowSci.GeeForceLib;

public sealed class GeeForceSubmod : ISubmod
{
    public string Name => "G-Force Monitor";

    private const double SampleIntervalSec = 0.025; // 25ms → 40 Hz
    private double _accumulator;
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
        _accumulator += dt;
        while (_accumulator >= SampleIntervalSec)
        {
            _accumulator -= SampleIntervalSec;
            var vehicle = VehicleProvider.GetControlledVehicle();
            if (vehicle != null)
            {
                double simTime = SimTimeProvider.GetElapsedTime().Seconds();
                _recorder.RecordSample(vehicle, simTime);
            }
        }
    }

    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##gf_content");
        GForceUI.RenderContent(_recorder, SampleIntervalSec);
        SubmodUI.EndContentArea();
    }

    public void Dispose() { }
}
