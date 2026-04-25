using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.SpaceTapeLib;

public enum GenerationState { Idle, Generating, Done, Failed }

public sealed class SubpartThumbnailGenerator : IDisposable
{
    /// <summary>Number of subparts to render per game frame.</summary>
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
    private VkCommandPool _commandPool;
    private bool _commandPoolCreated;
    private ushort _savedThumbnailSize;

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

        // Create our own command pool for thumbnail command buffers
        var poolInfo = new VkCommandPoolCreateInfo
        {
            QueueFamilyIndex = renderer.Graphics.Family,
            Flags = VkCommandPoolCreateFlags.TransientBit | VkCommandPoolCreateFlags.ResetCommandBufferBit
        };
        _commandPool = renderer.Device.CreateCommandPool(in poolInfo, null);
        _commandPoolCreated = true;

        Viewport viewport = Program.RenderedViewport;
        Camera camera = viewport.GetCamera();
        _root = new ThumbnailPart(camera);

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
        Program.Instance.UpdateShaderData(1.0 / 60.0, viewport);
        Program.Instance.UpdateRenderingResources(0);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;

        try
        {
            int batchEnd = Math.Min(_currentIndex + BatchSize, _subparts.Count);
            for (int i = _currentIndex; i < batchEnd; i++)
            {
                try
                {
                    RenderOneSubpart(_subparts[i], _root, _thumbRenderer,
                        renderer, viewport, camera, _commandPool, ViewCount);
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
            // Restore camera/viewport for normal game rendering
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }

        if (_currentIndex >= _subparts.Count)
        {
            Console.WriteLine($"inanimate-carbon-rod: Generated {SubpartThumbnailCache.All.Count} subpart thumbnails.");
            CleanupGenerationResources();
            State = GenerationState.Done;
        }
    }

    private void CleanupGenerationResources()
    {
        _root?.Dispose();
        _thumbRenderer?.Dispose();
        if (_commandPoolCreated)
        {
            Renderer renderer = Program.GetRenderer();
            renderer.Device.DestroyCommandPool(_commandPool, null);
            _commandPool = default;
            _commandPoolCreated = false;
        }
        _root = null;
        _thumbRenderer = null;
        _subparts = null;

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
        VkCommandPool commandPool,
        int viewCount)
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

        // Prepare all views: create images and collect draw data per rotation
        int size = ThumbnailRenderer.SIZE;
        var renderBatch = new List<(ThumbnailReference thumb, ThumbnailRenderResources resources)>(viewCount);

        for (int v = 0; v < viewCount; v++)
        {
            double roll = v * 2.0 * Math.PI / viewCount;
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, roll);

            var thumb = CreateThumbnailImage($"Thumb_V{v}_{subpart.Id}", renderer, size);
            var resources = new ThumbnailRenderResources(
                renderer,
                thumbRenderer.PerInstanceDataDescriptorSetLayout,
                thumbRenderer.PerDrawDataDescriptorSetLayout,
                thumbRenderer.Sampler,
                size);
            CollectDraws(root, resources);

            if ((int)resources.DrawCommandVector.ElementCount > 0)
            {
                renderBatch.Add((thumb, resources));
            }
            else
            {
                resources.Dispose();
                thumb.Dispose();
            }
        }

        // Record and submit all views in a single command buffer
        if (renderBatch.Count > 0)
        {
            CommandBuffer commandBuffer = renderer.Device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
            {
                CommandPool = commandPool,
                Level = VkCommandBufferLevel.Primary
            });
            commandBuffer.Begin(new VkCommandBufferBeginInfo
            {
                Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit
            });

            foreach (var (thumb, resources) in renderBatch)
            {
                resources.UpdateDescriptorSets();
                thumbRenderer.RecordPartRender(commandBuffer, thumb, resources, viewport, subpart.Id);
            }

            commandBuffer.End();

            VkFence fence = renderer.Device.CreateFence(new VkFenceCreateInfo(), null);
            Queue graphics = renderer.Graphics;
            CommandBuffer cbRef = commandBuffer;
            graphics.Submit(
                default(Span<VkSemaphore>),
                default(Span<VkPipelineStageFlags>),
                new Span<CommandBuffer>(ref cbRef),
                default(Span<VkSemaphore>),
                fence);

            renderer.Device.WaitForFence(fence, -1);
            renderer.Device.DestroyFence(fence, null);
            ReadOnlySpan<CommandBuffer> cbSpan = new ReadOnlySpan<CommandBuffer>(in cbRef);
            renderer.Device.FreeCommandBuffers(commandPool, cbSpan);

            foreach (var (_, resources) in renderBatch)
                resources.Dispose();
        }

        // Collect the thumbnail references in order
        var views = new ThumbnailReference[renderBatch.Count];
        for (int i = 0; i < renderBatch.Count; i++)
            views[i] = renderBatch[i].thumb;

        // Reset root for next subpart
        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;
        Program.LightSystem.ClearLights();

        // Store all views; set first as game-visible thumbnail
        if (views.Length > 0)
        {
            subpart.Thumbnail = views[0];
            SubpartThumbnailCache.Store(subpart.Id, new SubpartThumbnailEntry(views));
            Console.WriteLine($"inanimate-carbon-rod: Generated thumbnails for {subpart.Id}");
        }
    }

    internal static ThumbnailReference CreateThumbnailImage(string imageName, Renderer renderer, int size)
    {
        var thumb = new ThumbnailReference();
        thumb.CreateImageView(
            imageName,
            renderer,
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
                ImageUsage = VkImageUsageFlags.TransferSrcBit
                           | VkImageUsageFlags.TransferDstBit
                           | VkImageUsageFlags.SampledBit
                           | VkImageUsageFlags.ColorAttachmentBit,
                ImageFormat = ThumbnailRenderer.ColorFormat,
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

    internal static void CollectDraws(ThumbnailPart part, ThumbnailRenderResources resources)
    {
        if (part.Model != null)
            resources.AddDraw(part.GetMatrix(), part.Model.Template);
        if (part.ModelDynamic != null)
            resources.AddDraw(part.GetMatrix(), part.ModelDynamic.Template);

        if (part.Children == null) return;
        foreach (ThumbnailPart child in part.Children)
            CollectDraws(child, resources);
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
