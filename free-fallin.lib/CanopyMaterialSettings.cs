using Brutal.Numerics;

namespace MeowSci.FreeFallinLib;

public sealed class CanopyMaterialSettings
{
    public CanopyTextureMode TextureMode { get; set; } = CanopyTextureMode.Stock;
    public string? TextureName { get; set; }
    public float4 Tint { get; set; } = float4.One;
    public float Brightness { get; set; } = 1f;
    public float DecalScale { get; set; } = 0.45f;
    public bool UseStockPbrMap { get; set; } = true;
    public float AmbientOcclusion { get; set; } = 1f;
    public float Roughness { get; set; } = 1f;
    public float Metallic { get; set; } = 1f;
}
