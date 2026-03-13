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

  // Weld state
#pragma warning disable CS0414, CS0649
  private bool _isWelded = false;
  private Vehicle? _sourceVehicle;
  private Vehicle? _targetVehicle;
  private double3 _offsetInTargetBody;
  private doubleQuat _rotationOffset;
#pragma warning restore CS0414, CS0649


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
      Console.WriteLine($"garys-torch: Error during initialization: {ex.Message}");
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
      Console.WriteLine($"garys-torch: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"garys-torch: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    // Set initial window size
    ImGui.SetNextWindowSize(new float2(600, 800), ImGuiCond.FirstUseEver);

    // Begin window
    if (ImGui.Begin("garys-torch Mod", ref _windowVisible))
    {
      // Header
      ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "garys-torch");
      ImGui.Separator();

      // Zoom Out Animation Configuration
      if (ImGui.CollapsingHeader("thing", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Indent();
        
        if (ImGui.Button("press me"))
        {
          Console.WriteLine("button pressed!");
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

  private void InitiateWeld()
  {
    if (_sourceVehicle == null || _targetVehicle == null) return;

    // Get positions in CCI (inertial frame, relative to shared parent body)
    double3 srcPosCci = _sourceVehicle.GetPositionCci();
    double3 tgtPosCci = _targetVehicle.GetPositionCci();

    // Offset in CCI
    double3 offsetCci = srcPosCci - tgtPosCci;

    // Transform offset into target's body frame so it rotates with the target
    doubleQuat tgtBody2Cci = _targetVehicle.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();
    _offsetInTargetBody = offsetCci.Transform(cci2TgtBody);

    // Capture relative rotation: source orientation relative to target
    // To recover: newSrcBody2Cci = _rotationOffset * tgtBody2Cci
    doubleQuat srcBody2Cci = _sourceVehicle.GetBody2Cci();
    _rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);

    _isWelded = true;

    Console.WriteLine($"garys-torch: Welded {_sourceVehicle.Id} to {_targetVehicle.Id}");
    Console.WriteLine($"garys-torch: Offset (target body): {_offsetInTargetBody}");
  }
}

