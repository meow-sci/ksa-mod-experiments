using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using MeowSci.KittenAnimationsLib;

namespace MeowSci.KittenAnimations;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private KittenAnimationController _animController = new KittenAnimationController();



  [StarMapImmediateLoad]
  public void OnImmediateLoad() { }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Patcher.Patch();
      _isInitialized = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"kitten-animations: Error during initialization: {ex.Message}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt) 
  {
    try
    {
      if (!_isInitialized || _isDisposed) return;

      var avatar = KittenAvatarAccessor.GetKittenAvatar();
      _animController.Update(dt, avatar);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"kitten-animations: Error in OnBeforeUi: {ex.Message}");
    }
  }

  [StarMapAfterGui]
  public void OnAfterUi(double dt)
  {
    try
    {
      if (!_isInitialized || _isDisposed) return;

      if (ImGui.IsKeyPressed(ImGuiKey.F11))
        _windowVisible = !_windowVisible;

      if (_windowVisible)
        RenderWindow();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"kitten-animations: Error in OnAfterUi: {ex.Message}");
    }
  }

  [StarMapUnload]
  public void Unload()
  {
    try
    {
      Patcher.Unload();
      _isDisposed = true;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"kitten-animations: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    // Set initial window size
    ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("kitten-animations Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "kitten-animations");
      ImGui.Separator();

      var avatar = KittenAvatarAccessor.GetKittenAvatar();
      if (null != avatar)
      {

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
          // Duration slider
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



      // Close button
      if (ImGui.Button("Close"))
      {
        _windowVisible = false;
      }
    }
    ImGui.End();
  }
}

