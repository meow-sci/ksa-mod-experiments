namespace MeowSci.BlinkyLib;

/// <summary>
/// Configuration for the dynamically built LCD pixel engine grid.
/// </summary>
public class LcdGridConfig
{
    /// <summary>Number of pixel columns (width of the display).</summary>
    public int Width { get; set; } = 16;

    /// <summary>Number of pixel rows (height of the display).</summary>
    public int Height { get; set; } = 8;

    /// <summary>Spacing in metres between adjacent pixel positions.</summary>
    public float Spacing { get; set; } = 0.5f;

    /// <summary>X offset in metres from the attachment part's origin (positive = right).</summary>
    public float OffsetX { get; set; } = 0f;

    /// <summary>Y offset in metres from the attachment part's origin (positive = up).</summary>
    public float OffsetY { get; set; } = 5f;

    /// <summary>Z offset in metres from the attachment part's origin (positive = forward).</summary>
    public float OffsetZ { get; set; } = 2f;

    /// <summary>
    /// Engine part template ID for each pixel. Must exist in ModLibrary.
    /// Available liquid engine templates: CorePropulsionA_Prefab_EngineA1 through EngineA6.
    /// </summary>
    public string EnginePartId { get; set; } = "CorePropulsionA_Prefab_EngineA1";

    /// <summary>
    /// Uniform scale applied to each pixel engine part.
    /// Blinken uses 0.1 (10% of full size). At scale 1 the engines are full-size and visually massive.
    /// For a pixel display, keep this small (0.05 – 0.2).
    /// </summary>
    public double PartScale { get; set; } = 0.1;

    /// <summary>Total number of Part objects that will be created (Width × Height × 2 for a/b pairs).</summary>
    public int TotalParts => Width * Height * 2;
}
