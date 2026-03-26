using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KiwisMarblesLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.Grant.Submods;

internal sealed class KiwisMarblesSubmod : IGrantSubmod
{
    public string Name => "Kiwi's Marbles";

    private readonly List<CelestialWeldEntry> _welds = new();
    private int _pendingSourceIndex;
    private int _pendingTargetIndex;
    private float3 _pendingOffset = new(0f, 0f, 0f);
    private int _pendingOffsetScaleIndex = 1; // 0=m, 1=km, 2=Mm, 3=Gm
    private string? _weldError;
    private readonly Dictionary<int, (float3 proxy, int scaleIndex)> _weldEditState = new();
    private ImGuiTextFilter _sourceFilter = new();
    private ImGuiTextFilter _targetFilter = new();

    private static readonly string[] OffsetScaleLabels = { "m", "km", "Mm", "Gm" };
    private static readonly double[] OffsetScaleFactors = { 1.0, 1_000.0, 1_000_000.0, 1_000_000_000.0 };

    public void Initialize() { }

    public void Update(double dt)
    {
        var toRemove = new List<CelestialWeldEntry>();
        foreach (var weld in _welds)
            if (!CelestialWeldEngine.UpdateWeld(weld)) toRemove.Add(weld);
        foreach (var weld in toRemove)
            RemoveWeld(weld);
    }

    public void RenderContent()
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
            // Source dropdown (celestial bodies only)
            var celestialIds = new string[celestials.Count];
            for (int i = 0; i < celestials.Count; i++)
                celestialIds[i] = celestials[i].Id;

            _pendingSourceIndex = Math.Clamp(_pendingSourceIndex, 0, celestials.Count - 1);
            string sourcePrev = celestialIds[_pendingSourceIndex];

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
            ImGui.SetNextItemWidth(-185f);
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
            ImGui.SameLine();
            ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Source (planet/moon)");

            // Target dropdown (any orbiter)
            var orbiterIds = new string[orbiters.Count];
            for (int i = 0; i < orbiters.Count; i++)
                orbiterIds[i] = orbiters[i].Id;

            _pendingTargetIndex = Math.Clamp(_pendingTargetIndex, 0, orbiters.Count - 1);
            string targetPrev = orbiterIds[_pendingTargetIndex];

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
            ImGui.SetNextItemWidth(-185f);
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
            ImGui.SameLine();
            ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Target (any orbiter)");

            var selectedSource = celestials[_pendingSourceIndex];
            var selectedTarget = orbiters[_pendingTargetIndex];

            // Surface placement helper
            if (selectedTarget is Celestial targetCelestialPreview && (IOrbiter)selectedSource != selectedTarget)
            {
                double tR = targetCelestialPreview.MeanRadius;
                double sR = selectedSource.MeanRadius;
                double surfaceDist = tR + sR;
                ImGui.Spacing();
                ImGui.TextColored(new float4(0.6f, 0.8f, 0.6f, 1f),
                    $"  Target r: {FormatKm(tR)}   Source r: {FormatKm(sR)}   Center dist (surface): {FormatKm(surfaceDist)}");
                if (ImGui.Button("Place on Surface (along X+)##kmsurfX"))
                {
                    double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
                    _pendingOffset = new float3((float)(surfaceDist / s), 0f, 0f);
                }
                ImGui.SameLine();
                if (ImGui.Button("Place on Surface (along Y+)##kmsurfY"))
                {
                    double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
                    _pendingOffset = new float3(0f, (float)(surfaceDist / s), 0f);
                }
                ImGui.SameLine();
                if (ImGui.Button("Place on Surface (along Z+)##kmsurfZ"))
                {
                    double s = OffsetScaleFactors[_pendingOffsetScaleIndex];
                    _pendingOffset = new float3(0f, 0f, (float)(surfaceDist / s));
                }
            }

            // CCI offset input with unit scale selector
            ImGui.Spacing();
            ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "CCI Offset (x / y / z)");
            ImGui.SetNextItemWidth(-100f);
            ImGui.DragFloat3("##kmoffset", ref _pendingOffset, 1f, 0f, 0f);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(82f);
            ImGui.Combo("##kmunit", ref _pendingOffsetScaleIndex, OffsetScaleLabels, OffsetScaleLabels.Length);

            // Show computed offset in meters
            double scale = OffsetScaleFactors[_pendingOffsetScaleIndex];
            double3 computedOffset = new(
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

                // Ensure edit state exists for this weld index
                if (!_weldEditState.ContainsKey(i))
                {
                    int si = 1; // default km
                    double sf = OffsetScaleFactors[si];
                    _weldEditState[i] = (
                        new float3((float)(weld.Offset.X / sf), (float)(weld.Offset.Y / sf), (float)(weld.Offset.Z / sf)),
                        si
                    );
                }

                var (proxy, scaleIdx) = _weldEditState[i];

                // Unit scale selector
                ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "CCI Offset (x / y / z)");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(82f);
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

                _weldEditState[i] = (proxy, scaleIdx);

                // Show actual offset in meters
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

    public void Dispose() { }

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
        _welds.Add(new CelestialWeldEntry { Source = source, Target = target, Offset = offset });
        _pendingOffset = new float3(0f, 0f, 0f);
        SortWelds();
        Console.WriteLine($"grant/kiwis-marbles: Welded {source.Id} to {target.Id}");
    }

    private void RemoveWeld(CelestialWeldEntry entry)
    {
        int idx = _welds.IndexOf(entry);
        _welds.Remove(entry);

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

        Console.WriteLine($"grant/kiwis-marbles: Unwelded {entry.Source.Id} from {entry.Target.Id}");
    }

    private void SortWelds()
    {
        var sorted = CelestialWeldEngine.TopologicalSort(_welds);
        _welds.Clear();
        foreach (var w in sorted)
            _welds.Add(w);
        _weldEditState.Clear();
    }

    private static string FormatKm(double meters)
    {
        if (meters >= 1e9) return $"{meters / 1e9:G4} Gm";
        if (meters >= 1e6) return $"{meters / 1e6:G4} Mm";
        return $"{meters / 1e3:G4} km";
    }
}
