using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.KiwisMarblesLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.KiwisMarbles;

[StarMapMod]
public class Mod
{
  public bool ImmediateUnload => false;

  private bool _isInitialized = false;
  private bool _isDisposed = false;
  private bool _windowVisible = false;

  private readonly List<CelestialWeldEntry> _welds = new List<CelestialWeldEntry>();

  // Pending weld creation state
  private int _pendingSourceIndex = 0;
  private int _pendingTargetIndex = 0;
  private float3 _pendingOffset = new float3(0f, 0f, 0f);
  private int _pendingOffsetScaleIndex = 1; // 0=m, 1=km, 2=Mm, 3=Gm
  private string? _weldError = null;

  // Per-weld offset edit state: float3 proxy + unit-scale index, keyed by weld list index
  private readonly Dictionary<int, (float3 proxy, int scaleIndex)> _weldEditState =
      new Dictionary<int, (float3, int)>();

  // Per-weld surface orbit state: lon/lat angles in degrees, radial offset in km, whether surface mode is active
  private readonly Dictionary<int, (float lon, float lat, float radialKm, bool surfaceMode)> _weldSurfaceState =
      new Dictionary<int, (float, float, float, bool)>();

  private ImGuiTextFilter _sourceFilter = new ImGuiTextFilter();
  private ImGuiTextFilter _targetFilter = new ImGuiTextFilter();

  private static readonly string[] OffsetScaleLabels = { "m", "km", "Mm", "Gm" };
  private static readonly double[] OffsetScaleFactors = { 1.0, 1_000.0, 1_000_000.0, 1_000_000_000.0 };

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
      Console.WriteLine($"kiwis-marbles: Error during initialization: {ex.Message}");
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

      if (ImGui.IsKeyPressed(ImGuiKey.F9))
        _windowVisible = !_windowVisible;

      var toRemove = new List<CelestialWeldEntry>();
      foreach (var weld in _welds)
        if (!CelestialWeldEngine.UpdateWeld(weld)) toRemove.Add(weld);
      foreach (var weld in toRemove)
        RemoveWeld(weld);

      if (_windowVisible)
        RenderWindow();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"kiwis-marbles: Error in OnAfterUi: {ex.Message}");
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
      Console.WriteLine($"kiwis-marbles: Error during unload: {ex.Message}");
    }
  }

  private void RenderWindow()
  {
    ImGui.SetNextWindowSize(new float2(520, 600), ImGuiCond.FirstUseEver);

    if (ImGui.Begin("Kiwi's Marbles###kiwis-marbles", ref _windowVisible))
    {
      // --- Create Weld ---
      ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld");
      ImGui.Separator();
      ImGui.Indent();
      ImGui.Indent();

      var celestials = CelestialProvider.GetAllCelestials();
      var orbiters = CelestialProvider.GetAllOrbiters();

      if (celestials.Count == 0)
      {
        ImGui.Text("No celestial bodies available.");
      }
      else if (orbiters.Count == 0)
      {
        ImGui.Text("No orbiters available.");
      }
      else
      {
        // Source dropdown (celestial bodies only — not stars)
        var celestialIds = new string[celestials.Count];
        for (int i = 0; i < celestials.Count; i++)
          celestialIds[i] = celestials[i].Id;

        _pendingSourceIndex = Math.Clamp(_pendingSourceIndex, 0, celestials.Count - 1);

        ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Source (planet/moon)");
        string sourcePrev = celestialIds[_pendingSourceIndex];
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##kmsrc", sourcePrev, ImGuiComboFlags.HeightRegular))
        {
          if (ImGui.IsWindowAppearing())
          {
            ImGui.SetKeyboardFocusHere();
            _sourceFilter.Clear();
          }
          _sourceFilter.Draw("##kmsrcfilter", -1f);
          for (int i = 0; i < celestials.Count; i++)
          {
            if (_sourceFilter.PassFilter(celestialIds[i]))
            {
              bool sel = _pendingSourceIndex == i;
              if (ImGui.Selectable(celestialIds[i], sel))
                _pendingSourceIndex = i;
              if (sel) ImGui.SetItemDefaultFocus();
            }
          }
          ImGui.EndCombo();
        }
        ImGui.PopStyleColor();

        // Target dropdown (any orbiter: celestials + vehicles)
        var orbiterIds = new string[orbiters.Count];
        for (int i = 0; i < orbiters.Count; i++)
          orbiterIds[i] = orbiters[i].Id;

        _pendingTargetIndex = Math.Clamp(_pendingTargetIndex, 0, orbiters.Count - 1);

        ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Target (any orbiter)");
        string targetPrev = orbiterIds[_pendingTargetIndex];
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##kmtgt", targetPrev, ImGuiComboFlags.HeightRegular))
        {
          if (ImGui.IsWindowAppearing())
          {
            ImGui.SetKeyboardFocusHere();
            _targetFilter.Clear();
          }
          _targetFilter.Draw("##kmtgtfilter", -1f);
          for (int i = 0; i < orbiters.Count; i++)
          {
            if (_targetFilter.PassFilter(orbiterIds[i]))
            {
              bool sel = _pendingTargetIndex == i;
              if (ImGui.Selectable(orbiterIds[i], sel))
                _pendingTargetIndex = i;
              if (sel) ImGui.SetItemDefaultFocus();
            }
          }
          ImGui.EndCombo();
        }
        ImGui.PopStyleColor();

        var selectedSource = celestials[_pendingSourceIndex];
        var selectedTarget = orbiters[_pendingTargetIndex];

        // Surface placement helper
        if (selectedTarget is Celestial targetCelestialPreview && (IOrbiter)selectedSource != selectedTarget)
        {
          double tR = targetCelestialPreview.MeanRadius;
          double sR = selectedSource.MeanRadius;
          double surfaceDist = tR + sR;
          ImGui.Spacing();
          var dimGreen = new float4(0.6f, 0.8f, 0.6f, 1f);
          ImGui.TextColored(dimGreen, $"  Target r: {FormatKm(tR)}");
          ImGui.TextColored(dimGreen, $"  Source r: {FormatKm(sR)}");
          ImGui.TextColored(dimGreen, $"  Surface center dist: {FormatKm(surfaceDist)}");
          if (ImGui.Button("Place on Surface (along X+)##kmsurfX"))
          {
            double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
            _pendingOffset = new float3((float)(surfaceDist / s), 0f, 0f);
          }
          if (ImGui.Button("Place on Surface (along Y+)##kmsurfY"))
          {
            double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
            _pendingOffset = new float3(0f, (float)(surfaceDist / s), 0f);
          }
          if (ImGui.Button("Place on Surface (along Z+)##kmsurfZ"))
          {
            double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
            _pendingOffset = new float3(0f, 0f, (float)(surfaceDist / s));
          }
        }

        // CCI offset input with unit scale selector
        ImGui.Spacing();
        ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "CCI Offset (x / y / z)");
        ImGui.SetNextItemWidth(-131f);
        ImGui.DragFloat3("##kmoffset", ref _pendingOffset, 1f, 0f, 0f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(123f);
        ImGui.Combo("##kmunit", ref _pendingOffsetScaleIndex, OffsetScaleLabels, OffsetScaleLabels.Length);

        // Show computed offset in meters for verification
        double scale = OffsetScaleFactors[_pendingOffsetScaleIndex];
        double3 computedOffset = new double3(
            _pendingOffset.X * scale,
            _pendingOffset.Y * scale,
            _pendingOffset.Z * scale
        );
        ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1f),
            $"  = ({computedOffset.X:G5}, {computedOffset.Y:G5}, {computedOffset.Z:G5}) m");

        ImGui.Separator();

        if ((IOrbiter)selectedSource == selectedTarget)
        {
          ImGui.TextColored(new float4(1f, 0.4f, 0.4f, 1f), "Source and target must differ.");
        }
        else
        {
          if (_weldError != null)
            ImGui.TextColored(new float4(1f, 0.4f, 0.4f, 1f), _weldError);

          if (ImGui.Button("Create Weld##kmweld"))
            InitiateWeld(selectedSource, selectedTarget, computedOffset);
        }
      }

      ImGui.Unindent();
      ImGui.Unindent();

      // --- Active Welds ---
      ImGui.Spacing();
      ImGui.Separator();
      ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Active Welds");
      ImGui.Separator();

      CelestialWeldEntry? toRemoveEntry = null;
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
          string parentName = weld.Source.Parent?.Id ?? "unknown";
          ImGui.TextColored(new float4(0.5f, 0.8f, 1f, 1f), $"Source parent: {parentName}");
          ImGui.Separator();

          // Ensure raw edit state exists
          if (!_weldEditState.ContainsKey(i))
          {
            int si = 1;
            double sf = OffsetScaleFactors[si];
            _weldEditState[i] = (
              new float3((float)(weld.Offset.X / sf), (float)(weld.Offset.Y / sf), (float)(weld.Offset.Z / sf)),
              si
            );
          }

          var (proxy, scaleIdx) = _weldEditState[i];
          bool targetIsCelestial = weld.Target is Celestial;

          // Initialize surface state if needed
          if (targetIsCelestial && !_weldSurfaceState.ContainsKey(i))
          {
            var (initLon, initLat) = OffsetToLonLat(weld.Offset);
            _weldSurfaceState[i] = (initLon, initLat, 0f, false);
          }

          bool surfMode = targetIsCelestial && _weldSurfaceState.ContainsKey(i) && _weldSurfaceState[i].surfaceMode;
          bool newSurfMode = surfMode;

          if (targetIsCelestial)
          {
            ImGui.Checkbox($"Surface Orbit Mode##{i}", ref newSurfMode);
          }

          if (targetIsCelestial && newSurfMode)
          {
            var targetCel = (Celestial)weld.Target;
            double dist = targetCel.MeanRadius + weld.Source.MeanRadius;

            float curLon = _weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].lon : 0f;
            float curLat = _weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].lat : 0f;
            float curRadialKm = _weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].radialKm : 0f;

            // If just switched into surface mode, initialize angles from current offset
            if (!surfMode)
            {
              var (initLon2, initLat2) = OffsetToLonLat(weld.Offset);
              curLon = initLon2;
              curLat = initLat2;
              curRadialKm = 0f;
            }

            double actualDist = dist + curRadialKm * 1_000.0;
            ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1f),
              $"  Surface dist: {FormatKm(dist)}  (target r: {FormatKm(targetCel.MeanRadius)} + source r: {FormatKm(weld.Source.MeanRadius)})");

            ImGui.SetNextItemWidth(-1f);
            bool lonChanged = ImGui.DragFloat($"Longitude (left/right)##{i}", ref curLon, 0.3f, -360f, 360f, "%.1f deg");
            ImGui.SetNextItemWidth(-1f);
            bool latChanged = ImGui.DragFloat($"Latitude (up/down)##{i}", ref curLat, 0.3f, -90f, 90f, "%.1f deg");
            ImGui.SetNextItemWidth(-1f);
            bool radChanged = ImGui.DragFloat($"Altitude offset (in/out)##{i}", ref curRadialKm, 1f, -float.MaxValue, float.MaxValue, "%.1f km");

            if (lonChanged || latChanged || radChanged || !surfMode)
            {
              actualDist = dist + curRadialKm * 1_000.0;
              double lonRad = curLon * Math.PI / 180.0;
              double latRad = curLat * Math.PI / 180.0;
              weld.Offset = new double3(
                actualDist * Math.Cos(latRad) * Math.Cos(lonRad),
                actualDist * Math.Cos(latRad) * Math.Sin(lonRad),
                actualDist * Math.Sin(latRad)
              );
              double sf2 = OffsetScaleFactors[scaleIdx];
              proxy = new float3(
                (float)(weld.Offset.X / sf2),
                (float)(weld.Offset.Y / sf2),
                (float)(weld.Offset.Z / sf2)
              );
            }

            _weldSurfaceState[i] = (curLon, curLat, curRadialKm, true);
          }
          else
          {
            // Raw CCI offset controls
            ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "CCI Offset (x / y / z)");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(123f);
            if (ImGui.Combo($"##kmwunit{i}", ref scaleIdx, OffsetScaleLabels, OffsetScaleLabels.Length))
            {
              double newSf = OffsetScaleFactors[scaleIdx];
              proxy = new float3(
                (float)(weld.Offset.X / newSf),
                (float)(weld.Offset.Y / newSf),
                (float)(weld.Offset.Z / newSf)
              );
            }

            ImGui.SetNextItemWidth(-1f);
            if (ImGui.DragFloat3($"##kmwoffset{i}", ref proxy, 1f, 0f, 0f))
            {
              double sf = OffsetScaleFactors[scaleIdx];
              weld.Offset = new double3(proxy.X * sf, proxy.Y * sf, proxy.Z * sf);
            }

            if (targetIsCelestial)
              _weldSurfaceState[i] = (_weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].lon : 0f,
                                      _weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].lat : 0f,
                                      _weldSurfaceState.ContainsKey(i) ? _weldSurfaceState[i].radialKm : 0f,
                                      false);
          }

          _weldEditState[i] = (proxy, scaleIdx);

          ImGui.TextColored(new float4(0.5f, 0.5f, 0.5f, 1f),
            $"  = ({weld.Offset.X:G5}, {weld.Offset.Y:G5}, {weld.Offset.Z:G5}) m");

          ImGui.Separator();
          if (ImGui.Button($"Unweld##{i}"))
            toRemoveEntry = weld;

          ImGui.Unindent();
          ImGui.Unindent();
        }
      }
      if (toRemoveEntry != null)
        RemoveWeld(toRemoveEntry);
    }
    ImGui.End();
  }

  private void InitiateWeld(Celestial source, IOrbiter target, double3 offset)
  {
    foreach (var weld in _welds)
    {
      if (weld.Source == source)
      {
        _weldError = $"{source.Id} is already welded as a source.";
        return;
      }
    }

    _weldError = null;

    _welds.Add(new CelestialWeldEntry
    {
      Source = source,
      Target = target,
      Offset = offset,
      OriginalOrbit = source.Orbit,
    });

    _pendingOffset = new float3(0f, 0f, 0f);

    SortWelds();
    Console.WriteLine($"kiwis-marbles: Welded {source.Id} to {target.Id}");
  }

  private void RemoveWeld(CelestialWeldEntry entry)
  {
    int idx = _welds.IndexOf(entry);
    _welds.Remove(entry);

    if (entry.OriginalOrbit != null)
    {
      try
      {
        entry.Source.SetOrbit(entry.OriginalOrbit);
        entry.Source.UpdatePerFrameData();
        Console.WriteLine($"kiwis-marbles: Restored original orbit for {entry.Source.Id}");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"kiwis-marbles: Failed to restore orbit for {entry.Source.Id}: {ex.Message}");
      }
    }

    // Rebuild edit state indices, shifting keys > idx down by 1
    _weldEditState.Remove(idx);
    var shifted = new Dictionary<int, (float3, int)>();
    foreach (var kv in _weldEditState)
    {
      int newKey = kv.Key > idx ? kv.Key - 1 : kv.Key;
      shifted[newKey] = kv.Value;
    }
    _weldEditState.Clear();
    foreach (var kv in shifted)
      _weldEditState[kv.Key] = kv.Value;

    _weldSurfaceState.Remove(idx);
    var shiftedSurf = new Dictionary<int, (float, float, float, bool)>();
    foreach (var kv in _weldSurfaceState)
    {
      int newKey = kv.Key > idx ? kv.Key - 1 : kv.Key;
      shiftedSurf[newKey] = kv.Value;
    }
    _weldSurfaceState.Clear();
    foreach (var kv in shiftedSurf)
      _weldSurfaceState[kv.Key] = kv.Value;

    Console.WriteLine($"kiwis-marbles: Unwelded {entry.Source.Id} from {entry.Target.Id}");
  }

  private void SortWelds()
  {
    var sorted = CelestialWeldEngine.TopologicalSort(_welds);
    _welds.Clear();
    foreach (var w in sorted)
      _welds.Add(w);
    _weldEditState.Clear();
    _weldSurfaceState.Clear();
  }

  private static (float lon, float lat) OffsetToLonLat(double3 offset)
  {
    double len = Math.Sqrt(offset.X * offset.X + offset.Y * offset.Y + offset.Z * offset.Z);
    if (len < 1e-10) return (0f, 0f);
    double lat = Math.Asin(Math.Clamp(offset.Z / len, -1.0, 1.0)) * (180.0 / Math.PI);
    double lon = Math.Atan2(offset.Y, offset.X) * (180.0 / Math.PI);
    return ((float)lon, (float)lat);
  }

  private static string FormatKm(double meters)
  {
    if (meters >= 1e9) return $"{meters / 1e9:G4} Gm";
    if (meters >= 1e6) return $"{meters / 1e6:G4} Mm";
    return $"{meters / 1e3:G4} km";
  }
}

