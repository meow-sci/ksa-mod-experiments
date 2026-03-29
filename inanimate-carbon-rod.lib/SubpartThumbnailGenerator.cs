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

public enum GenerationState { Idle, Generating, Done, Failed }

public sealed class SubpartThumbnailGenerator : IDisposable
{
    /// <summary>Number of thumbnails to render per game frame.</summary>
    private const int BatchSize = 1;

    public GenerationState State { get; private set; } = GenerationState.Idle;
    public int ProgressCurrent { get; private set; }
    public int ProgressTotal { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Number of rotation views to render per subpart.</summary>
    public int ViewCount { get; set; } = 8;

    /// <summary>Pixel size for rendered thumbnail images.</summary>
    public int ThumbnailImageSize { get; set; } = 512;

    // Active generation state (non-null only while State == Generating)
    private List<PartTemplate>? _subparts;
    private int _currentIndex;
    private ThumbnailPart? _root;
    private ThumbnailRenderer? _thumbRenderer;
    private int _frameIndex;
    private ushort _savedThumbnailSize;

    // Staging buffer for GPU → CPU readback (created per generation run)
    private BufferEx? _stagingBuffer;

    /// <summary>
    /// Starts the generation process. Actual rendering happens in Update(),
    /// one small batch per game frame, to avoid overwhelming the GPU.
    /// </summary>
    public void GenerateAll()
    {
        if (State == GenerationState.Generating || State == GenerationState.Done)
            return;

        if (Universe.CurrentSystem == null)
        {
            LastError = "No celestial system loaded. Load a save first.";
            State = GenerationState.Failed;
            Console.WriteLine("inanimate-carbon-rod: " + LastError);
            return;
        }

        try
        {
            BeginGeneration();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = GenerationState.Failed;
            Console.WriteLine($"inanimate-carbon-rod: BeginGeneration failed - {ex}");
            CleanupGenerationResources();
        }
    }

    /// <summary>
    /// Must be called every frame. Renders the next batch of thumbnails
    /// if generation is in progress.
    /// </summary>
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
            Console.WriteLine($"inanimate-carbon-rod: StepGeneration failed - {ex}");
            CleanupGenerationResources();
        }
    }

    /// <summary>
    /// Resets state so generation can be triggered again.
    /// </summary>
    public void Reset()
    {
        if (State == GenerationState.Generating) return;
        CpuThumbnailCache.Clear();
        SubpartThumbnailCache.DestroyAll();
        State = GenerationState.Idle;
        ProgressCurrent = 0;
        ProgressTotal = 0;
        LastError = null;
    }

    public void Dispose()
    {
        CleanupGenerationResources();
    }

    private void BeginGeneration()
    {
        List<PartTemplate> allParts = GetAllParts();
        _subparts = allParts
            .Where(p => p.IsSubPart && !p.IsHidden && p.Thumbnail == null)
            .ToList();

        _currentIndex = 0;
        ProgressCurrent = 0;
        ProgressTotal = _subparts.Count;

        if (_subparts.Count == 0)
        {
            Console.WriteLine("inanimate-carbon-rod: No subparts need thumbnail generation.");
            State = GenerationState.Done;
            return;
        }

        Console.WriteLine($"inanimate-carbon-rod: Generating thumbnails for {_subparts.Count} subparts...");

        // Override game thumbnail size for our custom resolution
        _savedThumbnailSize = GameSettings.Current.Graphics.PartThumbnailSize;
        GameSettings.Current.Graphics.PartThumbnailSize = (ushort)ThumbnailImageSize;

        // Create render infrastructure (kept alive across frames)
        Renderer renderer = Program.GetRenderer();
        _thumbRenderer = new ThumbnailRenderer(renderer);

        // Create host-visible staging buffer for GPU → CPU readback
        int stagingSize = ThumbnailImageSize * ThumbnailImageSize * 4;
        _stagingBuffer = renderer.Device.CreateBuffer(new BufferEx.CreateInfo
        {
            Name = "ICR_ReadbackStaging",
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

    /// <summary>
    /// Renders the next batch of thumbnails. Called once per frame.
    /// Saves/restores camera and viewport state around the thumbnail work
    /// so the game's normal rendering is not affected between frames.
    /// </summary>
    private void StepGeneration()
    {
        if (_subparts == null || _root == null || _thumbRenderer == null)
            return;

        Renderer renderer = Program.GetRenderer();
        Viewport viewport = Program.RenderedViewport;
        Camera camera = viewport.GetCamera();

        // Save camera/viewport state (changes each frame from gameplay)
        int2 savedFramebufferSize = camera.FramebufferSize;
        int2 savedViewportSize = viewport.Size;
        IFollowable? savedFollowing = camera.Following;

        // Configure camera for thumbnail rendering
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
            int batchEnd = Math.Min(_currentIndex + BatchSize, _subparts.Count);
            for (int i = _currentIndex; i < batchEnd; i++)
            {
                try
                {
                    RenderOneSubpart(_subparts[i], _root, _thumbRenderer, renderer, viewport, camera, ref _frameIndex, ViewCount,
                        _stagingBuffer!.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"inanimate-carbon-rod: Failed to render thumbnail for {_subparts[i].Id}: {ex.Message}");
                }
                ProgressCurrent = i + 1;
            }
            _currentIndex = batchEnd;
        }
        finally
        {
            PartModelRenderer.ColorData.EndThumbnailPass();

            // Restore camera/viewport for normal game rendering
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }

        if (_currentIndex >= _subparts.Count)
        {
            Console.WriteLine($"inanimate-carbon-rod: Generated {CpuThumbnailCache.All.Count} subpart thumbnails (CPU-backed).");
            CleanupGenerationResources();
            State = GenerationState.Done;
        }
    }

    private void CleanupGenerationResources()
    {
        _root?.Dispose();
        _thumbRenderer?.Dispose();
        _root = null;
        _thumbRenderer = null;
        _subparts = null;

        _stagingBuffer?.Dispose();
        _stagingBuffer = null;

        // Restore original game thumbnail size setting
        GameSettings.Current.Graphics.PartThumbnailSize = _savedThumbnailSize;
    }

    private static void RenderOneSubpart(
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
        // Build ThumbnailPart child for this subpart's mesh
        var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
        var child = new ThumbnailPart(root, syntheticInstance);

        if (child.Model == null && child.ModelDynamic == null)
            return;

        root.AddChild(child);

        // Compute camera distance from bounding sphere
        float radius = root.ComputeBoundingSphereRadius();
        float dist = radius / (float)Math.Sin(camera.GetFieldOfView() * 0.5f);
        root.LocalPosition = Double3Ex.Forward * (camera.NearPlane + dist);
        root.LocalScale = Double3Ex.One;

        // Render N views, readback each to CPU byte[]
        int imageSize = ThumbnailRenderer.SIZE;
        var cpuViews = new byte[viewCount][];
        for (int v = 0; v < viewCount; v++)
        {
            double roll = v * 2.0 * Math.PI / viewCount;
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, roll);
            cpuViews[v] = RenderViewToImage($"Thumb_V{v}_{subpart.Id}", subpart,
                root, thumbRenderer, renderer, viewport, camera, ref frameIndex,
                stagingBuffer);
        }

        // Reset root for next subpart
        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;
        Program.LightSystem.ClearLights();

        // Store CPU-side pixel data
        CpuThumbnailCache.Store(subpart.Id, new CpuThumbnailData(cpuViews, imageSize));
        Console.WriteLine($"inanimate-carbon-rod: Generated thumbnails for {subpart.Id}");
    }

    /// <summary>
    /// Renders a single view to a temporary GPU image, reads back pixel data to CPU,
    /// then disposes the GPU image. Returns the pixel data as byte[].
    /// </summary>
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

        // Drive render
        camera.OnFrame(1.0 / 60.0);
        Program.Instance.UpdateShaderData(1.0 / 60.0, viewport);
        root.UpdateRenderData(viewport, frameIndex);
        Program.Instance.UpdateRenderingResources(frameIndex);

        // Render + blit HDR→LDR + CopyImageToBuffer → staging, all in one command buffer
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

        // GPU synchronization — staging buffer now contains the pixel data
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

        // Read pixel data from staging buffer into managed byte[]
        int byteCount = size * size * 4;
        byte[] pixels = new byte[byteCount];
        using (MappedMemory mapped = stagingBuffer.Map())
        {
            mapped.AsSpan<byte>().Slice(0, byteCount).CopyTo(pixels);
        }

        // Dispose temporary GPU image (free VRAM immediately)
        thumb.Dispose();

        return pixels;
    }

    /// <summary>
    /// Accesses ModLibrary.AllParts (internal field) via reflection to get all PartTemplates.
    /// </summary>
    private static List<PartTemplate> GetAllParts()
    {
        FieldInfo? field = typeof(ModLibrary).GetField("AllParts",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            throw new InvalidOperationException("Could not find ModLibrary.AllParts field via reflection.");

        object? collection = field.GetValue(null);
        if (collection == null)
            throw new InvalidOperationException("ModLibrary.AllParts is null.");

        // SerializedCollection<T> has a public GetList() method
        MethodInfo? getList = collection.GetType().GetMethod("GetList");
        if (getList == null)
            throw new InvalidOperationException("Could not find GetList() on AllParts collection.");

        return (List<PartTemplate>)getList.Invoke(collection, null)!;
    }
}
