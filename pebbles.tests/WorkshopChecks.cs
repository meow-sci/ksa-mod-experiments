using System;
using System.Numerics;
using MeowSci.PebblesLib;

internal static class WorkshopChecks
{
    public static void Run()
    {
        var view = new WorkshopView { Target = new Vector3(1, 2, 3), Distance = 8 };
        var matrix = WorkshopMath.ViewProjection(view, 800, 600);
        Check(WorkshopMath.Project(view.Target, matrix, Vector2.Zero, new(800, 600), out var screen), "target projects");
        Check(Vector2.Distance(screen, new(400, 300)) < .001f, "camera target centered with Vulkan Y");
        Check(WorkshopMath.Ray(screen, matrix, Vector2.Zero, new(800, 600), out var rayStart, out var rayDirection), "unproject ray");
        Check(Vector3.Cross(Vector3.Normalize(view.Target - rayStart), rayDirection).Length() < .0001f, "projection/unprojection agree");
        Check(WorkshopMath.Plane(rayStart, rayDirection, view.Target, Vector3.Normalize(view.Eye - view.Target), out var point)
            && Vector3.Distance(point, view.Target) < .001f, "ray plane recovers target");
        Check(!WorkshopMath.Plane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitY, out _), "parallel plane drag rejected");
        Check(WorkshopMath.AxisDrag(new(50, 0), new(100, 0), 2) == 1, "axis drag converts pixels to meters");
        Check(WorkshopMath.AxisDrag(Vector2.One, Vector2.Zero, 2) == 0, "foreshortened handle stays finite");
        Check(Math.Abs(WorkshopMath.RotationDrag(new(0, 1), new(1, 0), Vector2.Zero) - MathF.PI / 2) < .0001f, "rotation handle quarter turn");
        foreach (var euler in new[] { new Vector3(25, 35, 45), new Vector3(-35, 120, -65), new Vector3(0, 180, 0) })
        {
            var q = WorkshopMath.Rotation(euler);
            var roundTrip = WorkshopMath.Rotation(WorkshopMath.Euler(q));
            Check(Math.Abs(Quaternion.Dot(q, roundTrip)) > .99999f, "Euler rotation preserves orientation");
        }
        Check(WorkshopMath.RayBox(new(0, 0, 5), -Vector3.UnitZ, Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, .7f), Vector3.One, out float distance) && distance > 3, "rotated collider hit");
        Check(!WorkshopMath.RayBox(new(4, 0, 5), -Vector3.UnitZ, Vector3.Zero, Quaternion.Identity, Vector3.One, out _), "collider miss");
        var minimum = new Vector3(-1, -2, -3); var maximum = new Vector3(1, 2, 3);
        var box = WorkshopColliders.Fit(ColliderKind.Box, minimum, maximum);
        Check(box.Dimensions == new Vec3(2, 4, 6), "box fit uses full dimensions");
        var sphere = WorkshopColliders.Fit(ColliderKind.Sphere, minimum, maximum);
        Check(sphere.Dimensions.X >= Vector3.Distance(minimum, maximum) - .0001f, "sphere contains bounds corners");
        var capsule = WorkshopColliders.Fit(ColliderKind.Capsule, minimum, maximum);
        Check(capsule.Dimensions.Y >= capsule.Dimensions.X, "capsule height includes end caps");
        float radius = capsule.Dimensions.X * .5f;
        float segmentHalf = (capsule.Dimensions.Y - capsule.Dimensions.X) * .5f;
        foreach (float x in new[] { minimum.X, maximum.X })
            foreach (float y in new[] { minimum.Y, maximum.Y })
                foreach (float z in new[] { minimum.Z, maximum.Z })
                {
                    var corner = new Vector3(x, y, z) - capsule.Position.Vector;
                    var nearestAxis = new Vector3(0, Math.Clamp(corner.Y, -segmentHalf, segmentHalf), 0);
                    Check(Vector3.Distance(corner, nearestAxis) <= radius + .0001f, "capsule fit contains every AABB corner");
                }
        box.Position = new(2, 3, 4); box.RotationDegrees = new(15, 30, 50);
        var mirror = WorkshopColliders.Mirror(box, 0);
        Check(mirror.Id != box.Id && mirror.Position == new Vec3(-2, 3, 4), "mirror produces independently identified collider");
        var twice = WorkshopColliders.Mirror(mirror, 0);
        Check(Math.Abs(Quaternion.Dot(WorkshopMath.Rotation(box.RotationDegrees.Vector), WorkshopMath.Rotation(twice.RotationDegrees.Vector))) > .99999f, "double mirror preserves rotation");
        var recipe = new ObjectRecipe(); recipe.Colliders.Add(box);
        var history = new WorkshopHistory(); history.Record(recipe);
        recipe.Colliders[0].Dimensions = new(9, 9, 9);
        var undone = history.Undo(recipe);
        Check(undone.Colliders[0].Dimensions == new Vec3(2, 4, 6), "undo stores detached geometry");
        Check(history.Redo(undone).Colliders[0].Dimensions == new Vec3(9, 9, 9), "redo restores geometry edit");
        var state = new WorkshopState { View = view, Object = recipe };
        var restored = RecipeCopy.Clone(state);
        Check(restored.View.Target == view.Target, "camera target serializes through Vec3 properties");
        restored.Object.Colliders[0].Position = Vec3.Zero;
        Check(recipe.Colliders[0].Position != Vec3.Zero, "workshop snapshot remains detached");
        WorkshopValidation.Validate(restored);
        restored.View.Distance = float.NaN;
        try { WorkshopValidation.Validate(restored); throw new Exception("invalid camera accepted"); }
        catch (InvalidOperationException) { }
        Console.WriteLine("PASS: Pebbles workshop projection, manipulators, fitting, mirror, detached history and camera persistence.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("Workshop: " + message); }
}
