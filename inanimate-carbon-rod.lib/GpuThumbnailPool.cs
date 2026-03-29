using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Fixed-capacity LRU pool of reusable GPU thumbnail images.
/// Receives CPU pixel data (byte[]) and uploads to Vulkan images on demand.
/// Used at display time to keep VRAM bounded regardless of total subpart count.
/// </summary>
public sealed class GpuThumbnailPool : IDisposable
{
    private sealed class PoolEntry
    {
        public ThumbnailReference Thumbnail;
        public LinkedListNode<string> LruNode;

        public PoolEntry(ThumbnailReference thumbnail, LinkedListNode<string> lruNode)
        {
            Thumbnail = thumbnail;
            LruNode = lruNode;
        }
    }

    private readonly DeviceEx _device;
    private readonly Renderer _renderer;
    private readonly int _imageSize;
    private readonly int _maxSlots;
    private readonly VkSampler _sampler;

    private readonly Dictionary<string, PoolEntry> _active = new();
    private readonly Queue<ThumbnailReference> _freeImages = new();
    private readonly LinkedList<string> _lruOrder = new(); // head = most recent

    private readonly VkCommandPool _cmdPool;
    private readonly CommandBuffer _cmdBuffer;
    private readonly BufferEx _stagingBuffer;
    private readonly MappedMemory _stagingMapped;

    public int ImageSize => _imageSize;

    public GpuThumbnailPool(DeviceEx device, Renderer renderer, int imageSize, int maxSlots, VkSampler sampler)
    {
        _device = device;
        _renderer = renderer;
        _imageSize = imageSize;
        _maxSlots = maxSlots;
        _sampler = sampler;

        // Command pool for upload command buffers
        VkCommandPoolCreateInfo poolInfo = new VkCommandPoolCreateInfo
        {
            QueueFamilyIndex = renderer.Graphics.Index,
            Flags = VkCommandPoolCreateFlags.TransientBit | VkCommandPoolCreateFlags.ResetCommandBufferBit
        };
        _cmdPool = device.CreateCommandPool(in poolInfo, null);

        // Reusable command buffer (Begin implicitly resets with ResetCommandBufferBit)
        _cmdBuffer = device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
        {
            CommandPool = _cmdPool,
            Level = VkCommandBufferLevel.Primary
        });

        // Host-visible staging buffer for CPU → GPU pixel transfer
        int stagingSize = imageSize * imageSize * 4; // R8G8B8A8UNorm
        _stagingBuffer = device.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "ICR_PoolStaging",
            BufferUsage = VkBufferUsageFlags.TransferSrcBit,
            BufferSize = ByteSize.Of<byte>((ElementCount)stagingSize),
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        });
        _stagingMapped = _stagingBuffer.Map();
    }

    /// <summary>
    /// Returns the pool image for a given key if it exists, updating LRU order.
    /// Returns null if the key is not currently in the pool.
    /// </summary>
    public ThumbnailReference? TryGet(string key)
    {
        if (_active.TryGetValue(key, out var entry))
        {
            _lruOrder.Remove(entry.LruNode);
            _lruOrder.AddFirst(entry.LruNode);
            return entry.Thumbnail;
        }
        return null;
    }

    /// <summary>
    /// Uploads pixel data to a pool image, evicting LRU entries if at capacity.
    /// Returns the ThumbnailReference with an active ImGui descriptor.
    /// </summary>
    public ThumbnailReference Upload(string key, byte[] pixels)
    {
        // Already in pool — just update LRU (data doesn't change for same key)
        if (_active.TryGetValue(key, out var existing))
        {
            _lruOrder.Remove(existing.LruNode);
            _lruOrder.AddFirst(existing.LruNode);
            return existing.Thumbnail;
        }

        // Acquire a ThumbnailReference: free list → new allocation → LRU eviction
        ThumbnailReference thumb;
        if (_freeImages.Count > 0)
        {
            thumb = _freeImages.Dequeue();
        }
        else if (_active.Count < _maxSlots)
        {
            thumb = CreatePoolImage();
        }
        else
        {
            var lruKey = _lruOrder.Last!.Value;
            var evicted = _active[lruKey];
            _active.Remove(lruKey);
            _lruOrder.RemoveLast();
            evicted.Thumbnail.DestroyImGuiThumbnail();
            thumb = evicted.Thumbnail;
        }

        UploadToImage(thumb, pixels);
        thumb.CreateImGuiThumbnail(_sampler);

        var node = _lruOrder.AddFirst(key);
        _active[key] = new PoolEntry(thumb, node);

        return thumb;
    }

    /// <summary>
    /// Evicts all entries and returns images to the free list without disposing them.
    /// Useful when the underlying CPU cache is cleared and all pool data is stale.
    /// </summary>
    public void EvictAll()
    {
        foreach (var entry in _active.Values)
        {
            entry.Thumbnail.DestroyImGuiThumbnail();
            _freeImages.Enqueue(entry.Thumbnail);
        }
        _active.Clear();
        _lruOrder.Clear();
    }

    private ThumbnailReference CreatePoolImage()
    {
        int idx = _active.Count + _freeImages.Count;
        var thumb = new ThumbnailReference();
        thumb.CreateImageView(
            _device,
            new ImageEx.CreateInfo
            {
                Name = $"ICR_Pool_{idx}",
                AllocPreference = MemoryPreference.PreferGpu,
                ImageArrayLayers = 1,
                ImageInitialLayout = VkImageLayout.Undefined,
                ImageType = VkImageType._2D,
                ImageExtent = new VkExtent3D { Width = _imageSize, Height = _imageSize, Depth = 1 },
                ImageUsage = VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.SampledBit,
                ImageFormat = VkFormat.R8G8B8A8UNorm,
                ImageMipLevels = 1,
                ImageSamples = VkSampleCountFlags._1Bit,
                ImageSharingMode = VkSharingMode.Exclusive,
                ImageTiling = VkImageTiling.Optimal
            },
            VkImageViewType._2D,
            new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            });
        return thumb;
    }

    private void UploadToImage(ThumbnailReference thumb, byte[] pixels)
    {
        int byteCount = _imageSize * _imageSize * 4;

        // Copy pixel data to staging buffer
        pixels.AsSpan(0, Math.Min(pixels.Length, byteCount)).CopyTo(_stagingMapped.AsSpan<byte>());

        // Record transfer commands (Begin implicitly resets the command buffer)
        VkCommandBufferBeginInfo beginInfo = new VkCommandBufferBeginInfo
        {
            Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit
        };
        _cmdBuffer.Begin(in beginInfo);

        VkImage image = thumb.ImageView.Image.VkImage;

        // Transition image: Undefined → TransferDst (discard previous contents)
        Span<ImageTransition> toTransfer = stackalloc ImageTransition[1];
        toTransfer[0] = new ImageTransition(
            image,
            ImageBarrierInfo.Presets.Undefined,
            ImageBarrierInfo.Presets.TransferDst);
        _cmdBuffer.TransitionImages2(toTransfer);

        // Copy staging buffer → image
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
            ImageExtent = new VkExtent3D { Width = _imageSize, Height = _imageSize, Depth = 1 }
        };
        _cmdBuffer.CopyBufferToImage(
            _stagingBuffer.VkBuffer,
            image,
            VkImageLayout.TransferDstOptimal,
            new ReadOnlySpan<VkBufferImageCopy>(in copyRegion));

        // Transition image: TransferDst → SampledReadFragment
        Span<ImageTransition> toSampled = stackalloc ImageTransition[1];
        toSampled[0] = new ImageTransition(
            image,
            ImageBarrierInfo.Presets.TransferDst,
            ImageBarrierInfo.Presets.SampledReadFragment);
        _cmdBuffer.TransitionImages2(toSampled);

        _cmdBuffer.End();

        // Submit and wait synchronously
        VkFence fence = _device.CreateFence(new VkFenceCreateInfo(), null);
        CommandBuffer cbRef = _cmdBuffer;
        _renderer.Graphics.Submit(
            default(Span<VkSemaphore>),
            default(Span<VkPipelineStageFlags>),
            new Span<CommandBuffer>(ref cbRef),
            default(Span<VkSemaphore>),
            fence);
        _device.WaitForFence(fence, IntPtr.MaxValue);
        _device.DestroyFence(fence, null);
    }

    public void Dispose()
    {
        _device.WaitIdle();

        foreach (var entry in _active.Values)
        {
            entry.Thumbnail.DestroyImGuiThumbnail();
            entry.Thumbnail.Dispose();
        }
        _active.Clear();
        _lruOrder.Clear();

        while (_freeImages.Count > 0)
            _freeImages.Dequeue().Dispose();

        _stagingMapped.Unmap();
        _stagingBuffer.Dispose();
        _device.DestroyCommandPool(_cmdPool, null);
    }
}
