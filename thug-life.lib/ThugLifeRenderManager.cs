using System;
using System.Collections.Generic;
using Brutal.VulkanApi;
using KSA;

namespace MeowSci.ThugLifeLib;

/// <summary>
/// Holds the active <see cref="ThugLifeEntry"/> list and the shared GPU resources used
/// to draw them. The Harmony postfix on <c>SuperMeshRenderSystem.RenderMainPass</c>
/// reads <see cref="Active"/> + <see cref="Instance"/> on the main render thread.
/// </summary>
/// <remarks>
/// The GPU resources are built <b>lazily</b>, on the first entry — never in the constructor.
/// The mod is constructed from <c>[StarMapAllModsLoaded]</c>, which StarMap fires from a
/// postfix on <c>ModLibrary.LoadAll()</c> (<c>KSA/Program.cs:897</c>), and the game does not
/// create <c>Program.OffscreenTarget</c> until <c>BuildRenderTargets()</c> further down that
/// same boot method (<c>KSA/Program.cs:934</c>). Building the pipeline that early therefore
/// dereferences a null <c>RenderTarget</c>. By the time the player can add an entry the
/// render targets are long since live. A side benefit: an unused mod costs no GPU memory.
/// </remarks>
public sealed class ThugLifeRenderManager : IDisposable
{
    public static ThugLifeRenderManager? Instance { get; private set; }

    /// <summary>
    /// Toggled to false BEFORE GPU disposal so an in-flight frame's postfix cannot
    /// reach freed Vulkan handles. Only true once the GPU resources exist.
    /// </summary>
    public static bool Active { get; private set; }

    private readonly List<ThugLifeEntry> _entries = new();
    private ThugLifeTextureFactory? _texture;
    private ThugLifeQuadRenderer? _quad;
    private bool _disabledDueToError;
    private string? _lastError;
    private bool _disposed;

    public IReadOnlyList<ThugLifeEntry> Entries => _entries;
    public string? LastError => _lastError;

    /// <summary>
    /// True while the mod can still accept entries. Deliberately NOT "the GPU resources
    /// exist" — those are created on demand by <see cref="Add"/>.
    /// </summary>
    public bool IsReady => !_disposed && !_disabledDueToError;

    /// <summary>True once the pipeline, texture and buffers are live.</summary>
    public bool IsGpuReady => _quad is { IsValid: true };

    public ThugLifeRenderManager()
    {
        Instance = this;
    }

    /// <summary>
    /// Adds an entry, bringing the GPU resources up first if this is the first one.
    /// Returns false (with <see cref="LastError"/> set) when the renderer is unavailable.
    /// </summary>
    public bool Add(ThugLifeEntry entry)
    {
        if (entry == null) return false;
        if (!EnsureGpuResources()) return false;
        _entries.Add(entry);
        return true;
    }

    public bool Remove(ThugLifeEntry entry)
    {
        if (!_entries.Contains(entry)) return false;
        if (_entries.Count == 1) { Active = false; DisposeGpuResources(); }
        return _entries.Remove(entry);
    }

    /// <summary>
    /// Per-frame game-thread driver: advances any entry that is still sliding into place.
    /// Cheap no-op when nothing is animating.
    /// </summary>
    public void Update(double dt)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Slide == null) continue;

            entry.Position = entry.Slide.Advance(dt);
            if (entry.Slide.IsDone)
                entry.Slide = null;
        }
    }

    /// <summary>
    /// Builds the texture + quad pipeline once. Cheap no-op after the first success, and
    /// after a fault has disabled the feature.
    /// </summary>
    private bool EnsureGpuResources()
    {
        if (!IsReady) return false;
        if (IsGpuReady) return true;

        try
        {
            var renderer = Program.GetRenderer();
            _texture = new ThugLifeTextureFactory(renderer);
            _quad = new ThugLifeQuadRenderer(renderer, _texture);
            Active = true;
            _lastError = null;
            Console.WriteLine("thug-life: render manager initialized");
            return true;
        }
        catch (Exception ex)
        {
            _lastError = $"init failed: {ex.Message}";
            Console.WriteLine($"thug-life: {_lastError}");
            _disabledDueToError = true;
            Active = false;
            DisposeGpuResources();
            return false;
        }
    }

    /// <summary>
    /// Called from the Harmony postfix on the render thread, inside an active offscreen
    /// render pass. Iterates entries and submits a draw for each one.
    /// </summary>
    public void RecordDraws(CommandBuffer cmd)
    {
        if (!IsReady || !IsGpuReady) return;

        try
        {
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
            Active = false;
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
        if (_quad != null || _texture != null) Program.GetRenderer().Device.WaitIdle();
        _quad?.Dispose();
        _texture?.Dispose();
        _quad = null;
        _texture = null;
    }
}
