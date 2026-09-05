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
        if (_context != null)
            yield return new LiveStateItem<KittenAnimationDriver>("kitten", "Kitten animation", _context.Kitten.Id, _driver.ForcedLabel, _driver, driver => RenderRuntimeControls());
        yield return new LiveStateItem<string>("tuning", "Locomotion tuning", "Global kitten policy", "policy", _ =>
        { ImGui.TextDisabled("Locomotion tuning is shared by kittens."); Ui.TuningSection.Render(); if (ImGui.Button("Restore tuning defaults", new float2(-1, 0))) RestoreTuning(); });
    }
    private static void RestoreTuning()
    {
        KSA.KittenLocomotionTuning.Current.AnimBlendTime = KSA.KittenLocomotionTuning.Default.AnimBlendTime;
        KSA.KittenLocomotionTuning.Current.IdleSpeedThreshold = KSA.KittenLocomotionTuning.Default.IdleSpeedThreshold;
        KSA.KittenLocomotionTuning.Current.PlaybackRateMin = KSA.KittenLocomotionTuning.Default.PlaybackRateMin;
        KSA.KittenLocomotionTuning.Current.PlaybackRateMax = KSA.KittenLocomotionTuning.Default.PlaybackRateMax;
        KSA.KittenLocomotionTuning.Current.WalkClipNominalSpeed = KSA.KittenLocomotionTuning.Default.WalkClipNominalSpeed;
        KSA.KittenLocomotionTuning.Current.RunClipNominalSpeed = KSA.KittenLocomotionTuning.Default.RunClipNominalSpeed;
        KSA.KittenLocomotionTuning.Current.LadderNominalSpeed = KSA.KittenLocomotionTuning.Default.LadderNominalSpeed;
        KSA.KittenLocomotionTuning.Current.TumbleNominalSpeed = KSA.KittenLocomotionTuning.Default.TumbleNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkWalkNominalSpeed = KSA.KittenLocomotionTuning.Default.MoonwalkWalkNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkRunNominalSpeed = KSA.KittenLocomotionTuning.Default.MoonwalkRunNominalSpeed;
        KSA.KittenLocomotionTuning.Current.MoonwalkStartGravity = KSA.KittenLocomotionTuning.Default.MoonwalkStartGravity;
        KSA.KittenLocomotionTuning.Current.MoonwalkFullGravity = KSA.KittenLocomotionTuning.Default.MoonwalkFullGravity;
        KSA.KittenLocomotionTuning.Current.MoonwalkPlaybackScale = KSA.KittenLocomotionTuning.Default.MoonwalkPlaybackScale;
        KSA.KittenLocomotionTuning.Current.NominalSwimAnimSpeed = KSA.KittenLocomotionTuning.Default.NominalSwimAnimSpeed;
        KSA.KittenLocomotionTuning.Current.SwimBlendFullSpeed = KSA.KittenLocomotionTuning.Default.SwimBlendFullSpeed;
        KSA.KittenLocomotionTuning.Current.SwimBlendHalfLife = KSA.KittenLocomotionTuning.Default.SwimBlendHalfLife;
        KSA.KittenLocomotionTuning.Current.SwimEyePitchFactor = KSA.KittenLocomotionTuning.Default.SwimEyePitchFactor;
        KSA.KittenLocomotionTuning.Current.JumpLandDuration = KSA.KittenLocomotionTuning.Default.JumpLandDuration;
        KSA.KittenLocomotionTuning.Current.JumpLandBounceIgnoreTime = KSA.KittenLocomotionTuning.Default.JumpLandBounceIgnoreTime;
        KSA.KittenLocomotionTuning.Current.LadderEyePitchDeg = KSA.KittenLocomotionTuning.Default.LadderEyePitchDeg;
    }
}
