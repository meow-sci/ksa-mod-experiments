using System;
using System.Linq;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;

namespace mod;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private Random _random = new Random();

  // Expression state tracking
  private enum ExpressionType { None, Angry, Awe, Happy, Sad, Scared }
  private ExpressionType _currentExpression = ExpressionType.None;
  private KSA.AnimationAssetRef? _currentExpressionAnim = null;
  private float _expressionTimer = 0f;
  private float _expressionDuration = 1.5f;  // Default 1.5 seconds
  private float _expressionEaseInTimer = 0f;
  private const float EXPRESSION_EASE_IN_DURATION = 0.25f;  // 250ms ease-in



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

      // Update expression timer and maintain weight
      if (_expressionTimer > 0f)
      {
        _expressionTimer -= (float)dt;
        _expressionEaseInTimer += (float)dt;
        
        // Calculate eased weight using ease-in function (quadratic)
        float easeInProgress = Math.Min(_expressionEaseInTimer / EXPRESSION_EASE_IN_DURATION, 1.0f);
        float easedWeight = easeInProgress * easeInProgress;  // Quadratic ease-in: t^2

        // Keep the expression weight maintained during the display period
        var avatar = GetKittenAvatar();
        if (avatar != null && _currentExpressionAnim != null)
        {
          var expressionProcessor = avatar.Core.CharacterModel.AnimProcessors
            .OfType<KSA.CatExpressionAnim>()
            .FirstOrDefault();
          
          if (expressionProcessor != null)
          {
            expressionProcessor.ExpressionWeight = easedWeight;
          }
        }
      }
      else if (_currentExpression != ExpressionType.None)
      {
        // Timer expired, stop maintaining the expression
        _currentExpression = ExpressionType.None;
        _currentExpressionAnim = null;
        _expressionEaseInTimer = 0f;
      }
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



      var avatar = GetKittenAvatar();
      if (null != avatar)
      {

        if (ImGui.CollapsingHeader("MMU Animations"))
        {
          if (ImGui.Button("Idle Default"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuIdleDefaultAnim);
          if (ImGui.Button("Move Left"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveLeftLoopAnim);
          if (ImGui.Button("Move Right"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveRightLoopAnim);
          if (ImGui.Button("Move Forward"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveForwardLoopAnim);
          if (ImGui.Button("Move Backward"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveBackwardLoopAnim);
          if (ImGui.Button("Move Up"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveUpLoopAnim);
          if (ImGui.Button("Move Down"))
            PlayAvatarAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveDownLoopAnim);
        }

        if (ImGui.CollapsingHeader("Expressions"))
        {
          // Duration slider
          ImGui.SliderFloat("Expression Duration (s)", ref _expressionDuration, 1.0f, 5.0f);
          ImGui.Separator();

          if (ImGui.Button("Angry"))
          {
            var anim = avatar.Expressions.Angry?[_random.Next(avatar.Expressions.Angry.Count)];
            TriggerExpression(ExpressionType.Angry, anim);
          }
          if (ImGui.Button("Awe"))
          {
            var anim = avatar.Expressions.Awe?[_random.Next(avatar.Expressions.Awe.Count)];
            TriggerExpression(ExpressionType.Awe, anim);
          }
          if (ImGui.Button("Happy"))
          {
            var anim = avatar.Expressions.Happy?[_random.Next(avatar.Expressions.Happy.Count)];
            TriggerExpression(ExpressionType.Happy, anim);
          }
          if (ImGui.Button("Sad"))
          {
            var anim = avatar.Expressions.Sad?[_random.Next(avatar.Expressions.Sad.Count)];
            TriggerExpression(ExpressionType.Sad, anim);
          }
          if (ImGui.Button("Scared"))
          {
            var anim = avatar.Expressions.Scared?[_random.Next(avatar.Expressions.Scared.Count)];
            TriggerExpression(ExpressionType.Scared, anim);
          }
        }

        if (ImGui.CollapsingHeader("Walking Animations"))
        {
          if (ImGui.Button("Running"))
          {
            var anim = avatar.Animations.WalkingAnimations.RunningAnim;
            PlayAvatarAnimation(avatar, anim);
          }
          if (ImGui.Button("Walking"))
          {
            var anim = avatar.Animations.WalkingAnimations.WalkingAnim;
            PlayAvatarAnimation(avatar, anim);
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

  private KittenEva? GetKitten()
  {
    var vehicle = Program.ControlledVehicle;
    if (null == vehicle || !(vehicle is KittenEva kitten))
      return null;

    return kitten;
  }

  private KSA.CharacterAvatar? GetKittenAvatar()
  {
    var kitten = GetKitten();
    if (null == kitten)
      return null;

    var renderableField = typeof(KittenEva).GetField("_renderable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var renderable = renderableField?.GetValue(kitten);

    var characterAvatarField = typeof(KSA.KittenRenderable).GetField("_characterAvatar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    return characterAvatarField?.GetValue(renderable) as KSA.CharacterAvatar;
  }

  private void PlayAvatarAnimation(KSA.CharacterAvatar avatar, KSA.IAnimation? animation)
  {
    if (null == avatar || null == animation)
      return;

    try
    {
      // Console.WriteLine($"Playing animation: {animation}");

      avatar.Core.CharacterModel.SetAnimation(animation);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error playing animation: {ex.Message}");
    }
  }

  private void SetExpressionAnimation(KSA.CharacterAvatar avatar, KSA.AnimationAssetRef? animation)
  {
    if (null == avatar || null == animation)
      return;

    try
    {
      var characterModel = avatar.Core.CharacterModel;

      var expressionProcessor = characterModel.AnimProcessors
        .OfType<KSA.CatExpressionAnim>()
        .FirstOrDefault();

      if (expressionProcessor != null)
      {
        // Console.WriteLine($"[EXPR] Setting expression animation: {animation}");
        expressionProcessor.ExpressionAnim = animation;
        expressionProcessor.ExpressionWeight = 0f;  // Start at 0, will be eased in by OnBeforeUi
      }
      else
      {
        Console.WriteLine("[EXPR] Warning: CatExpressionAnim processor not found");
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[EXPR] Error setting expression animation: {ex.Message}");
    }
  }

  private void TriggerExpression(ExpressionType expressionType, KSA.AnimationAssetRef? animation)
  {
    if (null == animation)
      return;

    var avatar = GetKittenAvatar();
    if (null == avatar)
      return;

    // Console.WriteLine($"[EXPR] Triggering expression: {expressionType} for {_expressionDuration}s");

    // Set the expression animation
    SetExpressionAnimation(avatar, animation);

    // Update state for maintenance in OnBeforeUi
    _currentExpression = expressionType;
    _currentExpressionAnim = animation;
    _expressionTimer = _expressionDuration;
    _expressionEaseInTimer = 0f;  // Reset ease-in timer for smooth fade-in
  }
}

