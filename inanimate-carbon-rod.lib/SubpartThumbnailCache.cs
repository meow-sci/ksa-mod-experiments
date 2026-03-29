using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Z-axis rotation views for a single subpart (count varies by generation settings).
/// Retained for backward compatibility. New code should use <see cref="CpuThumbnailData"/>.
/// </summary>
public sealed class SubpartThumbnailEntry
{
    public ThumbnailReference[] Views { get; }

    public SubpartThumbnailEntry(ThumbnailReference[] views)
    {
        Views = views;
    }
}

/// <summary>
/// Legacy static cache of GPU-backed subpart thumbnails.
/// With the CPU-backed rendering pipeline, this cache is no longer populated during
/// normal generation. It exists for backward compatibility with any external consumers.
/// New code should use <see cref="CpuThumbnailCache"/> instead.
/// </summary>
public static class SubpartThumbnailCache
{
    private static readonly Dictionary<string, SubpartThumbnailEntry> _thumbnails = new();

    public static IReadOnlyDictionary<string, SubpartThumbnailEntry> All => _thumbnails;

    public static SubpartThumbnailEntry? Get(string subpartId)
        => _thumbnails.GetValueOrDefault(subpartId);

    public static bool HasAny => _thumbnails.Count > 0;

    internal static void Store(string id, SubpartThumbnailEntry entry)
        => _thumbnails[id] = entry;

    internal static void DestroyAll()
    {
        if (_thumbnails.Count == 0) return;

        Program.GetRenderer().Device.WaitIdle();

        List<PartTemplate> allParts = GetAllParts();
        foreach (var kvp in _thumbnails)
        {
            var subpart = allParts.FirstOrDefault(p => p.Id == kvp.Key);
            if (subpart != null && kvp.Value.Views.Length > 0 && subpart.Thumbnail == kvp.Value.Views[0])
                subpart.Thumbnail = null;

            foreach (var view in kvp.Value.Views)
                view?.Dispose();
        }
        _thumbnails.Clear();
    }

    private static List<PartTemplate> GetAllParts()
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) return new List<PartTemplate>();

        object? collection = field.GetValue(null);
        if (collection == null) return new List<PartTemplate>();

        MethodInfo? getList = collection.GetType().GetMethod("GetList");
        if (getList == null) return new List<PartTemplate>();

        return (List<PartTemplate>)getList.Invoke(collection, null)!;
    }
}
