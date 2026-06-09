namespace MeowSci.ItsSoShinyLib;

public enum ShinyGridLayout
{
    Flat,
    Cylinder,
}

public sealed class ShinyGridConfig
{
    public const string DefaultLightPartId = "LightPart";

    public ShinyGridLayout Layout { get; set; } = ShinyGridLayout.Flat;
    public int Width { get; set; } = 8;
    public int Height { get; set; } = 8;
    public float Spacing { get; set; } = 0.75f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 3f;
    public float OffsetZ { get; set; } = 2f;
    public double PartScale { get; set; } = 0.5;
    public string LightPartId { get; set; } = DefaultLightPartId;

    public int TotalParts => Width * Height;
}