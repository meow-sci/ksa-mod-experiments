using System;
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
  public void OnBeforeUi(double dt) { }

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

      // Zoom Out Animation Configuration
      if (ImGui.CollapsingHeader("thing", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Indent();

        var avatar = GetKittenAvatar();
        if (null != avatar)
        {
          if (ImGui.Button("Idle Default"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuIdleDefaultAnim);
          if (ImGui.Button("Move Left"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveLeftLoopAnim);
          if (ImGui.Button("Move Right"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveRightLoopAnim);
          if (ImGui.Button("Move Forward"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveForwardLoopAnim);
          if (ImGui.Button("Move Backward"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveBackwardLoopAnim);
          if (ImGui.Button("Move Up"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveUpLoopAnim);
          if (ImGui.Button("Move Down"))
            PlayMmuAnimation(avatar, avatar.Animations.MmuAnimations.MmuMoveDownLoopAnim);
        }

        ImGui.Unindent();
      }

      // Close button
      if (ImGui.Button("Close"))
      {
        _windowVisible = false;
      }
    }
    ImGui.End();
  }

  private KSA.CharacterAvatar? GetKittenAvatar()
  {
    var vehicle = Program.ControlledVehicle;
    if (null == vehicle || !(vehicle is KittenEva kitten))
      return null;

    var renderableField = typeof(KittenEva).GetField("_renderable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var renderable = renderableField?.GetValue(kitten);

    var characterAvatarField = typeof(KSA.KittenRenderable).GetField("_characterAvatar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    return characterAvatarField?.GetValue(renderable) as KSA.CharacterAvatar;
  }

  private void PlayMmuAnimation(KSA.CharacterAvatar avatar, KSA.IAnimation? animation)
  {
    if (null == avatar || null == animation)
      return;

    try
    {
      avatar.Core.CharacterModel.SetAnimation(animation);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error playing animation: {ex.Message}");
    }
  }
}

