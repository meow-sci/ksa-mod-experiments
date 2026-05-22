using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// Holds the active <see cref="ThugLifeEntry"/> list and the shared GPU resources used
/// to draw them. The Harmony postfix on <c>SuperMeshRenderSystem.RenderMainPass</c>
/// reads <see cref="Active"/> + <see cref="Instance"/> on the main render thread.
/// </summary>
public sealed class ThugLifeRenderManager : IDisposable
{
    public static ThugLifeRenderManager? Instance { get; private set; }

    /// <summary>
    /// Toggled to false BEFORE GPU disposal so an in-flight frame's postfix cannot
    /// reach freed Vulkan handles.
    /// </summary>
    public static bool Active { get; private set; }

    private readonly List<ThugLifeEntry> _entries = new();
    private ThugLifeTextureFactory? _texture;
    private ThugLifeQuadRenderer? _quad;
    private bool _disabledDueToError;
    private string? _lastError;
    private bool _disposed;
    private int _framesDrawn;

    public IReadOnlyList<ThugLifeEntry> Entries => _entries;
    public string? LastError => _lastError;
    public bool IsReady => !_disposed && _quad != null && _quad.IsValid && !_disabledDueToError;

    // ---- Debug overlay ----
    // When DebugCameraMode is true, one extra quad is drawn at DebugEgoOffset in ego (camera)
    // space, independent of any entries. Used to verify the render pipeline is reaching the GPU.
    public bool DebugCameraMode;
    public float3 DebugEgoOffset = new(0f, 0f, -3f);
    public float DebugWidth = 1.5f;
    public float DebugHeight = 0.4f;

    /// <summary>Number of frames the render postfix has actually fired. UI display only.</summary>
    public int FramesDrawn => _framesDrawn;

    public ThugLifeRenderManager()
    {
        try
        {
            var renderer = Program.GetRenderer();
            _texture = new ThugLifeTextureFactory(renderer);
            _quad = new ThugLifeQuadRenderer(renderer, _texture);
            Instance = this;
            Active = true;
            Console.WriteLine("thug-life: render manager initialized");
        }
        catch (Exception ex)
        {
            _lastError = $"init failed: {ex.Message}";
            Console.WriteLine($"thug-life: {_lastError}");
            _disabledDueToError = true;
            DisposeGpuResources();
        }
    }

    public void Add(ThugLifeEntry entry)
    {
        if (entry == null) return;
        _entries.Add(entry);
    }

    public bool Remove(ThugLifeEntry entry) => _entries.Remove(entry);

    /// <summary>
    /// Called from the Harmony postfix on the render thread, inside an active offscreen
    /// render pass. Iterates entries and submits a draw for each one.
    /// </summary>
    public void RecordDraws(CommandBuffer cmd)
    {
        if (!IsReady) return;

        try
        {
            // Log the first frame and every ~5s thereafter so it's visible the postfix is reaching us.
            if (_framesDrawn == 0)
                Console.WriteLine("thug-life: render postfix fired first frame");
            _framesDrawn++;

            if (DebugCameraMode)
                _quad!.RecordDebugDraw(cmd, DebugEgoOffset, DebugWidth, DebugHeight);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Vehicle == null || entry.Part == null) continue;
                _quad!.RecordDraw(cmd, entry);
            }
        }
        catch (Exception ex)
        {
            _lastError = $"draw failed (disabling render): {ex.Message}";
            Console.WriteLine($"thug-life: {_lastError}");
            _disabledDueToError = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Active = false;
        Instance = null;
        _entries.Clear();
        DisposeGpuResources();
    }

    private void DisposeGpuResources()
    {
        try { _quad?.Dispose(); } catch { }
        try { _texture?.Dispose(); } catch { }
        _quad = null;
        _texture = null;
    }
}
