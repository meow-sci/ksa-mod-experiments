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

      if (ImGui.CollapsingHeader("Zoom Out Animation"))
      {
        ImGui.Indent();
        
        // Status display
        string status = Patcher.IsAnimationEnabled 
          ? (Patcher.IsLerpingBack ? "Lerping Back..." : (Patcher.IsAnimationActive ? "Animation Running" : "Animation Starting...")) 
          : "Inactive";
        ImGui.Text($"Status: {status}");
        
        ImGui.Spacing();
        
        // Speed configuration
        float speed = (float)Patcher.AnimationSpeedMetersPerSecond;
        if (ImGui.SliderFloat("Speed (m/s)", ref speed, 1.0f, 250.0f))
        {
          Patcher.AnimationSpeedMetersPerSecond = speed;
        }
        
        // Animation duration configuration
        float duration = (float)Patcher.AnimationDurationSeconds;
        if (ImGui.SliderFloat("Duration (s)", ref duration, 1.0f, 30.0f))
        {
          Patcher.AnimationDurationSeconds = duration;
        }
        
        // Animation easing dropdown
        int currentMainEasing = (int)Patcher.MainAnimationEasingType;
        string[] mainEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Animation Easing", ref currentMainEasing, mainEasingNames, mainEasingNames.Length))
        {
          Patcher.MainAnimationEasingType = (EasingType)currentMainEasing;
        }
        
        // Lerp back toggle
        bool lerpBack = Patcher.LerpBackEnabled;
        if (ImGui.Checkbox("Lerp Back to Start", ref lerpBack))
        {
          Patcher.LerpBackEnabled = lerpBack;
        }
        
        // Lerp back duration configuration
        float lerpDuration = (float)Patcher.LerpBackDurationSeconds;
        if (ImGui.SliderFloat("Lerp Duration (s)", ref lerpDuration, 1.0f, 10.0f))
        {
          Patcher.LerpBackDurationSeconds = lerpDuration;
        }
        
        // Easing function dropdown
        int currentEasing = (int)Patcher.LerpBackEasingType;
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Lerp Easing", ref currentEasing, easingNames, easingNames.Length))
        {
          Patcher.LerpBackEasingType = (EasingType)currentEasing;
        }
        
        // Progress display (always visible)
        string elapsedText;
        float progress;
        
        if (Patcher.IsLerpingBack)
        {
          elapsedText = $"Lerp Back: {Patcher.DistanceTraveledReturn:F1}m / {Patcher.DistanceTraveledForward:F1}m";
          progress = (float)Patcher.LerpBackProgress;
        }
        else if (Patcher.IsAnimationActive)
        {
          elapsedText = $"Elapsed: {Patcher.AnimationElapsedTime:F2}s / {Patcher.AnimationDurationSeconds:F2}s";
          progress = (float)(Patcher.AnimationElapsedTime / Patcher.AnimationDurationSeconds);
        }
        else
        {
          elapsedText = $"Elapsed: 0.00s / {Patcher.AnimationDurationSeconds:F2}s";
          progress = 0.0f;
        }
        
        ImGui.Text(elapsedText);
        ImGui.ProgressBar(progress, new float2(-1, 0));
        
        ImGui.Spacing();
        
        // Toggle button
        string buttonLabel = Patcher.IsAnimationEnabled ? "Stop Animation" : "Run Animation";
        if (ImGui.Button(buttonLabel))
          Patcher.IsAnimationEnabled = !Patcher.IsAnimationEnabled;
        
        ImGui.SameLine();
        if (ImGui.Button("Add to Sequence"))
        {
          var animation = new ZoomOutAnimation(
            speedMetersPerSecond: Patcher.AnimationSpeedMetersPerSecond,
            durationSeconds: Patcher.AnimationDurationSeconds,
            easing: (Animation.EasingType)Patcher.MainAnimationEasingType);
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      
      // Orbit Animation Panel
      if (ImGui.CollapsingHeader("Orbit Animation"))
      {
        ImGui.Indent();
        
        // Status display
        string orbitStatus = Patcher.IsOrbitAnimationEnabled 
          ? (Patcher.IsOrbitLerpingBack ? "Lerping Back..." : (Patcher.IsOrbitAnimationActive ? "Orbiting..." : "Animation Starting...")) 
          : "Inactive";
        ImGui.Text($"Status: {orbitStatus}");
        
        ImGui.Spacing();
        
        // Orbit degrees slider
        float orbitDegrees = (float)Patcher.OrbitDegrees;
        if (ImGui.SliderFloat("Orbit Degrees", ref orbitDegrees, 90.0f, 1080.0f))
        {
          Patcher.OrbitDegrees = orbitDegrees;
        }
        
        // Orbit duration slider
        float orbitDuration = (float)Patcher.OrbitDurationSeconds;
        if (ImGui.SliderFloat("Duration (s)", ref orbitDuration, 1.0f, 30.0f))
        {
          Patcher.OrbitDurationSeconds = orbitDuration;
        }
        
        // Orbit easing dropdown
        int orbitEasing = (int)Patcher.OrbitEasingType;
        string[] orbitEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Animation Easing", ref orbitEasing, orbitEasingNames, orbitEasingNames.Length))
        {
          Patcher.OrbitEasingType = (EasingType)orbitEasing;
        }
        
        // Lerp back toggle
        bool orbitLerpBack = Patcher.OrbitLerpBackEnabled;
        if (ImGui.Checkbox("Lerp Back to Start", ref orbitLerpBack))
        {
          Patcher.OrbitLerpBackEnabled = orbitLerpBack;
        }
        
        // Lerp duration slider
        float orbitLerpDuration = (float)Patcher.OrbitLerpBackDurationSeconds;
        if (ImGui.SliderFloat("Lerp Duration (s)", ref orbitLerpDuration, 1.0f, 10.0f))
        {
          Patcher.OrbitLerpBackDurationSeconds = orbitLerpDuration;
        }
        
        // Lerp back easing dropdown
        int orbitLerpEasing = (int)Patcher.OrbitLerpBackEasingType;
        string[] orbitLerpEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Lerp Easing", ref orbitLerpEasing, orbitLerpEasingNames, orbitLerpEasingNames.Length))
        {
          Patcher.OrbitLerpBackEasingType = (EasingType)orbitLerpEasing;
        }
        
        // Progress display
        string orbitElapsedText;
        float orbitProgress;
        
        if (Patcher.IsOrbitLerpingBack)
        {
          orbitElapsedText = $"Lerp Back: {Patcher.OrbitLerpBackElapsedTime:F2}s / {Patcher.OrbitLerpBackDurationSeconds:F2}s";
          orbitProgress = (float)(Patcher.OrbitLerpBackElapsedTime / Patcher.OrbitLerpBackDurationSeconds);
        }
        else if (Patcher.IsOrbitAnimationActive)
        {
          orbitElapsedText = $"Elapsed: {Patcher.OrbitAnimationElapsedTime:F2}s / {Patcher.OrbitDurationSeconds:F2}s";
          orbitProgress = (float)(Patcher.OrbitAnimationElapsedTime / Patcher.OrbitDurationSeconds);
        }
        else
        {
          orbitElapsedText = $"Elapsed: 0.00s / {Patcher.OrbitDurationSeconds:F2}s";
          orbitProgress = 0.0f;
        }
        
        ImGui.Text(orbitElapsedText);
        ImGui.ProgressBar(orbitProgress, new float2(-1, 0));
        
        ImGui.Spacing();
        
        // Toggle button
        string orbitButtonLabel = Patcher.IsOrbitAnimationEnabled ? "Stop Animation" : "Run Animation";
        if (ImGui.Button(orbitButtonLabel))
          Patcher.IsOrbitAnimationEnabled = !Patcher.IsOrbitAnimationEnabled;
        
        ImGui.SameLine();
        if (ImGui.Button("Add to Sequence##Orbit"))
        {
          var animation = new OrbitAnimation(
            degrees: Patcher.OrbitDegrees,
            durationSeconds: Patcher.OrbitDurationSeconds,
            easing: (Animation.EasingType)Patcher.OrbitEasingType);
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Spacing();
      }

      ImGui.Spacing();
      
      // Loopy Orbit Animation Panel
      if (ImGui.CollapsingHeader("Loopy Orbit Animation"))
      {
        ImGui.Indent();
        
        // Status display
        string loopyStatus = Patcher.IsLoopyOrbitEnabled 
          ? (Patcher.IsLoopyLerpingBack ? "Lerping Back..." : (Patcher.IsLoopyOrbitActive ? "Loopy Orbiting..." : "Animation Starting...")) 
          : "Inactive";
        ImGui.Text($"Status: {loopyStatus}");
        
        ImGui.Spacing();
        
        // Orbit degrees slider
        float loopyOrbitDegrees = (float)Patcher.LoopyOrbitDegrees;
        if (ImGui.SliderFloat("Orbit Degrees##Loopy", ref loopyOrbitDegrees, 90.0f, 1080.0f))
        {
          Patcher.LoopyOrbitDegrees = loopyOrbitDegrees;
        }
        
        // Loop interval slider
        float loopInterval = (float)Patcher.LoopyLoopIntervalDegrees;
        if (ImGui.SliderFloat("Loop Interval (deg)", ref loopInterval, 30.0f, 180.0f))
        {
          Patcher.LoopyLoopIntervalDegrees = loopInterval;
        }
        
        // Amplitude slider
        float amplitude = (float)Patcher.LoopyAmplitudeMeters;
        if (ImGui.SliderFloat("Amplitude (m)", ref amplitude, 1.0f, 500.0f))
        {
          Patcher.LoopyAmplitudeMeters = amplitude;
        }
        
        // Duration slider
        float loopyDuration = (float)Patcher.LoopyOrbitDurationSeconds;
        if (ImGui.SliderFloat("Duration (s)##Loopy", ref loopyDuration, 1.0f, 60.0f))
        {
          Patcher.LoopyOrbitDurationSeconds = loopyDuration;
        }
        
        // Animation easing dropdown
        int loopyEasing = (int)Patcher.LoopyOrbitEasingType;
        string[] loopyEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Animation Easing##Loopy", ref loopyEasing, loopyEasingNames, loopyEasingNames.Length))
        {
          Patcher.LoopyOrbitEasingType = (EasingType)loopyEasing;
        }
        
        // Lerp back toggle
        bool loopyLerpBack = Patcher.LoopyLerpBackEnabled;
        if (ImGui.Checkbox("Lerp Back to Start##Loopy", ref loopyLerpBack))
        {
          Patcher.LoopyLerpBackEnabled = loopyLerpBack;
        }
        
        // Lerp duration slider
        float loopyLerpDuration = (float)Patcher.LoopyLerpBackDurationSeconds;
        if (ImGui.SliderFloat("Lerp Duration (s)##Loopy", ref loopyLerpDuration, 1.0f, 10.0f))
        {
          Patcher.LoopyLerpBackDurationSeconds = loopyLerpDuration;
        }
        
        // Lerp easing dropdown
        int loopyLerpEasing = (int)Patcher.LoopyLerpBackEasingType;
        string[] loopyLerpEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("Lerp Easing##Loopy", ref loopyLerpEasing, loopyLerpEasingNames, loopyLerpEasingNames.Length))
        {
          Patcher.LoopyLerpBackEasingType = (EasingType)loopyLerpEasing;
        }
        
        // Progress display
        string loopyElapsedText;
        float loopyProgress;
        
        if (Patcher.IsLoopyLerpingBack)
        {
          loopyElapsedText = $"Lerp Back: {Patcher.LoopyLerpBackElapsedTime:F2}s / {Patcher.LoopyLerpBackDurationSeconds:F2}s";
          loopyProgress = (float)(Patcher.LoopyLerpBackElapsedTime / Patcher.LoopyLerpBackDurationSeconds);
        }
        else if (Patcher.IsLoopyOrbitActive)
        {
          loopyElapsedText = $"Elapsed: {Patcher.LoopyOrbitElapsedTime:F2}s / {Patcher.LoopyOrbitDurationSeconds:F2}s";
          loopyProgress = (float)(Patcher.LoopyOrbitElapsedTime / Patcher.LoopyOrbitDurationSeconds);
        }
        else
        {
          loopyElapsedText = $"Elapsed: 0.00s / {Patcher.LoopyOrbitDurationSeconds:F2}s";
          loopyProgress = 0.0f;
        }
        
        ImGui.Text(loopyElapsedText);
        ImGui.ProgressBar(loopyProgress, new float2(-1, 0));
        
        ImGui.Spacing();
        
        // Toggle button
        string loopyButtonLabel = Patcher.IsLoopyOrbitEnabled ? "Stop Animation" : "Run Animation";
        if (ImGui.Button(loopyButtonLabel + "##Loopy"))
          Patcher.IsLoopyOrbitEnabled = !Patcher.IsLoopyOrbitEnabled;
        
        ImGui.SameLine();
        if (ImGui.Button("Add to Sequence##Loopy"))
        {
          var animation = new LoopyOrbitAnimation(
            degrees: Patcher.LoopyOrbitDegrees,
            loopIntervalDegrees: Patcher.LoopyLoopIntervalDegrees,
            amplitudeMeters: Patcher.LoopyAmplitudeMeters,
            durationSeconds: Patcher.LoopyOrbitDurationSeconds,
            easing: (Animation.EasingType)Patcher.LoopyOrbitEasingType);
          Patcher.SequencePlayer.AddKeyframe(animation);
        }
        
        ImGui.Spacing();
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
