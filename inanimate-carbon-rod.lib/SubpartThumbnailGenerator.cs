using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

public sealed class SubpartThumbnailGenerator
{
    public GenerationState State { get; private set; } = GenerationState.Idle;
    public int ProgressCurrent { get; private set; }
    public int ProgressTotal { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>
    /// Synchronously generates thumbnails for all subparts that don't have one yet.
    /// Must be called from the main game thread (e.g., from ImGui callback).
    /// Briefly stalls the frame while GPU work completes.
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

        State = GenerationState.Generating;
        LastError = null;

        try
        {
            RunGenerationPass();
            State = GenerationState.Done;
            Console.WriteLine($"inanimate-carbon-rod: Generated {SubpartThumbnailCache.All.Count} subpart thumbnails.");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = GenerationState.Failed;
            Console.WriteLine($"inanimate-carbon-rod: GenerateAll failed — {ex}");
        }
    }

    /// <summary>
    /// Resets state so generation can be triggered again.
    /// </summary>
    public void Reset()
    {
        if (State == GenerationState.Generating) return;
        State = GenerationState.Idle;
        ProgressCurrent = 0;
        ProgressTotal = 0;
        LastError = null;
    }

    private void RunGenerationPass()
    {
        // Collect candidates: subparts without thumbnails
        // ModLibrary.AllParts is internal — access via reflection
        List<PartTemplate> allParts = GetAllParts();
        List<PartTemplate> subparts = allParts
            .Where(p => p.IsSubPart && !p.IsHidden && p.Thumbnail == null)
            .ToList();

        ProgressCurrent = 0;
        ProgressTotal = subparts.Count;

        if (subparts.Count == 0)
        {
            Console.WriteLine("inanimate-carbon-rod: No subparts need thumbnail generation.");
            return;
        }

        Console.WriteLine($"inanimate-carbon-rod: Generating thumbnails for {subparts.Count} subparts...");

        // Get rendering infrastructure (mirrors ThumbnailCreator.PreparePartThumbnails)
        Renderer renderer = Program.GetRenderer();
        Viewport viewport = Program.RenderedViewport;
        Camera camera = viewport.GetCamera();

        // Save camera/viewport state to restore after
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

        // Create render infrastructure
        ThumbnailPart root = new ThumbnailPart(camera);
        using ThumbnailRenderer thumbRenderer = new ThumbnailRenderer(renderer);
        PartModelRenderer.ColorData.BeginThumbnailPass(thumbRenderer.RenderPass, thumbRenderer.SampleCount);

        int frameIndex = 0;

        try
        {
            for (int i = 0; i < subparts.Count; i++)
            {
                try
                {
                    RenderOneSubpart(subparts[i], root, thumbRenderer, renderer, viewport, camera, ref frameIndex);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"inanimate-carbon-rod: Failed to render thumbnail for {subparts[i].Id}: {ex.Message}");
                }
                ProgressCurrent = i + 1;
            }
        }
        finally
        {
            // Always clean up
            PartModelRenderer.ColorData.EndThumbnailPass();
            root.Dispose();

            // Restore camera/viewport state
            camera.Resize(savedFramebufferSize);
            viewport.Size = savedViewportSize;
            if (savedFollowing != null)
                camera.SetFollow(savedFollowing, tidalLocking: false);
            camera.OnFrame(1.0 / 60.0);
        }
    }

    private static void RenderOneSubpart(
        PartTemplate subpart,
        ThumbnailPart root,
        ThumbnailRenderer thumbRenderer,
        Renderer renderer,
        Viewport viewport,
        Camera camera,
        ref int frameIndex)
    {
        // 1. Allocate GPU image (mirrors ThumbnailCreator.CreateThumbnailImage)
        ImageEx.CreateInfo createInfo = new ImageEx.CreateInfo
        {
            Name = "Thumbnail_" + subpart.Id,
            AllocPreference = MemoryPreference.PreferGpu,
            ImageArrayLayers = 1,
            ImageInitialLayout = VkImageLayout.Undefined,
            ImageType = VkImageType._2D,
            ImageExtent = new VkExtent3D
            {
                Width = ThumbnailRenderer.SIZE,
                Height = ThumbnailRenderer.SIZE,
                Depth = 1
            },
            ImageUsage = VkImageUsageFlags.TransferSrcBit
                       | VkImageUsageFlags.TransferDstBit
                       | VkImageUsageFlags.SampledBit
                       | VkImageUsageFlags.ColorAttachmentBit,
            ImageFormat = ThumbnailRenderer.ColorFormat,
            ImageMipLevels = ThumbnailRenderer.MipLevels,
            ImageSamples = VkSampleCountFlags._1Bit,
            ImageSharingMode = VkSharingMode.Exclusive,
            ImageTiling = VkImageTiling.Optimal
        };

        subpart.Thumbnail = new ThumbnailReference();
        subpart.Thumbnail.CreateImageView(
            renderer.Device,
            createInfo,
            VkImageViewType._2D,
            new VkImageSubresourceRange
            {
                AspectMask = VkImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = ThumbnailRenderer.MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            });

        // 2. Build ThumbnailPart child for this subpart's own mesh.
        //    We create a synthetic PartInstance so ThumbnailPart reads the subpart's
        //    own Components (mesh) via GetTemplate().
        var syntheticInstance = new PartInstance { InstanceOf = subpart.Id };
        var child = new ThumbnailPart(root, syntheticInstance);

        if (child.Model == null && child.ModelDynamic == null)
        {
            // Subpart has no renderable mesh — clean up and skip
            subpart.Thumbnail.Dispose();
            subpart.Thumbnail = null;
            return;
        }

        root.AddChild(child);

        // 3. Position camera (mirrors ThumbnailCreator.MoveRootPart)
        if (subpart.Thumbnail.ModelTransform != null)
        {
            root.Transform = subpart.Thumbnail.ModelTransform.Create();
        }
        else
        {
            float radius = root.ComputeBoundingSphereRadius();
            float dist = radius / (float)Math.Sin(camera.GetFieldOfView() * 0.5f);
            root.LocalPosition = Double3Ex.Forward * (camera.NearPlane + dist);
            root.LocalRotation = doubleQuat.CreateFromYawPitchRoll(Math.PI, Math.PI / 4.0, 0.0);
            root.LocalScale = Double3Ex.One;
        }

        // 4. Drive render (mirrors ThumbnailCreator per-part loop body)
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
            new PostPassThumbnailCommand(thumbRenderer, subpart, Program.PlanetAtmosphereRenderer),
            subpart.Id,
            out VkFence fence);

        // 5. GPU synchronization (exact mirror of game code)
        renderer.Device.WaitForFence(fence, IntPtr.MaxValue);
        DeviceEx device = renderer.Device;
        VkFence fenceRef = fence;
        device.ResetFences(new ReadOnlySpan<VkFence>(in fenceRef));
        renderer.Device.DestroyFence(fence, null);

        PartModelRenderer.ClearFrameData(frameIndex);
        Program.DeviceHostSharedMemoryDebug.PostMemoryWrite = false;
        Program.DeviceHostSharedMemoryDebug.PostDescriptorSet = false;

        frameIndex = (frameIndex + 1) % 2;

        // 6. Reset root for next subpart (mirrors ThumbnailCreator.ResetRootPart)
        root.ClearAndDisposeChildren();
        root.LocalPosition = double3.Zero;
        root.LocalRotation = doubleQuat.Identity;
        root.LocalScale = Double3Ex.One;

        Program.LightSystem.ClearLights();

        // 7. Store in cache
        SubpartThumbnailCache.Store(subpart.Id, subpart.Thumbnail!);

        Console.WriteLine($"inanimate-carbon-rod: Generated thumbnail for {subpart.Id}");
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
