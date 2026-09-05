using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.ZippoLib;

/// <summary>Owns module-local light templates; never writes the shared part template.</summary>
internal sealed class DiscoLight : IDisposable
{
    public Part Part { get; }
    public DiscoRecipe Recipe { get; }
    public bool Paused;
    public double Elapsed { get; private set; }
    public readonly List<KeyframeAnimationModule> Actuators = new();
    private readonly Dictionary<KeyframeAnimationModule, (float Original, float Written)> _goals = new();
    private readonly List<(LightModule Module, LightModule.TemplateData Original, LightModule.TemplateData Owned)> _lights = new();
    private readonly uint _seed;
    public int SpotCount => _lights.Count(l => l.Owned.Type == LightModule.TemplateData.LightType.Spot);
    public bool OwnsTemplates => _lights.All(l => ReferenceEquals(l.Module.Template, l.Owned));

    public DiscoLight(Part part, DiscoRecipe recipe)
    {
        Part = part;
        Recipe = DraftJson.Clone(recipe);
        Recipe.Validate();
        _seed = (uint)Random.Shared.Next();
        foreach (var module in part.Modules.Get<LightModule>())
        {
            var original = module.Template;
            var owned = new LightModule.TemplateData
            {
                Id = original.Id, Type = original.Type, Transform = original.Transform,
                Range = original.Range, Intensity = original.Intensity, ColorRgb = original.ColorRgb,
                InnerAngle = original.InnerAngle, OuterAngle = original.OuterAngle,
                RayTracing = original.RayTracing, DisableInIva = original.DisableInIva
            };
            if (Recipe.Color)
            {
                owned.ColorRgb = new ColorRgbReference((float3)original.ColorRgb);
                owned.ColorRgb.OnDataLoad(null!);
            }
            if (Recipe.Spread && owned.Type == LightModule.TemplateData.LightType.Spot)
            {
                owned.InnerAngle = new FloatReference(original.InnerAngle.Value);
                owned.OuterAngle = new FloatReference(original.OuterAngle.Value);
            }
            _lights.Add((module, original, owned));
            module.Template = owned;
        }
    }

    public void AddActuator(KeyframeAnimationModule module)
    {
        Actuators.Add(module); _goals[module] = (module.TimeGoal, module.TimeGoal);
    }

    public void ReleaseActuator(KeyframeAnimationModule module)
    {
        if (!_goals.Remove(module, out var goal)) return;
        if (module.TimeGoal == goal.Written) module.TimeGoal = goal.Original;
        Actuators.Remove(module);
    }

    public void Update(double dt)
    {
        if (Paused) return;
        if (double.IsFinite(dt) && dt > 0) Elapsed += dt;
        var (step, mix) = Recipe.ColorTiming.Sample(Elapsed);
        var start = ColorAt(step); var end = ColorAt(step + 1);
        float3 color = start + (end - start) * mix;
        var (spreadStep, spreadMix) = Recipe.SpreadTiming.Sample(Elapsed);
        if (spreadStep % 2 != 0) spreadMix = 1 - spreadMix;
        foreach (var (module, _, owned) in _lights)
        {
            if (!ReferenceEquals(module.Template, owned)) continue;
            if (Recipe.Color)
            {
                owned.ColorRgb.R = color.X; owned.ColorRgb.G = color.Y; owned.ColorRgb.B = color.Z;
                owned.ColorRgb.OnDataLoad(null!);
            }
            if (Recipe.Spread && owned.Type == LightModule.TemplateData.LightType.Spot)
            {
                owned.InnerAngle.Value = Lerp(Recipe.InnerMin, Recipe.InnerMax, spreadMix) * MathF.PI / 180;
                owned.OuterAngle.Value = Lerp(Recipe.OuterMin, Recipe.OuterMax, spreadMix) * MathF.PI / 180;
            }
        }
        var (actStep, actMix) = Recipe.ActuationTiming.Sample(Elapsed);
        if (actStep % 2 != 0) actMix = 1 - actMix;
        foreach (var actuator in Actuators)
        {
            float goal = Lerp(Recipe.ActuationMin, Recipe.ActuationMax, actMix) * actuator.Shared.Duration;
            actuator.TimeGoal = goal;
            _goals[actuator] = (_goals[actuator].Original, goal);
        }
    }

    private float3 ColorAt(long step)
    {
        if (!Recipe.RandomColors) return Recipe.Palette[(int)(step % Recipe.Palette.Count)];
        // Stable per-step random hue: no frame-dependent random draws and no catch-up loop.
        uint hash = unchecked((uint)step * 747796405u + _seed + 2891336453u);
        hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
        float h = ((hash >> 22) ^ hash) / (float)uint.MaxValue * 6;
        float x = 1 - MathF.Abs(h % 2 - 1);
        return ((int)h % 6) switch { 0 => new(1, x, 0), 1 => new(x, 1, 0), 2 => new(0, 1, x),
            3 => new(0, x, 1), 4 => new(x, 0, 1), _ => new(1, 0, x) };
    }
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public void Dispose()
    {
        foreach (var (module, original, owned) in _lights)
            if (ReferenceEquals(module.Template, owned)) module.Template = original;
        foreach (var (module, goal) in _goals)
            if (module.TimeGoal == goal.Written) module.TimeGoal = goal.Original;
        _lights.Clear(); Actuators.Clear(); _goals.Clear();
    }
}
