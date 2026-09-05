using System;
using MeowSci.Unscience.Contracts;
using Brutal.Numerics;

namespace MeowSci.PyroLib;

/// <summary>
/// Named snapshot of every per-plume setting: template, position/rotation offsets, throttle,
/// nozzle physics and look overrides. The vehicle/part anchor is deliberately not part of a
/// preset — presets describe how a plume looks and behaves, not where it is welded.
/// </summary>
public sealed class PlumePreset
{
    public string TemplateId = "EngineALarge";
    public float3 Position;
    public float3 Rotation;
    public float Throttle = 1f;
    public NozzleSettings Nozzle = new();
    public float AbsorptionDensityScale = 1f;
    public float RefractionIntensity = 1f;

    public void Validate()
    {
        if (Nozzle == null || TemplateId == null) throw new InvalidOperationException("Missing plume settings.");
        DraftValueValidation.Range(Throttle, 0, 1, "Throttle");
        DraftValueValidation.Range(Nozzle.ExitRadius, .0001, 10000, "Exit radius");
        DraftValueValidation.Range(Nozzle.ThroatRadius, .0001, Nozzle.ExitRadius, "Throat radius");
        DraftValueValidation.Range(Nozzle.ChamberPressureBar, .0001, 1e8, "Chamber pressure");
        DraftValueValidation.Range(Nozzle.ChamberTemperatureK, .0001, 1e8, "Chamber temperature");
        DraftValueValidation.Range(Nozzle.Gamma, 1.0001, 10, "Gamma");
        DraftValueValidation.Range(Nozzle.GasConstant, .0001, 1e8, "Gas constant");
        DraftValueValidation.Range(AbsorptionDensityScale, 0, 1e8, "Absorption density");
        DraftValueValidation.Range(RefractionIntensity, 0, 1e8, "Refraction");
    }

    public PlumePreset Clone()
    {
        var clone = (PlumePreset)MemberwiseClone();
        clone.Nozzle = Nozzle.Clone();
        return clone;
    }

    /// <summary>Captures a plume's current settings as a preset.</summary>
    public static PlumePreset FromPlume(PlumeEntry plume) => new()
    {
        TemplateId = plume.TemplateId,
        Position = plume.Position,
        Rotation = plume.Rotation,
        Throttle = plume.Throttle,
        Nozzle = plume.Nozzle.Clone(),
        AbsorptionDensityScale = plume.AbsorptionDensityScale,
        RefractionIntensity = plume.RefractionIntensity,
    };
}
