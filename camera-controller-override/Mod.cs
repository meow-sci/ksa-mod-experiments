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
  public void OnImmediateLoad()
  {
    Console.WriteLine("camera-controller-override OnImmediateLoad");
  }

  [StarMapAllModsLoaded]
  public void OnFullyLoaded()
  {
    try
    {
      Console.WriteLine("camera-controller-override OnFullyLoaded");
      Patcher.Patch();

      _isInitialized = true;


      Console.WriteLine("camera-controller-override: Initialized successfully.");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"camera-controller-override: Error during initialization: {ex}");
    }
  }

  [StarMapBeforeGui]
  public void OnBeforeUi(double dt)
  {
    // No pre-UI logic needed
  }

  [StarMapAfterGui]
  public void OnAfterUi(double dt)
  {
    try
    {
      if (!_isInitialized || _isDisposed)
        return;

      // Check F11 key press
      if (ImGui.IsKeyPressed(ImGuiKey.F11))
      {
        Console.WriteLine("camera-controller-override: F11 pressed, toggling window.");
        _windowVisible = !_windowVisible;
        var controller = Program.OnFrameViewport.GetActiveController();
      }

      // Render window if visible
      if (_windowVisible)
      {
        RenderWindow();
      }

    }
    catch (Exception ex)
    {
      Console.WriteLine($"camera-controller-override: Error in OnAfterUi: {ex}");
    }
  }

  [StarMapUnload]
  public void Unload()
  {
    try
    {
      Console.WriteLine("camera-controller-override Unload");
      Patcher.Unload();
      _isDisposed = true;
      Console.WriteLine("camera-controller-override: Unloaded successfully");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"camera-controller-override: Error during unload: {ex}");
    }
  }

  private void RenderWindow()
  {
    // Set initial window size (larger for camera controls with orbit animation)
    ImGui.SetNextWindowSize(new float2(600, 950), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("camera-controller-override Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "camera-controller-override");
      ImGui.Separator();

      if (ImGui.CollapsingHeader("Simple Movement", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Indent();
        
        // Status display
        string status = Patcher.IsAnimationEnabled 
          ? (Patcher.IsLerpingBack ? "Lerping Back..." : (Patcher.IsAnimationActive ? "Animation Running" : "Animation Starting...")) 
          : "Inactive";
        ImGui.Text($"Status: {status}");
        
        // Lerp back toggle
        bool lerpBack = Patcher.LerpBackEnabled;
        if (ImGui.Checkbox("Lerp Back to Start", ref lerpBack))
        {
          Patcher.LerpBackEnabled = lerpBack;
        }
        
        // Easing function dropdown
        ImGui.Text("Lerp Easing:");
        ImGui.SameLine();
        int currentEasing = (int)Patcher.LerpBackEasingType;
        string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("##LerpEasing", ref currentEasing, easingNames, easingNames.Length))
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
        ImGui.Text("Animation Easing:");
        ImGui.SameLine();
        int currentMainEasing = (int)Patcher.MainAnimationEasingType;
        string[] mainEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("##MainEasing", ref currentMainEasing, mainEasingNames, mainEasingNames.Length))
        {
          Patcher.MainAnimationEasingType = (EasingType)currentMainEasing;
        }
        
        // Lerp back duration configuration
        float lerpDuration = (float)Patcher.LerpBackDurationSeconds;
        if (ImGui.SliderFloat("Lerp Duration (s)", ref lerpDuration, 1.0f, 10.0f))
        {
          Patcher.LerpBackDurationSeconds = lerpDuration;
        }
        
        ImGui.Spacing();
        
        // Toggle button
        string buttonLabel = Patcher.IsAnimationEnabled ? "Stop Patching" : "Start Patching";
        if (ImGui.Button(buttonLabel))
        {
          Patcher.IsAnimationEnabled = !Patcher.IsAnimationEnabled;
          Console.WriteLine($"camera-controller-override: Animation {(Patcher.IsAnimationEnabled ? "enabled" : "disabled")}");
        }
        
        ImGui.Unindent();
      }

      ImGui.Spacing();
      
      // Orbit Animation Panel
      if (ImGui.CollapsingHeader("Orbit Animation", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Indent();
        
        // Status display
        string orbitStatus = Patcher.IsOrbitAnimationEnabled 
          ? (Patcher.IsOrbitLerpingBack ? "Lerping Back..." : (Patcher.IsOrbitAnimationActive ? "Orbiting..." : "Animation Starting...")) 
          : "Inactive";
        ImGui.Text($"Status: {orbitStatus}");
        
        // Lerp back toggle
        bool orbitLerpBack = Patcher.OrbitLerpBackEnabled;
        if (ImGui.Checkbox("Lerp Back to Start##Orbit", ref orbitLerpBack))
        {
          Patcher.OrbitLerpBackEnabled = orbitLerpBack;
        }
        
        // Lerp back easing dropdown
        ImGui.Text("Lerp Back Easing:");
        ImGui.SameLine();
        int orbitLerpEasing = (int)Patcher.OrbitLerpBackEasingType;
        string[] orbitLerpEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("##OrbitLerpEasing", ref orbitLerpEasing, orbitLerpEasingNames, orbitLerpEasingNames.Length))
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
        
        // Orbit degrees slider
        float orbitDegrees = (float)Patcher.OrbitDegrees;
        if (ImGui.SliderFloat("Orbit Degrees", ref orbitDegrees, 90.0f, 720.0f))
        {
          Patcher.OrbitDegrees = orbitDegrees;
        }
        
        // Orbit duration slider
        float orbitDuration = (float)Patcher.OrbitDurationSeconds;
        if (ImGui.SliderFloat("Orbit Duration (s)", ref orbitDuration, 1.0f, 30.0f))
        {
          Patcher.OrbitDurationSeconds = orbitDuration;
        }
        
        // Orbit easing dropdown
        ImGui.Text("Orbit Easing:");
        ImGui.SameLine();
        int orbitEasing = (int)Patcher.OrbitEasingType;
        string[] orbitEasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
        if (ImGui.Combo("##OrbitEasing", ref orbitEasing, orbitEasingNames, orbitEasingNames.Length))
        {
          Patcher.OrbitEasingType = (EasingType)orbitEasing;
        }
        
        // Lerp duration slider
        float orbitLerpDuration = (float)Patcher.OrbitLerpBackDurationSeconds;
        if (ImGui.SliderFloat("Lerp Duration (s)##Orbit", ref orbitLerpDuration, 1.0f, 10.0f))
        {
          Patcher.OrbitLerpBackDurationSeconds = orbitLerpDuration;
        }
        
        ImGui.Spacing();
        
        // Toggle button
        string orbitButtonLabel = Patcher.IsOrbitAnimationEnabled ? "Stop Orbit" : "Start Orbit";
        if (ImGui.Button(orbitButtonLabel))
        {
          Patcher.IsOrbitAnimationEnabled = !Patcher.IsOrbitAnimationEnabled;
          Console.WriteLine($"camera-controller-override: Orbit animation {(Patcher.IsOrbitAnimationEnabled ? "enabled" : "disabled")}");
        }
        
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
