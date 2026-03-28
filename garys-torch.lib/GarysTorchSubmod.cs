using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.GarysTorchLib;

public sealed class GarysTorchSubmod : ISubmod
{
    public string Name => "Gary's Torch";

    private readonly List<WeldEntry> _welds = new();

    private int _pendingSourceIndex;
    private int _pendingTargetIndex;
    private float3 _pendingPosition = new float3(0f, 0f, 0f);
    private float3 _pendingRotation = new float3(0f, 0f, 0f);
    private float _pendingScale = 1f;
    private bool _pendingLockRotation = true;
    private string? _weldError;
    private int _selectedPresetIndex;

    public void Initialize() { }

    public void Update(double dt)
    {
        var toRemove = new List<WeldEntry>();
        foreach (var weld in _welds)
            if (!WeldEngine.UpdateWeld(weld)) toRemove.Add(weld);
        foreach (var weld in toRemove)
            RemoveWeld(weld);
    }

    public void RenderContent()
    {
        ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld");
        ImGui.Separator();
        ImGui.Indent();
        ImGui.Indent();

        var vehicles = VehicleProvider.GetAllVehicles();
        if (vehicles.Count == 0)
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
            ImGui.Combo("##gt_src", ref _pendingSourceIndex, vehicleIds, vehicleIds.Length);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Source");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.RadioactiveGreen));
            ImGui.Combo("##gt_tgt", ref _pendingTargetIndex, vehicleIds, vehicleIds.Length);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextColored((float4)KSAColor.Xkcd.RadioactiveGreen, "Target");

            if (ImGui.CollapsingHeader("Starting Data##gt_startingdata"))
            {
                ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Position (x / y / z, m)");
                ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat3("##gt_pendingpos", ref _pendingPosition, 0.001f, 0f, 0f);
                ImGui.Separator();
                ImGui.TextColored((float4)KSAColor.Xkcd.GreenApple, "Rotation (pitch / yaw / roll, deg)");
                ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat3("##gt_pendingrot", ref _pendingRotation, 0.025f, -180f, 180f);
                ImGui.Separator();
                ImGui.TextColored((float4)KSAColor.Xkcd.OrangishRed, "Scale");
                ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat("##gt_pendingscale", ref _pendingScale, 0.001f, 0.05f, 20f);
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.BrightMagenta));
                ImGui.Checkbox("Lock Rotation##gt_pendinglockrot", ref _pendingLockRotation);
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
                if (ImGui.Button("Create Weld##gt_addweld"))
                    InitiateWeld(vehicles[_pendingSourceIndex], vehicles[_pendingTargetIndex], _pendingPosition, _pendingRotation, _pendingScale, _pendingLockRotation);

                ImGui.Text("Preset:");
                ImGui.SameLine();

                var presets = WeldPreset.Presets;
                var presetNames = new string[presets.Length];
                for (int i = 0; i < presets.Length; i++)
                    presetNames[i] = presets[i].Name;

                ImGui.SetNextItemWidth(-340f);
                ImGui.Combo("##gt_presetcombo", ref _selectedPresetIndex, presetNames, presetNames.Length);
                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32((float4)KSAColor.Xkcd.HotPink));
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(new float4(1f, 1f, 1f, 1f)));
                if (ImGui.Button("I'm feeling lucky##gt_ifl"))
                {
                    var preset = presets[_selectedPresetIndex];
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
            string header = $"Weld {i + 1}: {weld.Source.Id} -> {weld.Target.Id}##gt_{i}";
            if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Indent();
                ImGui.Text($"Source: {weld.Source.Id}  ->  Target: {weld.Target.Id}");
                ImGui.Separator();

                ImGui.TextColored((float4)KSAColor.Xkcd.Orangeish, "Position (x / y / z, m)");
                ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat3($"##gt_pos{i}", ref weld.Position, 0.001f, 0f, 0f);

                ImGui.Separator();
                ImGui.TextColored((float4)KSAColor.Xkcd.GreenApple, "Rotation (pitch / yaw / roll, deg)");
                ImGui.SetNextItemWidth(-1f);
                ImGui.DragFloat3($"##gt_rot{i}", ref weld.Rotation, 0.025f, -180f, 180f);

                ImGui.Separator();
                ImGui.TextColored((float4)KSAColor.Xkcd.OrangishRed, "Scale");
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat($"##gt_scale{i}", ref weld.Scale, 0.001f, 0.05f, 20f))
                    WeldEngine.ApplyVehicleScale(weld.Source, weld.Scale);

                ImGui.Separator();
                bool lockRot = weld.LockRotation;
                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32((float4)KSAColor.Xkcd.BrightMagenta));
                if (ImGui.Checkbox($"Lock Rotation##gt_{i}", ref lockRot))
                    weld.LockRotation = lockRot;
                ImGui.PopStyleColor();

                ImGui.Separator();
                if (ImGui.Button($"Unweld##gt_{i}"))
                    toRemove = weld;
                ImGui.Unindent();
                ImGui.Unindent();
            }
        }
        if (toRemove != null)
            RemoveWeld(toRemove);
    }

    public void Dispose()
    {
        foreach (var weld in _welds)
            WeldEngine.ApplyVehicleScale(weld.Source, 1.0f);
        _welds.Clear();
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
            WeldEngine.ApplyVehicleScale(source, scale);

        _pendingPosition = new float3(0f, 0f, 0f);
        _pendingRotation = new float3(0f, 0f, 0f);
        _pendingScale = 1f;
        _pendingLockRotation = true;

        SortWelds();
        Console.WriteLine($"grant/garys-torch: Welded {source.Id} to {target.Id}");
    }

    private void RemoveWeld(WeldEntry entry)
    {
        WeldEngine.ApplyVehicleScale(entry.Source, 1.0f);
        Console.WriteLine($"grant/garys-torch: Unwelded {entry.Source.Id} from {entry.Target.Id}");
        _welds.Remove(entry);
    }

    private void SortWelds()
    {
        var sorted = WeldEngine.TopologicalSort(_welds);
        _welds.Clear();
        foreach (var w in sorted)
            _welds.Add(w);
    }
}
