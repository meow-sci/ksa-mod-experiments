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
  private bool _isWelded = false;
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
    ImGui.SetNextWindowSize(new float2(400, 300), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Gary's Torch###garys-torch", ref _windowVisible))
    {
      var controlled = Program.ControlledVehicle;
      if (controlled == null)
      {
        ImGui.Text("Control a vehicle first.");
      }
      else if (_isWelded)
      {
        // Welded state
        ImGui.TextColored(new float4(0f, 1f, 0f, 1f), "WELDED");
        ImGui.Text($"Source: {_sourceVehicle?.Id}");
        ImGui.Text($"Target: {_targetVehicle?.Id}");
        ImGui.Separator();
        if (ImGui.Button("Unweld"))
          Unweld();
      }
      else
      {
        // Vehicle picker
        ImGui.Text($"Controlled: {controlled.Id}");
        ImGui.Separator();
        ImGui.Text("Select target to weld to:");

        var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
        if (vehicles != null)
        {
          foreach (var v in vehicles)
          {
            if (v == controlled) continue;
            if (ImGui.Button($"Weld to: {v.Id}"))
            {
              _sourceVehicle = controlled;
              _targetVehicle = v;
              InitiateWeld();
            }
          }
        }
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

