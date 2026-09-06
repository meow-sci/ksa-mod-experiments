using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace MeowSci.PebblesLib;

/// <summary>Detached camera data; matrix convention matches the private Vulkan preview.</summary>
public sealed class WorkshopView
{
    public Vec3 TargetPosition { get; set; }
    [JsonIgnore] public Vector3 Target { get => TargetPosition.Vector; set => TargetPosition = Vec3.From(value); }
    public float YawRadians { get; set; } = .65f;
    public float PitchRadians { get; set; } = .35f;
    public float Distance { get; set; } = 5f;
    public float FovRadians { get; set; } = MathF.PI / 4;
    [JsonIgnore] public Vector3 Eye => Target + new Vector3(MathF.Cos(PitchRadians) * MathF.Sin(YawRadians),
        MathF.Sin(PitchRadians), MathF.Cos(PitchRadians) * MathF.Cos(YawRadians)) * Distance;
    public Matrix4x4 GetViewProjection(int width, int height) => WorkshopMath.ViewProjection(this, width, height);
    public WorkshopView Copy() => new() { Target = Target, YawRadians = YawRadians, PitchRadians = PitchRadians, Distance = Distance, FovRadians = FovRadians };
}

public static class WorkshopMath
{
    public static Matrix4x4 ViewProjection(WorkshopView view, int width, int height)
    {
        float near = Math.Max(.00001f, view.Distance * .001f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(Math.Clamp(view.FovRadians, .1f, 2.8f),
            Math.Max(1, width) / (float)Math.Max(1, height), near, Math.Max(near * 10, view.Distance * 1000));
        projection.M22 *= -1; // positive-height Vulkan viewport
        return Matrix4x4.CreateLookAt(view.Eye, view.Target, Vector3.UnitY) * projection;
    }

    public static bool Project(Vector3 point, Matrix4x4 matrix, Vector2 origin, Vector2 size, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(point, 1), matrix);
        screen = default;
        if (clip.W <= .000001f || !float.IsFinite(clip.W)) return false;
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        screen = origin + new Vector2(ndc.X + 1, ndc.Y + 1) * size * .5f;
        return ndc.Z >= 0 && ndc.Z <= 1;
    }

    public static bool Ray(Vector2 screen, Matrix4x4 matrix, Vector2 origin, Vector2 size, out Vector3 start, out Vector3 direction)
    {
        start = direction = default;
        if (size.X <= 0 || size.Y <= 0 || !Matrix4x4.Invert(matrix, out var inverse)) return false;
        var ndc = (screen - origin) / size * 2 - Vector2.One;
        var a = Vector4.Transform(new Vector4(ndc, 0, 1), inverse);
        // Avoid far-plane rounding at large far/near ratios.
        var b = Vector4.Transform(new Vector4(ndc, .99f, 1), inverse);
        if (Math.Abs(a.W) < 1e-8f || Math.Abs(b.W) < 1e-8f) return false;
        start = new Vector3(a.X, a.Y, a.Z) / a.W;
        var delta = new Vector3(b.X, b.Y, b.Z) / b.W - start;
        if (delta.LengthSquared() < 1e-12f) return false;
        direction = Vector3.Normalize(delta);
        return true;
    }

    public static bool Plane(Vector3 start, Vector3 direction, Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        float denominator = Vector3.Dot(normal, direction);
        if (Math.Abs(denominator) < .00001f) return false;
        float t = Vector3.Dot(point - start, normal) / denominator;
        if (t < 0 || !float.IsFinite(t)) return false;
        hit = start + direction * t;
        return true;
    }

    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var delta = b - a;
        float t = delta.LengthSquared() < .0001f ? 0 : Math.Clamp(Vector2.Dot(point - a, delta) / delta.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + delta * t);
    }

    public static float AxisDrag(Vector2 delta, Vector2 projectedAxis, float worldLength)
    {
        float length = projectedAxis.LengthSquared();
        return length < 4 ? 0 : Vector2.Dot(delta, projectedAxis) / length * worldLength;
    }

    public static Quaternion Rotation(Vector3 degrees) => Quaternion.CreateFromRotationMatrix(
        Matrix4x4.CreateRotationX(degrees.X * MathF.PI / 180) * Matrix4x4.CreateRotationY(degrees.Y * MathF.PI / 180) * Matrix4x4.CreateRotationZ(degrees.Z * MathF.PI / 180));

    public static Vector3 Euler(Quaternion rotation)
    {
        // Native KSA XYZ rotation: row-vector Rx * Ry * Rz, principal Euler solution.
        var q = Quaternion.Normalize(rotation);
        float x = MathF.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
        float y = MathF.Asin(Math.Clamp(2 * (q.W * q.Y - q.Z * q.X), -1, 1));
        float z = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
        return new Vector3(x, y, z) * (180 / MathF.PI);
    }

    public static float RotationDrag(Vector2 mouse, Vector2 start, Vector2 center) =>
        MathF.Atan2(Cross(start - center, mouse - center), Vector2.Dot(start - center, mouse - center));

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static bool RayBox(Vector3 start, Vector3 direction, Vector3 center, Quaternion rotation, Vector3 half, out float distance)
    {
        var inverse = Quaternion.Inverse(rotation);
        start = Vector3.Transform(start - center, inverse);
        direction = Vector3.Transform(direction, inverse);
        float enter = 0, exit = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            float o = Component(start, i), d = Component(direction, i), h = Component(half, i);
            if (Math.Abs(d) < 1e-7f) { if (Math.Abs(o) > h) { distance = 0; return false; } continue; }
            float a = (-h - o) / d, b = (h - o) / d;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a); exit = Math.Min(exit, b);
            if (exit < enter) { distance = 0; return false; }
        }
        distance = enter;
        return true;
    }

    public static float Component(Vector3 value, int axis) => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
    public static Vector3 SetComponent(Vector3 value, int axis, float component) => axis switch
    { 0 => new(component, value.Y, value.Z), 1 => new(value.X, component, value.Z), _ => new(value.X, value.Y, component) };
    public static Vector3 Axis(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
}
