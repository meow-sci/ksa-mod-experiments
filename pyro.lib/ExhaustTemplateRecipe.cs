using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
namespace MeowSci.PyroLib;
/// <summary>Detached authoring values for all supported shared exhaust-template controls.</summary>
public sealed class ExhaustTemplateRecipe
{
    public double AbsorptionDensity;
    public double AbsorptionScatteringBrightness;
    public double AbsorptionScatteringPhaseEccentricity;
    public double AbsorptionRefractionIntensity;
    public double EmissionBrightness;
    public double EmissionFlowMachDiamondsLeadIn;
    public double EmissionFlowMachDiamondsLeadOut;
    public double EmissionFlowMachDiamondsMiddleRadius;
    public double NoiseDensityNoiseSize;
    public double NoiseDensityNoiseIntensity;
    public double NoiseShapeNoiseSize;
    public double NoiseShapeNoiseIntensity;
    public double NoiseRadialShapeNoiseSize;
    public double NoiseRadialShapeNoiseIntensity;
    public double NoiseRadialShapeNoiseSpeed;
    public double NoiseRadialShapeNoiseBarrelShockIntensity;
    public double LengthWeightsRadiusWeight;
    public double LengthWeightsNozzlePressureWeight;
    public double LengthWeightsJetExpansionWeight;
    public double LengthWeightsExitMachNumberWeight;
    public bool AbsorptionFakeCleanBurnInAtmosphere;
    public int QualitySampleCount;
    public int QualitySelfShadowSampleCount;
    public bool QualityVolumetricVesselShadows;
    public float3 EmissionColorGradientColor0;
    public float3 EmissionColorGradientColor1;
    public float3 EmissionColorGradientColor2;
    public float3 EmissionColorGradientColor3;
    public static ExhaustTemplateRecipe Capture(VolumetricExhaustTemplate t) => new()
    {
        AbsorptionDensity = t.Absorption.Density.Value,
        AbsorptionScatteringBrightness = t.Absorption.ScatteringBrightness.Value,
        AbsorptionScatteringPhaseEccentricity = t.Absorption.ScatteringPhaseEccentricity.Value,
        AbsorptionRefractionIntensity = t.Absorption.RefractionIntensity.Value,
        EmissionBrightness = t.Emission.Brightness.Value,
        EmissionFlowMachDiamondsLeadIn = t.Emission.Flow.MachDiamonds.LeadIn.Value,
        EmissionFlowMachDiamondsLeadOut = t.Emission.Flow.MachDiamonds.LeadOut.Value,
        EmissionFlowMachDiamondsMiddleRadius = t.Emission.Flow.MachDiamonds.MiddleRadius.Value,
        NoiseDensityNoiseSize = t.Noise.DensityNoise.Size.Value,
        NoiseDensityNoiseIntensity = t.Noise.DensityNoise.Intensity.Value,
        NoiseShapeNoiseSize = t.Noise.ShapeNoise.Size.Value,
        NoiseShapeNoiseIntensity = t.Noise.ShapeNoise.Intensity.Value,
        NoiseRadialShapeNoiseSize = t.Noise.RadialShapeNoise.Size.Value,
        NoiseRadialShapeNoiseIntensity = t.Noise.RadialShapeNoise.Intensity.Value,
        NoiseRadialShapeNoiseSpeed = t.Noise.RadialShapeNoise.Speed.Value,
        NoiseRadialShapeNoiseBarrelShockIntensity = t.Noise.RadialShapeNoise.BarrelShockIntensity.Value,
        LengthWeightsRadiusWeight = t.LengthWeights.RadiusWeight.Value,
        LengthWeightsNozzlePressureWeight = t.LengthWeights.NozzlePressureWeight.Value,
        LengthWeightsJetExpansionWeight = t.LengthWeights.JetExpansionWeight.Value,
        LengthWeightsExitMachNumberWeight = t.LengthWeights.ExitMachNumberWeight.Value,
        AbsorptionFakeCleanBurnInAtmosphere = t.Absorption.FakeCleanBurnInAtmosphere.Value,
        QualitySampleCount = (int)t.Quality.SampleCount.Value,
        QualitySelfShadowSampleCount = (int)t.Quality.SelfShadowSampleCount.Value,
        QualityVolumetricVesselShadows = t.Quality.VolumetricVesselShadows,
        EmissionColorGradientColor0 = t.Emission.ColorGradient.Color0.Value.AsFloat3,
        EmissionColorGradientColor1 = t.Emission.ColorGradient.Color1.Value.AsFloat3,
        EmissionColorGradientColor2 = t.Emission.ColorGradient.Color2.Value.AsFloat3,
        EmissionColorGradientColor3 = t.Emission.ColorGradient.Color3.Value.AsFloat3,
    };
    public void Apply(VolumetricExhaustTemplate t)
    {
        t.Absorption.Density.Value = AbsorptionDensity;
        t.Absorption.ScatteringBrightness.Value = AbsorptionScatteringBrightness;
        t.Absorption.ScatteringPhaseEccentricity.Value = AbsorptionScatteringPhaseEccentricity;
        t.Absorption.RefractionIntensity.Value = AbsorptionRefractionIntensity;
        t.Emission.Brightness.Value = EmissionBrightness;
        t.Emission.Flow.MachDiamonds.LeadIn.Value = EmissionFlowMachDiamondsLeadIn;
        t.Emission.Flow.MachDiamonds.LeadOut.Value = EmissionFlowMachDiamondsLeadOut;
        t.Emission.Flow.MachDiamonds.MiddleRadius.Value = EmissionFlowMachDiamondsMiddleRadius;
        t.Noise.DensityNoise.Size.Value = NoiseDensityNoiseSize;
        t.Noise.DensityNoise.Intensity.Value = NoiseDensityNoiseIntensity;
        t.Noise.ShapeNoise.Size.Value = NoiseShapeNoiseSize;
        t.Noise.ShapeNoise.Intensity.Value = NoiseShapeNoiseIntensity;
        t.Noise.RadialShapeNoise.Size.Value = NoiseRadialShapeNoiseSize;
        t.Noise.RadialShapeNoise.Intensity.Value = NoiseRadialShapeNoiseIntensity;
        t.Noise.RadialShapeNoise.Speed.Value = NoiseRadialShapeNoiseSpeed;
        t.Noise.RadialShapeNoise.BarrelShockIntensity.Value = NoiseRadialShapeNoiseBarrelShockIntensity;
        t.LengthWeights.RadiusWeight.Value = LengthWeightsRadiusWeight;
        t.LengthWeights.NozzlePressureWeight.Value = LengthWeightsNozzlePressureWeight;
        t.LengthWeights.JetExpansionWeight.Value = LengthWeightsJetExpansionWeight;
        t.LengthWeights.ExitMachNumberWeight.Value = LengthWeightsExitMachNumberWeight;
        t.Absorption.FakeCleanBurnInAtmosphere.Value = AbsorptionFakeCleanBurnInAtmosphere;
        t.Quality.SampleCount.Value = QualitySampleCount;
        t.Quality.SelfShadowSampleCount.Value = QualitySelfShadowSampleCount;
        t.Quality.VolumetricVesselShadows = QualityVolumetricVesselShadows;
        t.Emission.ColorGradient.Color0 = new ColorRgbReference(EmissionColorGradientColor0); t.Emission.ColorGradient.Color0.OnDataLoad(new Mod());
        t.Emission.ColorGradient.Color1 = new ColorRgbReference(EmissionColorGradientColor1); t.Emission.ColorGradient.Color1.OnDataLoad(new Mod());
        t.Emission.ColorGradient.Color2 = new ColorRgbReference(EmissionColorGradientColor2); t.Emission.ColorGradient.Color2.OnDataLoad(new Mod());
        t.Emission.ColorGradient.Color3 = new ColorRgbReference(EmissionColorGradientColor3); t.Emission.ColorGradient.Color3.OnDataLoad(new Mod());
    }
    public void Render()
    {
        using var grid = new MeowSci.KsaAbstractions.FormGrid("exhaust-template-fields");
        { float v = (float)AbsorptionDensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AbsorptionDensity"), ref v, .01f)) AbsorptionDensity = v; }
        { float v = (float)AbsorptionScatteringBrightness; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AbsorptionScatteringBrightness"), ref v, .01f)) AbsorptionScatteringBrightness = v; }
        { float v = (float)AbsorptionScatteringPhaseEccentricity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AbsorptionScatteringPhaseEccentricity"), ref v, .01f)) AbsorptionScatteringPhaseEccentricity = v; }
        { float v = (float)AbsorptionRefractionIntensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AbsorptionRefractionIntensity"), ref v, .01f)) AbsorptionRefractionIntensity = v; }
        { float v = (float)EmissionBrightness; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EmissionBrightness"), ref v, .01f)) EmissionBrightness = v; }
        { float v = (float)EmissionFlowMachDiamondsLeadIn; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EmissionFlowMachDiamondsLeadIn"), ref v, .01f)) EmissionFlowMachDiamondsLeadIn = v; }
        { float v = (float)EmissionFlowMachDiamondsLeadOut; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EmissionFlowMachDiamondsLeadOut"), ref v, .01f)) EmissionFlowMachDiamondsLeadOut = v; }
        { float v = (float)EmissionFlowMachDiamondsMiddleRadius; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EmissionFlowMachDiamondsMiddleRadius"), ref v, .01f)) EmissionFlowMachDiamondsMiddleRadius = v; }
        { float v = (float)NoiseDensityNoiseSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseDensityNoiseSize"), ref v, .01f)) NoiseDensityNoiseSize = v; }
        { float v = (float)NoiseDensityNoiseIntensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseDensityNoiseIntensity"), ref v, .01f)) NoiseDensityNoiseIntensity = v; }
        { float v = (float)NoiseShapeNoiseSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseShapeNoiseSize"), ref v, .01f)) NoiseShapeNoiseSize = v; }
        { float v = (float)NoiseShapeNoiseIntensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseShapeNoiseIntensity"), ref v, .01f)) NoiseShapeNoiseIntensity = v; }
        { float v = (float)NoiseRadialShapeNoiseSize; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseRadialShapeNoiseSize"), ref v, .01f)) NoiseRadialShapeNoiseSize = v; }
        { float v = (float)NoiseRadialShapeNoiseIntensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseRadialShapeNoiseIntensity"), ref v, .01f)) NoiseRadialShapeNoiseIntensity = v; }
        { float v = (float)NoiseRadialShapeNoiseSpeed; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseRadialShapeNoiseSpeed"), ref v, .01f)) NoiseRadialShapeNoiseSpeed = v; }
        { float v = (float)NoiseRadialShapeNoiseBarrelShockIntensity; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NoiseRadialShapeNoiseBarrelShockIntensity"), ref v, .01f)) NoiseRadialShapeNoiseBarrelShockIntensity = v; }
        { float v = (float)LengthWeightsRadiusWeight; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LengthWeightsRadiusWeight"), ref v, .01f)) LengthWeightsRadiusWeight = v; }
        { float v = (float)LengthWeightsNozzlePressureWeight; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LengthWeightsNozzlePressureWeight"), ref v, .01f)) LengthWeightsNozzlePressureWeight = v; }
        { float v = (float)LengthWeightsJetExpansionWeight; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LengthWeightsJetExpansionWeight"), ref v, .01f)) LengthWeightsJetExpansionWeight = v; }
        { float v = (float)LengthWeightsExitMachNumberWeight; if (ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LengthWeightsExitMachNumberWeight"), ref v, .01f)) LengthWeightsExitMachNumberWeight = v; }
        ImGui.Checkbox(MeowSci.KsaAbstractions.FormField.Label("AbsorptionFakeCleanBurnInAtmosphere"), ref AbsorptionFakeCleanBurnInAtmosphere);
        ImGui.DragInt(MeowSci.KsaAbstractions.FormField.Label("QualitySampleCount"), ref QualitySampleCount, 1f, 0, 64);
        ImGui.DragInt(MeowSci.KsaAbstractions.FormField.Label("QualitySelfShadowSampleCount"), ref QualitySelfShadowSampleCount, 1f, 0, 64);
        ImGui.Checkbox(MeowSci.KsaAbstractions.FormField.Label("QualityVolumetricVesselShadows"), ref QualityVolumetricVesselShadows);
        ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("EmissionColorGradientColor0"), ref EmissionColorGradientColor0);
        ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("EmissionColorGradientColor1"), ref EmissionColorGradientColor1);
        ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("EmissionColorGradientColor2"), ref EmissionColorGradientColor2);
        ImGui.ColorEdit3(MeowSci.KsaAbstractions.FormField.Label("EmissionColorGradientColor3"), ref EmissionColorGradientColor3);
    }
}
