namespace MeowSci.InanimateCarbonRodLib;

/// <summary>
/// CPU-side pixel data for all rotation views of a single subpart.
/// Pixel format: R8G8B8A8UNorm (4 bytes/pixel), no mip chain.
/// </summary>
public sealed class CpuThumbnailData
{
    /// <summary>Pixel data for each rotation view. Index = view number.</summary>
    public byte[][] Views { get; }

    /// <summary>Image width/height in pixels (square).</summary>
    public int Size { get; }

    public CpuThumbnailData(byte[][] views, int size)
    {
        Views = views;
        Size = size;
    }
}
