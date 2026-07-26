// THREADING RULE (repeated in every parts-now file):
// Everything runs on the game thread except RuntimeModLoader's loader step, which runs on a
// Task.Run worker. The worker touches only ILoader.Load(). Completion is polled from Update(dt).
// Do NOT use MeowSci.KsaAbstractions.GameThread — its queue is only drained when
// unladen-swallow.lib is present, and parts-now must work standalone.
//
// Every member of PartThumbnailGenerator is game-thread only. Begin() / Step() / Dispose() all
// record and submit Vulkan work directly, so they must only ever be called from
// PartsNowSubmod.Update(dt).

using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using KSA.Rendering;
using KSA.Rendering.Thumbnails;

namespace MeowSci.PartsNowLib;

/// <summary>
/// Renders part-browser thumbnails for Parts that were loaded after boot, a couple of parts per
/// frame, against KSA's dedicated offscreen thumbnail viewport.
/// </summary>
/// <remarks>
/// <para>
/// This is the union of two things the game already does at runtime: the framing of
/// <c>ThumbnailCreator.PreparePartThumbnails</c> and the per-frame submit shape of
/// <c>ThumbnailDynamic.Render</c>.
/// </para>
/// <para>
/// <c>Program.ThumbnailViewport</c> (viewport index 1) is created at boot purely for thumbnails,
/// is marked <c>IsOffscreen</c> / <c>ShouldRenderGizmos = false</c>, and its camera is never driven
/// by the player. That is what lets this class skip every piece of ceremony a live-camera hijack
/// would need: no camera save/restore, no viewport resize, no follow alerts, and no
/// <c>Program.Instance.UpdateShaderData</c> / <c>UpdateRenderingResources</c> call — only this
/// viewport's camera UBO slice is written, exactly as <c>ThumbnailDynamic</c> does.
/// </para>
/// <para>
/// <b>Sharing the viewport with <c>ThumbnailDynamic</c> is safe and intentional.</b> The part
/// browser's hover preview (<c>VehicleEditor.DynamicThumbnail</c>) uses this same viewport and
/// camera, but our batch runs in <c>ISubmod.Update(dt)</c> (<c>Program.OnDrawUiFrame</c>) while
/// <c>ThumbnailDynamic.Render</c> runs later in the SAME frame (<c>Editor.OnPreRender</c>). Each
/// writes the camera UBO immediately before its own submit and waits on its own fence, so neither
/// observes the other's state. Do NOT defer our submit to a later frame phase.
/// </para>
/// </remarks>
public sealed class PartThumbnailGenerator : IDisposable
{
    /// <summary>Number of Parts rendered per call to <see cref="Step" /> (i.e. per game frame).</summary>
    public const int PartsPerFrame = 2;

    private readonly List<PartTemplate> _templates = new List<PartTemplate>();
    private readonly List<(string PartId, bool Rendered, string Reason)> _results =
        new List<(string PartId, bool Rendered, string Reason)>();

    private Renderer? _renderer;
    private ThumbnailRenderer? _thumbRenderer;
    private Viewport? _viewport;
    private Camera? _camera;
    private ThumbnailPart? _root;
    private ThumbnailReadback? _readback;
    private VkCommandPool _commandPool;
    private bool _commandPoolCreated;
    private int _index;
    private bool _running;
    private bool _disposed;

    /// <summary>
    /// When true, every rendered thumbnail is copied back into a host-visible buffer and the
    /// fraction of non-zero texels is logged and appended to that part's
    /// <see cref="Results" /> entry. Diagnostic only; off by default because it forces a
    /// full image copy per part.
    /// </summary>
    public bool DebugReadback { get; set; }

    /// <summary>Parts processed so far in the current job.</summary>
    public int ProgressCurrent => _index;

    /// <summary>Total Parts in the current job.</summary>
    public int ProgressTotal => _templates.Count;

    /// <summary>
    /// Per-part outcome, in processing order, so the caller can mark a part
    /// <c>Degraded</c> on its <see cref="LoadedModRecord" />. <c>Rendered</c> is false for both
    /// deliberate skips and failures; <c>Reason</c> always says which.
    /// </summary>
    public IReadOnlyList<(string PartId, bool Rendered, string Reason)> Results => _results;

    /// <summary>
    /// Begins a job over the given top-level templates: creates the <c>ThumbnailRenderer</c>, a
    /// private transient command pool, and the root <c>ThumbnailPart</c> parented to the thumbnail
    /// viewport's camera. Any previous job's resources are released first.
    /// </summary>
    /// <param name="topLevelParts">
    /// The Parts to thumbnail. Sub-parts are recorded as skipped rather than rendered.
    /// </param>
    public void Begin(IReadOnlyList<PartTemplate> topLevelParts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ReleaseJobResources();
        _templates.Clear();
        _results.Clear();
        _index = 0;
        _running = false;

        if (topLevelParts != null)
        {
            foreach (PartTemplate template in topLevelParts)
            {
                if (template != null)
                {
                    _templates.Add(template);
                }
            }
        }

        if (_templates.Count == 0)
        {
            Console.WriteLine("parts-now: thumbnail job has nothing to render.");
            return;
        }

        try
        {
            Renderer renderer = Program.GetRenderer();
            _renderer = renderer;
            _thumbRenderer = new ThumbnailRenderer(renderer);

            VkCommandPoolCreateInfo poolInfo = new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = renderer.Graphics.Family,
                Flags = VkCommandPoolCreateFlags.TransientBit | VkCommandPoolCreateFlags.ResetCommandBufferBit,
            };
            _commandPool = renderer.Device.CreateCommandPool(in poolInfo, null);
            _commandPoolCreated = true;

            _viewport = Program.ThumbnailViewport;
            _camera = _viewport.GetCamera();
            _root = new ThumbnailPart(_camera);

            WarnOnSizeMismatch(_viewport);

            _running = true;
            Console.WriteLine($"parts-now: thumbnail job started for {_templates.Count} part(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: could not start the thumbnail job: {ex}");
            foreach (PartTemplate template in _templates)
            {
                Record(template.Id, false, "thumbnail renderer unavailable: " + ex.Message);
            }

            _index = _templates.Count;
            ReleaseJobResources();
        }
    }

    /// <summary>
    /// Renders the next batch of <see cref="PartsPerFrame" /> Parts. Call once per frame from
    /// <c>Update(dt)</c>.
    /// </summary>
    /// <returns>True when the job is finished (including when there was nothing to do).</returns>
    public bool Step()
    {
        if (!_running || _disposed)
        {
            return true;
        }

        Camera camera = _camera!;
        Viewport viewport = _viewport!;

        // INVARIANT: never move this camera — move the root part. ThumbnailCreator.MoveRootPart
        // positions the ThumbnailPart in front of a camera parked at origin/identity, and
        // ThumbnailDynamic.Render assumes the same. This block only RE-ASSERTS origin/identity; it
        // must never set a non-zero camera transform.
        //
        // Unfollow MUST pass changeControl: false — the default overload nulls
        // Program.ControlledVehicle, which would drop the player's vessel mid-flight.
        try
        {
            camera.Unfollow(changeControl: false);
            camera.LocalPosition = double3.Zero;
            camera.LocalRotation = doubleQuat.Identity;
            camera.LocalScale = double3.One;
            camera.OnFrame(1.0 / 60.0);

            // Writes only this viewport's slice of the global camera UBO. Must happen AFTER
            // OnFrame (which recomputes MVP) and BEFORE the submit below.
            ThumbnailDynamic.UpdateGlobalCameraData(viewport, camera);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: thumbnail camera setup failed, aborting the job: {ex}");
            for (int i = _index; i < _templates.Count; i++)
            {
                Record(_templates[i].Id, false, "camera setup failed: " + ex.Message);
            }

            _index = _templates.Count;
            Finish();
            return true;
        }

        int batchEnd = Math.Min(_index + PartsPerFrame, _templates.Count);
        for (; _index < batchEnd; _index++)
        {
            PartTemplate template = _templates[_index];

            // Per-part try/catch: an unresolvable <Mesh Id> throws NullReferenceException out of
            // ModLibrary.Get<MeshReference>, and a PbrMaterial missing a channel throws out of
            // ThumbnailRenderResources.AddDraw. Record it and keep going.
            try
            {
                RenderOne(template);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: thumbnail for '{template.Id}' failed: {ex}");
                Record(template.Id, false, "render failed: " + ex.Message);
            }
        }

        if (_index >= _templates.Count)
        {
            Finish();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Disposes the root part and the <c>ThumbnailRenderer</c>, destroys the private command pool
    /// and any debug-readback buffer. Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseJobResources();
        _disposed = true;
    }

    private void RenderOne(PartTemplate template)
    {
        Renderer renderer = _renderer!;
        ThumbnailRenderer thumbRenderer = _thumbRenderer!;
        ThumbnailPart root = _root!;
        Camera camera = _camera!;

        // Only top-level parts get thumbnails, matching ThumbnailCreator.PreparePartThumbnails.
        // ThumbnailCreator.AddPart only walks SubPartInstances, so a sub-part collects no draws.
        if (template.IsSubPart)
        {
            Record(template.Id, false, "sub-part");
            return;
        }

        ThumbnailCreator.ResetRootPart(root);
        ThumbnailCreator.AddPart(root, template);
        if (root.Children is null or { Count: 0 })
        {
            Record(template.Id, false, "no sub-parts to draw");
            return;
        }

        // A <Thumbnail> declared in XML carries a ModelTransform but has never had CreateImageView
        // called, so its ImageViewEx is default and Dispose() would NRE on a null captured Device.
        // Only dispose a reference that actually owns an image, and carry the declared
        // ModelTransform across to the replacement so custom framing survives.
        // (KSA's own ThumbnailCreator.CreateThumbnailImage drops it — we deliberately do not.)
        TransformReference? declaredTransform = template.Thumbnail?.ModelTransform;
        if (template.Thumbnail is { } previous && !previous.ImageView.IsNull())
        {
            previous.Dispose();
        }

        ThumbnailReference thumbnail = ThumbnailCreator.CreateThumbnailReference(renderer, "Thumbnail_" + template.Id);
        thumbnail.ModelTransform = declaredTransform;
        template.Thumbnail = thumbnail;

        // Honours <Thumbnail><ModelTransform> when the part declares one, otherwise frames the
        // bounding sphere using camera.GetFieldOfView() / camera.NearPlane.
        ThumbnailCreator.MoveRootPart(root, thumbnail, camera);

        ThumbnailRenderResources resources = new ThumbnailRenderResources(
            renderer,
            thumbRenderer.PerInstanceDataDescriptorSetLayout,
            thumbRenderer.PerDrawDataDescriptorSetLayout,
            thumbRenderer.Sampler,
            ThumbnailRenderer.SIZE);

        try
        {
            ThumbnailCreator.CollectDraws(root, resources);

            // Logged for every part: a uniformly transparent (0,0,0,0) thumbnail is almost always
            // a zero draw count, i.e. no <PartModel> on the sub-parts or an unresolved mesh id.
            int drawCount = (int)resources.DrawCommandVector.ElementCount;
            Console.WriteLine($"parts-now: thumbnail '{template.Id}' collected {drawCount} draw(s).");

            if (drawCount == 0)
            {
                Record(template.Id, false, "no draws collected");
                return;
            }

            resources.UpdateDescriptorSets();
            SubmitOne(renderer, thumbRenderer, template, thumbnail, resources);
        }
        finally
        {
            resources.Dispose();
        }
    }

    private void SubmitOne(
        Renderer renderer,
        ThumbnailRenderer thumbRenderer,
        PartTemplate template,
        ThumbnailReference thumbnail,
        ThumbnailRenderResources resources)
    {
        Viewport viewport = _viewport!;
        int size = ThumbnailRenderer.SIZE;

        CommandBuffer commandBuffer = renderer.Device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo
        {
            CommandPool = _commandPool,
            Level = VkCommandBufferLevel.Primary,
        });

        try
        {
            commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
            thumbRenderer.RecordPartRender(commandBuffer, thumbnail, resources, viewport, template.Id);

            bool readbackRecorded = DebugReadback && TryRecordReadback(renderer, commandBuffer, thumbnail, size);

            commandBuffer.End();

            VkFence fence = renderer.Device.CreateFence(new VkFenceCreateInfo(), null);
            VkResult waitResult;
            try
            {
                CommandBuffer submitRef = commandBuffer;
                renderer.Graphics.Submit(
                    default,
                    default,
                    new Span<CommandBuffer>(ref submitRef),
                    default,
                    fence);

                waitResult = renderer.Device.WaitForFence(fence, -1);
            }
            finally
            {
                renderer.Device.DestroyFence(fence, null);
            }

            // "Nothing renders and the log is silent" is almost always a non-Success fence wait.
            if (waitResult != VkResult.Success)
            {
                Console.WriteLine(
                    $"parts-now: thumbnail '{template.Id}' fence wait returned {waitResult}; the image is not valid.");
                Record(template.Id, false, $"fence wait returned {waitResult}");
                return;
            }

            string reason = readbackRecorded ? DescribeReadback(template.Id, size) : string.Empty;
            Record(template.Id, true, reason);
        }
        finally
        {
            CommandBuffer freeRef = commandBuffer;
            renderer.Device.FreeCommandBuffers(_commandPool, new ReadOnlySpan<CommandBuffer>(in freeRef));
        }
    }

    private bool TryRecordReadback(Renderer renderer, CommandBuffer commandBuffer, ThumbnailReference thumbnail, int size)
    {
        try
        {
            _readback ??= new ThumbnailReadback();
            _readback.RecordCopy(renderer, commandBuffer, thumbnail, size);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: debug readback could not be recorded, disabling it: {ex.Message}");
            DebugReadback = false;
            return false;
        }
    }

    private string DescribeReadback(string partId, int size)
    {
        try
        {
            double fraction = _readback!.NonZeroTexelFraction(size);
            Console.WriteLine($"parts-now: thumbnail '{partId}' non-zero texels {fraction:P2}.");
            return $"non-zero texels {fraction:P2}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: debug readback for '{partId}' failed: {ex.Message}");
            return string.Empty;
        }
    }

    private void Record(string partId, bool rendered, string reason)
    {
        _results.Add((partId, rendered, reason));

        if (!rendered)
        {
            Console.WriteLine($"parts-now: no thumbnail for '{partId}' — {reason}.");
        }
    }

    private void Finish()
    {
        int rendered = 0;
        foreach ((string _, bool wasRendered, string _) in _results)
        {
            if (wasRendered)
            {
                rendered++;
            }
        }

        Console.WriteLine($"parts-now: thumbnail job finished — {rendered}/{_templates.Count} rendered.");
        ReleaseJobResources();
    }

    private void ReleaseJobResources()
    {
        _running = false;

        try
        {
            _root?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to dispose the thumbnail root part: {ex.Message}");
        }

        _root = null;
        _camera = null;
        _viewport = null;

        try
        {
            _thumbRenderer?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to dispose the thumbnail renderer: {ex.Message}");
        }

        _thumbRenderer = null;

        try
        {
            _readback?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parts-now: failed to dispose the thumbnail readback buffer: {ex.Message}");
        }

        _readback = null;

        if (_commandPoolCreated && _renderer != null)
        {
            try
            {
                _renderer.Device.DestroyCommandPool(_commandPool, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"parts-now: failed to destroy the thumbnail command pool: {ex.Message}");
            }
        }

        _commandPool = default;
        _commandPoolCreated = false;
        _renderer = null;
    }

    /// <summary>
    /// <c>ThumbnailRenderer.SIZE</c> reads <c>GameSettings.Current.Graphics.PartThumbnailSize</c>
    /// live, while the thumbnail viewport was sized at boot from the then-current value. If the
    /// player changed the setting mid-session the two differ — but both are square, so the
    /// projection is still correct. Warn and carry on; never mutate the game setting.
    /// </summary>
    private static void WarnOnSizeMismatch(Viewport viewport)
    {
        int size = ThumbnailRenderer.SIZE;
        if (viewport.Size.X != size || viewport.Size.Y != size)
        {
            Console.WriteLine(
                $"parts-now: WARNING — thumbnail size is {size}px but the thumbnail viewport is "
                + $"{viewport.Size.X}x{viewport.Size.Y}px (PartThumbnailSize changed since boot). "
                + "Both are square so framing is unaffected.");
        }
    }
}
