using System;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Process-wide facade around SubpartThumbnailGenerator for Space Tape UI.
/// Owns generator settings and generation state for modal/window consumers.
/// </summary>
public sealed class SubpartGenerationController : IDisposable
{
    public static readonly int[] ImageSizes = { 32, 64, 96, 128, 256, 512, 1024 };
    public static readonly string[] ImageSizeLabels = { "32", "64", "96", "128", "256", "512", "1024" };

    private readonly SubpartThumbnailGenerator _generator = new();
    private GenerationState _lastObservedState = GenerationState.Idle;

    public int ViewCount { get; set; } = 64;
    public int ImageSizeIndex { get; set; } = 1;
    public bool HasGeneratedAtLeastOnce { get; private set; }

    /// <summary>ViewCount value used for the most recent completed generation run.</summary>
    public int LastGeneratedViewCount { get; private set; }
    /// <summary>Image size in pixels used for the most recent completed generation run.</summary>
    public int LastGeneratedImageSize { get; private set; }

    /// <summary>True for exactly one frame after generation transitions to Done.</summary>
    public bool JustFinished { get; private set; }

    public GenerationState State => _generator.State;
    public int ProgressCurrent => _generator.ProgressCurrent;
    public int ProgressTotal => _generator.ProgressTotal;
    public string? LastError => _generator.LastError;

    public bool IsBusy => _generator.State == GenerationState.Generating;

    public void Update()
    {
        _generator.Update();

        bool justDone = _generator.State == GenerationState.Done && _lastObservedState != GenerationState.Done;
        JustFinished = justDone;

        if (justDone)
        {
            HasGeneratedAtLeastOnce = true;
            LastGeneratedViewCount = ViewCount;
            LastGeneratedImageSize = ImageSizes[ImageSizeIndex];
        }

        _lastObservedState = _generator.State;
    }

    public void Generate()
    {
        _generator.ViewCount = ViewCount;
        _generator.ThumbnailImageSize = ImageSizes[ImageSizeIndex];
        _generator.GenerateAll();
    }

    public void Reset() => _generator.Reset();

    internal void MarkGeneratedOnce() => HasGeneratedAtLeastOnce = true;

    public void Dispose() => _generator.Dispose();
}
