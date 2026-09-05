using Brutal.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

public sealed partial class KittenAnimationsSubmod
{
    public IEnumerable<ILiveStateItem> GetLiveItems()
    {
        if (_liveKittenTarget != null)
            yield return new LiveStateItem<KittenAnimationDriver>("kitten", "Kitten animation", _liveKittenTarget, _context == null ? "Target missing" : _driver.ForcedLabel, _driver, driver => { RenderRuntimeControls(); if (ImGui.Button("Release kitten overrides")) { Unbind(); _driver.Reset(); _liveKittenTarget = null; } });
        if (_originalTuning.HasValue) yield return new LiveStateItem<string>("tuning", "Locomotion tuning", "Global kitten policy", "policy", _ =>
        { ImGui.TextDisabled("Locomotion tuning is shared by kittens."); Ui.TuningSection.Render(); if (ImGui.Button("Restore original tuning", new float2(-1, 0))) RestoreTuning(); });
    }
    private void RestoreTuning()
    {
        if (!_originalTuning.HasValue) return;
        var original = _originalTuning.Value;
        KSA.KittenLocomotionTuning.Current.AnimBlendTime = original.AnimBlendTime;
        KSA.KittenLocomotionTuning.Current.IdleSpeedThreshold = original.IdleSpeedThreshold;
        KSA.KittenLocomotionTuning.Current.PlaybackRateMin = original.PlaybackRateMin;
        KSA.KittenLocomotionTuning.Current.PlaybackRateMax = original.PlaybackRateMax;
        KSA.KittenLocomotionTuning.Current.WalkClipNominalSpeed = original.WalkClipNominalSpeed;
        KSA.KittenLocomotionTuning.Current.RunClipNominalSpeed = original.RunClipNominalSpeed;
        KSA.KittenLocomotionTuning.Current.LadderNominalSpeed = original.LadderNominalSpeed;
        KSA.KittenLocomotionTuning.Current.TumbleNominalSpeed = original.TumbleNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkWalkNominalSpeed = original.MoonwalkWalkNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkRunNominalSpeed = original.MoonwalkRunNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkStartGravity = original.MoonwalkStartGravity;
        KSA.KittenLocomotionTuning.Current.MoonwalkFullGravity = original.MoonwalkFullGravity;
        KSA.KittenLocomotionTuning.Current.MoonwalkPlaybackScale = original.MoonwalkPlaybackScale;
        KSA.KittenLocomotionTuning.Current.NominalSwimAnimSpeed = original.NominalSwimAnimSpeed;
        KSA.KittenLocomotionTuning.Current.SwimBlendFullSpeed = original.SwimBlendFullSpeed;
        KSA.KittenLocomotionTuning.Current.SwimBlendHalfLife = original.SwimBlendHalfLife;
        KSA.KittenLocomotionTuning.Current.SwimEyePitchFactor = original.SwimEyePitchFactor;
        KSA.KittenLocomotionTuning.Current.JumpLandDuration = original.JumpLandDuration;
        KSA.KittenLocomotionTuning.Current.JumpLandBounceIgnoreTime = original.JumpLandBounceIgnoreTime;
        KSA.KittenLocomotionTuning.Current.LadderEyePitchDeg = original.LadderEyePitchDeg;
        _originalTuning = null;
    }
}
