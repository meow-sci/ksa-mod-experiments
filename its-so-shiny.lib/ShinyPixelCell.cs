using MeowSci.KsaLights;
using Brutal.Numerics;
using KSA;

namespace MeowSci.ItsSoShinyLib;

public sealed class ShinyPixelCell
{
    public int Row { get; }
    public int Col { get; }
    public Part HostPart { get; }
    public Part LightPart { get; }

    public ShinyPixelCell(int row, int col, Part hostPart, Part lightPart)
    {
        Row = row;
        Col = col;
        HostPart = hostPart;
        LightPart = lightPart;
    }

    public void SetEnabled(bool enabled, float onIntensity)
    {
        var lightSwitch = LightPart.LightSwitch ?? LightPart.FullPart.LightSwitch;
        if (lightSwitch != null)
        {
            lightSwitch.LightIsActive = enabled;
            return;
        }

        LightController.ApplyIntensity(LightPart, enabled ? onIntensity : 0f);
    }

    public void ApplyAppearance(float3 color, float intensity)
    {
        LightController.ApplyColor(LightPart, color);
        LightController.ApplyIntensity(LightPart, intensity);
    }
}