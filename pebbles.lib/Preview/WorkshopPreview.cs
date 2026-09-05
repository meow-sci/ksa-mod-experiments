using System;
using System.Numerics;
using Brutal.ImGuiApi;
using Brutal.VulkanApi;
using Core;
using KSA;

namespace MeowSci.PebblesLib;

/// <summary>
/// Independent textured mesh preview with no global viewport, camera, gizmo or physics state.
/// Constructor is detached. Refresh/Render run before GUI emission; Dispose runs at that same
/// safe phase or host unload. Never destroy a texture after adding it to this frame's ImGui list.
/// </summary>
public sealed class WorkshopPreview : IDisposable
{
    private Renderer? _renderer;
    private PreviewPipeline? _pipeline;
    private PreviewScene? _scene;
    private PreviewTarget? _target;
    private PreviewGeometry? _pending;
    private bool _disposed;
    private bool _failedRefresh;
    private bool _ready;
    private Matrix4x4 _lastMatrix;
    private Vector3 _lastEye;

    public ImTextureRef Texture => _target?.Texture ?? default;
    public bool IsReady => _ready && !_failedRefresh && !_disposed;
    public string Status { get; private set; } = "Choose a mesh and refresh the preview.";
    public Vector3 BoundsMin { get; private set; } = new(-.5f);
    public Vector3 BoundsMax { get; private set; } = new(.5f);
    public Matrix4x4 ViewProjection => _lastMatrix;

    /// <summary>Resolve and copy CPU data only. The next Render builds an independent GPU scene.</summary>
    public void Refresh(ObjectRecipe recipe, ClutterAssets assets, int lodIndex = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            _pending = PreviewGeometry.Prepare(recipe, assets, lodIndex);
            BoundsMin = _pending.Min; BoundsMax = _pending.Max;
            _failedRefresh = false;
            _ready = false;
            Status = "Preparing textured preview…";
        }
        catch (Exception ex)
        {
            _pending = null;
            _failedRefresh = true;
            Status = $"Preview unavailable: {ex.Message}";
            Console.WriteLine($"pebbles preview: {ex.Message}");
        }
    }

    /// <summary>
    /// Call in BeforeGui, before this frame can record an ImGui reference to Texture.
    /// Changed previews conservatively wait for prior GPU/UI readers. The preview submission
    /// finishes before publishing its image. Unchanged previews do no GPU work and never wait.
    /// </summary>
    public void Render(WorkshopView view, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_failedRefresh || (_pending == null && _scene == null)) return;
        var matrix = WorkshopMath.ViewProjection(view, width, height);
        // Keep the logical panel aspect for overlay/picking, even if GPU resolution is capped.
        width = Math.Clamp(width, 64, 1536); height = Math.Clamp(height, 64, 1536);
        var eye = view.Eye;
        if (!Finite(eye) || !Finite(matrix)) { Status = "Preview camera contains invalid values."; _ready = false; return; }
        if (_ready && _pending == null && _target?.Width == width && _target.Height == height &&
            matrix == _lastMatrix && eye == _lastEye) return;

        PreviewScene? nextScene = null;
        PreviewTarget? nextTarget = null;
        try
        {
            _renderer ??= Program.GetRenderer();
            // Retires submitted ImGui consumers too, unlike a fence for the preview producer alone.
            // BeforeGui scheduling is essential: there must be no unsubmitted current-frame UI use.
            _renderer.Device.WaitIdle();
            _pipeline ??= new PreviewPipeline(_renderer);
            if (_pending != null) nextScene = new PreviewScene(_renderer, _pipeline, _pending);
            var drawScene = nextScene ?? _scene!;
            bool resize = _target == null || _target.Width != width || _target.Height != height;
            if (resize) nextTarget = new PreviewTarget(_renderer, width, height);
            var drawTarget = nextTarget ?? _target!;
            drawTarget.Render(_pipeline, drawScene, matrix, eye);
            if (nextScene != null) { _scene?.Dispose(); _scene = nextScene; nextScene = null; }
            if (nextTarget != null) { _target?.Dispose(); _target = nextTarget; nextTarget = null; }
            _pending = null;
            _lastMatrix = matrix; _lastEye = eye;
            _ready = true;
            Status = "Studio preview · terrain lighting and placement are evaluated on Apply.";
        }
        catch (Exception ex)
        {
            // Do not publish failed requests or automatically retry allocations every frame.
            _renderer?.Device.WaitIdle();
            nextScene?.Dispose(); nextTarget?.Dispose();
            // A failed record may leave a reused target in attachment layout. Never reuse it
            // with a guessed sampled layout, or expose its previous ImGui descriptor again.
            _target?.Dispose(); _target = null;
            _pending = null; _failedRefresh = true; _ready = false;
            Status = $"Preview failed: {ex.Message}. Refresh to retry.";
            Console.WriteLine($"pebbles preview: {ex}");
        }
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Matrix4x4 value) =>
        float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
        float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
        float.IsFinite(value.M31) && float.IsFinite(value.M32) && float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
        float.IsFinite(value.M41) && float.IsFinite(value.M42) && float.IsFinite(value.M43) && float.IsFinite(value.M44);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _ready = false; _pending = null;
        if (_renderer == null) return;
        _renderer.Device.WaitIdle();
        _target?.Dispose(); _target = null;
        _scene?.Dispose(); _scene = null;
        _pipeline?.Dispose(); _pipeline = null;
    }
}
