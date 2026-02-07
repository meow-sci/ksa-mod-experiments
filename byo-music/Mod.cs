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
      Console.WriteLine($"byo-music: Error during initialization: {ex.Message}");
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
      Console.WriteLine($"byo-music: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"byo-music: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    // Set initial window size
    ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("byo-music Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "byo-music");
      ImGui.Separator();

      // Zoom Out Animation Configuration
      if (ImGui.CollapsingHeader("thing", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Indent();
        
        if (ImGui.Button("Listen all ya'll"))
        {
          Console.WriteLine("This is Sabotage!!!");
          
          // var sabotageMulti = ModLibrary.Get<MultiSound>("SabotageMulti");
          // Console.WriteLine($"sabotageMulti: {sabotageMulti}");

          // sabotageMulti.Play();

          var sabotageMusic = ModLibrary.Get<MusicPlayList>("SabotageMusic");
          // Console.WriteLine($"sabotageMusic: {sabotageMusic}");
          ChannelWrapper? iChannel = null;
          // sabotageMusic.Play();
          sabotageMusic.PlayMusic(out iChannel);

          // KSA.GameAudio.PlaySound();
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
}

