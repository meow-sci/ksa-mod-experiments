using System;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.KittenAnimationsLib;

public sealed class KittenAnimationsSubmod : ISubmod
{
    public string Name => "Kitten Animations";

    private readonly KittenAnimationController _animController = new();

    public void Initialize() { }

    public void Update(double dt)
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();
        _animController.Update(dt, avatar);
    }

    public void RenderContent()
    {
        var avatar = KittenAvatarAccessor.GetKittenAvatar();
        if (null == avatar) return;

        if (ImGui.CollapsingHeader("MMU Animations"))
        {
            if (ImGui.Button("Idle Default"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuIdleDefaultAnim);
            if (ImGui.Button("Move Left"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveLeftLoopAnim);
            if (ImGui.Button("Move Right"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveRightLoopAnim);
            if (ImGui.Button("Move Forward"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveForwardLoopAnim);
            if (ImGui.Button("Move Backward"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveBackwardLoopAnim);
            if (ImGui.Button("Move Up"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveUpLoopAnim);
            if (ImGui.Button("Move Down"))
                KittenAnimationController.PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveDownLoopAnim);
        }

        if (ImGui.CollapsingHeader("Expressions"))
        {
            var duration = _animController.ExpressionDuration;
            ImGui.SliderFloat("Expression Duration (s)", ref duration, 1.0f, 5.0f);
            _animController.ExpressionDuration = duration;
            ImGui.Separator();

            if (ImGui.Button("Angry"))
            {
                var anim = avatar.Expressions.Angry?[_animController.Random.Next(avatar.Expressions.Angry.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Angry, anim, avatar);
            }
            if (ImGui.Button("Awe"))
            {
                var anim = avatar.Expressions.Awe?[_animController.Random.Next(avatar.Expressions.Awe.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Awe, anim, avatar);
            }
            if (ImGui.Button("Happy"))
            {
                var anim = avatar.Expressions.Happy?[_animController.Random.Next(avatar.Expressions.Happy.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Happy, anim, avatar);
            }
            if (ImGui.Button("Sad"))
            {
                var anim = avatar.Expressions.Sad?[_animController.Random.Next(avatar.Expressions.Sad.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Sad, anim, avatar);
            }
            if (ImGui.Button("Scared"))
            {
                var anim = avatar.Expressions.Scared?[_animController.Random.Next(avatar.Expressions.Scared.Count)];
                _animController.TriggerExpression(KittenAnimationController.ExpressionType.Scared, anim, avatar);
            }
        }

        if (ImGui.CollapsingHeader("Walking Animations"))
        {
            if (ImGui.Button("Running"))
            {
                var anim = avatar.Animations.WalkingAnimations.RunningAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }
            if (ImGui.Button("Walking"))
            {
                var anim = avatar.Animations.WalkingAnimations.WalkingAnim;
                KittenAnimationController.PlayAvatarAnimation(avatar, anim);
            }
        }
    }

    public void Dispose() { }
}
