using System;
using System.Numerics;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PebblesLib;

public sealed partial class WorkshopEditor
{
    private void Line(ImDrawListPtr draw, Vector3 a, Vector3 b, ImColor8 color, float thickness = 1)
    {
        if (Project(a, out var pa) && Project(b, out var pb)) draw.AddLine(Pixel(pa), Pixel(pb), color, thickness);
    }

    private void DrawGrid(ImDrawListPtr draw)
    {
        float spacing = MathF.Pow(10, MathF.Floor(MathF.Log10(Math.Max(.001f, _state.View.Distance * .1f))));
        var center = _state.View.Target;
        center.X = MathF.Round(center.X / spacing) * spacing;
        center.Z = MathF.Round(center.Z / spacing) * spacing; center.Y = 0;
        for (int i = -10; i <= 10; i++)
        {
            var color = (ImColor8)0x404c4c4cu;
            Line(draw, center + new Vector3(i * spacing, 0, -10 * spacing), center + new Vector3(i * spacing, 0, 10 * spacing), color);
            Line(draw, center + new Vector3(-10 * spacing, 0, i * spacing), center + new Vector3(10 * spacing, 0, i * spacing), color);
        }
    }

    private void DrawCollider(ImDrawListPtr draw, ColliderRecipe collider)
    {
        bool selected = collider.Id == _state.SelectedColliderId;
        ImColor8 color = selected ? (ImColor8)0xff5adfffu : collider.Enabled ? (ImColor8)0xff7cdd8du : (ImColor8)0xff888888u;
        float width = selected ? 2 : 1;
        var rotation = WorkshopMath.Rotation(collider.RotationDegrees.Vector);
        Vector3 Transform(Vector3 p) => collider.Position.Vector + Vector3.Transform(p, rotation);
        void Segment(Vector3 a, Vector3 b) => Line(draw, Transform(a), Transform(b), color, width);
        void Arc(float radius, float y, int plane, float from = 0, float to = MathF.PI * 2, float yRadius = -1)
        {
            Vector3 Point(float t) => plane switch
            {
                0 => new Vector3(radius * MathF.Cos(t), y, radius * MathF.Sin(t)),
                1 => new Vector3(radius * MathF.Cos(t), y + (yRadius < 0 ? radius : yRadius) * MathF.Sin(t), 0),
                _ => new Vector3(0, y + (yRadius < 0 ? radius : yRadius) * MathF.Sin(t), radius * MathF.Cos(t))
            };
            var previous = Point(from);
            for (int i = 1; i <= 48; i++) { var next = Point(from + (to - from) * i / 48); Segment(previous, next); previous = next; }
        }
        var half = WorkshopColliders.HalfExtents(collider);
        if (collider.Kind == ColliderKind.ConvexHull)
        {
            if (!DrawHull(draw, collider, color, width) && Project(collider.Position.Vector, out var origin))
                draw.AddText(Pixel(origin), color, "Hull source unavailable"u8);
        }
        else if (collider.Kind == ColliderKind.Box)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        var start = Vector3.Zero;
                        int other = (axis + 1) % 3, third = (axis + 2) % 3;
                        start = WorkshopMath.SetComponent(start, other, a * WorkshopMath.Component(half, other));
                        start = WorkshopMath.SetComponent(start, third, b * WorkshopMath.Component(half, third));
                        Segment(start - WorkshopMath.Axis(axis) * WorkshopMath.Component(half, axis), start + WorkshopMath.Axis(axis) * WorkshopMath.Component(half, axis));
                    }
        }
        else if (collider.Kind == ColliderKind.Sphere) { Arc(half.X, 0, 0); Arc(half.X, 0, 1); Arc(half.X, 0, 2); }
        else
        {
            float radius = half.X;
            float side = collider.Kind == ColliderKind.Capsule ? Math.Max(0, half.Y - radius) : half.Y;
            Arc(radius, side, 0); Arc(radius, -side, 0);
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector3(radius * MathF.Cos(i * MathF.PI / 2), 0, radius * MathF.Sin(i * MathF.PI / 2));
                Segment(p + Vector3.UnitY * side, p - Vector3.UnitY * side);
            }
            if (collider.Kind == ColliderKind.Capsule)
            {
                Arc(radius, side, 1, 0, MathF.PI); Arc(radius, side, 2, 0, MathF.PI);
                Arc(radius, -side, 1, MathF.PI, MathF.PI * 2); Arc(radius, -side, 2, MathF.PI, MathF.PI * 2);
            }
        }
    }

    private int DrawHandles(ImDrawListPtr draw, ColliderRecipe collider, Vector2 mouse)
    {
        if (!Project(collider.Position.Vector, out var center)) return -1;
        _handleLength = _state.View.Distance * .16f;
        int hit = -1; float best = 9;
        for (int axis = 0; axis < 3; axis++)
        {
            var color = _activeAxis == axis ? (ImColor8)0xffffffffu : AxisColors[axis];
            var direction = HandleAxis(collider, axis);
            if (_state.Tool == WorkshopTool.Rotate)
            {
                var tangent = Vector3.Normalize(Vector3.Cross(direction, Math.Abs(direction.Y) < .9f ? Vector3.UnitY : Vector3.UnitX));
                var bitangent = Vector3.Cross(direction, tangent);
                var previous = collider.Position.Vector + tangent * _handleLength;
                for (int i = 1; i <= 64; i++)
                {
                    float angle = i * MathF.PI * 2 / 64;
                    var next = collider.Position.Vector + (tangent * MathF.Cos(angle) + bitangent * MathF.Sin(angle)) * _handleLength;
                    if (Project(previous, out var a) && Project(next, out var b))
                    {
                        draw.AddLine(Pixel(a), Pixel(b), color, 2);
                        float distance = WorkshopMath.SegmentDistance(mouse, a, b);
                        if (distance < best) { best = distance; hit = axis; }
                    }
                    previous = next;
                }
            }
            else if (Project(collider.Position.Vector + direction * _handleLength, out var tip))
            {
                draw.AddLine(Pixel(center), Pixel(tip), color, 3);
                if (_state.Tool == WorkshopTool.Resize)
                    draw.AddRectFilled(Pixel(tip - new Vector2(5)), Pixel(tip + new Vector2(5)), color);
                else draw.AddCircleFilled(Pixel(tip), 5, color);
                draw.AddText(Pixel(tip + new Vector2(7, 2)), color, AxisNames[axis]);
                float distance = WorkshopMath.SegmentDistance(mouse, center, tip);
                if (distance < best) { best = distance; hit = axis; }
            }
        }
        return hit;
    }
}
