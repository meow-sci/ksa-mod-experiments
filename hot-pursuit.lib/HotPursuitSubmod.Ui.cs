using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace MeowSci.HotPursuitLib;

public sealed partial class HotPursuitSubmod
{
    private static bool RenderCamera(HotPursuitCamera entry, int index)
    {
        var id = $"##hp_camera_{entry.Id}_{index}";
        var title = $"Camera {entry.Id}: {entry.TargetDescription}##hp_header_{entry.Id}";
        if (!MeowSci.KsaAbstractions.WorkspaceUi.Header(title, ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        ImGui.Text(entry.Status);
        ImGui.TextDisabled($"Stable target: {entry.VehicleId} / Part.InstanceId {entry.PartInstanceId}");

        if (entry.Viewport == null)
        {
            if (ImGui.Button($"Reopen viewport##hp_reopen_{entry.Id}"))
                Instance?.TryOpenViewport(entry);
        }
        else
        {
            if (ImGui.Checkbox($"Visible##hp_visible_{entry.Id}", ref entry.Visible))
                entry.Viewport.SetVisible(entry.Visible && entry.IsResolved);
        }

        ImGui.Spacing();
        ImGui.Text("Translation (m), clicked part-local frame");
        var translation = float3.Pack(in entry.Translation);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.DragFloat3($"##hp_translation_{entry.Id}", ref translation, 0.001f, 0f, 0f))
            entry.Translation = double3.Unpack(in translation);

        ImGui.Text("Rotation (pitch, yaw, roll), degrees from outward mount basis");
        var rotation = float3.Pack(in entry.RotationDeg);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.DragFloat3($"##hp_rotation_{entry.Id}", ref rotation, 0.25f, -180f, 180f))
            entry.RotationDeg = double3.Unpack(in rotation);

        ImGui.SetNextItemWidth(-1f);
        ImGui.DragFloat($"FOV (degrees)##hp_fov_{entry.Id}", ref entry.FieldOfView,
            0.5f, 15f, 120f, "%.1f");

        var width = entry.Width;
        var height = entry.Height;
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputInt($"Width##hp_width_{entry.Id}", ref width);
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputInt($"Height##hp_height_{entry.Id}", ref height);
        entry.Width = ClampDimension(width);
        entry.Height = ClampDimension(height);
        if (ImGui.Button($"Apply resize##hp_resize_{entry.Id}"))
            ApplyResize(entry);

        ImGui.Spacing();
        var remove = false;
        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
        if (ImGui.Button($"Remove##hp_remove_{entry.Id}"))
            remove = true;
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
        ImGui.Separator();
        return remove;
    }

    private static void ApplyResize(HotPursuitCamera entry)
    {
        if (entry.Viewport == null)
            return;
        entry.Viewport.SetResizeAllowed(true);
        entry.ResizePending = entry.Viewport.RequestResize(new int2(entry.Width, entry.Height));
        entry.Viewport.SetResizeAllowed(false);
        if (!entry.ResizePending)
            Console.WriteLine($"hot-pursuit: resize request rejected for camera #{entry.Id}");
    }

    private static int ClampDimension(int value) => value < 64 ? 64 : value > 2048 ? 2048 : value;
}
