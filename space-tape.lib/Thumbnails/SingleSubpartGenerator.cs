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

    /// <summary>Generated thumbnail entry. Only valid when State == Done.</summary>
    public SubpartThumbnailEntry? Result { get; private set; }

    private PartTemplate? _subpart;
    private ThumbnailPart? _root;
    private ThumbnailRenderer? _thumbRenderer;
    private VkCommandPool _commandPool;
    private bool _commandPoolCreated;
    private ushort _savedThumbnailSize;

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
            Console.WriteLine($"space-tape: SingleSubpart BeginGeneration failed - {ex}");
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
            Console.WriteLine($"space-tape: SingleSubpart StepGeneration failed - {ex}");
            CleanupGenerationResources();
        }
    }

    /// <summary>Destroys any generated result and resets to Idle.</summary>
    public void DestroyResult()
    {
        if (Result != null)
        {
            Program.GetRenderer().Device.WaitIdle();
            foreach (var view in Result.Views)
            {
                view?.DestroyImGuiThumbnail();
                view?.Dispose();
            }
            Result = null;
        }
        State = GenerationState.Idle;
        LastError = null;
    }

    /// <summary>
    /// Detaches the generated result without disposing it. The caller takes
    /// ownership of the returned entry and is responsible for disposal.
    /// Resets state to Idle.
    /// </summary>
    public SubpartThumbnailEntry? DetachResult()
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
        Program.Instance.UpdateShaderData(1.0 / 60.0, viewport);
        Program.Instance.UpdateRenderingResources(0);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;

        try
        {
            Result = RenderSubpartViews(_subpart, _root, _thumbRenderer,
                renderer, viewport, _commandPool, ViewCount);
        }
        finally
        {
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }

        CleanupGenerationResources();
        State = GenerationState.Done;
        Console.WriteLine($"space-tape: Generated {ViewCount} hi-res views ({ThumbnailImageSize}px) for {_subpart.Id}");
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
        GameSettings.Current.Graphics.PartThumbnailSize = _savedThumbnailSize;
    }

    private static SubpartThumbnailEntry RenderSubpartViews(
        PartTemplate subpart,
        ThumbnailPart root,
        ThumbnailRenderer thumbRenderer,
        Renderer renderer,
        Viewport viewport,
        VkCommandPool commandPool,
        int viewCount)
    {
        var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
        var child = new ThumbnailPart(root, syntheticInstance);

        if (child.Model == null && child.ModelDynamic == null)
            return new SubpartThumbnailEntry(Array.Empty<ThumbnailReference>());

        root.AddChild(child);

        float radius = root.ComputeBoundingSphereRadius();
        float dist = radius / (float)Math.Sin(viewport.GetCamera().GetFieldOfView() * 0.5f);
        root.LocalPosition = Double3Ex.Forward * (viewport.GetCamera().NearPlane + dist);
        root.LocalScale = Double3Ex.One;

        int size = ThumbnailRenderer.SIZE;
        var renderBatch = new List<(ThumbnailReference thumb, ThumbnailRenderResources resources)>(viewCount);

        for (int v = 0; v < viewCount; v++)
        {
            double roll = v * 2.0 * Math.PI / viewCount;
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, roll);

            var thumb = SubpartThumbnailGenerator.CreateThumbnailImage($"HiRes_V{v}_{subpart.Id}", renderer, size);
            var resources = new ThumbnailRenderResources(
                renderer,
                thumbRenderer.PerInstanceDataDescriptorSetLayout,
                thumbRenderer.PerDrawDataDescriptorSetLayout,
                thumbRenderer.Sampler,
                size);
            SubpartThumbnailGenerator.CollectDraws(root, resources);

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

        var views = new ThumbnailReference[renderBatch.Count];
        for (int i = 0; i < renderBatch.Count; i++)
            views[i] = renderBatch[i].thumb;

        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;
        Program.LightSystem.ClearLights();

        return new SubpartThumbnailEntry(views);
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
