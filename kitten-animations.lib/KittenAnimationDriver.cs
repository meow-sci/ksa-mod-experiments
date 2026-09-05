using System;
using KSA;

namespace MeowSci.KittenAnimationsLib;

/// <summary>
/// Holds the mod's animation overrides and stamps them onto the kitten model at the one point in the
/// frame where they survive.
///
/// KittenRenderable.UpdateRenderData re-picks the body clip every frame from the locomotion state
/// (idle/walk/run/jump/tumble/ladder/swim/MMU blend), so a clip set from a StarMap callback is
/// overwritten before it is ever sampled. <see cref="ApplyBeforePose"/> runs from a Harmony prefix on
/// AnimatedRenderable.UpdateAnimation — after the game has finished choosing, before the pose is
/// evaluated — so whatever it sets is what gets rendered.
/// </summary>
public sealed class KittenAnimationDriver
{
    private bool _wasOverriding;
    private bool _restartRequested;

    /// <summary>The kitten body model the overrides apply to. Refreshed once per frame by the submod.</summary>
    public AnimatedRenderable? TargetModel { get; set; }

    /// <summary>The game's own animation processors on that model.</summary>
    private KittenAnimProcessors? _processors;
    private float? _earOriginal, _eyeOriginal, _personalityOriginal;
    public KittenAnimProcessors? Processors
    {
        get => _processors;
        set { if (!ReferenceEquals(_processors, value)) RestoreProcessors(); _processors = value; }
    }
    public bool HasOverrides => OverrideActive || OverrideEarWeight || OverrideEyeLookAngle ||
        OverrideEyePitch || OverridePersonalityWeight || LimitReactiveExpression;

    public void RestoreProcessors()
    {
        if (_earOriginal.HasValue && _processors?.Ear != null) _processors.Ear.ExpressionWeight = _earOriginal.Value;
        if (_eyeOriginal.HasValue && _processors?.Eye != null) _processors.Eye.MaxLookAtAngle = _eyeOriginal.Value;
        if (_personalityOriginal.HasValue && _processors?.Personality != null) _processors.Personality.ExpressionWeight = _personalityOriginal.Value;
        _earOriginal = _eyeOriginal = _personalityOriginal = null;
    }

    public void RestoreDisabledProcessors()
    {
        if (!OverrideEarWeight && _earOriginal.HasValue && _processors?.Ear != null)
        { _processors.Ear.ExpressionWeight = _earOriginal.Value; _earOriginal = null; }
        if (!OverrideEyeLookAngle && _eyeOriginal.HasValue && _processors?.Eye != null)
        { _processors.Eye.MaxLookAtAngle = _eyeOriginal.Value; _eyeOriginal = null; }
        if (!OverridePersonalityWeight && _personalityOriginal.HasValue && _processors?.Personality != null)
        { _processors.Personality.ExpressionWeight = _personalityOriginal.Value; _personalityOriginal = null; }
    }

    // --- clip override ---

    public bool OverrideActive { get; set; }
    public IAnimation? ForcedAnimation { get; private set; }
    public string ForcedLabel { get; private set; } = string.Empty;

    /// <summary>Cross-fade time (s) used when the forced clip changes.</summary>
    public float BlendTime { get; set; } = 0.15f;

    /// <summary>Multiplies the animation delta time, on top of the game's own playback rate.</summary>
    public float PlaybackRateScale { get; set; } = 1f;

    /// <summary>Freezes the forced clip on its current frame.</summary>
    public bool Paused { get; set; }

    // --- animation processor strength ---

    public bool OverrideEarWeight { get; set; }
    public float EarWeight { get; set; } = 1f;

    public bool OverrideEyeLookAngle { get; set; }
    public float EyeLookAngleDeg { get; set; } = 30f;

    public bool OverrideEyePitch { get; set; }
    public float EyePitchDeg { get; set; }

    public bool OverridePersonalityWeight { get; set; }
    public float PersonalityWeight { get; set; } = 1f;

    public bool LimitReactiveExpression { get; set; }
    public float ReactiveExpressionMax { get; set; } = 1f;

    /// <summary>Starts forcing an animation, restarting it from the top.</summary>
    public void Play(AnimationEntry entry)
    {
        ForcedAnimation = entry.Animation;
        ForcedLabel = entry.Label;
        OverrideActive = true;
        _restartRequested = true;
    }

    /// <summary>Restarts the current forced clip from its first frame.</summary>
    public void Restart() => _restartRequested = true;

    /// <summary>Hands the body animation back to the game, keeping the selected clip for re-enabling.</summary>
    public void Release()
    {
        OverrideActive = false;
        Paused = false;
    }

    /// <summary>Forgets the selected clip as well as releasing control.</summary>
    public void ClearClip()
    {
        Release();
        ForcedAnimation = null;
        ForcedLabel = string.Empty;
    }

    /// <summary>Clears every override and forgets the resolved model. Called on unload / kitten change.</summary>
    public void Reset()
    {
        Release();
        ForcedAnimation = null;
        ForcedLabel = string.Empty;
        PlaybackRateScale = 1f;
        OverrideEarWeight = false;
        OverrideEyeLookAngle = false;
        OverrideEyePitch = false;
        OverridePersonalityWeight = false;
        LimitReactiveExpression = false;
        TargetModel = null;
        Processors = null;
        _wasOverriding = false;
    }

    /// <summary>
    /// Applies the mod's overrides to <paramref name="model"/> if it is the kitten body. Called from the
    /// Harmony prefix on every AnimatedRenderable in the scene, so it stays cheap and never throws.
    /// </summary>
    public void ApplyBeforePose(AnimatedRenderable model, ref double dt)
    {
        var target = TargetModel;
        if (target == null || !ReferenceEquals(model, target)) return;

        try
        {
            ApplyProcessorOverrides();
            ApplyClipOverride(model, ref dt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error applying animation override: {ex.Message}");
            Reset();
        }
    }

    private void ApplyProcessorOverrides()
    {
        RestoreDisabledProcessors();
        var processors = Processors;
        if (processors == null) return;

        if (OverrideEarWeight && processors.Ear != null)
            { _earOriginal ??= processors.Ear.ExpressionWeight; processors.Ear.ExpressionWeight = EarWeight; }

        if (processors.Eye != null)
        {
            if (OverrideEyeLookAngle)
                { _eyeOriginal ??= processors.Eye.MaxLookAtAngle; processors.Eye.MaxLookAtAngle = EyeLookAngleDeg; }

            // The game rewrites LookPitchOffsetDeg every frame (0 unless on a ladder or swimming).
            if (OverrideEyePitch)
                processors.Eye.LookPitchOffsetDeg = EyePitchDeg;
        }

        if (OverridePersonalityWeight && processors.Personality != null)
            { _personalityOriginal ??= processors.Personality.ExpressionWeight; processors.Personality.ExpressionWeight = PersonalityWeight; }

        // The reactive face is driven from vehicle acceleration; we can only cap it after the fact.
        if (LimitReactiveExpression && processors.Reactive != null)
            processors.Reactive.ExpressionWeight = Math.Min(processors.Reactive.ExpressionWeight, ReactiveExpressionMax);
    }

    private void ApplyClipOverride(AnimatedRenderable model, ref double dt)
    {
        if (!OverrideActive || ForcedAnimation == null)
        {
            if (_wasOverriding)
            {
                model.FreezeAnimation = false;
                _wasOverriding = false;
            }
            return;
        }

        if (_restartRequested)
        {
            model.PlayAnimation(ForcedAnimation, BlendTime);
            _restartRequested = false;
        }
        else
        {
            // No-op when the clip is already current, so this is safe to call every frame.
            model.SetAnimation(ForcedAnimation, BlendTime);
        }

        model.FreezeAnimation = Paused;
        _wasOverriding = true;

        if (PlaybackRateScale != 1f)
            dt *= PlaybackRateScale;
    }
}
