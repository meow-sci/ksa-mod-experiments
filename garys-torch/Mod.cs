using System;
using System.Collections.Generic;
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

  private readonly List<WeldEntry> _welds = new List<WeldEntry>();
  private float _pendingDistance = 10f;

  private class WeldEntry
  {
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    public double3 OffsetInTargetBody;
    public doubleQuat RotationOffset;
    public float DesiredDistance;
  }


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

      var toRemove = new List<WeldEntry>();
      foreach (var weld in _welds)
        if (!UpdateWeld(weld)) toRemove.Add(weld);
      foreach (var weld in toRemove)
        RemoveWeld(weld);

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
      foreach (var weld in _welds)
      {
        ImGui.Text($"{weld.Source.Id} \u2192 {weld.Target.Id}");
        ImGui.SameLine();
        if (ImGui.Button($"Unweld##{weld.Source.Id}-{weld.Target.Id}"))
        {
          RemoveWeld(weld);
          break;
        }
      }

      if (_welds.Count > 0)
        ImGui.Separator();

      var controlled = Program.ControlledVehicle;
      if (controlled == null)
      {
        ImGui.Text("Control a vehicle first.");
      }
      else
      {
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
              InitiateWeld(controlled, v, _pendingDistance);
          }
        }
      }
    }
    ImGui.End();
  }

  private void InitiateWeld(Vehicle source, Vehicle target, float desiredDistance)
  {
    double3 srcPosCci = source.GetPositionCci();
    double3 tgtPosCci = target.GetPositionCci();
    double3 rawOffsetCci = srcPosCci - tgtPosCci;

    // Normalize direction; fallback to CCI Z-axis if vehicles are coincident
    double3 directionCci = rawOffsetCci.Length() > 1e-6
      ? rawOffsetCci.Normalized()
      : double3.UnitZ;

    // Scale to desired distance
    double3 offsetCci = directionCci * (double)desiredDistance;

    doubleQuat tgtBody2Cci = target.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();
    double3 offsetInTargetBody = offsetCci.Transform(cci2TgtBody);

    doubleQuat srcBody2Cci = source.GetBody2Cci();
    doubleQuat rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);

    _welds.Add(new WeldEntry
    {
      Source = source,
      Target = target,
      OffsetInTargetBody = offsetInTargetBody,
      RotationOffset = rotationOffset,
      DesiredDistance = desiredDistance,
    });

    Console.WriteLine($"garys-torch: Welded {source.Id} to {target.Id}");
    Console.WriteLine($"garys-torch: Offset (target body): {offsetInTargetBody}");
  }

  private bool UpdateWeld(WeldEntry entry)
  {
    if (entry.Source.Parent != entry.Target.Parent)
    {
      Console.WriteLine("garys-torch: Parent body mismatch, unwelding");
      return false;
    }

    double3 tgtPosCci = entry.Target.GetPositionCci();
    double3 tgtVelCci = entry.Target.GetVelocityCci();
    doubleQuat tgtBody2Cci = entry.Target.GetBody2Cci();

    double3 offsetCci = entry.OffsetInTargetBody.Transform(tgtBody2Cci);
    double3 newSrcPosCci = tgtPosCci + offsetCci;
    double3 newSrcVelCci = tgtVelCci;

    doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(entry.RotationOffset, tgtBody2Cci);
    doubleQuat cci2Cce = entry.Source.Parent.GetCci2Cce();
    doubleQuat newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce);

    double3 newBodyRates = entry.Target.BodyRates;

    Orbit newOrbit = Orbit.CreateFromStateCci(
      entry.Source.Parent,
      Universe.GetElapsedSimTime(),
      newSrcPosCci,
      newSrcVelCci,
      entry.Source.Orbit.OrbitLineColor
    );

    entry.Source.Teleport(newOrbit, newSrcBody2Cce, newBodyRates);
    return true;
  }

  private void RemoveWeld(WeldEntry entry)
  {
    Console.WriteLine($"garys-torch: Unwelded {entry.Source.Id} from {entry.Target.Id}");
    _welds.Remove(entry);
  }
}

