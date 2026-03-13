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
#pragma warning disable CS0414
  private bool _isWelded = false;
#pragma warning restore CS0414
  private Vehicle? _sourceVehicle;
  private Vehicle? _targetVehicle;
  private double3 _offsetInTargetBody;
  private doubleQuat _rotationOffset;


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

  private void UpdateWeld()
  {
    if (_sourceVehicle == null || _targetVehicle == null) return;

    // Check vehicles share the same parent body (SOI change would break weld)
    if (_sourceVehicle.Parent != _targetVehicle.Parent)
    {
      Console.WriteLine("garys-torch: Parent body mismatch, unwelding");
      Unweld();
      return;
    }

    // Current target state in CCI (inertial frame)
    double3 tgtPosCci = _targetVehicle.GetPositionCci();
    double3 tgtVelCci = _targetVehicle.GetVelocityCci();
    doubleQuat tgtBody2Cci = _targetVehicle.GetBody2Cci();

    // Compute source position: transform stored body-frame offset back to CCI
    double3 offsetCci = _offsetInTargetBody.Transform(tgtBody2Cci);
    double3 newSrcPosCci = tgtPosCci + offsetCci;

    // Match velocity to target (simple approach; ignores rotational ω×r contribution)
    double3 newSrcVelCci = tgtVelCci;

    // Compute source orientation: _rotationOffset ⊙ tgtBody2Cci
    doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(_rotationOffset, tgtBody2Cci);

    // Convert Body2Cci back to Body2Cce (what Teleport expects)
    doubleQuat cci2Cce = _sourceVehicle.Parent.GetCci2Cce();
    doubleQuat newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce);

    // Match body rates from target
    double3 newBodyRates = _targetVehicle.BodyRates;

    // Create new orbit from computed CCI state vectors
    Orbit newOrbit = Orbit.CreateFromStateCci(
      _sourceVehicle.Parent,
      Universe.GetElapsedSimTime(),
      newSrcPosCci,
      newSrcVelCci,
      _sourceVehicle.Orbit.OrbitLineColor
    );

    // Teleport source vehicle to new position
    _sourceVehicle.Teleport(newOrbit, newSrcBody2Cce, newBodyRates);
  }

  private void Unweld()
  {
    if (_sourceVehicle != null && _targetVehicle != null)
      Console.WriteLine($"garys-torch: Unwelded {_sourceVehicle.Id} from {_targetVehicle.Id}");

    _sourceVehicle = null;
    _targetVehicle = null;
    _isWelded = false;
    _offsetInTargetBody = default;
    _rotationOffset = default;
  }
}

