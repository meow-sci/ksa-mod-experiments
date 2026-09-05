using System;
using System.Collections.Generic;
using KSA;

namespace MeowSci.KsaLights;

/// <summary>Captures shared light fields once and restores them when their last owner releases.</summary>
public sealed class LightStateLease : IDisposable
{
    private static readonly MeowSci.Unscience.Contracts.SharedRestoration Originals = new();
    private readonly List<IDisposable> _leases = new();

    public LightStateLease(Part part, bool appearance = true)
    {
        if (appearance)
            foreach (var component in LightController.GetLightComponents(part.Template))
            {
                var light = (LightModule.TemplateData)component;
                Acquire(light, () =>
                {
                    var color = light.ColorRgb;
                    var r = color.R; var g = color.G; var b = color.B;
                    var indexed = color.IndexedColor;
                    var intensity = light.Intensity.Value;
                    return () =>
                    {
                        color.R = r; color.G = g; color.B = b; color.IndexedColor = indexed;
                        color.OnDataLoad(null!);
                        light.Intensity.Value = intensity;
                    };
                });
            }
        var power = part.LightSwitch ?? part.FullPart.LightSwitch;
        if (power != null) Acquire(power, () =>
        {
            bool enabled = power.LightIsActive;
            return () => power.LightIsActive = enabled;
        });
    }

    private void Acquire(object key, Func<Action> capture) => _leases.Add(Originals.Acquire(key, capture));

    public void Dispose()
    {
        for (int i = _leases.Count - 1; i >= 0; i--)
        {
            _leases[i].Dispose();
            _leases.RemoveAt(i);
        }
    }
}
