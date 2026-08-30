using System;
using KSA;

namespace MeowSci.PyroLib;

/// <summary>
/// Builds a <see cref="PlumeData"/> from user-facing nozzle settings. Mirrors the maths in
/// <c>RocketNozzle.UpdatePlumeData</c> so the renderer sees exactly the kind of input a real engine produces,
/// but sourced from an isentropic chamber → throat → exit model instead of a live combustor.
/// Pressures are pascals (matching the game's internal units); ambient pressure is passed in pascals.
/// </summary>
public static class PlumePhysics
{
    private const float MaxExpansionAngle = 1.3962634f; // 80°, same clamp the game uses
    private const float MinFullyExpandedMachSq = 1.1024998f;

    public static bool TryCompute(NozzleSettings n, VolumetricExhaustTemplate template, float ambientPressurePa,
        out PlumeData plume)
    {
        plume = PlumeData.Zero;

        float exitRadius = Math.Max(n.ExitRadius, 0.001f);
        float throatRadius = Math.Clamp(n.ThroatRadius, 0.0005f, exitRadius * 0.999f);
        float gamma = Math.Clamp(n.Gamma, 1.05f, 1.67f);
        float gasConstant = Math.Max(n.GasConstant, 10f);
        float chamberPressure = Math.Max(n.ChamberPressureBar, 0.01f) * 100000f;
        float chamberTemperature = Math.Max(n.ChamberTemperatureK, 50f);

        var gas = new GasProperties { Gamma = gamma, SpecificGasConstant = gasConstant };

        // Isentropic expansion through the nozzle area ratio.
        float areaRatio = (exitRadius * exitRadius) / (throatRadius * throatRadius);
        float exitMach = RocketDesign.SolveMachNumberFromAreaRatio(gas, areaRatio);
        if (!float.IsFinite(exitMach) || exitMach < 1f) exitMach = 1f;
        float stagnationFactor = 1f + 0.5f * (gamma - 1f) * exitMach * exitMach;
        float exitPressure = chamberPressure * MathF.Pow(stagnationFactor, -gamma / (gamma - 1f));
        float exitTemperature = chamberTemperature / stagnationFactor;

        var exhaust = new GasConditions { Pressure = exitPressure, Temperature = exitTemperature };
        var chamber = new GasConditions { Pressure = chamberPressure, Temperature = chamberTemperature };

        float speedOfSound = gas.ComputeSpeedOfSound(exitTemperature);
        float exhaustVelocity = Math.Max(exitMach * speedOfSound, speedOfSound * 1.05f);
        float density = exhaust.ComputeDensity(gas);
        float mach = exhaustVelocity / speedOfSound;

        float ambient = Math.Max(ambientPressurePa, 0.0001f);
        float nozzlePressureRatio = chamberPressure / ambient;
        float jetExpansionRatio = exitPressure / ambient;
        float invJetExpansion = 1f / jetExpansionRatio;

        float expansionRadius = exitRadius * MathF.Sqrt(MathF.Pow(jetExpansionRatio, 1f / gamma));
        float expansionAngle = Math.Min(gas.ComputeSupersonicExpansionPressureAngle(invJetExpansion, mach), MaxExpansionAngle);

        float fullyExpandedSq = 2f / (gamma - 1f) * (MathF.Pow(nozzlePressureRatio, (gamma - 1f) / gamma) - 1f);
        float fullyExpandedMach = MathF.Sqrt(Math.Max(fullyExpandedSq, MinFullyExpandedMachSq));

        float diskMach = invJetExpansion <= 1f
            ? gas.ComputeSupersonicExpansionPressureMach(invJetExpansion, mach)
            : gas.ComputeSupersonicExpansionPressureMach(1f / invJetExpansion, mach);
        double machDiskAreaRatio = RocketDesign.ComputeAreaRatioFromMachNumber(diskMach, gamma);

        // "Apparent" exhaust velocity — the game slows the visual scroll for off-design jets.
        float velocityFactor = ((gamma - 1f) * mach * mach + 2f) / ((gamma + 1f) * mach * mach);
        float offDesign = Math.Clamp(1f - MathF.Min(jetExpansionRatio, invJetExpansion), 0f, 1f);
        if (jetExpansionRatio > 1f) offDesign *= 0.5f;
        float apparentVelocity = exhaustVelocity + (exhaustVelocity * velocityFactor - exhaustVelocity) * offDesign;

        float densityThreshold = ComputeMinVisibleDensity(template, exitRadius);
        if (!float.IsFinite(densityThreshold) || !float.IsFinite(density) || density <= 0f)
            return false;

        plume = new PlumeData
        {
            Gas = gas,
            Exhaust = exhaust,
            ApparentExhaustVelocity = apparentVelocity,
            NozzleExitRadius = exitRadius,
            NozzlePressureRatio = nozzlePressureRatio,
            JetExpansionRatio = jetExpansionRatio,
            ExpansionAngle = expansionAngle,
            ExpansionRadius = expansionRadius,
            Density = density,
            DensityThreshold = densityThreshold,
            MachNumber = mach,
            MachAngle = gas.ComputePrandtlMeyer(mach),
            FullyExpandedMach = fullyExpandedMach,
            DesignMach = exitMach,
            MachDiskAreaRatio = (float)machDiskAreaRatio,
            ExhaustTemperature = exitTemperature,
            ThroatRadius = throatRadius,
            ThroatDensity = chamber.ComputeDensity(gas),
            InletTemperature = chamberTemperature,
        };
        return true;
    }

    /// <summary>Same formula as <c>RocketNozzle.RecomputeGasVisibilityDensity</c>: the density below which the plume is invisible.</summary>
    public static float ComputeMinVisibleDensity(VolumetricExhaustTemplate template, float exitRadius)
    {
        const double epsilon = 0.0001;
        double diameter = exitRadius * 2.0;
        double emissiveLimit = epsilon / (template.Emission.Brightness.Value * diameter);
        double scatterLimit = -Math.Log(1.0 - epsilon / Math.Max(template.Absorption.ScatteringBrightness.Value, 1.0))
                              / (diameter * template.Absorption.Density.Value);
        return (float)Math.Min(scatterLimit, emissiveLimit);
    }

    /// <summary>Ambient pressure at the camera in pascals (the game's helper returns atmospheres).</summary>
    public static float AmbientPressurePa(Camera camera)
    {
        const double PaPerAtm = 101325.0;
        try { return (float)(PhysicalAtmosphereReference.GetAtmosphericPressure(camera) * PaPerAtm); }
        catch { return 0f; }
    }
}
