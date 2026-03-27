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
        int capacity = GForceUI.GetRequiredCapacity(SampleIntervalSec);
        _recorder = new GForceRecorder(capacity, SampleIntervalSec);
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
        GForceUI.RenderContent(_recorder, SampleIntervalSec);
    }

    public void Dispose() { }
}
