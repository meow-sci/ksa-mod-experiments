using System;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;
namespace MeowSci.KittenAnimationsLib;
public sealed partial class KittenAnimationsSubmod
{
    private KittenAnimationRecipe _recipe = new();
    private string _kittenTarget = "";
    public void RenderContent()
    {
        SubmodUI.BeginContentArea("##kitten-recipe");
        if (ImGui.BeginCombo(MeowSci.KsaAbstractions.FormField.Label("Kitten target"), _kittenTarget.Length == 0 ? "Select…" : _kittenTarget))
        {
            if (ImGui.Selectable("Controlled kitten", _kittenTarget == "$controlled")) _kittenTarget = "$controlled";
            foreach (var kitten in VehicleProvider.GetAllVehicles().OfType<KittenEva>())
                if (ImGui.Selectable(kitten.Id, _kittenTarget == kitten.Id)) _kittenTarget = kitten.Id;
            ImGui.EndCombo();
        }
        var draftKitten = ResolveKitten(_kittenTarget);
        var draftAvatar = KittenAvatarAccessor.GetAvatar(draftKitten?.Renderable);
        var draftCatalog = draftAvatar == null ? null : KittenAnimationCatalog.Build(draftAvatar, draftKitten!.Renderable!);
        if (ImGui.BeginCombo(MeowSci.KsaAbstractions.FormField.Label("Clip"), _recipe.Clip.Length == 0 ? "Select…" : _recipe.Clip))
        {
            if (draftCatalog != null) foreach (var group in draftCatalog.Groups)
                foreach (var entry in group.Entries)
                { string id = group.Name + "/" + entry.Source + "/" + entry.Label;
                  if (ImGui.Selectable(id, id == _recipe.Clip)) _recipe.Clip = id; }
            ImGui.EndCombo();
        }
        if (WorkspaceUi.Header("Playback, strength and expression settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Checkbox("OverrideActive", ref _recipe.OverrideActive);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("BlendTime"), ref _recipe.BlendTime, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PlaybackRateScale"), ref _recipe.PlaybackRateScale, .01f);
            ImGui.Checkbox("Paused", ref _recipe.Paused);
            ImGui.Checkbox("OverrideEarWeight", ref _recipe.OverrideEarWeight);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EarWeight"), ref _recipe.EarWeight, .01f);
            ImGui.Checkbox("OverrideEyeLookAngle", ref _recipe.OverrideEyeLookAngle);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EyeLookAngleDeg"), ref _recipe.EyeLookAngleDeg, .01f);
            ImGui.Checkbox("OverrideEyePitch", ref _recipe.OverrideEyePitch);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("EyePitchDeg"), ref _recipe.EyePitchDeg, .01f);
            ImGui.Checkbox("OverridePersonalityWeight", ref _recipe.OverridePersonalityWeight);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PersonalityWeight"), ref _recipe.PersonalityWeight, .01f);
            ImGui.Checkbox("LimitReactiveExpression", ref _recipe.LimitReactiveExpression);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ReactiveExpressionMax"), ref _recipe.ReactiveExpressionMax, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ExpressionEaseInDuration"), ref _recipe.ExpressionEaseInDuration, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ExpressionHoldDuration"), ref _recipe.ExpressionHoldDuration, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ExpressionEaseOutDuration"), ref _recipe.ExpressionEaseOutDuration, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("ExpressionPeakWeight"), ref _recipe.ExpressionPeakWeight, .01f);
            ImGui.Checkbox("ExpressionLatch", ref _recipe.ExpressionLatch);
            ImGui.Combo(MeowSci.KsaAbstractions.FormField.Label("Expression"), ref _recipe.Expression, Enum.GetNames<KittenExpressionController.ExpressionType>(), 6);
            ImGui.SetNextItemWidth(-1); ImGui.InputInt("Variant (-1 = random)", ref _recipe.Variant);
        }
        if (WorkspaceUi.Header("Locomotion tuning"))
        {
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("AnimBlendTime"), ref _recipe.AnimBlendTime, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("IdleSpeedThreshold"), ref _recipe.IdleSpeedThreshold, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PlaybackRateMin"), ref _recipe.PlaybackRateMin, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("PlaybackRateMax"), ref _recipe.PlaybackRateMax, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("WalkClipNominalSpeed"), ref _recipe.WalkClipNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("RunClipNominalSpeed"), ref _recipe.RunClipNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LadderNominalSpeed"), ref _recipe.LadderNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("TumbleNominalSpeed"), ref _recipe.TumbleNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MoonwalkWalkNominalSpeed"), ref _recipe.MoonwalkWalkNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MoonwalkRunNominalSpeed"), ref _recipe.MoonwalkRunNominalSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MoonwalkStartGravity"), ref _recipe.MoonwalkStartGravity, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MoonwalkFullGravity"), ref _recipe.MoonwalkFullGravity, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("MoonwalkPlaybackScale"), ref _recipe.MoonwalkPlaybackScale, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("NominalSwimAnimSpeed"), ref _recipe.NominalSwimAnimSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("SwimBlendFullSpeed"), ref _recipe.SwimBlendFullSpeed, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("SwimBlendHalfLife"), ref _recipe.SwimBlendHalfLife, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("SwimEyePitchFactor"), ref _recipe.SwimEyePitchFactor, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("JumpLandDuration"), ref _recipe.JumpLandDuration, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("JumpLandBounceIgnoreTime"), ref _recipe.JumpLandBounceIgnoreTime, .01f);
            ImGui.DragFloat(MeowSci.KsaAbstractions.FormField.Label("LadderEyePitchDeg"), ref _recipe.LadderEyePitchDeg, .01f);
        }
        bool resolved = draftAvatar != null;
        if (!resolved) ImGui.TextDisabled("Take control of the selected kitten, or choose an available target.");
        ImGui.BeginDisabled(!resolved);
        if (ImGui.Button("Apply animation settings", new float2(-1, 0))) ApplyRecipe(false);
        if (ImGui.Button("Apply and play clip / expression", new float2(-1, 0))) ApplyRecipe(true);
        ImGui.EndDisabled();
        SubmodUI.EndContentArea();
    }
    private void ApplyRecipe(bool play)
    {
        _recipe.Validate();
        var kitten = ResolveKitten(_kittenTarget);
        var avatar = KittenAvatarAccessor.GetAvatar(kitten?.Renderable);
        if (kitten?.Renderable == null || avatar == null) return;
        var catalog = KittenAnimationCatalog.Build(avatar, kitten.Renderable);
        AnimationEntry? selected = null;
        foreach (var group in catalog.Groups)
            foreach (var entry in group.Entries)
                if (group.Name + "/" + entry.Source + "/" + entry.Label == _recipe.Clip) selected = entry;
        if (play && _recipe.Clip.Length > 0 && selected == null) { Console.WriteLine("kitten-animations: selected clip unresolved"); return; }
        if (!ReferenceEquals(avatar, _boundAvatar)) { Unbind(); Bind(kitten, kitten.Renderable, avatar); }
        _liveKittenTarget = _kittenTarget;
        _driver.TargetModel = avatar.Core.CharacterModel;
        _originalTuning ??= KittenLocomotionTuning.Current;
        _driver.OverrideActive = _recipe.OverrideActive;
        _driver.BlendTime = _recipe.BlendTime;
        _driver.PlaybackRateScale = _recipe.PlaybackRateScale;
        _driver.Paused = _recipe.Paused;
        _driver.OverrideEarWeight = _recipe.OverrideEarWeight;
        _driver.EarWeight = _recipe.EarWeight;
        _driver.OverrideEyeLookAngle = _recipe.OverrideEyeLookAngle;
        _driver.EyeLookAngleDeg = _recipe.EyeLookAngleDeg;
        _driver.OverrideEyePitch = _recipe.OverrideEyePitch;
        _driver.EyePitchDeg = _recipe.EyePitchDeg;
        _driver.OverridePersonalityWeight = _recipe.OverridePersonalityWeight;
        _driver.PersonalityWeight = _recipe.PersonalityWeight;
        _driver.LimitReactiveExpression = _recipe.LimitReactiveExpression;
        _driver.ReactiveExpressionMax = _recipe.ReactiveExpressionMax;
        _expressions.EaseInDuration = _recipe.ExpressionEaseInDuration;
        _expressions.HoldDuration = _recipe.ExpressionHoldDuration;
        _expressions.EaseOutDuration = _recipe.ExpressionEaseOutDuration;
        _expressions.PeakWeight = _recipe.ExpressionPeakWeight;
        _expressions.Latch = _recipe.ExpressionLatch;
        KittenLocomotionTuning.Current.AnimBlendTime = _recipe.AnimBlendTime;
        KittenLocomotionTuning.Current.IdleSpeedThreshold = _recipe.IdleSpeedThreshold;
        KittenLocomotionTuning.Current.PlaybackRateMin = _recipe.PlaybackRateMin;
        KittenLocomotionTuning.Current.PlaybackRateMax = _recipe.PlaybackRateMax;
        KittenLocomotionTuning.Current.WalkClipNominalSpeed = _recipe.WalkClipNominalSpeed;
        KittenLocomotionTuning.Current.RunClipNominalSpeed = _recipe.RunClipNominalSpeed;
        KittenLocomotionTuning.Current.LadderNominalSpeed = _recipe.LadderNominalSpeed;
        KittenLocomotionTuning.Current.TumbleNominalSpeed = _recipe.TumbleNominalSpeed;
        KittenLocomotionTuning.Current.MoonwalkWalkNominalSpeed = _recipe.MoonwalkWalkNominalSpeed;
        KittenLocomotionTuning.Current.MoonwalkRunNominalSpeed = _recipe.MoonwalkRunNominalSpeed;
        KittenLocomotionTuning.Current.MoonwalkStartGravity = _recipe.MoonwalkStartGravity;
        KittenLocomotionTuning.Current.MoonwalkFullGravity = _recipe.MoonwalkFullGravity;
        KittenLocomotionTuning.Current.MoonwalkPlaybackScale = _recipe.MoonwalkPlaybackScale;
        KittenLocomotionTuning.Current.NominalSwimAnimSpeed = _recipe.NominalSwimAnimSpeed;
        KittenLocomotionTuning.Current.SwimBlendFullSpeed = _recipe.SwimBlendFullSpeed;
        KittenLocomotionTuning.Current.SwimBlendHalfLife = _recipe.SwimBlendHalfLife;
        KittenLocomotionTuning.Current.SwimEyePitchFactor = _recipe.SwimEyePitchFactor;
        KittenLocomotionTuning.Current.JumpLandDuration = _recipe.JumpLandDuration;
        KittenLocomotionTuning.Current.JumpLandBounceIgnoreTime = _recipe.JumpLandBounceIgnoreTime;
        KittenLocomotionTuning.Current.LadderEyePitchDeg = _recipe.LadderEyePitchDeg;
        if (!play) return;
        if (selected != null) _driver.Play(selected);
        if (_recipe.Expression != 0) _expressions.Trigger(avatar, (KittenExpressionController.ExpressionType)_recipe.Expression, _recipe.Variant, _random);
    }
}
