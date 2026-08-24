using System;
using KSA;

namespace MeowSci.KittenAnimationsLib.Ui;

/// <summary>Everything the UI sections need for the currently bound kitten.</summary>
public sealed class AnimationUiContext
{
    public required KittenEva Kitten { get; init; }
    public required CharacterAvatar Avatar { get; init; }
    public required KittenAnimationCatalog Catalog { get; init; }
    public required KittenAnimationDriver Driver { get; init; }
    public required KittenExpressionController Expressions { get; init; }
    public required KittenAnimProcessors Processors { get; init; }
    public required Random Random { get; init; }

    /// <summary>Selected expression variant, or -1 for a random pick on every trigger.</summary>
    public int ExpressionVariant { get; set; } = -1;
}
