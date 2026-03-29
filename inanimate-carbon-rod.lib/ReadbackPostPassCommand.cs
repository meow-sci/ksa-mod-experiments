using System;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.VulkanApi;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Post-pass command that blits HDR (R16G16B16A16SFloat) → LDR (R8G8B8A8UNorm)
/// and then copies the result into a host-visible staging buffer for CPU readback.
/// Used during thumbnail generation to capture pixel data into system RAM.
/// </summary>
public readonly struct ReadbackPostPassCommand : IRenderCommandRecord
{
    private readonly ThumbnailRenderer _renderer;
    private readonly PartTemplate _template;
    private readonly AtmosphereRenderer _atmosphereRenderer;
    private readonly VkBuffer _stagingBuffer;
    private readonly int _imageSize;

    public ReadbackPostPassCommand(
        ThumbnailRenderer inRenderer,
        PartTemplate inTemplate,
        AtmosphereRenderer inAtmosphereRenderer,
        VkBuffer stagingBuffer,
        int imageSize)
    {
        _renderer = inRenderer;
        _template = inTemplate;
        _atmosphereRenderer = inAtmosphereRenderer;
        _stagingBuffer = stagingBuffer;
        _imageSize = imageSize;
    }

    public void ProcessCommands(CommandBuffer inCommandBuffer)
    {
        VkImage src = _renderer.ColorImage;
        VkImage dst = _template.Thumbnail!.ImageView.Image.VkImage;
        int size = _imageSize;

        // Phase 1: Blit from renderer's HDR color image → destination LDR image
        Span<ImageTransition> preps = stackalloc ImageTransition[2];
        preps[0] = new ImageTransition(
            src,
            ImageBarrierInfo.Presets.SampledReadFragment,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        preps[1] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.Undefined,
            ImageBarrierInfo.Presets.TransferDst);
        inCommandBuffer.TransitionImages2(preps);

        VkOffset3D_2 srcOffsets = default;
        srcOffsets[0] = new VkOffset3D(0, 0, 0);
        srcOffsets[1] = new VkOffset3D(size, size, 1);

        VkOffset3D_2 dstOffsets = default;
        dstOffsets[0] = new VkOffset3D(0, 0, 0);
        dstOffsets[1] = new VkOffset3D(size, size, 1);

        VkImageBlit blit = new VkImageBlit
        {
            SrcSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            DstSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcOffsets = srcOffsets,
            DstOffsets = dstOffsets
        };

        inCommandBuffer.BlitImage(
            src, VkImageLayout.TransferSrcOptimal,
            dst, VkImageLayout.TransferDstOptimal,
            MemoryMarshal.CreateSpan(ref blit, 1),
            VkFilter.Linear);

        // Phase 2: Copy LDR image → staging buffer for CPU readback
        Span<ImageTransition> toSrc = stackalloc ImageTransition[1];
        toSrc[0] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.TransferDst,
            ImageBarrierInfo.Presets.TransferSrc);
        inCommandBuffer.TransitionImages2(toSrc);

        VkBufferImageCopy copyRegion = new VkBufferImageCopy
        {
            BufferOffset = ByteSize.Zero,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new VkImageSubresourceLayers
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new VkOffset3D(0, 0, 0),
            ImageExtent = new VkExtent3D { Width = size, Height = size, Depth = 1 }
        };
        inCommandBuffer.CopyImageToBuffer(
            dst, VkImageLayout.TransferSrcOptimal,
            _stagingBuffer,
            new ReadOnlySpan<VkBufferImageCopy>(in copyRegion));

        // Phase 3: Transition both images back to shader-readable
        Span<ImageTransition> finals = stackalloc ImageTransition[2];
        finals[0] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageBarrierInfo.Presets.SampledReadFragment);
        finals[1] = new ImageTransition(
            src,
            ImageBarrierInfo.Presets.TransferSrc,
            ImageBarrierInfo.Presets.SampledReadFragment,
            ImageTransition.Subresource(VkImageAspectFlags.ColorBit, 0, ThumbnailRenderer.MipLevels));
        inCommandBuffer.TransitionImages2(finals);

        _atmosphereRenderer.TransitionLuts(
            inCommandBuffer,
            ImageBarrierInfo.Presets.SampledReadFragment,
            ImageBarrierInfo.Presets.StorageWriteCompute);
    }
}
