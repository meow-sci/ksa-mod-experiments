using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSA;

namespace MeowSci.RockyMcRockFaceLib;

/// <summary>
/// Enumerates the game's ModLibrary registries for meshes and textures that can be
/// offered as ring-object replacements. The registries (AllMeshes, AllFiles) are
/// internal static fields on ModLibrary, so a one-time reflection lookup per
/// collection is needed; the SerializedCollection API itself is public.
/// </summary>
public sealed class RingAssetCatalog
{
    private readonly Dictionary<string, MeshReference> _meshById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextureReference> _textureById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TexturePowerReference> _normalById = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ids of all meshes with retained CPU-side geometry (drop-in or convertible).</summary>
    public string[] MeshIds { get; private set; } = Array.Empty<string>();

    /// <summary>Ids of all bound textures (valid bindless handle).</summary>
    public string[] TextureIds { get; private set; } = Array.Empty<string>();

    /// <summary>Ids of bound normal-map textures (TexturePowerReference entries only).</summary>
    public string[] NormalTextureIds { get; private set; } = Array.Empty<string>();

    public void Refresh()
    {
        RefreshMeshes();
        RefreshTextures();
    }

    public bool TryGetMesh(string id, out MeshReference mesh) => _meshById.TryGetValue(id, out mesh!);
    public bool TryGetTexture(string id, out TextureReference texture) => _textureById.TryGetValue(id, out texture!);
    public bool TryGetNormalTexture(string id, out TexturePowerReference texture) => _normalById.TryGetValue(id, out texture!);

    private void RefreshMeshes()
    {
        _meshById.Clear();
        var meshes = Collection<MeshReference>("AllMeshes")?.GetList();
        if (meshes == null)
        {
            Console.WriteLine("rocky-mcrock-face: could not resolve ModLibrary.AllMeshes");
            MeshIds = Array.Empty<string>();
            return;
        }

        // Copy before iterating — GetList() returns the live registry list.
        foreach (var mesh in meshes.ToArray())
        {
            if (string.IsNullOrEmpty(mesh.Id)) continue;
            if (mesh.HostPrimitives is not { Length: > 0 } || mesh.HostPrimitives[0] == null) continue;
            // "_VM" meshes are invisible MeshView pick/collision hulls — not useful visually.
            if (mesh.Id.EndsWith("_VM", StringComparison.OrdinalIgnoreCase)) continue;
            _meshById[mesh.Id] = mesh;
        }
        MeshIds = _meshById.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void RefreshTextures()
    {
        _textureById.Clear();
        _normalById.Clear();
        var files = Collection<FileReference>("AllFiles")?.GetList();
        if (files == null)
        {
            Console.WriteLine("rocky-mcrock-face: could not resolve ModLibrary.AllFiles");
            TextureIds = Array.Empty<string>();
            NormalTextureIds = Array.Empty<string>();
            return;
        }

        foreach (var file in files.ToArray())
        {
            if (file is not TextureReference texture) continue;
            if (string.IsNullOrEmpty(texture.Id)) continue;
            // Unbound textures (handle 0) have no GPU image view and would sample as empty.
            if (texture.BindlessHandle == 0) continue;
            _textureById[texture.Id] = texture;
            if (texture is TexturePowerReference normal)
                _normalById[normal.Id] = normal;
        }
        TextureIds = _textureById.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        NormalTextureIds = _normalById.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static SerializedCollection<T>? Collection<T>(string fieldName) where T : ILibraryData, IListable
    {
        var field = typeof(ModLibrary).GetField(
            fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        return field?.GetValue(null) as SerializedCollection<T>;
    }
}
