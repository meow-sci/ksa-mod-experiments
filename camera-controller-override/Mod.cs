using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using mod.UI;
using mod.Animation;
using mod.Animation.Animations;

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
      Console.WriteLine($"camera-controller-override: Error during initialization: {ex.Message}");
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
      Console.WriteLine($"camera-controller-override: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"camera-controller-override: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    // Set initial window size (larger for camera controls with orbit animation and keyframe sequence)
    ImGui.SetNextWindowSize(new float2(600, 1200), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("camera-controller-override Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "camera-controller-override");
      ImGui.Separator();

      // TODO: UI will be rebuilt in Task 5 to work with sequence player only
      ImGui.Text("Standalone animation UI removed - will be rebuilt in Task 5");
      ImGui.Spacing();
      ImGui.Separator();
      
      // Keyframe Sequence Panel (this still works)
      if (ImGui.CollapsingHeader("Keyframe Sequence"))
      {
        ImGui.Indent();
        KeyframeSequencePanel.Render(Patcher.SequencePlayer);
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Close button
      if (ImGui.Button("Close"))
      {
        _windowVisible = false;
      }
    }
    ImGui.End();
  }

      /* TEMPORARILY COMMENTED OUT - WILL BE REBUILT IN TASK 5
      ============================================================
       All standalone animation UI code has been removed because
       Patcher no longer has those properties. This will be rebuilt
       in Task 5 to work with the sequence player.
      ============================================================
      */
}

