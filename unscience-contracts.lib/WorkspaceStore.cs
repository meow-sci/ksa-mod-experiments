using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MeowSci.Unscience.Contracts;

public sealed record SavedWorkspace(string Path, WorkspaceDocument? Document, string? Error);

/// <summary>Versioned, atomic disk storage shared by whole workspaces and feature presets.</summary>
public sealed class WorkspaceStore
{
    private const long MaxBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 64 };
    public string DirectoryPath { get; }
    public WorkspaceStore(string directory) => DirectoryPath = directory;
    public static string NormalizeName(string name) => name.Trim().Normalize(NormalizationForm.FormC);

    public IReadOnlyList<SavedWorkspace> List()
    {
        if (!Directory.Exists(DirectoryPath)) return Array.Empty<SavedWorkspace>();
        return Directory.EnumerateFiles(DirectoryPath, "*.json").Select(path =>
        {
            try { return new SavedWorkspace(path, Read(path), null); }
            catch (Exception ex) { return new SavedWorkspace(path, null, ex.Message); }
        }).OrderBy(x => x.Document?.Name ?? Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public WorkspaceDocument Read(string path)
    {
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Save exceeds 16 MiB.");
        var document = JsonSerializer.Deserialize<WorkspaceDocument>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Empty save.");
        Validate(document);
        return document;
    }

    public WorkspaceDocument Save(WorkspaceDocument document, string name, bool overwrite)
    {
        name = NormalizeName(name);
        if (name.Length == 0 || name.Length > 128 || name.Any(char.IsControl))
            throw new InvalidDataException("Enter a name of 1–128 characters without control characters.");
        var existing = List().FirstOrDefault(x => x.Document != null &&
            string.Equals(NormalizeName(x.Document.Name), name, StringComparison.OrdinalIgnoreCase));
        if (existing != null && !overwrite) throw new IOException("A save with this name exists. Choose Overwrite.");
        document = JsonSerializer.Deserialize<WorkspaceDocument>(JsonSerializer.Serialize(document, Json), Json)!;
        document.Id = existing?.Document?.Id ?? Guid.NewGuid().ToString("N");
        document.Name = name;
        document.Modified = DateTimeOffset.UtcNow;
        Write(Path.Combine(DirectoryPath, document.Id + ".json"), document);
        return document;
    }

    public static void Write(string path, WorkspaceDocument document)
    {
        Validate(document);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Save exceeds 16 MiB.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(WorkspaceDocument document)
    {
        if (document.Version != 1) throw new InvalidDataException($"Unsupported workspace version {document.Version}.");
        if (!Guid.TryParseExact(document.Id, "N", out _)) throw new InvalidDataException("Invalid save ID.");
        if (document.Name == null || document.Name.Length > 128 || document.Name.Any(char.IsControl) ||
            document.SelectedFeature == null || document.SelectedLiveItem == null || document.FeatureFilter == null || document.LiveFilter == null || document.LoadFilter == null || document.SelectedSave == null)
            throw new InvalidDataException("Invalid workspace metadata.");
        if (document.Features == null || document.Features.Count > 128 || document.Windows == null)
            throw new InvalidDataException("Invalid feature/window collection.");
        foreach (var feature in document.Features.Values)
        {
            if (feature?.Draft == null || feature.SelectedPreset == null || feature.PresetFilter == null || feature.Draft.Version != 1 || feature.Draft.Fields == null ||
                feature.Draft.Targets == null || feature.Draft.Sections == null)
                throw new InvalidDataException("Unsupported or malformed feature state.");
        }
        foreach (var window in document.Windows.Values)
            if (window == null || !float.IsFinite(window.X) || !float.IsFinite(window.Y) ||
                !float.IsFinite(window.Width) || !float.IsFinite(window.Height))
                throw new InvalidDataException("Invalid window coordinates.");
    }
}
