using System;
using Brutal.Numerics;
using KSA;

namespace mod;

public struct GForceSample
{
    public double TimeSec;
    public double Magnitude;
    public double Longitudinal; // X-axis (thrust axis)
    public double Lateral;      // Y-axis
    public double Normal;       // Z-axis
}

public class GForceRecorder
{
    private const double StandardGravity = 9.80665;

    private GForceSample[] _buffer;
    private int _head;
    private int _count;

    public double PeakG { get; private set; }
    public double MinG { get; private set; }
    public double AvgG { get; private set; }
    public GForceSample Latest { get; private set; }
    public bool IsRecording { get; set; } = true;

    public int Count => _count;
    public int Capacity => _buffer.Length;

    private double _sumG;

    public GForceRecorder(int capacity)
    {
        _buffer = new GForceSample[capacity];
        _head = 0;
        _count = 0;
        PeakG = 0.0;
        MinG = double.MaxValue;
        _sumG = 0.0;
    }

    /// <summary>
    /// Get sample by index where 0 is the oldest sample and Count-1 is the newest.
    /// </summary>
    public GForceSample this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException();
            int bufferIndex = (_head - _count + index + _buffer.Length) % _buffer.Length;
            return _buffer[bufferIndex];
        }
    }

    public void RecordSample(Vehicle vehicle, double simTimeSec)
    {
        if (!IsRecording) return;

        double3 acc = vehicle.AccelerationBody;
        double mag = acc.Length() / StandardGravity;

        var sample = new GForceSample
        {
            TimeSec = simTimeSec,
            Magnitude = mag,
            Longitudinal = acc.X / StandardGravity,
            Lateral = acc.Y / StandardGravity,
            Normal = acc.Z / StandardGravity,
        };

        // If buffer is full, subtract the sample being overwritten from running sum
        if (_count == _buffer.Length)
        {
            _sumG -= _buffer[_head].Magnitude;
        }
        else
        {
            _count++;
        }

        _buffer[_head] = sample;
        _head = (_head + 1) % _buffer.Length;

        _sumG += mag;
        Latest = sample;

        if (mag > PeakG) PeakG = mag;
        if (mag < MinG) MinG = mag;
        AvgG = _count > 0 ? _sumG / _count : 0.0;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        PeakG = 0.0;
        MinG = double.MaxValue;
        AvgG = 0.0;
        _sumG = 0.0;
        Latest = default;
    }

    public void Resize(int newCapacity)
    {
        if (newCapacity == _buffer.Length) return;

        var newBuffer = new GForceSample[newCapacity];
        int copyCount = Math.Min(_count, newCapacity);

        // Copy newest samples
        for (int i = 0; i < copyCount; i++)
        {
            int srcIndex = _count - copyCount + i;
            newBuffer[i] = this[srcIndex];
        }

        _buffer = newBuffer;
        _head = copyCount % newCapacity;
        _count = copyCount;

        // Recompute stats from remaining samples
        _sumG = 0.0;
        PeakG = 0.0;
        MinG = _count > 0 ? double.MaxValue : 0.0;
        for (int i = 0; i < _count; i++)
        {
            double m = newBuffer[i].Magnitude;
            _sumG += m;
            if (m > PeakG) PeakG = m;
            if (m < MinG) MinG = m;
        }
        AvgG = _count > 0 ? _sumG / _count : 0.0;
    }
}
