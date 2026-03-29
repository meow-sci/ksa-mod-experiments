using System;
using System.Runtime.InteropServices;
using Brutal.VulkanApi;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Post-pass command that blits mip 0 from the ThumbnailRenderer's internal
/// R16G16B16A16SFloat color image into the destination ThumbnailReference image
/// which uses R8G8B8A8UNorm. Performs HDR-to-LDR conversion via hardware blit.
/// Destination images must be created with ImageMipLevels = 1.
/// </summary>
public readonly struct LdrPostPassCommand : IRenderCommandRecord
{
    private readonly ThumbnailRenderer _renderer;
    private readonly PartTemplate _template;
    private readonly AtmosphereRenderer _atmosphereRenderer;

    public LdrPostPassCommand(
        ThumbnailRenderer inRenderer,
        PartTemplate inTemplate,
        AtmosphereRenderer inAtmosphereRenderer)
    {
        _renderer = inRenderer;
        _template = inTemplate;
        _atmosphereRenderer = inAtmosphereRenderer;
    }

    public void ProcessCommands(CommandBuffer inCommandBuffer)
    {
        VkImage src = _renderer.ColorImage;
        VkImage dst = _template.Thumbnail!.ImageView.Image.VkImage;
        int size = ThumbnailRenderer.SIZE;

        // Transition source (all renderer mips) → TransferSrc,
        // destination (single mip) → TransferDst
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

        // Blit mip 0 with format conversion: R16G16B16A16SFloat → R8G8B8A8UNorm
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

        // Transition both to shader-readable
        Span<ImageTransition> finals = stackalloc ImageTransition[2];
        finals[0] = new ImageTransition(
            dst,
            ImageBarrierInfo.Presets.TransferDst,
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
