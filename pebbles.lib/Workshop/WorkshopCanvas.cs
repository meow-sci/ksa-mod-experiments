using System;
using System.Numerics;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.PebblesLib;

public sealed partial class WorkshopEditor
{
    private bool _frameAfterRefresh;
    private Vector2 _origin, _size, _lastMouse, _dragMouse, _dragAxisScreen, _dragCenterScreen;
    private Matrix4x4 _matrix;
    private ObjectRecipe? _dragBefore;
    private ColliderRecipe? _dragCollider;
    private int _activeAxis = -1, _cameraDrag;
    private float _handleLength;
    private Vector3 _dragAxisWorld;
    private static readonly ImColor8[] AxisColors = { (ImColor8)0xff6060eeu, (ImColor8)0xff70d070u, (ImColor8)0xffffb050u };

    private void FrameMesh()
    {
        _state.View.Target = (_preview.BoundsMin + _preview.BoundsMax) * .5f;
        float radius = Math.Max(.01f, Vector3.Distance(_preview.BoundsMin, _preview.BoundsMax) * .5f);
        _state.View.Distance = radius / MathF.Sin(_state.View.FovRadians * .5f) * 1.3f;
    }

    private void Canvas(float height)
    {
        var pos = ImGui.GetCursorScreenPos();
        var extent = new float2(Math.Max(100, ImGui.GetContentRegionAvail().X), height);
        _width = (int)extent.X; _height = (int)extent.Y;
        _origin = new(pos.X, pos.Y); _size = new(extent.X, extent.Y);
        _matrix = !_stale && _preview.IsReady && _state.ShowMesh ? _preview.ViewProjection : WorkshopMath.ViewProjection(_state.View, _width, _height);
        ImGui.InvisibleButton("##workshop-canvas"u8, extent,
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        bool hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();
        ImGui.PushClipRect(pos, pos + extent, true);
        try
        {
        draw.AddRectFilled(pos, pos + extent, (ImColor8)0xff262321u);
        if (!_stale && _preview.IsReady && _state.ShowMesh) draw.AddImage(_preview.Texture, pos, pos + extent);
        DrawGrid(draw);
        if (_state.ShowColliders)
            foreach (var collider in _state.Object.Colliders)
                if (collider.Visible) DrawCollider(draw, collider);
        int hoveredAxis = -1;
        var mouse = ImGui.GetMousePos(); var mouseVector = new Vector2(mouse.X, mouse.Y);
        if (Selected is { Visible: true } selected && _state.ShowColliders)
            hoveredAxis = DrawHandles(draw, selected, mouseVector);
        HandleInput(hovered, mouseVector, hoveredAxis);
        }
        finally { ImGui.PopClipRect(); }
    }

    private void HandleInput(bool hovered, Vector2 mouse, int hoveredAxis)
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && (_activeAxis >= 0 || _cameraDrag != 0)) { CancelGesture(); return; }
        if (hovered && _activeAxis < 0)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) { _cameraDrag = 1; _lastMouse = mouse; }
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) { _cameraDrag = 2; _lastMouse = mouse; }
            float wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0) _state.View.Distance = Math.Clamp(_state.View.Distance * MathF.Exp(-wheel * .13f), .002f, 100000);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (hoveredAxis >= 0 && Selected is { } selected) BeginDrag(selected, hoveredAxis, mouse);
                else PickCollider(mouse);
            }
        }
        if (_cameraDrag != 0)
        {
            var button = _cameraDrag == 1 ? ImGuiMouseButton.Right : ImGuiMouseButton.Middle;
            if (!ImGui.IsMouseDown(button)) _cameraDrag = 0;
            else
            {
                var delta = mouse - _lastMouse; _lastMouse = mouse;
                if (_cameraDrag == 1)
                {
                    _state.View.YawRadians -= delta.X * .009f;
                    _state.View.PitchRadians = Math.Clamp(_state.View.PitchRadians + delta.Y * .009f, -1.5f, 1.5f);
                }
                else
                {
                    var forward = Vector3.Normalize(_state.View.Target - _state.View.Eye);
                    var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                    var up = Vector3.Cross(right, forward);
                    float scale = _state.View.Distance * 2 * MathF.Tan(_state.View.FovRadians * .5f) / _size.Y;
                    _state.View.Target += (-right * delta.X + up * delta.Y) * scale;
                }
            }
        }
        if (_activeAxis >= 0)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) Drag(mouse);
            else
            {
                if (_dragBefore != null) _history.Record(_dragBefore);
                _dragBefore = null; _activeAxis = -1;
            }
        }
    }

    private void PickCollider(Vector2 mouse)
    {
        if (!WorkshopMath.Ray(mouse, _matrix, _origin, _size, out var start, out var direction)) return;
        string closest = ""; float distance = float.MaxValue;
        foreach (var collider in _state.Object.Colliders)
        {
            if (!collider.Visible) continue;
            var bounds = PickBounds(collider);
            if (WorkshopMath.RayBox(start, direction, bounds.Center,
                WorkshopMath.Rotation(collider.RotationDegrees.Vector), bounds.Half, out float d) && d < distance)
            { distance = d; closest = collider.Id; }
        }
        _state.SelectedColliderId = closest;
    }

    private void BeginDrag(ColliderRecipe collider, int axis, Vector2 mouse)
    {
        _dragBefore = RecipeCopy.Clone(_state.Object); _dragCollider = RecipeCopy.Clone(collider);
        _activeAxis = axis; _dragMouse = mouse;
        _dragAxisWorld = HandleAxis(collider, axis);
        Project(collider.Position.Vector, out _dragCenterScreen);
        Project(collider.Position.Vector + _dragAxisWorld * _handleLength, out var tip);
        _dragAxisScreen = tip - _dragCenterScreen;
    }

    private void Drag(Vector2 mouse)
    {
        if (Selected is not { } current || _dragCollider == null) return;
        var original = _dragCollider;
        float delta = WorkshopMath.AxisDrag(mouse - _dragMouse, _dragAxisScreen, _handleLength);
        if (_state.Tool == WorkshopTool.Move)
        {
            delta = Snap(delta, _state.MoveSnap);
            current.Position = Vec3.From(original.Position.Vector + _dragAxisWorld * delta);
        }
        else if (_state.Tool == WorkshopTool.Rotate)
        {
            float angle = WorkshopMath.RotationDrag(mouse, _dragMouse, _dragCenterScreen);
            if (WorkshopMath.Ray(_dragMouse, _matrix, _origin, _size, out var a, out var ad) &&
                WorkshopMath.Ray(mouse, _matrix, _origin, _size, out var b, out var bd) &&
                WorkshopMath.Plane(a, ad, original.Position.Vector, _dragAxisWorld, out var ap) &&
                WorkshopMath.Plane(b, bd, original.Position.Vector, _dragAxisWorld, out var bp))
            {
                var from = ap - original.Position.Vector; var to = bp - original.Position.Vector;
                angle = MathF.Atan2(Vector3.Dot(_dragAxisWorld, Vector3.Cross(from, to)), Vector3.Dot(from, to));
            }
            angle = Snap(angle * 180 / MathF.PI, _state.AngleSnap) * MathF.PI / 180;
            var rotation = Quaternion.CreateFromAxisAngle(_dragAxisWorld, angle) * WorkshopMath.Rotation(original.RotationDegrees.Vector);
            current.RotationDegrees = Vec3.From(WorkshopMath.Euler(rotation));
        }
        else
        {
            if (original.Kind == ColliderKind.ConvexHull)
            {
                float scale = WorkshopMath.Component(original.HullScale.Vector, _activeAxis);
                float extent = _hulls.TryGetValue(original.HullMeshId, out var hull) ? WorkshopMath.Component(hull.Maximum - hull.Minimum, _activeAxis) : 1;
                current.HullScale = Vec3.From(WorkshopMath.SetComponent(original.HullScale.Vector, _activeAxis, Math.Max(.001f, scale + Snap(delta * 2, _state.SizeSnap) / Math.Max(.001f, extent))));
                return;
            }
            var dimensions = original.Dimensions.Vector;
            int dimensionAxis = original.Kind == ColliderKind.Sphere ? 0 :
                original.Kind is ColliderKind.Capsule or ColliderKind.Cylinder ? (_activeAxis == 1 ? 1 : 0) : _activeAxis;
            float value = Math.Max(.001f, Snap(WorkshopMath.Component(dimensions, dimensionAxis) + delta * 2, _state.SizeSnap));
            dimensions = WorkshopMath.SetComponent(dimensions, dimensionAxis, value);
            if (original.Kind == ColliderKind.Sphere) dimensions = new Vector3(value);
            else if (original.Kind is ColliderKind.Capsule or ColliderKind.Cylinder) dimensions.Z = dimensions.X;
            if (original.Kind == ColliderKind.Capsule) dimensions.Y = Math.Max(dimensions.Y, dimensions.X);
            current.Dimensions = Vec3.From(dimensions);
        }
    }

    private float Snap(float value, float interval) => _state.Snap && interval > 0 ? MathF.Round(value / interval) * interval : value;
    private Vector3 HandleAxis(ColliderRecipe collider, int axis) => _state.LocalAxes || _state.Tool == WorkshopTool.Resize
        ? Vector3.Transform(WorkshopMath.Axis(axis), WorkshopMath.Rotation(collider.RotationDegrees.Vector)) : WorkshopMath.Axis(axis);
    private bool Project(Vector3 point, out Vector2 screen) => WorkshopMath.Project(point, _matrix, _origin, _size, out screen);
    private static float2 Pixel(Vector2 point) => new(point.X, point.Y);
}
