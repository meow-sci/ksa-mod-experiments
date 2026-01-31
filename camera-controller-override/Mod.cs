using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;

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
