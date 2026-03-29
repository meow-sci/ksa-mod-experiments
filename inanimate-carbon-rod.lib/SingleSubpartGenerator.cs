using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Brutal;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Lighting;
using KSA.Rendering.Thumbnails;

namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// Generates thumbnail views for a single subpart by ID, storing the result
/// locally rather than in the shared <see cref="SubpartThumbnailCache"/>.
/// Follows the same frame-based generation pattern as <see cref="SubpartThumbnailGenerator"/>.
/// </summary>
public sealed class SingleSubpartGenerator : IDisposable
{
    public GenerationState State { get; private set; } = GenerationState.Idle;
    public string? LastError { get; private set; }
    public int ViewCount { get; set; } = 32;
    public int ThumbnailImageSize { get; set; } = 512;

    /// <summary>Generated CPU-backed thumbnail data. Only valid when State == Done.</summary>
    public CpuThumbnailData? Result { get; private set; }

    private PartTemplate? _subpart;
    private ThumbnailPart? _root;
    private ThumbnailRenderer? _thumbRenderer;
    private int _frameIndex;
    private ushort _savedThumbnailSize;
    private BufferEx? _stagingBuffer;

    public void Generate(string subpartId)
    {
        if (State == GenerationState.Generating) return;
        DestroyResult();

        if (Universe.CurrentSystem == null)
        {
            LastError = "No celestial system loaded. Load a save first.";
            State = GenerationState.Failed;
            return;
        }

        try
        {
            _subpart = FindSubpart(subpartId);
            if (_subpart == null)
            {
                LastError = $"Subpart '{subpartId}' not found.";
                State = GenerationState.Failed;
                return;
            }
            BeginGeneration();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = GenerationState.Failed;
            Console.WriteLine($"inanimate-carbon-rod: SingleSubpart BeginGeneration failed - {ex}");
            CleanupGenerationResources();
        }
    }

    public void Update()
    {
        if (State != GenerationState.Generating) return;

        try
        {
            StepGeneration();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = GenerationState.Failed;
            Console.WriteLine($"inanimate-carbon-rod: SingleSubpart StepGeneration failed - {ex}");
            CleanupGenerationResources();
        }
    }

    /// <summary>Destroys any generated result and resets to Idle.</summary>
    public void DestroyResult()
    {
        Result = null;
        State = GenerationState.Idle;
        LastError = null;
    }

    /// <summary>
    /// Detaches the generated result. The caller takes ownership.
    /// Resets state to Idle.
    /// </summary>
    public CpuThumbnailData? DetachResult()
    {
        var entry = Result;
        Result = null;
        State = GenerationState.Idle;
        LastError = null;
        return entry;
    }

    public void Dispose()
    {
        DestroyResult();
        CleanupGenerationResources();
    }

    private void BeginGeneration()
    {
        _savedThumbnailSize = GameSettings.Current.Graphics.PartThumbnailSize;
        GameSettings.Current.Graphics.PartThumbnailSize = (ushort)ThumbnailImageSize;

        Renderer renderer = Program.GetRenderer();
        _thumbRenderer = new ThumbnailRenderer(renderer);

        // Create host-visible staging buffer for GPU → CPU readback
        int stagingSize = ThumbnailImageSize * ThumbnailImageSize * 4;
        _stagingBuffer = renderer.Device.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "ICR_HiResReadbackStaging",
            BufferUsage = VkBufferUsageFlags.TransferDstBit,
            BufferSize = ByteSize.Of<byte>((ElementCount)stagingSize),
            AllocRequiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit
        });

        Viewport viewport = Program.RenderedViewport;
        Camera camera = viewport.GetCamera();
        _root = new ThumbnailPart(camera);
        _frameIndex = 0;

        State = GenerationState.Generating;
        LastError = null;
    }

    private void StepGeneration()
    {
        if (_subpart == null || _root == null || _thumbRenderer == null) return;

        Renderer renderer = Program.GetRenderer();
        Viewport viewport = Program.RenderedViewport;
        Camera camera = viewport.GetCamera();

        int2 savedFramebufferSize = camera.FramebufferSize;
        int2 savedViewportSize = viewport.Size;
        IFollowable? savedFollowing = camera.Following;

        camera.Unfollow();
        int2 thumbSize = new int2(ThumbnailRenderer.SIZE);
        camera.Resize(thumbSize);
        viewport.Size = thumbSize;
        camera.LocalPosition = double3.Zero;
        camera.LocalRotation = doubleQuat.Identity;
        camera.LocalScale = double3.One;
        camera.OnFrame(1.0 / 60.0);

        PartModelRenderer.ColorData.BeginThumbnailPass(_thumbRenderer.RenderPass, _thumbRenderer.SampleCount);

        try
        {
            Result = RenderSubpartViews(_subpart, _root, _thumbRenderer,
                renderer, viewport, camera, ref _frameIndex, ViewCount,
                _stagingBuffer!.Value);
        }
        finally
        {
            PartModelRenderer.ColorData.EndThumbnailPass();
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }

        CleanupGenerationResources();
        State = GenerationState.Done;
        Console.WriteLine($"inanimate-carbon-rod: Generated {ViewCount} hi-res views ({ThumbnailImageSize}px) for {_subpart.Id}");
    }

    private void CleanupGenerationResources()
    {
        _root?.Dispose();
        _thumbRenderer?.Dispose();
        _root = null;
        _thumbRenderer = null;

        _stagingBuffer?.Dispose();
        _stagingBuffer = null;

        GameSettings.Current.Graphics.PartThumbnailSize = _savedThumbnailSize;
    }

    private static CpuThumbnailData RenderSubpartViews(
        PartTemplate subpart,
        ThumbnailPart root,
        ThumbnailRenderer thumbRenderer,
        Renderer renderer,
        Viewport viewport,
        Camera camera,
        ref int frameIndex,
        int viewCount,
        BufferEx stagingBuffer)
    {
        var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
        var child = new ThumbnailPart(root, syntheticInstance);

        if (child.Model == null && child.ModelDynamic == null)
            return new CpuThumbnailData(Array.Empty<byte[]>(), ThumbnailRenderer.SIZE);

        root.AddChild(child);

        float radius = root.ComputeBoundingSphereRadius();
        float dist = radius / (float)Math.Sin(camera.GetFieldOfView() * 0.5f);
        root.LocalPosition = Double3Ex.Forward * (camera.NearPlane + dist);
        root.LocalScale = Double3Ex.One;

        int imageSize = ThumbnailRenderer.SIZE;
        var cpuViews = new byte[viewCount][];
        for (int v = 0; v < viewCount; v++)
        {
            double roll = v * 2.0 * Math.PI / viewCount;
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, roll);
            cpuViews[v] = RenderViewToImage($"HiRes_V{v}_{subpart.Id}", subpart,
                root, thumbRenderer, renderer, viewport, camera, ref frameIndex,
                stagingBuffer);
        }

        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;
        Program.LightSystem.ClearLights();

        return new CpuThumbnailData(cpuViews, imageSize);
    }

    private static byte[] RenderViewToImage(
        string imageName,
        PartTemplate subpart,
        ThumbnailPart root,
        ThumbnailRenderer thumbRenderer,
        Renderer renderer,
        Viewport viewport,
        Camera camera,
        ref int frameIndex,
        BufferEx stagingBuffer)
    {
        int size = ThumbnailRenderer.SIZE;
        int mipLevels = 1;

        // Allocate temporary GPU image for rendering + readback
        var thumb = new ThumbnailReference();
        thumb.CreateImageView(
            renderer.Device,
            new ImageEx.CreateInfo
            {
                Name = imageName,
                AllocPreference = MemoryPreference.PreferGpu,
                ImageArrayLayers = 1,
                ImageInitialLayout = VkImageLayout.Undefined,
                ImageType = VkImageType._2D,
                ImageExtent = new VkExtent3D
                {
                    Width = size,
                    Height = size,
                    Depth = 1
                },
                ImageUsage = VkImageUsageFlags.TransferDstBit
                           | VkImageUsageFlags.TransferSrcBit
                           | VkImageUsageFlags.SampledBit,
                ImageFormat = VkFormat.R8G8B8A8UNorm,
                ImageMipLevels = mipLevels,
                ImageSamples = VkSampleCountFlags._1Bit,
                ImageSharingMode = VkSharingMode.Exclusive,
                ImageTiling = VkImageTiling.Optimal
            },
            VkImageViewType._2D,
            new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = mipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            });

        var savedThumb = subpart.Thumbnail;
        subpart.Thumbnail = thumb;

        camera.OnFrame(1.0 / 60.0);
        Program.Instance.UpdateShaderData(1.0 / 60.0, viewport);
        root.UpdateRenderData(viewport, frameIndex);
        Program.Instance.UpdateRenderingResources(frameIndex);

        thumbRenderer.RenderThumbnail(
            new PrePassThumbnailCommand(
                viewport, frameIndex,
                Program.GetCSMSystem(),
                Program.LightSystem,
                Program.PlanetAtmosphereRenderer),
            new PassThumbnailCommand(viewport, frameIndex),
            new ReadbackPostPassCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer,
                stagingBuffer.VkBuffer, size),
            subpart.Id,
            out VkFence fence);

        renderer.Device.WaitForFence(fence, IntPtr.MaxValue);
        DeviceEx device = renderer.Device;
        VkFence fenceRef = fence;
        device.ResetFences(new ReadOnlySpan<VkFence>(in fenceRef));
        renderer.Device.DestroyFence(fence, null);

        PartModelRenderer.ClearFrameData(frameIndex);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;

        frameIndex = (frameIndex + 1) % 2;

        subpart.Thumbnail = savedThumb;

        // Read pixel data from staging buffer
        int byteCount = size * size * 4;
        byte[] pixels = new byte[byteCount];
        using (MappedMemory mapped = stagingBuffer.Map())
        {
            mapped.AsSpan<byte>().Slice(0, byteCount).CopyTo(pixels);
        }

        // Dispose temporary GPU image
        thumb.Dispose();

        return pixels;
    }

    private static PartTemplate? FindSubpart(string id)
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) return null;

        object? collection = field.GetValue(null);
        if (collection == null) return null;

        MethodInfo? getList = collection.GetType().GetMethod("GetList");
        if (getList == null) return null;

        var allParts = (List<PartTemplate>)getList.Invoke(collection, null)!;
        return allParts.FirstOrDefault(p => p.Id == id);
    }
}
