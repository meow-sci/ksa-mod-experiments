using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Brutal.GltfApi;
using KSA;

namespace MeowSci.PebblesLib;

/// <summary>Read-only registry discovery. Imported geometry is CPU-only and never registered globally.</summary>
public sealed class ClutterAssets : IDisposable
{
    private readonly GlbImportLibrary _external = new();
    public int ImportedGlbCount => _external.SourceCount;
    public IReadOnlyList<GlbMeshOption> ImportGlb(string path) { var options = _external.Import(path); RefreshIds(); return options; }
    public IReadOnlyList<GlbMeshOption> GlbOptions(string id) => _external.OptionsFor(id);
    public List<MaterialRecipe> GlbMaterials(string id) { var result = _external.MaterialsFor(id); RefreshIds(); return result; }
    public IReadOnlyList<string> GlbWarnings(string id) => _external.WarningsFor(id);
    /// <summary>Call before GUI or on unload, after retiring all live and preview borrowers.</summary>
    public void ReleaseGlbImports() { _external.Dispose(); RefreshIds(); }
    private readonly Dictionary<string, MeshReference> _meshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextureReference> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Path, int Index)> _gltfs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MeshReference> _imports = new(StringComparer.Ordinal);
    public string[] MeshIds { get; private set; } = [];
    public string[] TextureIds { get; private set; } = [];

    public void Refresh()
    {
        _meshes.Clear(); _textures.Clear(); _gltfs.Clear();
        foreach (var mesh in Collection<MeshReference>("AllMeshes"))
            if (!string.IsNullOrEmpty(mesh.Id) && mesh.HostPrimitives is { Length: > 0 }) _meshes.TryAdd(mesh.Id, mesh);
        foreach (var file in Collection<FileReference>("AllFiles"))
            if (file is TextureReference texture && !string.IsNullOrEmpty(texture.Id) && texture.BindlessHandle != 0)
                _textures.TryAdd(texture.Id, texture);
        foreach (var gltf in Collection<Gltf2Reference>("AllGltfs"))
        {
            if (gltf.Source == null || !File.Exists(gltf.Source.ModPath)) continue;
            try
            {
                var json = GltfUtility.LoadModel(gltf.Source.ModPath);
                if (json.Meshes == null) continue;
                for (var i = 0; i < json.Meshes.Length; i++)
                {
                    var name = json.Meshes[i].Name;
                    var id = $"{gltf.Id}:{(string.IsNullOrEmpty(name) ? $"mesh_{i}" : name)}";
                    if (_gltfs.ContainsKey(id)) id += $"#{i}";
                    _gltfs[id] = (gltf.Source.ModPath, i);
                }
            }
            catch (Exception ex) { Console.WriteLine($"pebbles: cannot inspect {gltf.Id}: {ex.Message}"); }
        }
        RefreshIds();
    }

    private void RefreshIds()
    {
        MeshIds = _meshes.Keys.Concat(_gltfs.Keys).Concat(_external.MeshIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        TextureIds = _textures.Keys.Concat(_external.TextureIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public MeshReference ResolveMesh(string id)
    {
        if (id.StartsWith(GlbIdentity.Prefix, StringComparison.Ordinal)) return _external.ResolveMesh(id);
        if (_meshes.TryGetValue(id, out var mesh)) return mesh;
        if (_imports.TryGetValue(id, out mesh)) return mesh;
        if (!_gltfs.TryGetValue(id, out var file)) throw new InvalidOperationException($"Mesh '{id}' is unavailable. Refresh assets or select another mesh.");
        using var loader = new GltfLoader(file.Path);
        mesh = new MeshReference { Id = id };
        mesh.Load(loader, file.Index, createDeviceMesh: false);
        mesh.SetHash();
        _imports.Add(id, mesh);
        return mesh;
    }

    public TextureReference ResolveTexture(string id)
    {
        if (id.StartsWith(GlbIdentity.Prefix, StringComparison.Ordinal)) return _external.ResolveTexture(id);
        if (_textures.TryGetValue(id, out var value) && value.BindlessHandle != 0) return value.Get();
        foreach (var fallback in new[] { TextureReference.EmptyWhite, TextureReference.EmptyBlack, TextureReference.EmptyNormal })
            if (fallback != null && fallback.Id == id) return fallback;
        throw new InvalidOperationException($"Texture '{id}' is unavailable or not bound.");
    }

    internal static T[] Collection<T>(string name) where T : ILibraryData, IListable
    {
        var field = typeof(ModLibrary).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ModLibrary).FullName, name);
        return field.GetValue(null) is SerializedCollection<T> collection ? collection.GetList().ToArray() : [];
    }

    public void Dispose()
    {
        _external.Dispose();
        foreach (var mesh in _imports.Values)
        {
            mesh.Dispose();
            foreach (var primitive in mesh.HostPrimitives) primitive.Dispose();
        }
        _imports.Clear(); _meshes.Clear(); _textures.Clear(); _gltfs.Clear();
        MeshIds = []; TextureIds = [];
    }
}
