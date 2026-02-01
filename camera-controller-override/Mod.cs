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

  // Zoom Out configuration
  private float _zoomOutSpeed = 25.0f;
  private float _zoomOutDuration = 5.0f;
  private int _zoomOutEasing = (int)Animation.EasingType.EaseOut;

  // Orbit configuration  
  private float _orbitDegrees = 270.0f;
  private float _orbitDuration = 5.0f;
  private int _orbitEasing = (int)Animation.EasingType.EaseOut;

  // Loopy Orbit configuration
  private float _loopyOrbitDegrees = 270.0f;
  private float _loopyLoopInterval = 90.0f;
  private float _loopyAmplitude = 50.0f;
  private float _loopyDuration = 8.0f;
  private int _loopyEasing = (int)Animation.EasingType.EaseOut;

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
    // Set initial window size
    ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("camera-controller-override Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "camera-controller-override");
      ImGui.Separator();

      // Zoom Out Animation Configuration
      if (ImGui.CollapsingHeader("Zoom Out Animation"))
      {
        ImGui.Indent();
        
        // Speed slider
        if (ImGui.SliderFloat("Speed (m/s)", ref _zoomOutSpeed, 1.0f, 250.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)", ref _zoomOutDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing", ref _zoomOutEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence"))
        {
          var animation = new ZoomOutAnimation(
            speedMetersPerSecond: _zoomOutSpeed,
            durationSeconds: _zoomOutDuration,
            easing: (Animation.EasingType)_zoomOutEasing
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Orbit Animation Configuration
      if (ImGui.CollapsingHeader("Orbit Animation"))
      {
        ImGui.Indent();
        
        // Degrees slider
        if (ImGui.SliderFloat("Degrees", ref _orbitDegrees, 0.0f, 360.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)", ref _orbitDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing", ref _orbitEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##Orbit"))
        {
          var animation = new OrbitAnimation(
            degrees: _orbitDegrees,
            durationSeconds: _orbitDuration,
            easing: (Animation.EasingType)_orbitEasing
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Loopy Orbit Animation Configuration
      if (ImGui.CollapsingHeader("Loopy Orbit Animation"))
      {
        ImGui.Indent();
        
        // Degrees slider
        if (ImGui.SliderFloat("Degrees", ref _loopyOrbitDegrees, 0.0f, 360.0f))
        {
          // Value updated
        }
        
        // Loop Interval slider
        if (ImGui.SliderFloat("Loop Interval", ref _loopyLoopInterval, 10.0f, 180.0f))
        {
          // Value updated
        }
        
        // Amplitude slider
        if (ImGui.SliderFloat("Amplitude", ref _loopyAmplitude, 10.0f, 200.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)", ref _loopyDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing", ref _loopyEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##Loopy"))
        {
          var animation = new LoopyOrbitAnimation(
            degrees: _loopyOrbitDegrees,
            loopIntervalDegrees: _loopyLoopInterval,
            amplitudeMeters: _loopyAmplitude,
            durationSeconds: _loopyDuration,
            easing: (Animation.EasingType)_loopyEasing
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();
      
      // Keyframe Sequence Panel
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
}

