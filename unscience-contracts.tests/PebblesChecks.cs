using System;
using System.Text.Json;
using MeowSci.PebblesLib;

internal static class PebblesChecks
{
    public static void Run()
    {
        var recipe = new PebblesRecipe { Ecotypes = [new() { Name = "Rocks", Objects = [new() { SourceId = "Rock" }] }] };
        RecipeValidation.Validate(recipe);
        recipe.Ecotypes[0].Objects[0].Transform.Position = new(1, 2, 3);
        var detached = RecipeCopy.Clone(recipe);
        detached.Ecotypes[0].Objects[0].Transform.Position = new(8, 9, 10);
        Check(recipe.Ecotypes[0].Objects[0].Transform.Position.X == 1, "Draft clones must not alias live recipes.");
        Check(!JsonSerializer.Serialize(recipe).Contains("\"Vector\""), "Computed vectors must not leak into saved recipes.");
        var roundtrip = RecipeCopy.Clone(recipe);
        Check(roundtrip.Ecotypes[0].Objects[0].Transform.Position == new Vec3(1, 2, 3), "Vector transforms round trip.");
        var placement = detached.Ecotypes[0].Placement;
        detached.Ecotypes[0].CollisionMode = ClutterCollisionMode.PrimitiveList;
        placement.MaxScale = new(1, 2, 1);
        Reject(() => RecipeValidation.Validate(detached));
        placement.MaxScale = Vec3.One; placement.Orientation = OrientationMode.SurfaceNormalSmooth;
        Reject(() => RecipeValidation.Validate(detached));
        detached.Ecotypes[0].CollisionMode = ClutterCollisionMode.None;
        RecipeValidation.Validate(detached);
        placement.AltitudeCurve[1].Altitude = placement.AltitudeCurve[0].Altitude;
        Reject(() => RecipeValidation.Validate(detached));
        var item = new ObjectRecipe { Colliders = [new() { Kind = ColliderKind.Capsule, Dimensions = new(2, 1, 2) }] };
        Reject(() => RecipeValidation.Object(item));
        item.Colliders[0].Dimensions = new(2, 4, 2); RecipeValidation.Object(item);
        item.Colliders.Add(RecipeCopy.Clone(item.Colliders[0])); Reject(() => RecipeValidation.Object(item));
        item.Colliders.Clear(); item.Lods[1].MinScreenSize = 2; Reject(() => RecipeValidation.Object(item));
        item.Lods[1].MinScreenSize = 0; item.Lods.RemoveAt(4); Reject(() => RecipeValidation.Object(item));
        recipe.CandidateBudget = long.MaxValue; Reject(() => RecipeValidation.Validate(recipe));
        Console.WriteLine("PASS: Pebbles detached recipes, transform round trips, collider geometry, collision/placement constraints and resource budgets.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid Pebbles recipe accepted."); }
}
