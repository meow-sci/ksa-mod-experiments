using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeowSci.KsaAbstractions;

namespace MeowSci.FreeFallinLib;

public static class ParachuteTextureLibrary
{
    public static string TexturesDir { get; } =
        Path.Combine(KsaPaths.UserDataDir, ".unscience", "parachutes");

    public static void EnsureDir()
    {
        try { Directory.CreateDirectory(TexturesDir); }
        catch (Exception ex) { Console.WriteLine($"free-fallin: could not create texture directory: {ex.Message}"); }
    }

    public static string FullPath(string name) => Path.Combine(TexturesDir, name);

    public static string[] Scan()
    {
        try
        {
            if (!Directory.Exists(TexturesDir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(TexturesDir, "*.png")
                .Select(Path.GetFileName).OfType<string>()
                .Where(name => !name.StartsWith('.'))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"free-fallin: texture scan failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public static string? Import(string sourcePath, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(sourcePath)) { error = "File not found."; return null; }
            if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                error = "Only .png files are supported.";
                return null;
            }

            EnsureDir();
            string fileName = Path.GetFileName(sourcePath);
            string name = UniqueName(fileName);
            File.Copy(sourcePath, FullPath(name));
            Console.WriteLine($"free-fallin: imported '{sourcePath}' as '{name}'");
            return name;
        }
        catch (Exception ex) { error = $"Import failed: {ex.Message}"; return null; }
    }

    public static string DefaultBrowseDir()
    {
        foreach (string path in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                 })
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
        return Directory.GetCurrentDirectory();
    }

    public static IReadOnlyList<(string Label, string Path)> QuickLinks()
    {
        var links = new List<(string, string)>();
        void Add(string label, string path) { if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) links.Add((label, path)); }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Add("Home", home);
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add("Downloads", Path.Combine(home, "Downloads"));
        Add("Canopies", TexturesDir);
        if (OperatingSystem.IsWindows())
        {
            try { foreach (DriveInfo drive in DriveInfo.GetDrives()) if (drive.IsReady) Add(drive.Name, drive.RootDirectory.FullName); }
            catch { }
        }
        return links;
    }

    private static string UniqueName(string fileName)
    {
        if (!File.Exists(FullPath(fileName))) return fileName;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        for (int i = 2; ; i++)
        {
            string candidate = $"{stem} ({i}).png";
            if (!File.Exists(FullPath(candidate))) return candidate;
        }
    }
}
