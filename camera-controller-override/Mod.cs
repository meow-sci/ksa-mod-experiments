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

  // Zoom In configuration
  private float _zoomInSpeed = 25.0f;
  private float _zoomInDuration = 5.0f;
  private int _zoomInEasing = (int)Animation.EasingType.EaseOut;

  // Zoom In To Offset configuration
  private float _zoomInOffsetSpeed = 25.0f;
  private float _zoomInOffsetDuration = 5.0f;
  private int _zoomInOffsetEasing = (int)Animation.EasingType.EaseOut;
  private float _zoomInOffsetX = 0.0f;   // meters
  private float _zoomInOffsetY = 0.5f;   // meters (default: slightly above center)
  private float _zoomInOffsetZ = 0.0f;   // meters

  // Orbit configuration  
  private float _orbitDegrees = 360.0f;
  private float _orbitDuration = 5.0f;
  private int _orbitEasing = (int)Animation.EasingType.EaseOut;

  // Loopy Orbit configuration
  private float _loopyOrbitDegrees = 720.0f;
  private float _loopyLoopInterval = 90.0f;
  private float _loopyAmplitude = 50.0f;
  private float _loopyDuration = 8.0f;
  private int _loopyEasing = (int)Animation.EasingType.EaseOut;

  // Shake configuration
  private float _shakeDuration = 2.0f;
  private int _shakeCount = 4;
  private float _shakeAmplitude = 5.0f;  // degrees
  private float _shakeSpeed = 1.0f;       // speed modifier
  private int _shakeEasing = (int)Animation.EasingType.EaseInOut;

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
        if (ImGui.SliderFloat("Duration (s)##ZoomOut", ref _zoomOutDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##ZoomOut", ref _zoomOutEasing, easingNames, easingNames.Length))
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

      // Zoom In Animation Configuration
      if (ImGui.CollapsingHeader("Zoom In Animation"))
      {
        ImGui.Indent();
        
        // Speed slider
        if (ImGui.SliderFloat("Speed (m/s)##ZoomIn", ref _zoomInSpeed, 1.0f, 250.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##ZoomIn", ref _zoomInDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##ZoomIn", ref _zoomInEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##ZoomIn"))
        {
          var animation = new ZoomInAnimation(
            speedMetersPerSecond: _zoomInSpeed,
            durationSeconds: _zoomInDuration,
            easing: (Animation.EasingType)_zoomInEasing
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Zoom In To Offset Animation Configuration
      if (ImGui.CollapsingHeader("Zoom In To Offset Animation"))
      {
        ImGui.Indent();
        
        // Speed slider
        if (ImGui.SliderFloat("Speed (m/s)##ZoomInOffset", ref _zoomInOffsetSpeed, 1.0f, 250.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##ZoomInOffset", ref _zoomInOffsetDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##ZoomInOffset", ref _zoomInOffsetEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Offset sliders
        if (ImGui.SliderFloat("X Offset (m)##ZoomInOffset", ref _zoomInOffsetX, -20.0f, 20.0f))
        {
          // Value updated
        }
        
        if (ImGui.SliderFloat("Y Offset (m)##ZoomInOffset", ref _zoomInOffsetY, -20.0f, 20.0f))
        {
          // Value updated
        }
        
        if (ImGui.SliderFloat("Z Offset (m)##ZoomInOffset", ref _zoomInOffsetZ, -20.0f, 20.0f))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##ZoomInOffset"))
        {
          var animation = new ZoomInToOffsetAnimation(
            speedMetersPerSecond: _zoomInOffsetSpeed,
            durationSeconds: _zoomInOffsetDuration,
            easing: (Animation.EasingType)_zoomInOffsetEasing,
            offsetX: _zoomInOffsetX,
            offsetY: _zoomInOffsetY,
            offsetZ: _zoomInOffsetZ
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Shake Animation Configuration
      if (ImGui.CollapsingHeader("Shake Animation"))
      {
        ImGui.Indent();
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##Shake", ref _shakeDuration, 1.0f, 10.0f))
        {
          // Value updated
        }
        
        // Shake Count slider
        if (ImGui.SliderInt("Shake Count##Shake", ref _shakeCount, 1, 20))
        {
          // Value updated
        }
        
        // Amplitude slider
        if (ImGui.SliderFloat("Amplitude (degrees)##Shake", ref _shakeAmplitude, 1.0f, 45.0f))
        {
          // Value updated
        }
        
        // Speed slider
        if (ImGui.SliderFloat("Speed Modifier##Shake", ref _shakeSpeed, 0.5f, 3.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##Shake", ref _shakeEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##Shake"))
        {
          var animation = new ShakeAnimation(
            durationSeconds: _shakeDuration,
            shakeCount: _shakeCount,
            amplitudeDegrees: _shakeAmplitude,
            shakeSpeed: _shakeSpeed,
            easing: (Animation.EasingType)_shakeEasing
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
        if (ImGui.SliderFloat("Degrees##Orbit", ref _orbitDegrees, 90.0f, 1080.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##Orbit", ref _orbitDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##Orbit", ref _orbitEasing, easingNames, easingNames.Length))
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
        if (ImGui.SliderFloat("Degrees##Loopy", ref _loopyOrbitDegrees, 90.0f, 1080.0f))
        {
          // Value updated
        }
        
        // Loop Interval slider
        if (ImGui.SliderFloat("Loop Interval##Loopy", ref _loopyLoopInterval, 10.0f, 180.0f))
        {
          // Value updated
        }
        
        // Amplitude slider
        if (ImGui.SliderFloat("Amplitude##Loopy", ref _loopyAmplitude, 10.0f, 200.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##Loopy", ref _loopyDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##Loopy", ref _loopyEasing, easingNames, easingNames.Length))
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

