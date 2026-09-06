using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace MeowSci.PebblesLib;

/// <summary>Bounded GLB 2 container. Never resolves network or external file references.</summary>
public sealed partial class GlbDocument : IDisposable
{
    public const int MaximumBytes = 128 * 1024 * 1024;
    private readonly JsonDocument _json;
    public JsonElement Root => _json.RootElement;
    public byte[] Binary { get; }
    public string Hash { get; }
    public int MeshCount => Required(Root, "meshes").GetArrayLength();
    private GlbDocument(JsonDocument json, byte[] binary, string hash) { _json = json; Binary = binary; Hash = hash; }
    public static GlbDocument Load(string path)
    {
        if (!Path.GetExtension(path).Equals(".glb", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a .glb file.");
        using var input = File.OpenRead(path);
        if (input.Length is < 20 or > MaximumBytes) throw new InvalidDataException("GLB files must be at most 128 MiB.");
        var bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
        return Parse(bytes);
    }
    public static GlbDocument Parse(byte[] bytes)
    {
        if (bytes.Length is < 20 or > MaximumBytes || U32(bytes, 0) != 0x46546C67 || U32(bytes, 4) != 2 || U32(bytes, 8) != bytes.Length)
            throw new InvalidDataException("Expected a complete GLB 2.0 file.");
        JsonDocument? json = null; byte[]? binary = null;
        try
        {
            int offset = 12;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 8) throw new InvalidDataException("Truncated GLB chunk.");
                uint length = U32(bytes, offset), type = U32(bytes, offset + 4); offset += 8;
                if (length % 4 != 0 || length > bytes.Length - offset) throw new InvalidDataException("Invalid GLB chunk length.");
                if (json == null && type != 0x4E4F534A) throw new InvalidDataException("GLB must start with JSON.");
                if (type == 0x4E4F534A)
                {
                    if (json != null || length > 8 * 1024 * 1024) throw new InvalidDataException("Invalid or oversized GLB JSON.");
                    json = JsonDocument.Parse(bytes.AsMemory(offset, (int)length), new JsonDocumentOptions { MaxDepth = 64 });
                }
                else if (type == 0x004E4942)
                {
                    if (binary != null) throw new InvalidDataException("Duplicate GLB binary chunk.");
                    binary = bytes.AsSpan(offset, (int)length).ToArray();
                }
                offset += (int)length;
            }
            if (json == null || binary == null) throw new InvalidDataException("GLB needs JSON and embedded binary geometry.");
            var root = json.RootElement;
            if (Required(Required(root, "asset"), "version").GetString() != "2.0") throw new InvalidDataException("Only glTF 2.0 is supported.");
            var buffers = Required(root, "buffers");
            if (buffers.GetArrayLength() != 1 || buffers[0].TryGetProperty("uri", out _) || Required(buffers[0], "byteLength").GetInt64() > binary.Length || Required(buffers[0], "byteLength").GetInt64() < binary.Length - 3)
                throw new InvalidDataException("Use a self-contained GLB with one embedded binary buffer.");
            if (Required(root, "meshes").GetArrayLength() is < 1 or > 512) throw new InvalidDataException("GLB needs 1–512 meshes.");
            GlbCompatibility.RequiredExtensions(root);
            return new(json, binary, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch { json?.Dispose(); throw; }
    }
    public string MeshName(int index)
    {
        var mesh = Element(Required(Root, "meshes"), index);
        return mesh.TryGetProperty("name", out var name) ? name.GetString() ?? $"Mesh {index}" : $"Mesh {index}";
    }
    public byte[] ReadBufferView(int index) => BufferView(index).ToArray();
    internal ReadOnlySpan<byte> BufferView(int index)
    {
        var view = Element(Required(Root, "bufferViews"), index);
        if (Int(view, "buffer", 0) != 0 || view.TryGetProperty("extensions", out _)) throw new InvalidDataException("Unsupported GLB buffer view.");
        int start = Int(view, "byteOffset", 0), length = Required(view, "byteLength").GetInt32();
        if (start < 0 || length < 0 || (long)start + length > Required(Required(Root, "buffers")[0], "byteLength").GetInt64()) throw new InvalidDataException("GLB buffer view exceeds binary data.");
        return Binary.AsSpan(start, length);
    }
    internal static JsonElement Required(JsonElement element, string key) => element.TryGetProperty(key, out var value) ? value : throw new InvalidDataException("GLB is missing " + key + ".");
    internal static int Int(JsonElement element, string key, int fallback) => element.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;
    internal static JsonElement Element(JsonElement array, int index) => index >= 0 && index < array.GetArrayLength() ? array[index] : throw new InvalidDataException("GLB index is out of range.");
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    public void Dispose() => _json.Dispose();
}
