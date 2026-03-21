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
  private int _selectedPresetIndex = 0;

  private struct WeldPreset
  {
    public string Name;
    public float3 Position;
    public float3 Rotation;
    public float Scale;
    public bool LockRotation;
  }

  private readonly WeldPreset[] _presets = new[]
  {
    new WeldPreset { Name = "Ridin' Dirty 1", Position = new float3(-0.375f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
    new WeldPreset { Name = "Ridin' Dirty 2", Position = new float3(-1.287f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
    new WeldPreset { Name = "Ridin' Dirty 3", Position = new float3(-2.215f, 0f, -1.894f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
    new WeldPreset { Name = "Shotgun", Position = new float3(5.675f, 0.413f, -0.125f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
    new WeldPreset { Name = "Not Shotgun", Position = new float3(5.675f, -0.413f, -0.125f), Rotation = new float3(0f, 0f, 0f), Scale = 1f, LockRotation = true },
  };

  private class WeldEntry
  {
    public Vehicle Source = null!;
    public Vehicle Target = null!;
    // Adjustable fields — modified via UI sliders
    public float3 Position;   // offset in target's body frame (metres)
    public float3 Rotation;   // Euler pitch/yaw/roll relative to target orientation (degrees)
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
      ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld");
      ImGui.Separator();
      ImGui.Indent();
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

        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
        ImGui.Combo("##src", ref _pendingSourceIndex, vehicleIds, vehicleIds.Length);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Source");

        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
        ImGui.Combo("##tgt", ref _pendingTargetIndex, vehicleIds, vehicleIds.Length);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Target");

        if (ImGui.CollapsingHeader("Starting Data##startingdata"))
        {
          ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Position (x / y / z, m)");
          ImGui.SetNextItemWidth(-1f);
          ImGui.DragFloat3("##pendingpos", ref _pendingPosition, 0.001f, 0f, 0f);
          ImGui.Separator();
          ImGui.TextColored((float4)KSAColor.Xkcd.GreenApple, "Rotation (pitch / yaw / roll, deg)");
          ImGui.SetNextItemWidth(-1f);
          ImGui.DragFloat3("##pendingrot", ref _pendingRotation, 0.025f, -180f, 180f);
          ImGui.Separator();
          ImGui.TextColored((float4)KSAColor.Xkcd.OrangishRed, "Scale");
          ImGui.SetNextItemWidth(-1f);
          ImGui.DragFloat("##pendingscale", ref _pendingScale, 0.001f, 0.05f, 20f);
          ImGui.Separator();
          ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.BrightMagenta));
          ImGui.Checkbox("Lock Rotation##pendinglockrot", ref _pendingLockRotation);
          ImGui.PopStyleColor();
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
          if (ImGui.Button("Create Weld##addweld"))
            InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex], _pendingPosition, _pendingRotation, _pendingScale, _pendingLockRotation);
          
          ImGui.Text("Preset:");
          ImGui.SameLine();
          
          var presetNames = new string[_presets.Length];
          for (int i = 0; i < _presets.Length; i++)
            presetNames[i] = _presets[i].Name;
          
          ImGui.SetNextItemWidth(-340f);
          ImGui.Combo("##presetcombo", ref _selectedPresetIndex, presetNames, presetNames.Length);
          ImGui.SameLine();
          
          ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32((float4)KSAColor.Xkcd.HotPink));
          ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new float4(1f, 1f, 1f, 1f)));
          if (ImGui.Button("I'm feeling lucky##ifl"))
          {
            var preset = _presets[_selectedPresetIndex];
            InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex], preset.Position, preset.Rotation, preset.Scale, preset.LockRotation);
          }
          ImGui.PopStyleColor(2);
        }
      }

      ImGui.Unindent();
      ImGui.Unindent();

      // --- Weld List ---
      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Active Welds");
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
          ImGui.Indent();
          ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");
          ImGui.Separator();

          ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Position (x / y / z, m)");
          ImGui.SetNextItemWidth(-1f);
          ImGui.DragFloat3($"##pos{i}", ref weld.Position, 0.001f, 0f, 0f);

          ImGui.Separator();
          ImGui.TextColored((float4)KSAColor.Xkcd.GreenApple, "Rotation (pitch / yaw / roll, deg)");
          ImGui.SetNextItemWidth(-1f);
          ImGui.DragFloat3($"##rot{i}", ref weld.Rotation, 0.025f, -180f, 180f);

          ImGui.Separator();
          ImGui.TextColored((float4)KSAColor.Xkcd.OrangishRed, "Scale");
          ImGui.SetNextItemWidth(-1f);
          if (ImGui.DragFloat($"##scale{i}", ref weld.Scale, 0.001f, 0.05f, 20f))
            ApplyVehicleScale(weld.Source, weld.Scale);

          ImGui.Separator();
          bool lockRot = weld.LockRotation;
          ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.BrightMagenta));
          if (ImGui.Checkbox($"Lock Rotation##{i}", ref lockRot))
            weld.LockRotation = lockRot;
          ImGui.PopStyleColor();

          ImGui.Separator();
          if (ImGui.Button($"Unweld##{i}"))
            toRemove = weld;
          ImGui.Unindent();
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

    _welds.Add(new WeldEntry
    {
      Source = source,
      Target = target,
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
      // Apply Euler rotation relative to target orientation
      doubleQuat deltaRot = EulerDegreesToQuat(entry.Rotation.X, entry.Rotation.Y, entry.Rotation.Z);
      doubleQuat newSrcBody2Cci = doubleQuat.Concatenate(deltaRot, tgtBody2Cci);
      newSrcBody2Cce = doubleQuat.Concatenate(newSrcBody2Cci, cci2Cce).NormalizedOrZero();
      newBodyRates = entry.Target.BodyRates;
    }
    else
    {
      // Rotation unlocked — preserve source's current orientation and body rates
      doubleQuat srcBody2Cci = entry.Source.GetBody2Cci();
      newSrcBody2Cce = doubleQuat.Concatenate(srcBody2Cci, cci2Cce).NormalizedOrZero();
      newBodyRates = entry.Source.BodyRates;

      // Guard against NaN body rates that can feed back into physics
      if (double.IsNaN(newBodyRates.X) || double.IsNaN(newBodyRates.Y) || double.IsNaN(newBodyRates.Z))
      {
        Console.WriteLine("garys-torch: NaN detected in body rates, resetting to zero");
        newBodyRates = new double3(0, 0, 0);
      }
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

