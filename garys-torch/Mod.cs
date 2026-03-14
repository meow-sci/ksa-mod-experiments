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

  private int _pendingSourceIndex = 0;
  private int _pendingTargetIndex = 0;

  private class WeldEntry
  {
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    // Source orientation relative to target, captured at weld time
    public doubleQuat RotationOffset;
    // Adjustable fields — modified via UI sliders
    public float3 Position;   // offset in target's body frame (metres)
    public float3 Rotation;   // Euler pitch/yaw/roll delta in degrees
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
        string header = $"Weld {i + 1}: {weld.Source.Id} -> {weld.Target.Id}";
        if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
        {
          ImGui.Indent();
          ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");
          ImGui.Separator();

          ImGui.Text("Position (x / y / z, m)");
          ImGui.DragFloat3($"##pos{i}", ref weld.Position, 0.05f, 0f, 0f);

          ImGui.Separator();
          ImGui.Text("Rotation (pitch / yaw / roll, deg)");
          ImGui.DragFloat3($"##rot{i}", ref weld.Rotation, 0.05f, -180f, 180f);

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
      var vehicles = Universe.CurrentSystem?.Vehicles.GetList();
      if (vehicles == null || vehicles.Count == 0)
      {
        ImGui.Text("No vehicles available.");
      }
      else
      {
        var vehicleIds = new string[vehicles.Count];
        for (int i = 0; i < vehicles.Count; i++)
          vehicleIds[i] = vehicles[i].Id;

        _pendingSourceIndex = Math.Clamp(_pendingSourceIndex, 0, vehicles.Count - 1);
        _pendingTargetIndex = Math.Clamp(_pendingTargetIndex, 0, vehicles.Count - 1);

        ImGui.Combo("Source##src", ref _pendingSourceIndex, vehicleIds, vehicleIds.Length);
        ImGui.Combo("Target##tgt", ref _pendingTargetIndex, vehicleIds, vehicleIds.Length);

        if (_pendingSourceIndex == _pendingTargetIndex)
        {
          ImGui.TextColored(new float4(1, 0.4f, 0.4f, 1), "Source and target must differ.");
        }
        else
        {
          if (ImGui.Button("Weld##addweld"))
            InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex]);
        }
      }
    }
    ImGui.End();
  }

  private void InitiateWeld(Vehicle source, Vehicle target)
  {
    double3 srcPosCci = source.GetPositionCci();
    double3 tgtPosCci = target.GetPositionCci();
    double3 rawOffsetCci = srcPosCci - tgtPosCci;

    doubleQuat tgtBody2Cci = target.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();

    // Capture current offset in target's body frame
    double3 offsetBody = rawOffsetCci.Transform(cci2TgtBody);
    float3 initialPosition = new float3((float)offsetBody.X, (float)offsetBody.Y, (float)offsetBody.Z);

    doubleQuat srcBody2Cci = source.GetBody2Cci();
    doubleQuat rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);

    _welds.Add(new WeldEntry
    {
      Source = source,
      Target = target,
      RotationOffset = rotationOffset,
      Position = initialPosition,
      Rotation = new float3(0f, 0f, 0f),
    });

    Console.WriteLine($"garys-torch: Welded {source.Id} to {target.Id}");
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

    // Compute positional offset from the 3D body-frame position
    double3 offsetInBodyFrame = new double3(entry.Position.X, entry.Position.Y, entry.Position.Z);
    double3 offsetCci = offsetInBodyFrame.Transform(tgtBody2Cci);
    double3 newSrcPosCci = tgtPosCci + offsetCci;
    double3 newSrcVelCci = tgtVelCci;

    // Apply Euler rotation delta on top of base rotation offset
    doubleQuat deltaRot = EulerDegreesToQuat(entry.Rotation.X, entry.Rotation.Y, entry.Rotation.Z);
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

