using System;
namespace MeowSci.AverageTwrLib;

public static class TwrStatistics
{
    public static double ComputeMean(double sum, int count) =>
        count > 0 ? sum / count : 0.0;

    public static double ComputeStdDev(double sum, double sumSq, int count)
    {
        if (count <= 0) return 0.0;
        var mean = sum / count;
        var variance = sumSq / count - mean * mean;
        return variance > 0.0 ? Math.Sqrt(variance) : 0.0;
    }

    public static double ComputeHarmonicMean(double sumInverse, int count) =>
        sumInverse > 0.0 ? count / sumInverse : 0.0;

    public static double ComputeBrachiMean(double sumInverseSqrt, int count) =>
        sumInverseSqrt > 0.0 ? Math.Pow(count / sumInverseSqrt, 2.0) : 0.0;
}
