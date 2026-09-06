using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MeowSci.KsaAbstractions;

/// <summary>
/// Shared PNG catalog for every Unscience feature that lets users select an image. Imports are
/// always copied into <c>.unscience/pngs</c>; file names are the stable ids stored by consumers.
/// </summary>
public static class PngLibrary
{
    private static readonly List<(string Label, string Path)> QuickLinksCache = new();

    /// <summary>The single managed PNG directory shared by all mods.</summary>
    public static string PngsDir { get; } = Path.Combine(KsaPaths.ModDataDir, "pngs");

    public static void EnsureDir()
    {
        try
        {
            Directory.CreateDirectory(PngsDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience/pngs: failed to create PNG directory: {ex.Message}");
        }
    }

    /// <summary>Returns the managed path for a catalog file name.</summary>
    public static string FullPath(string name) => Path.Combine(PngsDir, name);

    /// <summary>Returns all PNG file names in the shared catalog, sorted case-insensitively.</summary>
    public static string[] Scan()
    {
        try
        {
            if (!Directory.Exists(PngsDir))
                return Array.Empty<string>();
            return Directory.EnumerateFiles(PngsDir)
                .Where(IsPngPath)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !name.StartsWith('.'))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unscience/pngs: failed to scan PNG directory: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Copies a PNG into the shared catalog. Existing files are never overwritten; duplicate
    /// names become <c>name (2).png</c>, <c>name (3).png</c>, and so on.
    /// </summary>
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
            if (!IsPngPath(sourcePath))
            {
                error = "Only .png files are supported.";
                return null;
            }

            EnsureDir();
            var fullSourcePath = Path.GetFullPath(sourcePath);
            var fileName = Path.GetFileName(fullSourcePath);
            var directDestination = FullPath(fileName);
            if (PathsEqual(fullSourcePath, directDestination))
                return fileName;

            var importedName = UniqueName(fileName);
            File.Copy(fullSourcePath, FullPath(importedName));
            Console.WriteLine($"unscience/pngs: imported '{fullSourcePath}' as '{importedName}'");
            return importedName;
        }
        catch (Exception ex)
        {
            error = $"Import failed: {ex.Message}";
            return null;
        }
    }

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

    /// <summary>Quick-navigation targets for the shared file browser.</summary>
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
        Add("PNG Library", PngsDir);
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
                // Drive enumeration is best-effort.
            }
        }
        return QuickLinksCache;
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

    private static bool IsPngPath(string path)
        => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }
}
