# Easing Modifiers Implementation Plan

## Analysis Summary

### Current Implementation
The animation system uses a basic `EasingType` enum with 4 options (Linear, EaseIn, EaseOut, EaseInOut) and fixed cubic polynomial easing functions in `AnimationHelpers.ApplyEasing()`.

**Current easing formulas:**
- `EaseIn`: t³ (cubic acceleration - starts slow, ends fast)
- `EaseOut`: 1 - (1-t)³ (cubic deceleration - starts fast, ends slow)
- `EaseInOut`: t²(3-2t) (smoothstep - slow start/end, fast middle)

**Animations using easing (8 total):**
1. ZoomInAnimation
2. ZoomOutAnimation
3. OrbitAnimation
4. LoopyOrbitAnimation
5. ShakeAnimation
6. SpiralZoomInAnimation
7. SpiralZoomOutAnimation
8. ZoomInToOffsetAnimation

**Additional easing usages:**
- KeyframeSequencePlayer (return-to-start animation)

### Proposed Enhancement: Easing Power Parameter

Add a configurable **easing power** parameter to control the intensity/steepness of easing curves:
- **Power = 1.0**: Linear (no easing)
- **Power = 2.0**: Gentle quadratic ease
- **Power = 3.0**: Current cubic ease (default for compatibility)
- **Power = 4.0+**: More aggressive/pronounced ease

This allows fine-grained control over how "front-loaded" or "back-loaded" the easing effect is:
- **EaseIn** with high power → very slow start, explosive ending
- **EaseOut** with high power → explosive start, very slow ending
- Lower power values → gentler, more gradual transitions

### Implementation Strategy

#### Phase 1: Core Infrastructure
1. Add `EasingPower` property to `IKeyframeAnimation` interface
2. Update `AnimationHelpers.ApplyEasing()` to accept power parameter
3. Implement generalized easing formulas using configurable exponents

#### Phase 2: Animation Updates
Update all 8 animation classes to:
1. Add `EasingPower` property with default value 3.0
2. Add constructor parameter for easing power
3. Pass easing power to `ApplyEasing()` calls
4. Include power in `GetDisplayProperties()`

#### Phase 3: UI Implementation
1. Add easing power slider to animation creation forms (range: 1.0 to 6.0)
2. Display current easing power in keyframe list
3. Update KeyframeSequencePanel to show easing power per keyframe

#### Phase 4: Return-to-Start Support
Update `KeyframeSequencePlayer` to support easing power for return-to-start animation.

---

## Task Breakdown

### Task 1: Core Infrastructure - Interface & Helper Updates

**Description:** Update the core animation infrastructure to support easing power parameter.

**Files to modify:**
- `camera-controller-override/Animation/IKeyframeAnimation.cs`
- `camera-controller-override/Animation/AnimationHelpers.cs`

**Changes required:**

1. **IKeyframeAnimation.cs**
   - Add property: `double EasingPower { get; }`
   - Place after `EasingType Easing { get; }` property
   - Add XML documentation explaining power parameter (1.0=linear, 3.0=cubic default)

2. **AnimationHelpers.cs**
   - Update `ApplyEasing()` method signature: add `double power = 3.0` parameter
   - Replace current easing formulas with power-based implementations:
     ```csharp
     EaseIn: Math.Pow(t, power)
     EaseOut: 1.0 - Math.Pow(1.0 - t, power)
     EaseInOut: t < 0.5 
         ? Math.Pow(2 * t, power) / 2.0
         : 1.0 - Math.Pow(2 * (1 - t), power) / 2.0
     ```
   - Update `GetEasedFrameProgress()` to accept and pass power parameter

**Acceptance criteria:**
- Interface adds EasingPower property
- ApplyEasing supports power parameter with default value 3.0
- Easing formulas use Math.Pow for generalized curves
- Code compiles without errors

---

### Task 2: Update ZoomInAnimation

**Description:** Add easing power support to ZoomInAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/ZoomInAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `easing` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 3: Update ZoomOutAnimation

**Description:** Add easing power support to ZoomOutAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/ZoomOutAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `easing` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 4: Update OrbitAnimation

**Description:** Add easing power support to OrbitAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/OrbitAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `easing` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 5: Update LoopyOrbitAnimation

**Description:** Add easing power support to LoopyOrbitAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/LoopyOrbitAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `easing` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 6: Update ShakeAnimation

**Description:** Add easing power support to ShakeAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/ShakeAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `easing` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. ShakeAnimation doesn't directly call ApplyEasing (easing is for sequence-level), but must implement interface
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Note:** ShakeAnimation uses sinusoidal oscillation internally, not easing. The EasingPower property satisfies the interface but may not affect the animation behavior unless explicitly integrated.

**Acceptance criteria:**
- Animation accepts and stores easing power
- Property satisfies IKeyframeAnimation interface
- Power is displayed in properties
- Code compiles without errors

---

### Task 7: Update SpiralZoomInAnimation

**Description:** Add easing power support to SpiralZoomInAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/SpiralZoomInAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `spiralDegrees` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 8: Update SpiralZoomOutAnimation

**Description:** Add easing power support to SpiralZoomOutAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/SpiralZoomOutAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `spiralDegrees` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 9: Update ZoomInToOffsetAnimation

**Description:** Add easing power support to ZoomInToOffsetAnimation.

**Files to modify:**
- `camera-controller-override/Animation/Animations/ZoomInToOffsetAnimation.cs`

**Changes required:**

1. Add property: `public double EasingPower { get; }`
2. Add constructor parameter: `double easingPower = 3.0` (after `offsetZ` parameter)
3. Initialize property in constructor: `EasingPower = easingPower;`
4. Update `ApplyEasing()` call in `Update()`: pass `EasingPower` as second argument
5. Add to `GetDisplayProperties()`: `{ "Easing Power", $"{EasingPower:F1}" }`

**Acceptance criteria:**
- Animation accepts and stores easing power
- Easing power is passed to ApplyEasing()
- Power is displayed in properties
- Code compiles without errors

---

### Task 10: Update KeyframeSequencePlayer Return-to-Start

**Description:** Add easing power support to KeyframeSequencePlayer's return-to-start animation.

**Files to modify:**
- `camera-controller-override/Animation/KeyframeSequencePlayer.cs`

**Changes required:**

1. Add private field: `private double _returnToStartEasingPower = 3.0;`
2. Add public property: `public double ReturnToStartEasingPower { get; set; }`
3. In method where `ApplyEasing(t, _returnToStartEasing)` is called (around line 405):
   - Update to: `ApplyEasing(t, _returnToStartEasing, _returnToStartEasingPower)`

**Acceptance criteria:**
- Return-to-start animation uses configurable easing power
- Default power is 3.0 for backward compatibility
- Code compiles without errors

---

### Task 11: UI Updates - KeyframeSequencePanel

**Description:** Add UI controls for easing power in the keyframe display and return-to-start settings.

**Files to modify:**
- `camera-controller-override/UI/KeyframeSequencePanel.cs`

**Changes required:**

1. **In `RenderReturnToStartControls()` method:**
   - After the "Return Easing" combo box, add easing power slider:
   ```csharp
   // Return easing power slider
   float returnEasingPower = (float)player.ReturnToStartEasingPower;
   if (ImGui.SliderFloat("Return Easing Power", ref returnEasingPower, 1.0f, 6.0f))
   {
       player.ReturnToStartEasingPower = returnEasingPower;
   }
   ```

2. **In `RenderKeyframeItem()` method:**
   - The easing power is already displayed via `GetDisplayProperties()` which was updated in animation tasks
   - No changes needed here

**Acceptance criteria:**
- Return-to-start easing power has slider control (1.0 to 6.0 range)
- Keyframe list displays easing power for each animation
- Code compiles without errors
- UI is functional and responsive

---

### Task 12: UI Updates - Animation Creation Forms (Future Work)

**Description:** Add easing power input to any UI forms that create animations. This task documents where future UI integration would occur.

**Files to check:**
- `camera-controller-override/Mod.cs` - Main mod UI panel
- Any animation builder/creation forms

**Changes required:**
- Add easing power slider/input field to animation creation forms
- Default value: 3.0
- Range: 1.0 to 6.0
- Pass easing power to animation constructors

**Note:** This task may require investigation to locate all animation creation points. If animations are only created programmatically (not via UI forms), this task can be marked as not applicable.

**Acceptance criteria:**
- All animation creation UI includes easing power control
- Default values are set correctly
- Code compiles without errors

---

## Implementation Order

Recommended execution order for AI subagent delegation:

1. **Task 1** (Core Infrastructure) - MUST be completed first
2. **Tasks 2-9** (Animation updates) - Can be done in parallel or any order
3. **Task 10** (KeyframeSequencePlayer) - Depends on Task 1
4. **Task 11** (UI Panel) - Depends on Task 10
5. **Task 12** (Creation Forms) - Can be done last or deferred

## Testing Checklist

After all tasks are complete, verify:

- [ ] All animations compile without errors
- [ ] Easing power of 1.0 produces linear motion
- [ ] Easing power of 3.0 produces same behavior as before (cubic)
- [ ] Higher powers (4.0-6.0) produce more pronounced easing
- [ ] Keyframe list displays easing power correctly
- [ ] Return-to-start animation respects easing power setting
- [ ] UI controls are responsive and update values correctly

## Data Serialization Notes

The easing power parameter is stored as a property of each animation class, making it automatically serializable if/when keyframe sequences are saved to disk. Future serialization work should ensure the `EasingPower` property is included in any JSON/data output.
