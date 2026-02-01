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
    // Set initial window size (larger for camera controls)
    ImGui.SetNextWindowSize(new float2(600, 700), ImGuiCond.FirstUseEver);

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
