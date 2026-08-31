using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeowSci.KsaAbstractions;

namespace MeowSci.GraffitiLib;

/// <summary>
/// The on-disk decal library: PNGs under <c>My Games/Kitten Space Agency/.unscience/decals</c>.
/// The file picker copies imports here, and PNGs dropped into the folder by hand are picked up by
/// a rescan. File names (with extension) are the decal ids used everywhere else.
/// </summary>
public static class DecalLibrary
{
    /// <summary>The directory the library lives in.</summary>
    public static string DecalsDir { get; } =
        Path.Combine(KsaPaths.UserDataDir, ".unscience", "decals");

    /// <summary>Creates the library directory if it does not exist yet.</summary>
    public static void EnsureDir()
    {
        try
        {
            Directory.CreateDirectory(DecalsDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: failed to create decals directory: {ex.Message}");
        }
    }

    /// <summary>The full path of a library decal by its file name.</summary>
    public static string FullPath(string name) => Path.Combine(DecalsDir, name);

    /// <summary>All PNG file names currently in the library, sorted case-insensitively.</summary>
    public static string[] Scan()
    {
        try
        {
            if (!Directory.Exists(DecalsDir))
                return Array.Empty<string>();
            return Directory.EnumerateFiles(DecalsDir, "*.png")
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(n => !n.StartsWith('.'))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"graffiti: failed to scan decals directory: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Copies a PNG from anywhere on the filesystem into the library, auto-uniquifying the name
    /// (<c>cat.png</c> → <c>cat (2).png</c>) so an import never silently overwrites.
    /// </summary>
    /// <returns>The library file name on success, null with <paramref name="error"/> set otherwise.</returns>
    public static string? Import(string sourcePath, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(sourcePath))
            {
                error = "File not found.";
                return null;
            }
            if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only .png files are supported.";
                return null;
            }

            EnsureDir();
            var name = UniqueName(Path.GetFileName(sourcePath));
            File.Copy(sourcePath, FullPath(name));
            Console.WriteLine($"graffiti: imported '{sourcePath}' as '{name}'");
            return name;
        }
        catch (Exception ex)
        {
            error = $"Import failed: {ex.Message}";
            return null;
        }
    }

    private static string UniqueName(string fileName)
    {
        if (!File.Exists(FullPath(fileName)))
            return fileName;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem} ({i}).png";
            if (!File.Exists(FullPath(candidate)))
                return candidate;
        }
    }

    /// <summary>A sensible starting directory for the file picker (Pictures, else home).</summary>
    public static string DefaultBrowseDir()
    {
        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                 })
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                return folder;
        }
        return Directory.GetCurrentDirectory();
    }

    private static readonly List<(string Label, string Path)> QuickLinksCache = new();

    /// <summary>Quick-navigation targets for the file picker: home folders plus Windows drives.</summary>
    public static IReadOnlyList<(string Label, string Path)> QuickLinks()
    {
        if (QuickLinksCache.Count > 0)
            return QuickLinksCache;

        void Add(string label, string? path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                QuickLinksCache.Add((label, path));
        }

        Add("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add("Downloads", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        Add("Decals", DecalsDir);
        if (OperatingSystem.IsWindows())
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                    if (drive.IsReady)
                        Add(drive.Name, drive.RootDirectory.FullName);
            }
            catch
            {
                // drive enumeration is best-effort
            }
        }
        return QuickLinksCache;
    }
}
