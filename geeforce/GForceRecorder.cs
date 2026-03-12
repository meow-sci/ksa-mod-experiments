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
    public double Jerk;         // rate of change of g-force (g/s)
}

public class GForceRecorder
{
    private const double StandardGravity = 9.80665;

    private GForceSample[] _buffer;
    private int _head;
    private int _count;
    private double _sampleInterval;

    public double PeakG { get; private set; }
    public double MinG { get; private set; }
    public double AvgG { get; private set; }
    public double MaxJerk { get; private set; }
    public int KillGeesBreaches { get; private set; }
    public int JerkBreaches { get; private set; }
    public int PeakIndex { get; private set; }
    public GForceSample Latest { get; private set; }
    public bool IsRecording { get; set; } = true;

    public int Count => _count;
    public int Capacity => _buffer.Length;

    private double _sumG;
    private bool _wasAboveKillGees;
    private bool _wasAboveJerkThreshold;

    public GForceRecorder(int capacity, double sampleInterval)
    {
        _buffer = new GForceSample[capacity];
        _head = 0;
        _count = 0;
        PeakG = 0.0;
        MinG = double.MaxValue;
        MaxJerk = 0.0;
        KillGeesBreaches = 0;
        _wasAboveKillGees = false;
        JerkBreaches = 0;
        _wasAboveJerkThreshold = false;
        PeakIndex = -1;
        _sumG = 0.0;
        _sampleInterval = sampleInterval;
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

    /// <summary>
    /// Binary search for the first sample index with TimeSec >= targetTime.
    /// Returns Count if all samples are before targetTime.
    /// </summary>
    public int FindIndexAtOrAfter(double targetTime)
    {
        int lo = 0, hi = _count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (this[mid].TimeSec < targetTime)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    public void RecordSample(Vehicle vehicle, double simTimeSec)
    {
        if (!IsRecording) return;

        double3 acc = vehicle.AccelerationBody;
        double mag = acc.Length() / StandardGravity;

        // Compute jerk from previous sample
        double jerk = 0.0;
        if (_count > 0 && _sampleInterval > 0.0)
        {
            jerk = (mag - Latest.Magnitude) / _sampleInterval;
        }

        var sample = new GForceSample
        {
            TimeSec = simTimeSec,
            Magnitude = mag,
            Longitudinal = acc.X / StandardGravity,
            Lateral = acc.Y / StandardGravity,
            Normal = acc.Z / StandardGravity,
            Jerk = jerk,
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

        if (Math.Abs(jerk) > MaxJerk) MaxJerk = Math.Abs(jerk);
        if (mag > PeakG)
        {
            PeakG = mag;
            PeakIndex = _count - 1; // newest sample index
        }
        if (mag < MinG) MinG = mag;
        AvgG = _count > 0 ? _sumG / _count : 0.0;
    }

    public void CheckKillGeesBreaches(double threshold)
    {
        if (_count == 0) return;
        bool isAbove = Latest.Magnitude > threshold;
        if (isAbove && !_wasAboveKillGees)
            KillGeesBreaches++;
        _wasAboveKillGees = isAbove;
    }

    public void CheckJerkBreaches(double threshold)
    {
        if (_count == 0) return;
        bool isAbove = Math.Abs(Latest.Jerk) > threshold;
        if (isAbove && !_wasAboveJerkThreshold)
            JerkBreaches++;
        _wasAboveJerkThreshold = isAbove;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        PeakG = 0.0;
        MinG = double.MaxValue;
        MaxJerk = 0.0;
        KillGeesBreaches = 0;
        _wasAboveKillGees = false;
        JerkBreaches = 0;
        _wasAboveJerkThreshold = false;
        PeakIndex = -1;
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
        MaxJerk = 0.0;
        PeakIndex = -1;
        MinG = _count > 0 ? double.MaxValue : 0.0;
        for (int i = 0; i < _count; i++)
        {
            double m = newBuffer[i].Magnitude;
            double j = newBuffer[i].Jerk;
            _sumG += m;
            if (Math.Abs(j) > MaxJerk) MaxJerk = Math.Abs(j);
            if (m > PeakG)
            {
                PeakG = m;
                PeakIndex = i;
            }
            if (m < MinG) MinG = m;
        }
        KillGeesBreaches = 0;
        _wasAboveKillGees = false;
        JerkBreaches = 0;
        _wasAboveJerkThreshold = false;
        AvgG = _count > 0 ? _sumG / _count : 0.0;
    }
}
