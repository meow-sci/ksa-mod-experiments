using System;
using System.Collections.Generic;
using System.Numerics;

namespace MeowSci.PebblesLib;

public enum WorkshopTool { Move, Rotate, Resize }

/// <summary>Only authoring data; assigning this state cannot allocate a preview or affect live clutter.</summary>
public sealed class WorkshopState
{
    public bool IsOpen { get; set; }
    public Dictionary<string, bool> Sections { get; set; } = new(StringComparer.Ordinal);
    public float InspectorScroll { get; set; }
    public ObjectRecipe Object { get; set; } = new();
    public string SelectedColliderId { get; set; } = "";
    public WorkshopTool Tool { get; set; }
    public int NewColliderKind { get; set; }
    public int MirrorAxis { get; set; }
    public int PreviewLod { get; set; }
    public string AssetFilter { get; set; } = "";
    public WorkshopView View { get; set; } = new();
    public bool ShowMesh { get; set; } = true;
    public bool ShowColliders { get; set; } = true;
    public bool LocalAxes { get; set; } = true;
    public bool Snap { get; set; }
    public float MoveSnap { get; set; } = .1f;
    public float AngleSnap { get; set; } = 15;
    public float SizeSnap { get; set; } = .1f;
    public float Width { get; set; } = 1000;
    public float Height { get; set; } = 720;
    public float WindowX { get; set; } = -1;
    public float WindowY { get; set; } = -1;

    public void Validate()
    {
        RecipeValidation.Object(Object);
        if (Sections == null || Sections.Count > 1024 || !float.IsFinite(InspectorScroll) || InspectorScroll < 0) throw new InvalidOperationException("Invalid Workshop layout.");
        if (AssetFilter == null || AssetFilter.Length > 128 || SelectedColliderId == null) throw new InvalidOperationException("Invalid workshop selector.");
        if (View == null || !Enum.IsDefined(Tool) || NewColliderKind is < 0 or > 3 || MirrorAxis is < 0 or > 2 || PreviewLod is < 0 or > 4)
            throw new InvalidOperationException("Invalid workshop view or tool.");
        static void Finite(float value) { if (!float.IsFinite(value)) throw new InvalidOperationException("Workshop values must be finite."); }
        foreach (float value in new[] { View.Target.X, View.Target.Y, View.Target.Z, View.YawRadians, View.PitchRadians,
            View.Distance, View.FovRadians, MoveSnap, AngleSnap, SizeSnap, Width, Height, WindowX, WindowY }) Finite(value);
        if (View.Distance < .002f || View.Distance > 100000 || Math.Abs(View.PitchRadians) > 1.5f ||
            View.FovRadians < .1f || View.FovRadians > 2.8f || MoveSnap <= 0 || AngleSnap <= 0 || SizeSnap <= 0 ||
            Width < 100 || Height < 100) throw new InvalidOperationException("Workshop view or snap steps are out of range.");
    }
}

/// <summary>Bounded detached history. A whole drag is committed as one edit.</summary>
public sealed class WorkshopHistory
{
    private readonly List<ObjectRecipe> _undo = new(), _redo = new();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public void Record(ObjectRecipe previous)
    {
        _undo.Add(RecipeCopy.Clone(previous)); _redo.Clear();
        if (_undo.Count > 80) _undo.RemoveAt(0);
    }
    public ObjectRecipe Undo(ObjectRecipe current) => Transfer(_undo, _redo, current);
    public ObjectRecipe Redo(ObjectRecipe current) => Transfer(_redo, _undo, current);
    private static ObjectRecipe Transfer(List<ObjectRecipe> source, List<ObjectRecipe> destination, ObjectRecipe current)
    {
        if (source.Count == 0) return current;
        destination.Add(RecipeCopy.Clone(current));
        var next = source[^1]; source.RemoveAt(source.Count - 1); return RecipeCopy.Clone(next);
    }
    public void Clear() { _undo.Clear(); _redo.Clear(); }
}

public static class WorkshopColliders
{
    public static ColliderRecipe Fit(ColliderKind kind, Vector3 minimum, Vector3 maximum)
    {
        var size = Vector3.Max(maximum - minimum, new Vector3(.001f));
        float diameter = Math.Max(size.X, size.Z);
        if (kind == ColliderKind.Sphere) size = new Vector3(size.Length());
        else if (kind == ColliderKind.Capsule) { diameter = MathF.Sqrt(size.X * size.X + size.Z * size.Z); size = new Vector3(diameter, size.Y + diameter, diameter); }
        else if (kind == ColliderKind.Cylinder) { diameter = MathF.Sqrt(size.X * size.X + size.Z * size.Z); size = new Vector3(diameter, size.Y, diameter); }
        return new() { Kind = kind, Name = kind.ToString(), Position = Vec3.From((minimum + maximum) * .5f), Dimensions = Vec3.From(size) };
    }

    public static ColliderRecipe Mirror(ColliderRecipe source, int axis)
    {
        var result = RecipeCopy.Clone(source);
        result.Id = Guid.NewGuid().ToString("N"); result.Name += " mirror";
        result.Position = Vec3.From(WorkshopMath.SetComponent(source.Position.Vector, axis, -WorkshopMath.Component(source.Position.Vector, axis)));
        // S * R * S is a proper rotation of the mirrored symmetric primitive.
        var signs = Vector3.One; signs = WorkshopMath.SetComponent(signs, axis, -1);
        var mirror = Matrix4x4.CreateScale(signs);
        var rotation = mirror * Matrix4x4.CreateFromQuaternion(WorkshopMath.Rotation(source.RotationDegrees.Vector)) * mirror;
        result.RotationDegrees = Vec3.From(WorkshopMath.Euler(Quaternion.CreateFromRotationMatrix(rotation)));
        return result;
    }

    public static Vector3 HalfExtents(ColliderRecipe collider) => collider.Kind switch
    {
        ColliderKind.Sphere => new Vector3(collider.Dimensions.X * .5f),
        ColliderKind.Capsule or ColliderKind.Cylinder => new(collider.Dimensions.X * .5f, collider.Dimensions.Y * .5f, collider.Dimensions.X * .5f),
        _ => collider.Dimensions.Vector * .5f
    };
}

public static class WorkshopValidation
{
    public static void Validate(WorkshopState state)
    {
        if (state == null) throw new InvalidOperationException("Workshop state cannot be null.");
        state.Validate();
    }
}
