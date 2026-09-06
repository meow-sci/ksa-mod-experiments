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
        SimpleAuthoring();
        Console.WriteLine("PASS: Pebbles detached recipes, transform round trips, collider geometry, collision/placement constraints and resource budgets.");
    }
    private static void SimpleAuthoring()
    {
        var source = new ObjectRecipe { Collision = CollisionPolicy.None };
        var materials = new System.Collections.Generic.List<MaterialRecipe>
        {
            new() { SourceId = "import/material/0", DiffuseId = "embedded-diffuse", SourceColors = true },
            new() { SourceId = "import/material/1", DiffuseId = "second-diffuse", NormalId = "embedded-normal", SourceColors = true }
        };
        ClutterAuthoring.AssignMesh(source, "mesh", materials);
        source.Colliders.Add(new() { Position = new(1, 2, 3), Dimensions = new(2, 4, 6), RotationDegrees = new(0, 45, 0) });
        source.Collision = CollisionPolicy.Custom;
        ClutterAuthoring.SetScale(source, 3);
        Check(source.Colliders[0].Position == new Vec3(3, 6, 9) && source.Colliders[0].Dimensions == new Vec3(6, 12, 18), "Scale must carry collider offsets and dimensions.");
        Check(source.Colliders[0].RotationDegrees.Y == 45, "Scaling retains collider rotation.");
        ClutterAuthoring.SetScale(source, 2);
        Check(source.Colliders[0].Position == new Vec3(2, 4, 6), "Rescaling uses the ratio, not the absolute scale twice.");
        Reject(() => ClutterAuthoring.SetScale(source, float.NaN));
        var planet = new PebblesRecipe { Ecotypes = [
            new() { Name = "Rocks", Signature = "rocks-v1", Objects = [new() { SourceId = "rock-a" }, new() { SourceId = "rock-b" }],
                Placement = new() { MinScale = new(2, 3, 4), MaxScale = new(6, 7, 8), Orientation = OrientationMode.SurfaceNormalSmooth } },
            new() { Name = "Grass", Signature = "grass-v1", Objects = [new() { SourceId = "grass" }] }
        ] };
        string unchanged = JsonSerializer.Serialize(planet.Ecotypes[1]);
        var result = ClutterAuthoring.Replace(planet, source, ["Rocks"]);
        Check(JsonSerializer.Serialize(result.Ecotypes[1]) == unchanged, "Unselected clutter must remain untouched.");
        var rocks = result.Ecotypes[0];
        Check(rocks.Signature == "rocks-v1" && rocks.Objects[1].SourceId == "rock-b", "Target slot identities must survive replacement.");
        Check(rocks.Placement.MinScale == Vec3.One && rocks.Placement.MaxScale == Vec3.One, "Planet placement must preserve authored size.");
        Check(rocks.CollisionMode == ClutterCollisionMode.PrimitiveList && rocks.Placement.Orientation == OrientationMode.SurfaceNormal, "Custom colliders must enable compatible native collision automatically.");
        foreach (var target in rocks.Objects)
            foreach (var lod in target.Lods)
                Check(lod.Materials.Count == 2 && lod.Materials[0].DiffuseId == "embedded-diffuse" && lod.Materials[1].NormalId == "embedded-normal" && lod.Materials[0].SourceColors, "Every destination and LOD must retain imported material maps.");
        rocks.Objects[0].Colliders[0].Dimensions = Vec3.One;
        Check(source.Colliders[0].Dimensions != Vec3.One && rocks.Objects[1].Colliders[0].Dimensions != Vec3.One, "Applied copies must not alias source or other targets.");
        Check(planet.Ecotypes[0].Placement.MinScale == new Vec3(2, 3, 4), "Apply preparation must remain detached.");
        Reject(() => ClutterAuthoring.Replace(planet, source, ["Missing"]));
        Reject(() => ClutterAuthoring.Replace(planet, source, []));
        var all = ClutterAuthoring.Replace(planet, source, ["Rocks", "Grass"]);
        Check(all.Ecotypes[1].Objects[0].Transform.Scale.X == 2, "All-target replacement must carry authored scale.");
        Console.WriteLine("PASS: Pebbles simple targets, linked scale, automatic material assignment and detached replacement.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Invalid Pebbles recipe accepted."); }
}
