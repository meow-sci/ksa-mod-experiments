using System;
namespace MeowSci.AverageTwrLib;

public class TwrSampleAccumulator
{
    public int SampleCount { get; private set; }
    public double TwrSum { get; private set; }
    public double TwrSumSq { get; private set; }
    public double TwrSumInv { get; private set; }
    public double TwrSumInvSqrt { get; private set; }
    public double AccelSum { get; private set; }
    public double AccelSumSq { get; private set; }
    public double AccelSumInv { get; private set; }
    public double AccelSumInvSqrt { get; private set; }

    public void AddSample(double twr, double accel)
    {
        TwrSum += twr;
        TwrSumSq += twr * twr;
        if (twr > 1e-30) { TwrSumInv += 1.0 / twr; TwrSumInvSqrt += 1.0 / Math.Sqrt(twr); }
        AccelSum += accel;
        AccelSumSq += accel * accel;
        if (accel > 1e-30) { AccelSumInv += 1.0 / accel; AccelSumInvSqrt += 1.0 / Math.Sqrt(accel); }
        SampleCount++;
    }

    public void Reset()
    {
        SampleCount = 0;
        TwrSum = TwrSumSq = TwrSumInv = TwrSumInvSqrt = 0.0;
        AccelSum = AccelSumSq = AccelSumInv = AccelSumInvSqrt = 0.0;
    }
}
