// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do not introduce background access to KSA state; parts-now must remain safe standalone.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Reads the mesh node names out of a glTF 2.0 asset without loading any geometry.
/// </summary>
/// <remarks>
/// <para>
/// <c>MeshAtlasFileReference.DoLoad()</c> derives one <c>MeshReference</c> per glTF mesh node, using
/// <c>GltfLoader.GltfJson.Meshes[i].Name</c> as the id and skipping any name starting with
/// <c>'_'</c>. Validation rule V6 has to know those ids before anything is loaded, so this class
/// reproduces exactly that naming rule.
/// </para>
/// <para>
/// It deliberately does NOT use <c>Brutal.GltfApi.GltfLoader</c>: <c>Brutal.Gltf.dll</c> is not
/// referenced by <c>parts-now.lib</c> (nor by anything else in this repository), and pulling the real
/// loader in would decode buffers and images just to read a handful of names. Only the JSON chunk of
/// the container is read here, so the cost is independent of the asset's size.
/// </para>
/// </remarks>
public static class GlbMeshNames
{
    // "glTF" little-endian, the GLB container magic.
    private const uint GlbMagic = 0x46546C67u;

    // "JSON" little-endian, the chunk type of the structured glTF payload.
    private const uint JsonChunkType = 0x4E4F534Au;

    private const int MaxJsonChunkBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Reads the names of every mesh node in a <c>.glb</c> or <c>.gltf</c> file, in declaration order.
    /// Names beginning with <c>'_'</c> are omitted because KSA skips them when building an atlas.
    /// </summary>
    /// <param name="filePath">Absolute path to the glTF/GLB file.</param>
    /// <returns>The usable mesh node names.</returns>
    /// <exception cref="InvalidDataException">The file is not a readable glTF 2.0 container.</exception>
    public static List<string> Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        byte[] json = IsBinaryContainer(filePath) ? ReadJsonChunk(filePath) : File.ReadAllBytes(filePath);

        List<string> names = new List<string>();
        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("meshes", out JsonElement meshes)
            || meshes.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (JsonElement mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("name", out JsonElement name)
                || name.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            string? value = name.GetString();
            if (!string.IsNullOrEmpty(value) && !value.StartsWith('_'))
            {
                names.Add(value);
            }
        }

        return names;
    }

    private static bool IsBinaryContainer(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        Span<byte> magic = stackalloc byte[4];
        return stream.ReadAtLeast(magic, 4, throwOnEndOfStream: false) == 4
            && ReadUInt32(magic) == GlbMagic;
    }

    private static byte[] ReadJsonChunk(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using BinaryReader reader = new BinaryReader(stream);

        // GLB header: magic, version, total length — 12 bytes.
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();

        while (stream.Position + 8 <= stream.Length)
        {
            uint chunkLength = reader.ReadUInt32();
            uint chunkType = reader.ReadUInt32();

            if (chunkLength > MaxJsonChunkBytes || stream.Position + chunkLength > stream.Length)
            {
                throw new InvalidDataException(
                    "GLB chunk length " + chunkLength + " is out of range for '" + filePath + "'.");
            }

            if (chunkType == JsonChunkType)
            {
                return reader.ReadBytes((int)chunkLength);
            }

            stream.Position += chunkLength;
        }

        throw new InvalidDataException("GLB file '" + filePath + "' has no JSON chunk.");
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes) =>
        (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
}
