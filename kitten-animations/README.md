# Kitten Animations - Avatar Animation Controller

Manages animation playback for the kitten avatar character, including body animations and facial expressions. Provides smooth expression transitions with easing-in and support for multiple expression types.

## Overview

Kitten Animations lets you:
- **Play character animations** - MMU (body movement) animations
- **Trigger expressions** - Angry, Awe, Happy, Sad, Scared
- **Configure duration** - Expression playback length (1-5 seconds)
- **Smooth transitions** - Automatic ease-in over 250ms
- **Kitten avatar access** - Reflection-based avatar retrieval
- **Real-time control** - Play expressions on demand

## Features

- **Expression system** - 5 different kitten face expressions
- **Ease-in animation** - Smooth quadratic ramp-in over 250ms
- **Expression duration** - Configurable per-expression length
- **Animation weight blending** - Smooth blend of animations
- **Weight-based fading** - Expressions fade naturally
- **Avatar reflex access** - Safe reflection-based avatar retrieval
- **Per-frame updates** - Expression state updated every frame
- **KSA animation cache compatibility** - Clears KSA's cached expression pose when swapping expression assets so each expression button samples its own animation

## Architecture

### Core Classes

#### KittenAnimationController
Central animation state and update manager.

**Key State**:
```csharp
public class KittenAnimationController
{
    public float ExpressionDuration { get; set; }  // 1.0 to 5.0 sec
    public ExpressionType CurrentExpression { get; set; }
    private double expressionTimer = 0.0;
    private double easeInTimer = 0.0;
    private const double EaseInDuration = 0.25;  // 250ms
}
```

**Key Methods**:
- `Update(double deltaTime, CharacterAvatar avatar)` - Update animation state
- `TriggerExpression(ExpressionType type, AnimationAssetRef asset, CharacterAvatar avatar)` - Play expression
- `PlayAvatarAnimation(CharacterAvatar avatar, IAnimation animation)` - Set body/MMU animation
- `SetExpressionAnimation(CharacterAvatar avatar, AnimationAssetRef asset)` - Apply expression

#### KittenAnimationsSubmod
ISubmod implementation that owns the animation controller and all animation UI.

**Architecture**:
- Implements `ISubmod` (from `ksa-abstractions.lib`): `Name="Kitten Animations"`, `Initialize()`, `Update(dt)`, `RenderContent()`, `Dispose()`
- Owns `KittenAnimationController` instance; calls `Update(dt, avatar)` in `Update()`
- `RenderContent()` renders MMU Animations, Expressions, and Walking Animations collapsible headers — no window framing
- Used standalone via `kitten-animations/Mod.cs` (which wraps in its own ImGui window) and embedded in unscience's collapsible header

#### KittenAvatarAccessor
Reflection-based access to KSA's kitten avatar system.

**Key Methods**:
- `GetKitten()` - Retrieve the KittenEva instance from game state
- `GetKittenAvatar()` - Get CharacterAvatar from KittenEva
- `GetAvatarAccessor(KittenEva kitten)` - Safe reflection access

**Implementation Detail**:
```csharp
// Access avatar via reflection
private KittenEva kitten = GetKitten();
private CharacterAvatar avatar = ReflectionHelpers.GetFieldValue<CharacterAvatar>(
    kitten._renderable, 
    "_characterAvatar"
);
```

### Expression System

#### Supported Expressions
| Expression | Type | Usage |
|-----------|------|-------|
| Angry | Negative | Alert, alarm |
| Awe | Surprise | Discovery, wonder |
| Happy | Positive | Success, joy |
| Sad | Negative | Failure, disappointment |
| Scared | Fear | Danger, uncertainty |

#### Expression Animation Assets
Expressions reference `AnimationAssetRef` objects:
```csharp
public AnimationAssetRef AngerAsset { get; set; }
public AnimationAssetRef AweAsset { get; set; }
public AnimationAssetRef HappyAsset { get; set; }
// etc.
```

### Easing-In Mechanism

#### Expression Ease-In Curve
```
ease_in(t) = t²  (quadratic, 0.0 to 1.0)
```

Over 250ms (0.25 seconds), expression weight rises smoothly:

| Time (ms) | Progress | Ease-In Value | Blended Weight |
|-----------|----------|---------------|----------------|
| 0         | 0.00     | 0.00          | 0.00           |
| 62.5      | 0.25     | 0.0625        | 0.0625         |
| 125       | 0.50     | 0.25          | 0.25           |
| 187.5     | 0.75     | 0.5625        | 0.5625         |
| 250       | 1.00     | 1.00          | 1.00           |

**Implementation**:
```csharp
if (easeInTimer < EaseInDuration)
{
    easeInTimer += deltaTime;
    float progress = (float)(easeInTimer / EaseInDuration);
    expressionWeight = progress * progress;  // Quadratic ease-in
}
else
{
    expressionWeight = 1.0f;
}
```

#### Expression Fade-Out
After duration expires, weight gradually fades:
```csharp
if (expressionTimer > ExpressionDuration)
{
    // Fade out phase
    float timeAfterComplete = expressionTimer - ExpressionDuration;
    float fadeProgress = timeAfterComplete / FadeDuration;
    expressionWeight = Mathf.Max(0.0f, 1.0f - fadeProgress);
}
```

## UI (Mod.cs)

ImGui window with:
- **Expression buttons** - Quick-trigger buttons: Angry, Awe, Happy, Sad, Scared
- **Duration slider** - 1.0 to 5.0 seconds configurable
- **Current expression display** - Shows active expression and remaining time
- **Animation state indicator** - Ease-in, playing, or idle
- **Random expression button** - Play random expression
- **Stop button** - Cancel current expression
- **Animation selector** - Choose MMU animation to play

## Implementation Details

### Avatar Retrieval

```csharp
// Get KittenEva instance from game singleton
var kitten = KittenService.Instance.GetKitten();

// Access CharacterAvatar via reflection
var avatar = ReflectionHelpers.GetFieldValue<CharacterAvatar>(
    component: kitten._renderable,
    fieldName: "_characterAvatar"
);
```

### Expression Application

```csharp
public void TriggerExpression(ExpressionType type, AnimationAssetRef asset, CharacterAvatar avatar)
{
    CurrentExpression = type;
    
    // Apply animation asset
    SetExpressionAnimation(avatar, asset);
    
    // Reset timers
    expressionTimer = 0.0;
    easeInTimer = 0.0;
    
    // Set target duration
    ExpressionDuration = configuredDuration;
}
```

Current KSA builds cache sampled expression poses inside `CatExpressionAnim`. When changing `ExpressionAnim`, the controller invalidates that private pose cache so Angry, Awe, Happy, Sad, and Scared can be triggered independently instead of reusing the first sampled pose.

### Per-Frame Update

```csharp
public void Update(double deltaTime, CharacterAvatar avatar)
{
    if (CurrentExpression == ExpressionType.None)
        return;
    
    // Update ease-in timer
    if (easeInTimer < EaseInDuration)
    {
        easeInTimer += deltaTime;
    }
    
    // Update expression timer
    expressionTimer += deltaTime;
    
    // Check if expression duration expired
    if (expressionTimer >= ExpressionDuration + FadeDuration)
    {
        CurrentExpression = ExpressionType.None;
        SetExpressionWeight(avatar, 0.0f);
    }
    
    // Compute current weight and apply to avatar
    float weight = ComputeExpressionWeight();
    SetExpressionWeight(avatar, weight);
}
```

## Usage Example

```csharp
// Get kitten avatar
var kitten = KittenAvatarAccessor.GetKitten();
var avatar = KittenAvatarAccessor.GetKittenAvatar();

// Trigger happy animation
var happyAsset = new AnimationAssetRef("HappyAnimation");
controller.TriggerExpression(ExpressionType.Happy, happyAsset, avatar);
controller.ExpressionDuration = 2.0f;  // 2 seconds

// Update each frame
controller.Update(deltaTime, avatar);
```

## Configuration

Configurable via ImGui:

| Setting | Range | Notes |
|---------|-------|-------|
| Expression Duration | 1.0 - 5.0 sec | How long expression plays |
| Ease-In Duration | (hardcoded) | 250ms quadratic ease-in |
| Random Expression Interval | 0 - infinity | Auto-trigger interval |

## Notes for Future Development

- **Emotion system**: Extend to support emotional states (happy, sad) with automatic expression scheduling
- **Expression sequencing**: Chain multiple expressions in sequence
- **Animation blending**: Blend between different expression animations smoothly
- **Facial gestures**: Additional gesture types beyond expressions
- **Voice sync**: Play audio alongside expressions
- **Performance**: Cache animation assets to reduce lookup overhead
- **Randomization**: Vary timing slightly to make animations less mechanical

## Technical Considerations

### Reflection Dependency
Avatar access uses reflection to reach internal fields:
```
KittenEva._renderable._characterAvatar
```

This pattern may change across KSA versions—update carefully.

### Animation Asset References
Expressions reference KSA's animation assets by ID. Asset IDs must match KSA's internal naming conventions.

### Timing Precision
Expression timing is based on deltaTime accumulation. Frame-rate dependent—high framerate users will see smoother ease-in than low framerate users.

## Dependencies

- **MeowSci.KsaAbstractions**: For game state access
- **KSA Game**: Avatar system, animation assets
