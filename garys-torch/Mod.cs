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
  private float3 _pendingPosition = new float3(0f, 0f, 0f);
  private float3 _pendingRotation = new float3(0f, 0f, 0f);
  private float _pendingScale = 1f;
  private bool _pendingLockRotation = true;
  private string? _weldError = null;

  private class WeldEntry
  {
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    // Source orientation relative to target, captured at weld time
    public doubleQuat RotationOffset;
    // Adjustable fields — modified via UI sliders
    public float3 Position;   // offset in target's body frame (metres)
    public float3 Rotation;   // Euler pitch/yaw/roll delta in degrees
    public float Scale = 1f;  // uniform scale factor applied to all source parts
    public bool LockRotation = true;  // when false, only position is locked; source can rotate freely
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
      // --- Create Weld ---
      ImGui.Text("Create Weld");
      ImGui.Separator();
      ImGui.Indent();

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

        if (ImGui.CollapsingHeader("Starting Data##startingdata"))
        {
          ImGui.Text("Position (x / y / z, m)");
          ImGui.DragFloat3("##pendingpos", ref _pendingPosition, 0.001f, 0f, 0f);
          ImGui.Separator();
          ImGui.Text("Rotation (pitch / yaw / roll, deg)");
          ImGui.DragFloat3("##pendingrot", ref _pendingRotation, 0.025f, -180f, 180f);
          ImGui.Separator();
          ImGui.Text("Scale");
          ImGui.DragFloat("##pendingscale", ref _pendingScale, 0.001f, 0.05f, 20f);
          ImGui.Separator();
          ImGui.Checkbox("Lock Rotation##pendinglockrot", ref _pendingLockRotation);
        }
        ImGui.Separator();

        if (_pendingSourceIndex == _pendingTargetIndex)
        {
          ImGui.TextColored(new float4(1, 0.4f, 0.4f, 1), "Source and target must differ.");
        }
        else
        {
          if (_weldError != null)
            ImGui.TextColored(new float4(1, 0.4f, 0.4f, 1), _weldError);
          if (ImGui.Button("Weld##addweld"))
            InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex], _pendingPosition, _pendingRotation, _pendingScale, _pendingLockRotation);
        }
      }

      ImGui.Unindent();

      // --- Weld List ---
      ImGui.Spacing();
      ImGui.Separator();
      ImGui.Text("Active Welds");
      ImGui.Separator();

      WeldEntry? toRemove = null;
      for (int i = 0; i < _welds.Count; i++)
      {
        ImGui.Spacing();
        var weld = _welds[i];
        string header = $"Weld {i + 1}: {weld.Source.Id} -> {weld.Target.Id}";
        if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
        {
          ImGui.Indent();
          ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");
          ImGui.Separator();

          ImGui.Text("Position (x / y / z, m)");
          ImGui.DragFloat3($"##pos{i}", ref weld.Position, 0.001f, 0f, 0f);

          ImGui.Separator();
          ImGui.Text("Rotation (pitch / yaw / roll, deg)");
          ImGui.DragFloat3($"##rot{i}", ref weld.Rotation, 0.025f, -180f, 180f);

          ImGui.Separator();
          ImGui.Text("Scale");
          if (ImGui.DragFloat($"##scale{i}", ref weld.Scale, 0.001f, 0.05f, 20f))
            ApplyVehicleScale(weld.Source, weld.Scale);

          ImGui.Separator();
          bool lockRot = weld.LockRotation;
          if (ImGui.Checkbox($"Lock Rotation##{i}", ref lockRot))
            weld.LockRotation = lockRot;

          ImGui.Separator();
          if (ImGui.Button($"Unweld##{i}"))
            toRemove = weld;
          ImGui.Unindent();
        }
      }
      if (toRemove != null)
        RemoveWeld(toRemove);
    }
    ImGui.End();
  }

  private void InitiateWeld(Vehicle source, Vehicle target, float3 position, float3 rotation, float scale, bool lockRotation)
  {
    foreach (var weld in _welds)
    {
      if (weld.Source == source)
      {
        _weldError = $"Vehicle {source.Id} is already welded as a source.";
        return;
      }
    }

    _weldError = null;

    doubleQuat tgtBody2Cci = target.GetBody2Cci();
    doubleQuat cci2TgtBody = tgtBody2Cci.Inverse();

    doubleQuat srcBody2Cci = source.GetBody2Cci();
    doubleQuat rotationOffset = doubleQuat.Concatenate(srcBody2Cci, cci2TgtBody);

    _welds.Add(new WeldEntry
    {
      Source = source,
      Target = target,
      RotationOffset = rotationOffset,
      Position = position,
      Rotation = rotation,
      Scale = scale,
      LockRotation = lockRotation,
    });

    if (scale != 1f)
      ApplyVehicleScale(source, scale);

    _pendingPosition = new float3(0f, 0f, 0f);
    _pendingRotation = new float3(0f, 0f, 0f);
    _pendingScale = 1f;
    _pendingLockRotation = true;

    SortWelds();
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

    doubleQuat cci2Cce = entry.Source.Parent.GetCci2Cce();
    doubleQuat newSrcBody2Cce;
    double3 newBodyRates;

    if (entry.LockRotation)
    {
      // Apply Euler rotation delta on top of base rotation offset
      doubleQuat deltaRot = EulerDegreesToQuat(entry.Rotation.X, entry.Rotation.Y, entry.Rotation.Z);
      doubleQuat effectiveRot = doubleQuat.Concatenate(deltaRot, entry.RotationOffset);
      doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(effectiveRot, tgtBody2Cci);
      newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce);
      newBodyRates = entry.Target.BodyRates;
    }
    else
    {
      // Rotation unlocked — preserve source's current orientation and body rates
      doubleQuat srcBody2Cci = entry.Source.GetBody2Cci();
      newSrcBody2Cce = doubleQuat.Concatenate(srcBody2Cci, cci2Cce);
      newBodyRates = entry.Source.BodyRates;
    }

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
    // Restore source vehicle parts to default scale
    ApplyVehicleScale(entry.Source, 1.0f);
    Console.WriteLine($"garys-torch: Unwelded {entry.Source.Id} from {entry.Target.Id}");
    _welds.Remove(entry);
  }

  private static void ApplyVehicleScale(Vehicle vehicle, float factor)
  {
    foreach (var part in vehicle.Parts.Parts)
      SetPartScaleRecursive(part, factor);

    // KittenEva renders via CharacterAvatar.Core.Scale (Core.Scale 0.01 = 1:1)
    if (vehicle.GetType().Name == "KittenEva")
    {
      try
      {
        var allFlags = System.Reflection.BindingFlags.Instance
                     | System.Reflection.BindingFlags.Public
                     | System.Reflection.BindingFlags.NonPublic;

        var renderable = vehicle.GetType().GetField("_renderable", allFlags)?.GetValue(vehicle);
        if (renderable == null) return;

        var avatar = renderable.GetType().GetField("_characterAvatar", allFlags)?.GetValue(renderable);
        if (avatar == null) return;

        var coreField = avatar.GetType().GetField("Core", allFlags);
        var core = coreField?.GetValue(avatar);
        if (core == null) return;

        var scaleField = core.GetType().GetField("Scale", allFlags);
        var scaleProp  = core.GetType().GetProperty("Scale", allFlags);

        if (scaleField != null && scaleField.FieldType == typeof(float))
        {
          scaleField.SetValue(core, factor * 0.01f);
          coreField!.SetValue(avatar, core);
        }
        else if (scaleProp != null && scaleProp.PropertyType == typeof(float))
        {
          scaleProp.SetValue(core, factor * 0.01f);
          coreField!.SetValue(avatar, core);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"garys-torch: KittenEva scale error: {ex.Message}");
      }
    }
  }

  private static void SetPartScaleRecursive(Part part, float factor)
  {
    part.Scale = new double3(factor, factor, factor);
    foreach (var sub in part.SubParts)
      SetPartScaleRecursive(sub, factor);
  }

  private void SortWelds()
  {
    var inDegree = new Dictionary<WeldEntry, int>();
    var adj = new Dictionary<WeldEntry, List<WeldEntry>>();

    foreach (var w in _welds)
    {
      inDegree[w] = 0;
      adj[w] = new List<WeldEntry>();
    }

    foreach (var x in _welds)
    {
      foreach (var y in _welds)
      {
        if (x.Source == y.Target)
        {
          adj[x].Add(y);
          inDegree[y]++;
        }
      }
    }

    var queue = new Queue<WeldEntry>();
    foreach (var w in _welds)
      if (inDegree[w] == 0)
        queue.Enqueue(w);

    var sorted = new List<WeldEntry>();
    while (queue.Count > 0)
    {
      var current = queue.Dequeue();
      sorted.Add(current);
      foreach (var neighbor in adj[current])
      {
        inDegree[neighbor]--;
        if (inDegree[neighbor] == 0)
          queue.Enqueue(neighbor);
      }
    }

    if (sorted.Count == _welds.Count)
    {
      _welds.Clear();
      foreach (var w in sorted)
        _welds.Add(w);
    }
    else
    {
      Console.WriteLine("garys-torch: SortWelds: cycle detected, leaving order as-is.");
    }
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

