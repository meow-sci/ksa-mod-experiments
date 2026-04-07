using System;
using System.Collections.Generic;
using System.Reflection;
using Brutal.Numerics;
using KSA;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Cached, reflection-based lookup for all KSAColor.Xkcd named colors.
/// Lazy-initialized on first access.
/// </summary>
public static class XkcdColorHelper
{
    private static (string Name, float4 Color)[]? _colors;

    /// <summary>Returns all XKCD colors sorted alphabetically. Cached after first call.</summary>
    public static (string Name, float4 Color)[] GetAll()
    {
        if (_colors != null) return _colors;

        var props = typeof(KSAColor.Xkcd).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var list = new List<(string, float4)>();
        foreach (var prop in props)
        {
            try
            {
                float4 val = (Color.Preset)prop.GetValue(null)!;
                list.Add((prop.Name, val));
            }
            catch { }
        }
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
        _colors = list.ToArray();
        Console.WriteLine($"ksa-abstractions: Cached {_colors.Length} XKCD colors");
        return _colors;
    }

    /// <summary>Looks up an XKCD color by name (case-insensitive). Returns null if not found.</summary>
    public static float4? FindByName(string name)
    {
        var all = GetAll();
        foreach (var (n, c) in all)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>Returns all color names as a string array (for combo filtering).</summary>
    public static string[] GetNames()
    {
        var all = GetAll();
        var names = new string[all.Length];
        for (int i = 0; i < all.Length; i++)
            names[i] = all[i].Name;
        return names;
    }
}
