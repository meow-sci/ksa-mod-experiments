using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.CameraControllerOverrideLib.UI;
using MeowSci.CameraControllerOverrideLib.Animation;
using MeowSci.CameraControllerOverrideLib.Animation.Animations;

namespace MeowSci.CameraControllerOverride;

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
  private int _zoomOutEasing = (int)EasingType.EaseOut;
  private float _zoomOutEasingPowerStart = 3.0f;
  private float _zoomOutEasingPowerEnd = 3.0f;

  // Zoom In configuration
  private float _zoomInSpeed = 25.0f;
  private float _zoomInDuration = 5.0f;
  private int _zoomInEasing = (int)EasingType.EaseOut;
  private float _zoomInEasingPowerStart = 3.0f;
  private float _zoomInEasingPowerEnd = 3.0f;

  // Zoom In To Offset configuration
  private float _zoomInOffsetSpeed = 25.0f;
  private float _zoomInOffsetDuration = 5.0f;
  private int _zoomInOffsetEasing = (int)EasingType.EaseOut;
  private float _zoomInOffsetEasingPowerStart = 3.0f;
  private float _zoomInOffsetEasingPowerEnd = 3.0f;
  private float _zoomInOffsetX = 0.0f;   // meters
  private float _zoomInOffsetY = 0.5f;   // meters (default: slightly above center)
  private float _zoomInOffsetZ = 0.0f;   // meters

  // Spiral Zoom In configuration
  private float _spiralZoomInSpeed = 25.0f;
  private float _spiralZoomInDuration = 5.0f;
  private int _spiralZoomInEasing = (int)EasingType.EaseOut;
  private float _spiralZoomInEasingPowerStart = 3.0f;
  private float _spiralZoomInEasingPowerEnd = 3.0f;
  private float _spiralZoomInDegrees = 360.0f;

  // Orbit configuration  
  private float _orbitDegrees = 360.0f;
  private float _orbitDuration = 5.0f;
  private int _orbitEasing = (int)EasingType.EaseOut;
  private float _orbitEasingPowerStart = 3.0f;
  private float _orbitEasingPowerEnd = 3.0f;

  // Loopy Orbit configuration
  private float _loopyOrbitDegrees = 720.0f;
  private float _loopyLoopInterval = 90.0f;
  private float _loopyAmplitude = 50.0f;
  private float _loopyDuration = 8.0f;
  private int _loopyEasing = (int)EasingType.EaseOut;
  private float _loopyEasingPowerStart = 3.0f;
  private float _loopyEasingPowerEnd = 3.0f;

  // Shake configuration
  private float _shakeDuration = 2.0f;
  private int _shakeCount = 4;
  private float _shakeAmplitude = 5.0f;  // degrees
  private float _shakeSpeed = 1.0f;       // speed modifier
  private int _shakeEasing = (int)EasingType.EaseInOut;
  private float _shakeEasingPowerStart = 3.0f;
  private float _shakeEasingPowerEnd = 3.0f;

  // Spiral Zoom Out configuration
  private float _spiralZoomOutSpeed = 25.0f;
  private float _spiralZoomOutDuration = 5.0f;
  private int _spiralZoomOutEasing = (int)EasingType.EaseOut;
  private float _spiralZoomOutEasingPowerStart = 3.0f;
  private float _spiralZoomOutEasingPowerEnd = 3.0f;
  private float _spiralZoomOutDegrees = 360.0f;

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
        
        // Conditional easing power sliders
        var zoomOutEasingType = (EasingType)_zoomOutEasing;
        if (zoomOutEasingType == EasingType.EaseIn || zoomOutEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##ZoomOut", ref _zoomOutEasingPowerStart, 1.0f, 6.0f);
        }
        if (zoomOutEasingType == EasingType.EaseOut || zoomOutEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##ZoomOut", ref _zoomOutEasingPowerEnd, 1.0f, 6.0f);
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence"))
        {
          var animation = new ZoomOutAnimation(
            speedMetersPerSecond: _zoomOutSpeed,
            durationSeconds: _zoomOutDuration,
            easing: (EasingType)_zoomOutEasing,
            easingPowerStart: _zoomOutEasingPowerStart,
            easingPowerEnd: _zoomOutEasingPowerEnd
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
        
        // Conditional easing power sliders
        var zoomInEasingType = (EasingType)_zoomInEasing;
        if (zoomInEasingType == EasingType.EaseIn || zoomInEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##ZoomIn", ref _zoomInEasingPowerStart, 1.0f, 6.0f);
        }
        if (zoomInEasingType == EasingType.EaseOut || zoomInEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##ZoomIn", ref _zoomInEasingPowerEnd, 1.0f, 6.0f);
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##ZoomIn"))
        {
          var animation = new ZoomInAnimation(
            speedMetersPerSecond: _zoomInSpeed,
            durationSeconds: _zoomInDuration,
            easing: (EasingType)_zoomInEasing,
            easingPowerStart: _zoomInEasingPowerStart,
            easingPowerEnd: _zoomInEasingPowerEnd
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
        
        // Conditional easing power sliders
        var zoomInOffsetEasingType = (EasingType)_zoomInOffsetEasing;
        if (zoomInOffsetEasingType == EasingType.EaseIn || zoomInOffsetEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##ZoomInOffset", ref _zoomInOffsetEasingPowerStart, 1.0f, 6.0f);
        }
        if (zoomInOffsetEasingType == EasingType.EaseOut || zoomInOffsetEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##ZoomInOffset", ref _zoomInOffsetEasingPowerEnd, 1.0f, 6.0f);
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
            easing: (EasingType)_zoomInOffsetEasing,
            offsetX: _zoomInOffsetX,
            offsetY: _zoomInOffsetY,
            offsetZ: _zoomInOffsetZ,
            easingPowerStart: _zoomInOffsetEasingPowerStart,
            easingPowerEnd: _zoomInOffsetEasingPowerEnd
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Spiral Zoom In Animation Configuration
      if (ImGui.CollapsingHeader("Spiral Zoom In Animation"))
      {
        ImGui.Indent();
        
        // Speed slider
        if (ImGui.SliderFloat("Speed (m/s)##SpiralZoomIn", ref _spiralZoomInSpeed, 1.0f, 250.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##SpiralZoomIn", ref _spiralZoomInDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##SpiralZoomIn", ref _spiralZoomInEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        // Conditional easing power sliders
        var spiralZoomInEasingType = (EasingType)_spiralZoomInEasing;
        if (spiralZoomInEasingType == EasingType.EaseIn || spiralZoomInEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##SpiralZoomIn", ref _spiralZoomInEasingPowerStart, 1.0f, 6.0f);
        }
        if (spiralZoomInEasingType == EasingType.EaseOut || spiralZoomInEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##SpiralZoomIn", ref _spiralZoomInEasingPowerEnd, 1.0f, 6.0f);
        }
        
        // Spiral Degrees slider
        if (ImGui.SliderFloat("Spiral Degrees##SpiralZoomIn", ref _spiralZoomInDegrees, -1080.0f, 1080.0f))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##SpiralZoomIn"))
        {
          var animation = new SpiralZoomInAnimation(
            speedMetersPerSecond: _spiralZoomInSpeed,
            durationSeconds: _spiralZoomInDuration,
            easing: (EasingType)_spiralZoomInEasing,
            spiralDegrees: _spiralZoomInDegrees,
            easingPowerStart: _spiralZoomInEasingPowerStart,
            easingPowerEnd: _spiralZoomInEasingPowerEnd
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
        
        // Conditional easing power sliders
        var shakeEasingType = (EasingType)_shakeEasing;
        if (shakeEasingType == EasingType.EaseIn || shakeEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##Shake", ref _shakeEasingPowerStart, 1.0f, 6.0f);
        }
        if (shakeEasingType == EasingType.EaseOut || shakeEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##Shake", ref _shakeEasingPowerEnd, 1.0f, 6.0f);
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
            easing: (EasingType)_shakeEasing,
            easingPowerStart: _shakeEasingPowerStart,
            easingPowerEnd: _shakeEasingPowerEnd
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
        
        // Conditional easing power sliders
        var orbitEasingType = (EasingType)_orbitEasing;
        if (orbitEasingType == EasingType.EaseIn || orbitEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##Orbit", ref _orbitEasingPowerStart, 1.0f, 6.0f);
        }
        if (orbitEasingType == EasingType.EaseOut || orbitEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##Orbit", ref _orbitEasingPowerEnd, 1.0f, 6.0f);
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##Orbit"))
        {
          var animation = new OrbitAnimation(
            degrees: _orbitDegrees,
            durationSeconds: _orbitDuration,
            easing: (EasingType)_orbitEasing,
            easingPowerStart: _orbitEasingPowerStart,
            easingPowerEnd: _orbitEasingPowerEnd
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
        
        // Conditional easing power sliders
        var loopyEasingType = (EasingType)_loopyEasing;
        if (loopyEasingType == EasingType.EaseIn || loopyEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##Loopy", ref _loopyEasingPowerStart, 1.0f, 6.0f);
        }
        if (loopyEasingType == EasingType.EaseOut || loopyEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##Loopy", ref _loopyEasingPowerEnd, 1.0f, 6.0f);
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
            easing: (EasingType)_loopyEasing,
            easingPowerStart: _loopyEasingPowerStart,
            easingPowerEnd: _loopyEasingPowerEnd
          );
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      ImGui.Separator();

      // Spiral Zoom Out Animation Configuration
      if (ImGui.CollapsingHeader("Spiral Zoom Out Animation"))
      {
        ImGui.Indent();
        
        // Speed slider
        if (ImGui.SliderFloat("Speed (m/s)##SpiralZoomOut", ref _spiralZoomOutSpeed, 1.0f, 250.0f))
        {
          // Value updated
        }
        
        // Duration slider
        if (ImGui.SliderFloat("Duration (s)##SpiralZoomOut", ref _spiralZoomOutDuration, 1.0f, 30.0f))
        {
          // Value updated
        }
        
        // Easing dropdown
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Easing##SpiralZoomOut", ref _spiralZoomOutEasing, easingNames, easingNames.Length))
        {
          // Value updated
        }
        
        // Conditional easing power sliders
        var spiralZoomOutEasingType = (EasingType)_spiralZoomOutEasing;
        if (spiralZoomOutEasingType == EasingType.EaseIn || spiralZoomOutEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (Start)##SpiralZoomOut", ref _spiralZoomOutEasingPowerStart, 1.0f, 6.0f);
        }
        if (spiralZoomOutEasingType == EasingType.EaseOut || spiralZoomOutEasingType == EasingType.EaseInOut)
        {
          ImGui.SliderFloat("Easing Power (End)##SpiralZoomOut", ref _spiralZoomOutEasingPowerEnd, 1.0f, 6.0f);
        }
        
        // Spiral Degrees slider (negative = counter-clockwise)
        if (ImGui.SliderFloat("Spiral Degrees##SpiralZoomOut", ref _spiralZoomOutDegrees, -1080.0f, 1080.0f))
        {
          // Value updated
        }
        
        ImGui.Spacing();
        
        // Add to Sequence button
        if (ImGui.Button("Add to Sequence##SpiralZoomOut"))
        {
          var animation = new SpiralZoomOutAnimation(
            speedMetersPerSecond: _spiralZoomOutSpeed,
            durationSeconds: _spiralZoomOutDuration,
            easing: (EasingType)_spiralZoomOutEasing,
            spiralDegrees: _spiralZoomOutDegrees,
            easingPowerStart: _spiralZoomOutEasingPowerStart,
            easingPowerEnd: _spiralZoomOutEasingPowerEnd
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

