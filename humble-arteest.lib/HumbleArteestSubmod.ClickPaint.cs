using System;
using System.Collections.Generic;
using System.Linq;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using MeowSci.KsaAbstractions;

namespace MeowSci.HumbleArteestLib;

public sealed partial class HumbleArteestSubmod
{
    private int _clickScope;
    private float _clickRange = 2000;
    private bool _paintArmed;
    private int _armedScope, _armedBlend;
    private float _armedPaintRange;
    private float3 _armedColor;
    private string? _clickStatus;

    private void RenderClickPaint()
    {
        if (!WorkspaceUi.Header("Paint at cursor", ImGuiTreeNodeFlags.DefaultOpen)) return;
        using (new FormGrid("##click-paint-grid"))
        {
            ImGui.Combo(FormField.Label("Click scope"), ref _clickScope,
                new[] { "Individual mesh instance", "Whole clicked subpart", "All instances of clicked mesh" }, 3);
            ImGui.DragFloat(FormField.Label("Pick range (m)"), ref _clickRange, 10, 1, 100_000);
        }
        ImGui.TextWrapped("Uses the brush and blend above. Click a craft mesh; Esc cancels. Shared mesh paint also applies to future instances. More specific overrides take priority. Blend is shared by all paint.");
        ImGui.BeginDisabled(Program.EditorFlag);
        if (ImGui.Button("Paint at next world click", new float2(-1, 0)))
        {
            _armedScope = _clickScope; _armedBlend = _settings.Blend; _armedColor = _settings.Color;
            _armedPaintRange = Math.Clamp(_clickRange, 1, 100_000); _paintArmed = true; _clickStatus = null;
        }
        ImGui.EndDisabled();
        if (Program.EditorFlag) ImGui.TextDisabled("Cursor painting is available in flight.");
        if (_paintArmed && ImGui.Button("Cancel click painting", new float2(-1, 0))) CancelAuthoringGesture();
        if (_clickStatus != null) ImGui.TextWrapped(_clickStatus);
    }

    public void CancelAuthoringGesture() { _paintArmed = false; _clickStatus = null; }

    public void RenderFloatingWindows()
    {
        if (!_paintArmed) return;
        if (Program.EditorFlag || ImGui.IsKeyPressed(ImGuiKey.Escape)) { CancelAuthoringGesture(); return; }
        var pos = ImGui.GetMousePos() + new float2(18, 18);
        ImString hint = "Click a craft mesh to paint (Esc cancels)";
        var draw = ImGui.GetForegroundDrawList();
        draw.AddText(pos + new float2(1, 1), ImColor8.Black, hint);
        draw.AddText(pos, ImColor8.White, hint);
        if (ImGui.GetIO().WantCaptureMouse || !ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        if (PaintPicker.Pick(_armedPaintRange) is not { } hit)
        { _clickStatus = "No paintable mesh in range. Click again or press Esc."; return; }
        if (!VehiclePaint.Enable()) { _clickStatus = VehiclePaint.LastError ?? "Paint shader could not be enabled."; return; }
        VehiclePaint.BlendMode = (PaintBlendMode)_armedBlend;
        switch (_armedScope)
        {
            case 0: VehiclePaint.SetMeshInstance(hit, _armedColor); break;
            case 1: VehiclePaint.SetPart(hit.Part, _armedColor); break;
            case 2: VehiclePaint.SetMesh(hit.MeshId, _armedColor); break;
        }
        _paintArmed = false;
        _clickStatus = $"Painted {hit.Part.DisplayName} / {hit.MeshId}. Manage the result in Live State.";
    }

    private IEnumerable<ILiveStateItem> GetMeshPaintItems()
    {
        foreach (var key in VehiclePaint.PaintedMeshInstances.ToArray())
            yield return new LiveStateItem<VehiclePaint.MeshInstance>("paint-mesh-instance/" + LiveIdentity.Get(key.Part) + "/" + key.MeshId,
                "Mesh instance paint", key.Part.DisplayName + " #" + key.Part.InstanceId + " / " + key.MeshId, key, k =>
                {
                    if (VehiclePaint.TryGetMeshInstanceColor(k, out var color))
                    {
                        if (ImGui.ColorEdit3(FormField.Label("Color"), ref color)) VehiclePaint.SetMeshInstance(k, color);
                        if (ImGui.Button("Copy brush to workspace", new float2(-1, 0))) _settings.Color = color;
                    }
                    if (ImGui.Button("Remove mesh instance paint", new float2(-1, 0))) VehiclePaint.ClearMeshInstance(k);
                });
        foreach (var id in VehiclePaint.PaintedMeshes.ToArray())
            yield return new LiveStateItem<string>("paint-mesh/" + id, "Shared mesh paint", id, id, k =>
            {
                if (VehiclePaint.TryGetMeshColor(k, out var color))
                {
                    if (ImGui.ColorEdit3(FormField.Label("Color"), ref color)) VehiclePaint.SetMesh(k, color);
                    if (ImGui.Button("Copy brush to workspace", new float2(-1, 0))) _settings.Color = color;
                }
                if (ImGui.Button("Remove shared mesh paint", new float2(-1, 0))) VehiclePaint.ClearMesh(k);
            });
    }
}
