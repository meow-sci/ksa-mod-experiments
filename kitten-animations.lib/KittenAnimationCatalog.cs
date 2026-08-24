using System;
using System.Collections.Generic;
using System.Reflection;
using KSA;
using KSA.Rendering;

namespace MeowSci.KittenAnimationsLib;

/// <summary>One playable animation the game loaded for the kitten.</summary>
public sealed class AnimationEntry
{
    public required string Label { get; init; }
    public required IAnimation Animation { get; init; }

    /// <summary>Asset id of the underlying glTF clip, or a description for blend samplers.</summary>
    public required string Source { get; init; }

    /// <summary>Clip length in seconds.</summary>
    public float Length => Animation.AnimLength;
}

/// <summary>A named set of animations shown as one section in the UI.</summary>
public sealed class AnimationGroup
{
    public required string Name { get; init; }
    public required string Tooltip { get; init; }
    public List<AnimationEntry> Entries { get; } = new();
}

/// <summary>
/// Discovers every animation the game has loaded for the controlled kitten.
///
/// The ground locomotion set (idle/walk/run/jump/land/tumble/ladder/moonwalk/moonrun/swim/seated)
/// is loaded by KittenRenderable into private fields from CharacterGroundAnimationsReference — it is
/// NOT reachable through CharacterAvatar.Animations, so those are read by reflection. The MMU set and
/// the overlay clips are plain typed fields on CharacterAvatar.
/// </summary>
public sealed class KittenAnimationCatalog
{
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly Dictionary<string, FieldInfo?> RenderableFields = new();

    public List<AnimationGroup> Groups { get; } = new();

    /// <summary>Private KittenRenderable fields that could not be resolved — a game-update breakage signal.</summary>
    public List<string> UnresolvedFields { get; } = new();

    public AnimationPairBlendSampler? WalkPairSampler { get; private set; }
    public AnimationPairBlendSampler? RunPairSampler { get; private set; }
    public AnimationPairBlendSampler? SwimPairSampler { get; private set; }
    public AnimationDirectionalBlendSampler? MmuBlendSampler { get; private set; }

    public static KittenAnimationCatalog Build(CharacterAvatar avatar, KittenRenderable renderable)
    {
        var catalog = new KittenAnimationCatalog();
        catalog.AddGroundGroup(renderable);
        catalog.AddMmuGroup(avatar);
        catalog.AddBlendGroup(renderable);
        catalog.AddOverlayGroup(avatar);
        return catalog;
    }

    private void AddGroundGroup(KittenRenderable renderable)
    {
        var group = new AnimationGroup
        {
            Name = "Ground / EVA",
            Tooltip = "Locomotion clips from CharacterGroundAnimations. KittenRenderable keeps these in "
                    + "private fields, so they are read by reflection.",
        };

        AddClip(group, renderable, "_groundIdleAnim", "Idle");
        AddClip(group, renderable, "_groundWalkAnim", "Walk");
        AddClip(group, renderable, "_groundRunAnim", "Run");
        AddClip(group, renderable, "_jumpIntroAnim", "Jump");
        AddClip(group, renderable, "_jumpLandAnim", "Jump Land");
        AddClip(group, renderable, "_flailAnim", "Tumble / Flail");
        AddClip(group, renderable, "_ladderAnim", "Ladder");
        AddClip(group, renderable, "_moonWalkAnim", "Moon Walk");
        AddClip(group, renderable, "_moonRunAnim", "Moon Run");
        AddClip(group, renderable, "_swimAnim", "Swim");
        AddClip(group, renderable, "_swimIdleAnim", "Swim Idle");
        AddClip(group, renderable, "_seatedIdleAnim", "Seated Idle");
        AddClipList(group, renderable, "_seatedIdleActionAnims", "Seated Action");

        Groups.Add(group);
    }

    private void AddMmuGroup(CharacterAvatar avatar)
    {
        var group = new AnimationGroup
        {
            Name = "MMU",
            Tooltip = "Jetpack clips from CharacterMMUAnimations, exposed directly on CharacterAvatar.",
        };

        var mmu = avatar.Animations.MmuAnimations;
        Add(group, mmu.MmuIdleDefaultAnim, "Idle Default");
        AddList(group, mmu.MmuIdleActionsAnim, "Idle Action");
        Add(group, mmu.MmuMoveForwardLoopAnim, "Move Forward");
        Add(group, mmu.MmuMoveBackwardLoopAnim, "Move Backward");
        Add(group, mmu.MmuMoveLeftLoopAnim, "Move Left");
        Add(group, mmu.MmuMoveRightLoopAnim, "Move Right");
        Add(group, mmu.MmuMoveUpLoopAnim, "Move Up");
        Add(group, mmu.MmuMoveDownLoopAnim, "Move Down");
        Add(group, mmu.MmuArmRetractAnim, "Arm Retract");

        Groups.Add(group);
    }

    private void AddBlendGroup(KittenRenderable renderable)
    {
        WalkPairSampler = GetField<AnimationPairBlendSampler>(renderable, "_walkPairSampler");
        RunPairSampler = GetField<AnimationPairBlendSampler>(renderable, "_runPairSampler");
        SwimPairSampler = GetField<AnimationPairBlendSampler>(renderable, "_swimPairSampler");
        MmuBlendSampler = GetField<AnimationDirectionalBlendSampler>(renderable, "_blendSampler");

        var group = new AnimationGroup
        {
            Name = "Blends",
            Tooltip = "Live blend samplers the game plays instead of a single clip. The game drives their "
                    + "weight from gravity (moonwalk), swim speed and MMU acceleration.",
        };

        AddSampler(group, WalkPairSampler, "Walk / Moon Walk", "AnimationPairBlendSampler(walk, moonwalk)");
        AddSampler(group, RunPairSampler, "Run / Moon Run", "AnimationPairBlendSampler(run, moonrun)");
        AddSampler(group, SwimPairSampler, "Swim Idle / Swim", "AnimationPairBlendSampler(swimIdle, swim)");
        AddSampler(group, MmuBlendSampler, "MMU Directional", "AnimationDirectionalBlendSampler(7 candidates)");

        Groups.Add(group);
    }

    private void AddOverlayGroup(CharacterAvatar avatar)
    {
        var group = new AnimationGroup
        {
            Name = "Overlays",
            Tooltip = "Single-pose clips the game samples additively (blink, ear/helmet mask). Playing one "
                    + "on the body works but is not how the game uses them.",
        };

        Add(group, avatar.Animations.BlinkAnim, "Blink");
        Add(group, avatar.Animations.HelmetMaskAnim, "Ear / Helmet Mask");

        Groups.Add(group);
    }

    private void AddClip(AnimationGroup group, KittenRenderable renderable, string fieldName, string label)
    {
        var field = ResolveField(fieldName);
        if (field == null)
        {
            UnresolvedFields.Add($"KittenRenderable.{fieldName}");
            return;
        }

        Add(group, field.GetValue(renderable) as AnimationAssetRef, label);
    }

    private void AddClipList(AnimationGroup group, KittenRenderable renderable, string fieldName, string labelPrefix)
    {
        var field = ResolveField(fieldName);
        if (field == null)
        {
            UnresolvedFields.Add($"KittenRenderable.{fieldName}");
            return;
        }

        AddList(group, field.GetValue(renderable) as List<AnimationAssetRef>, labelPrefix);
    }

    private static void Add(AnimationGroup group, AnimationAssetRef? animation, string label)
    {
        // A zero-length clip divides by its loop period inside BoneAnimRuntime, so never offer one.
        if (animation == null || animation.LoopPeriod <= 0f) return;

        group.Entries.Add(new AnimationEntry
        {
            Label = label,
            Animation = animation,
            Source = animation.Id.ToString(),
        });
    }

    private static void AddList(AnimationGroup group, List<AnimationAssetRef>? animations, string labelPrefix)
    {
        if (animations == null) return;

        for (int i = 0; i < animations.Count; i++)
            Add(group, animations[i], $"{labelPrefix} {i + 1}");
    }

    private static void AddSampler(AnimationGroup group, IAnimation? sampler, string label, string source)
    {
        if (sampler == null || sampler.LoopPeriod <= 0f) return;

        group.Entries.Add(new AnimationEntry { Label = label, Animation = sampler, Source = source });
    }

    private T? GetField<T>(KittenRenderable renderable, string fieldName) where T : class
    {
        var field = ResolveField(fieldName);
        if (field == null)
        {
            UnresolvedFields.Add($"KittenRenderable.{fieldName}");
            return null;
        }

        return field.GetValue(renderable) as T;
    }

    private static FieldInfo? ResolveField(string fieldName)
    {
        if (RenderableFields.TryGetValue(fieldName, out var cached))
            return cached;

        FieldInfo? field = null;
        try
        {
            field = typeof(KittenRenderable).GetField(fieldName, InstanceNonPublic);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"kitten-animations: Error resolving KittenRenderable.{fieldName}: {ex.Message}");
        }

        RenderableFields[fieldName] = field;
        return field;
    }
}
