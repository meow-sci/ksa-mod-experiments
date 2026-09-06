using System;
using System.Collections.Generic;
using System.Linq;

namespace MeowSci.PebblesLib;

/// <summary>Detached mesh, scale and target authoring.</summary>
public static class ClutterAuthoring
{
    public static void AssignMesh(ObjectRecipe item, string mesh, List<MaterialRecipe> materials)
    {
        if (string.IsNullOrWhiteSpace(mesh)) throw new InvalidOperationException("Choose a mesh first.");
        foreach (var lod in item.Lods) { lod.MeshIds = [mesh]; lod.Materials = RecipeCopy.Clone(materials); }
        item.Name = GlbIdentity.Label(mesh);
        item.Collision = CollisionPolicy.None;
        item.Colliders.Clear();
    }

    // Collider coordinates already include the preview transform. Scale offsets around
    // the mesh origin and dimensions once; uniform scaling preserves rotated primitives.
    public static void SetScale(ObjectRecipe item, float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0 || scale > 10000)
            throw new InvalidOperationException("Scale must be above zero and at most 10,000.");
        var previous = item.Transform.Scale;
        if (previous.X != previous.Y || previous.X != previous.Z)
            throw new InvalidOperationException("This older recipe has nonuniform scale; choose a new mesh recipe first.");
        float ratio = scale / previous.X;
        foreach (var collider in item.Colliders)
        {
            collider.Position = Vec3.From(item.Transform.Position.Vector + (collider.Position.Vector - item.Transform.Position.Vector) * ratio);
            collider.Dimensions = Vec3.From(collider.Dimensions.Vector * ratio);
            collider.HullScale = Vec3.From(collider.HullScale.Vector * ratio);
        }
        item.Transform.Scale = new(scale, scale, scale);
    }

    public static PebblesRecipe Replace(PebblesRecipe current, ObjectRecipe source, IEnumerable<string> targetNames)
    {
        RecipeValidation.Object(source);
        if (source.Lods.Any(l => l.MeshIds.Count == 0)) throw new InvalidOperationException("Choose a replacement mesh first.");
        var names = targetNames.ToHashSet(StringComparer.Ordinal);
        if (names.Count == 0) throw new InvalidOperationException("Select at least one clutter target type.");
        if (names.Any(n => current.Ecotypes.All(e => e.Name != n))) throw new InvalidOperationException("A selected clutter target type is unavailable. Select its replacement explicitly.");
        var result = RecipeCopy.Clone(current);
        foreach (var ecotype in result.Ecotypes.Where(e => names.Contains(e.Name)))
        {
            foreach (var target in ecotype.Objects)
            {
                target.Transform = RecipeCopy.Clone(source.Transform);
                target.Colliders = RecipeCopy.Clone(source.Colliders);
                target.Collision = source.Collision == CollisionPolicy.Custom ? CollisionPolicy.Custom : CollisionPolicy.None;
                target.MassKg = Math.Max(1, source.MassKg);
                for (int i = 0; i < 5; i++)
                {
                    // Retain each destination's identity and native LOD distances.
                    target.Lods[i].MeshIds = source.Lods[i].MeshIds.ToList();
                    target.Lods[i].Materials = RecipeCopy.Clone(source.Lods[i].Materials);
                }
            }
            ecotype.Placement.MinScale = ecotype.Placement.MaxScale = Vec3.One;
            bool collision = source.Collision == CollisionPolicy.Custom && source.Colliders.Any(c => c.Enabled);
            ecotype.CollisionMode = collision ? ClutterCollisionMode.PrimitiveList : ClutterCollisionMode.None;
            if (collision && ecotype.Placement.Orientation == OrientationMode.SurfaceNormalSmooth)
                ecotype.Placement.Orientation = OrientationMode.SurfaceNormal;
        }
        RecipeValidation.Validate(result);
        return result;
    }
}
