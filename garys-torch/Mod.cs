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
    // Base rotation captured at weld time (source orientation relative to target)
    public doubleQuat RotationOffset;
    // Unit vector giving direction of offset in target's body frame (captured at weld time)
    public double3 NormOffsetDir;
    // Adjustable fields — modified via UI sliders
    public float PosDistance;   // distance in metres from target
    public float RotPitch;      // Euler pitch delta in degrees
    public float RotYaw;        // Euler yaw delta in degrees
    public float RotRoll;       // Euler roll delta in degrees
    // Keep DesiredDistance for display in window header only
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
    ImGui.SetNextWindowSize(new float2(450, 500), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Gary's Torch###garys-torch", ref _windowVisible))
    {
      // Active welds — one collapsible section per weld
      WeldEntry? toRemove = null;
      for (int i = 0; i < _welds.Count; i++)
      {
        var weld = _welds[i];
        string header = $"Weld {i + 1}: {weld.Source.Id} -> {weld.Target.Id} ({weld.DesiredDistance:F1} m)";
        if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
        {
          ImGui.Indent();
          ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");
          ImGui.Separator();

          ImGui.Text("Position");
          ImGui.SliderFloat($"Distance (m)##{i}", ref weld.PosDistance, 0f, 100f);

          ImGui.Separator();
          ImGui.Text("Rotation");
          ImGui.SliderFloat($"Pitch (deg)##{i}", ref weld.RotPitch, -180f, 180f);
          ImGui.SliderFloat($"Yaw (deg)##{i}",   ref weld.RotYaw,   -180f, 180f);
          ImGui.SliderFloat($"Roll (deg)##{i}",  ref weld.RotRoll,  -180f, 180f);

          ImGui.Separator();
          if (ImGui.Button($"Unweld##{i}"))
            toRemove = weld;
          ImGui.Unindent();
        }
      }
      if (toRemove != null)
        RemoveWeld(toRemove);

      ImGui.Separator();

      // Add New Weld section
      var controlled = Program.ControlledVehicle;
      if (controlled == null)
      {
        ImGui.Text("Control a vehicle first to add a weld.");
      }
      else
      {
        ImGui.Text("Add New Weld");
        ImGui.Text($"Source: {controlled.Id}");
        ImGui.SliderFloat("Distance (m)##pending", ref _pendingDistance, 0f, 100f);
        ImGui.Separator();
        ImGui.Text("Weld to:");

        var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
        if (vehicles != null)
        {
          foreach (var v in vehicles)
          {
            if (v == controlled) continue;
            if (ImGui.Button($"{v.Id}##weld"))
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

    double3 directionCci = rawOffsetCci.Length() > 1e-6
      ? rawOffsetCci.Normalized()
      : double3.UnitZ;

    doubleQuat tgtBody2Cci = target.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();

    // Store offset direction in target body frame (unit vector)
    double3 normOffsetDir = (directionCci * (double)desiredDistance).Transform(cci2TgtBody).Normalized();

    doubleQuat srcBody2Cci = source.GetBody2Cci();
    doubleQuat rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);

    _welds.Add(new WeldEntry
    {
      Source = source,
      Target = target,
      RotationOffset = rotationOffset,
      NormOffsetDir = normOffsetDir,
      PosDistance = desiredDistance,
      RotPitch = 0f,
      RotYaw = 0f,
      RotRoll = 0f,
      DesiredDistance = desiredDistance,
    });

    Console.WriteLine($"garys-torch: Welded {source.Id} to {target.Id} at {desiredDistance:F1} m");
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

    // Compute positional offset from stored direction + adjustable distance
    double3 offsetInBodyFrame = entry.NormOffsetDir * (double)entry.PosDistance;
    double3 offsetCci = offsetInBodyFrame.Transform(tgtBody2Cci);
    double3 newSrcPosCci = tgtPosCci + offsetCci;
    double3 newSrcVelCci = tgtVelCci;

    // Apply Euler rotation delta on top of base rotation offset
    doubleQuat deltaRot = EulerDegreesToQuat(entry.RotPitch, entry.RotYaw, entry.RotRoll);
    doubleQuat effectiveRot = doubleQuat.Concatenate(deltaRot, entry.RotationOffset);
    doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(effectiveRot, tgtBody2Cci);

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

  private static doubleQuat EulerDegreesToQuat(float pitchDeg, float yawDeg, float rollDeg)
  {
    double pitchRad = pitchDeg * (Math.PI / 180.0);
    double yawRad   = yawDeg   * (Math.PI / 180.0);
    double rollRad  = rollDeg  * (Math.PI / 180.0);

    double cp = Math.Cos(pitchRad / 2), sp = Math.Sin(pitchRad / 2);
    double cy = Math.Cos(yawRad   / 2), sy = Math.Sin(yawRad   / 2);
    double cr = Math.Cos(rollRad  / 2), sr = Math.Sin(rollRad  / 2);

    // Individual axis quaternions: new doubleQuat(x, y, z, w)
    var qPitch = new doubleQuat(sp,  0,  0, cp);
    var qYaw   = new doubleQuat( 0, sy,  0, cy);
    var qRoll  = new doubleQuat( 0,  0, sr, cr);

    // Compose: Yaw * Pitch * Roll (ZYX intrinsic Euler)
    return doubleQuat.Concatenate(doubleQuat.Concatenate(qYaw, qPitch), qRoll);
  }
}

