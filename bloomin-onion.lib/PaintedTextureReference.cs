using MeowSci.KsaRings;
using System;
using System.Reflection;
using Brutal.Numerics;
using Brutal.TextureApi.Abstractions;
using Brutal.VulkanApi.Abstractions;
using KSA;
using RenderCore;

namespace MeowSci.BloominOnionLib;

/// <summary>
/// A <see cref="TextureReference"/> whose pixels come from memory instead of a content file.
/// It goes through the game's own <c>Bind</c> (SimpleVkTexture upload + bindless handle), so
/// the ring renderer treats it exactly like a stock texture. The only non-public step is
/// seeding the private-set <c>TextureAsset</c> property that <c>Bind</c> reads.
/// </summary>
public sealed class PaintedTextureReference : TextureReference
{
    private static readonly FieldInfo? TextureAssetField = typeof(TextureReference).GetField(
        "<TextureAsset>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    private bool _released;

    /// <summary>False when the game's TextureReference layout changed and painting is unavailable.</summary>
    public static bool IsSupported => TextureAssetField != null;

    private PaintedTextureReference() { }

    /// <summary>
    /// Uploads a tightly packed RGBA8 image and returns a bound reference, or null with an error.
    /// </summary>
    public static PaintedTextureReference? Create(string id, byte[] rgba, int width, int height, out string? error)
    {
        error = null;
        if (TextureAssetField == null)
        {
            error = "TextureReference.TextureAsset backing field not found (game update?)";
            return null;
        }
        if (rgba.Length != width * height * 4)
        {
            error = $"pixel buffer size {rgba.Length} does not match {width}x{height} RGBA8";
            return null;
        }

        try
        {
            // The CPU copy must outlive the GPU texture: the ring renderer samples the control
            // strip through TextureAsset.Texture.Data every frame.
            var texture = GenericTexture.Defaults.RGBA8UNorm(new int2(width, height));
            rgba.AsSpan().CopyTo(texture.Data);

            var reference = new PaintedTextureReference
            {
                Id = id,
                Category = TextureCategory.Default,
                Width = width,
                Height = height,
            };
            reference.SetHash();
            TextureAssetField.SetValue(reference, new TextureAsset(texture, id));

            var renderer = Program.GetRenderer();
            // StagingPool disposal submits and waits, so the upload is complete on return.
            using (var stagingPool = renderer.Allocator.CreateStagingPool(renderer.GraphicsAndCompute, 1))
                reference.Bind(renderer, stagingPool);

            if (reference.BindlessHandle == 0)
            {
                error = $"texture '{id}' did not receive a bindless handle";
                return null;
            }
            return reference;
        }
        catch (Exception ex)
        {
            error = $"painted texture '{id}' upload failed: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Frees the GPU image and bindless slot. Only call after the device is idle and no ring
    /// render data references this texture any more (i.e. right after a renderer rebuild).
    /// </summary>
    public void Release()
    {
        if (_released) return;
        _released = true;
        try
        {
            Dispose(Program.GetRenderer().Device);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"bloomin-onion: painted texture '{Id}' release failed: {ex.Message}");
        }
    }
}
