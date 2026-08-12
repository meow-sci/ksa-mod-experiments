// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.
//
// Every member of ThumbnailReadback is game-thread only; it records into, and reads back after,
// PartThumbnailGenerator's own command buffer.

using System;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Diagnostic readback for <see cref="PartThumbnailGenerator" />: copies a freshly rendered
/// thumbnail image into a host-visible staging buffer so the fraction of non-zero texels can be
/// measured. A thumbnail that renders nothing is uniformly <c>(0,0,0,0)</c> (the render pass
/// clear colour), so a zero fraction is a positive diagnosis rather than a guess.
/// </summary>
/// <remarks>
/// The copy is recorded into the SAME command buffer as the render, between the render's closing
/// <c>SampledReadFragment</c> transition and the submit, so a single fence covers both.
/// </remarks>
internal sealed class ThumbnailReadback : IDisposable
{
    private const int BytesPerTexel = 4;

    private BufferEx _buffer;
    private bool _hasBuffer;
    private uint _capacityBytes;

    /// <summary>
    /// Records the image-to-buffer copy for one thumbnail. Transitions the image out of
    /// <c>ShaderReadOnlyOptimal</c> to <c>TransferSrcOptimal</c> and straight back, so the image is
    /// left exactly as <c>ThumbnailRenderer.RecordPartRender</c> left it.
    /// </summary>
    /// <param name="renderer">The game renderer (used to allocate the staging buffer).</param>
    /// <param name="commandBuffer">The recording command buffer the render was recorded into.</param>
    /// <param name="thumbnail">The thumbnail image just rendered.</param>
    /// <param name="size">Edge length of the square thumbnail, in texels.</param>
    public void RecordCopy(Renderer renderer, CommandBuffer commandBuffer, ThumbnailReference thumbnail, int size)
    {
        EnsureBuffer(renderer, size);

        VkImage image = thumbnail.ImageView.Image.VkImage;

        ImageTransition toTransferSrc = new ImageTransition(
            image,
            ImageBarrierInfo.Presets.SampledReadF,
            ImageBarrierInfo.Presets.TransferSrc);
        commandBuffer.TransitionImages2(new ReadOnlySpan<ImageTransition>(in toTransferSrc));

        VkBufferImageCopy region = new VkBufferImageCopy
        {
            BufferOffset = ByteSize.Zero,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new VkOffset3D { X = 0, Y = 0, Z = 0 },
            ImageExtent = new VkExtent3D(size, size, 1),
        };
        commandBuffer.CopyImageToBuffer(
            image,
            VkImageLayout.TransferSrcOptimal,
            _buffer.VkBuffer,
            new ReadOnlySpan<VkBufferImageCopy>(in region));

        ImageTransition backToSampled = new ImageTransition(
            image,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageBarrierInfo.Presets.SampledReadF);
        commandBuffer.TransitionImages2(new ReadOnlySpan<ImageTransition>(in backToSampled));
    }

    /// <summary>
    /// Reads the staging buffer written by the last <see cref="RecordCopy" /> and returns the
    /// fraction of texels with any non-zero channel. Only valid after the submit's fence has been
    /// waited on.
    /// </summary>
    /// <param name="size">Edge length of the square thumbnail, in texels.</param>
    /// <returns>A value in <c>[0,1]</c>; zero means the image is entirely the clear colour.</returns>
    public double NonZeroTexelFraction(int size)
    {
        if (!_hasBuffer || size <= 0)
        {
            return 0.0;
        }

        int texels = size * size;
        MappedMemory mapped = _buffer.Map();
        try
        {
            Span<byte> bytes = mapped.AsSpan();
            int usable = Math.Min(texels, bytes.Length / BytesPerTexel);
            int nonZero = 0;

            for (int i = 0; i < usable; i++)
            {
                int offset = i * BytesPerTexel;
                if (bytes[offset] != 0 || bytes[offset + 1] != 0 || bytes[offset + 2] != 0 || bytes[offset + 3] != 0)
                {
                    nonZero++;
                }
            }

            return usable == 0 ? 0.0 : (double)nonZero / usable;
        }
        finally
        {
            mapped.Unmap();
        }
    }

    /// <summary>Frees the staging buffer. Idempotent.</summary>
    public void Dispose()
    {
        if (!_hasBuffer)
        {
            return;
        }

        _buffer.Dispose();
        _buffer = default;
        _hasBuffer = false;
        _capacityBytes = 0u;
    }

    private void EnsureBuffer(Renderer renderer, int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "thumbnail size must be positive");
        }

        uint required = (uint)size * (uint)size * BytesPerTexel;
        if (_hasBuffer && _capacityBytes >= required)
        {
            return;
        }

        Dispose();

        _buffer = renderer.Allocator.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "parts-now Thumbnail Readback",
            BufferSize = new ByteSize(required),
            BufferUsage = VkBufferUsageFlags.TransferDstBit,
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit,
        });
        _hasBuffer = true;
        _capacityBytes = required;
    }
}
