using System.Collections.Generic;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// CPU-side cache of thumbnail pixel data, keyed by PartTemplate.Id.
/// All data lives in managed byte arrays (system RAM, not VRAM).
/// </summary>
public static class CpuThumbnailCache
{
    private static readonly Dictionary<string, CpuThumbnailData> _data = new();

    public static IReadOnlyDictionary<string, CpuThumbnailData> All => _data;
    public static bool HasAny => _data.Count > 0;

    public static CpuThumbnailData? Get(string subpartId)
        => _data.GetValueOrDefault(subpartId);

    internal static void Store(string id, CpuThumbnailData data)
        => _data[id] = data;

    internal static void Clear()
        => _data.Clear();
}
