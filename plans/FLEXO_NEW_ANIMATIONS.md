# KSA Keyframe Animation System — Research & Implementation Guide

Covers: how `KeyframeAnimationModule` works end-to-end, how `CoreElectricalA_Prefab_SolarPanelB` is the first example, and how to author custom parts with animated rotor/hinge subparts.

---

## 1. The Animation Pipeline at a Glance

```
Assets XML          GameData XML             .glb file
  Part              PartGameData             Animation nodes
  ├─ SubPart(Id)  + KeyframeAnimationModule  ├─ AnimatedNode (keyframes)
  ├─ SubPart(Id)    └─ KeyframeAnimation     │   └─ "<SubPart Id>" (named, static)
  └─ SubPart(Id)        Path="…_Anim.glb"   └─ AnimatedNode
                                                 └─ "<SubPart Id>"
        ↓                    ↓                         ↓
  SubPart instances    Module created          PartLookup[name] → AnimatedPart
  with IDs             on part load            keyed by SubPart.Id

At runtime:
  TimeGoal (float, set by code)
    └─ UpdateModules() interpolates TimeCurrent toward TimeGoal (real-time rate)
          └─ EvaluateWorldMatrix(AnimatedPart, TimeCurrent) → float4x4
                └─ PartModelModule.UpdateRenderData() applies matrix to mesh render
```

**Key rule:** GLB node names **must exactly match** SubPart instance IDs in the Assets XML.

---

## 2. SolarPanelB — Full XML Reference

### 2.1 CoreElectricalAAssets.xml

Solar cell subpart template (the mesh for each panel segment):
```xml
<SubPart Id="CoreElectricalA_Subpart_SolarPanelB_CellA">
    <PartModel Id="CoreElectricalA_Subpart_SolarPanelB_CellA_Model">
        <Mesh Id="CoreElectricalA_Subpart_SolarPanelB_CellA" />
        <Material Id="CoreElectricalA_Material" />
    </PartModel>
    <MeshView>
        <Mesh Id="CoreElectricalA_Subpart_SolarPanelB_CellA_VM" />
    </MeshView>
</SubPart>
```

The full part definition (165 lines, simplified to show structural pattern):
```xml
<Part Id="CoreElectricalA_Prefab_SolarPanelB">

    <!-- Drive mechanism: one rotor + housing at the root -->
    <SubPart Id="CoreStructuralA_Subpart_DriveRotorB1"
             InstanceOf="CoreStructuralA_Subpart_DriveRotorB">
        <Transform>
            <Position X="0.07287" />
        </Transform>
    </SubPart>
    <SubPart Id="CoreStructuralA_Subpart_DriveHousingB1"
             InstanceOf="CoreStructuralA_Subpart_DriveHousingB" />

    <!-- 4 truss frames along the arm -->
    <SubPart Id="CoreStructuralA_Subpart_TrussFrameB4"
             InstanceOf="CoreStructuralA_Subpart_TrussFrameB">
        <Transform><Position X="4.22550" Z="-0.00699" /><Rotation Y="1.57053" /></Transform>
    </SubPart>
    <!-- ... B3, B2, B1 at X=3.30562, 2.39087, 1.47077 ... -->

    <SubPart Id="CoreStructuralA_Subpart_TrussFrameArmB1"
             InstanceOf="CoreStructuralA_Subpart_TrussFrameArmB">
        <Transform><Position X="0.52020" Z="-0.00808" /></Transform>
    </SubPart>

    <!-- 4 truss cross-members -->
    <SubPart Id="CoreStructuralA_Subpart_TrussB4"
             InstanceOf="CoreStructuralA_Subpart_TrussB">
        <Transform><Position X="4.22550" Z="-0.02728" /><Rotation Y="1.57053" /></Transform>
    </SubPart>
    <!-- ... B3, B2, B1 ... -->

    <!-- 8 hinge outer shells (2 per fold joint, paired ±Y) -->
    <SubPart Id="CoreStructuralA_Subpart_HingeOuterB8"
             InstanceOf="CoreStructuralA_Subpart_HingeOuterB">
        <Transform><Position X="3.74685" Y="0.41749" Z="-0.03026" /></Transform>
    </SubPart>
    <SubPart Id="CoreStructuralA_Subpart_HingeOuterB7"
             InstanceOf="CoreStructuralA_Subpart_HingeOuterB">
        <Transform><Position X="3.74685" Y="-0.41742" Z="-0.03026" /></Transform>
    </SubPart>
    <!-- ... HingeOuterB6 through B1 (alternating rotation X=3.14159, Z=3.14159 for mirrored pairs) ... -->

    <!-- 8 hinge inner pins (animated, match the outer shells) -->
    <SubPart Id="CoreStructuralA_Subpart_HingeInnerB8"
             InstanceOf="CoreStructuralA_Subpart_HingeInnerB">
        <Transform><Position X="3.78443" Y="0.41749" Z="-0.02619" /></Transform>
    </SubPart>
    <!-- ... HingeInnerB7 through B1 ... -->

    <!-- 4 solar cell panels, one per truss section -->
    <SubPart Id="CoreElectricalA_Subpart_SolarPanelB_CellA4"
             InstanceOf="CoreElectricalA_Subpart_SolarPanelB_CellA">
        <Transform>
            <Position X="4.22550" Z="-0.02511" />
            <Rotation X="3.14159" Z="1.57080" />
        </Transform>
    </SubPart>
    <!-- ... CellA3, CellA2, CellA1 at X=3.30562, 2.39087, 1.47077 ... -->

    <Connector Id="_connector6">
        <Transform>
            <Position X="-0.04988" Z="-0.00068" />
            <Rotation X="3.14159" Z="3.14159" />
            <Scale X="0.50000" Y="0.50000" Z="0.50000" />
        </Transform>
    </Connector>
</Part>
```

The structural subpart templates come from `CoreStructuralAAssets.xml`:
```xml
<SubPart Id="CoreStructuralA_Subpart_DriveRotorB">
    <PartModel Id="CoreStructuralA_Subpart_DriveRotorB_Model">
        <Mesh Id="CoreStructuralA_Subpart_DriveRotorB" /><Material Id="CoreStructuralA_Material" />
    </PartModel>
    <MeshView><Mesh Id="CoreStructuralA_Subpart_DriveRotorB_VM" /></MeshView>
</SubPart>
<SubPart Id="CoreStructuralA_Subpart_DriveHousingB">...</SubPart>
<SubPart Id="CoreStructuralA_Subpart_HingeOuterB">...</SubPart>
<SubPart Id="CoreStructuralA_Subpart_HingeInnerB">...</SubPart>
<SubPart Id="CoreStructuralA_Subpart_TrussB">...</SubPart>
<SubPart Id="CoreStructuralA_Subpart_TrussFrameB">...</SubPart>
<SubPart Id="CoreStructuralA_Subpart_TrussFrameArmB">...</SubPart>
```

### 2.2 CoreElectricalAGameData.xml

```xml
<PartGameData Id="CoreElectricalA_Prefab_SolarPanelB">
    <EditorTag Value="Electrical" />
    <KeyframeAnimationModule Id="SolarPanelAnimation">
        <KeyframeAnimation Path="Animations/CoreElectricalA_Prefab_SolarPanelB_Anim.glb"
                           Id="CoreElectricalA_Prefab_SolarPanelB_Anim" />
    </KeyframeAnimationModule>
</PartGameData>
```

**The `Path` is relative to the mod/content directory.** The `Id` is used internally as the asset identifier.

---

## 3. Code Architecture Deep Dive

### 3.1 KeyframeAnimationData — The Data Asset

`KeyframeAnimationData` is a singleton data object loaded from the `.glb` file. It is shared across all instances of a part template (one load per template, cached in `InstanceLookup`).

**Loading pipeline** (`Template.DoLoad()`):
1. Open the `.glb` file with `GltfLoader`
2. Build a parent-index array for all nodes
3. Find the root node (the one with no parent, `parent_index == -1`)
4. Read `gltfJson.Animations[0]` — **only the first animation is used**
5. For each animation channel, group by target node into `dictionary` (node_idx → channels)
6. For each animated node, build an `Animation` object with sampled keyframe arrays (PositionTimes/Positions, RotationTimes/Rotations, ScaleTimes/Scales) + default transforms
7. Link `animation.Parent` to the nearest animated ancestor node (for compound chains)
8. For each **non-animated** named node: walk up the parent chain to find an animated ancestor; if found, add to `PartLookup[node.Name] = new AnimatedPart { ParentAnimation = ..., Translation/Rotation/Scale = node's static transform }`
9. Pre-cache `WorldMatrixStart` (t=0) and `WorldMatrixEnd` (t=Duration) on each `AnimatedPart`

**Key fields:**
```
PartLookup        Dictionary<string, AnimatedPart>   name → part binding
Duration          float                              total animation length (seconds)
```

**`AnimatedPart`:**
```
ParentAnimation   Animation     the animated GLB node that drives this subpart
Translation       float3        subpart's static offset from parent (rest pose)
Rotation          floatQuat     subpart's static rotation in parent space (rest pose)
Scale             float3        subpart's static scale
WorldMatrixStart  float4x4      cached matrix at t=0
WorldMatrixEnd    float4x4      cached matrix at t=Duration
```

**`EvaluateWorldMatrix(part, time)`:**
```csharp
// Start with the subpart's own static transform
float4x4 result = Scale(part.Scale) * Rotation(part.Rotation) * Translation(part.Translation);
// Chain through animated ancestors (inner → outer)
for (Animation anim = part.ParentAnimation; anim != null; anim = anim.Parent)
    result *= EvaluateLocalMatrix(anim, time);
return result;
```

Where `EvaluateLocalMatrix` linearly interpolates (or step/cubic-spline) between keyframes in the animation at the given `time`.

At `time <= 0` or `time >= Duration` the cached start/end matrices are returned immediately (no interpolation work).

### 3.2 KeyframeAnimationModule — The Per-Part Module

One `KeyframeAnimationModule` instance per part (not per subpart). It owns the `TimeGoal` control point and holds a reference to the shared `KeyframeAnimationData`.

```csharp
public class KeyframeAnimationModule : ModuleStateful<...>
{
    public struct State { public float TimeCurrent; }  // physics-thread state
    public required KeyframeAnimationData Shared;       // the loaded GLB data
    public float TimeGoal;                              // SET THIS to drive animation
}
```

**Tick logic (`UpdateModules`, called every simulation frame):**
```
if TimeGoal == TimeCurrent  →  nothing to do
if TimeGoal < TimeCurrent   →  TimeCurrent -= DeltaTime  (reverse playback)
if TimeGoal > TimeCurrent   →  TimeCurrent += DeltaTime  (forward playback)
clamp TimeCurrent to TimeGoal to prevent overshoot
```

Playback rate = **1 real second per 1 animation second**. The animation plays at wall-clock speed regardless of simulation time warp.

**Binding (`CreateComponents`):**  
Called once when the Part is instantiated from its PartTemplate:
```csharp
foreach (Part subpart in part.SubParts)
{
    if (Shared.PartLookup.TryGetValue(subpart.Id, out AnimatedPart animatedPart))
    {
        // Wire up every PartModelModule on this subpart
        foreach (PartModelModule pmm in subpart.Modules.GetUsing<PartModelModule>())
        {
            pmm.KeyframeAnimationModule = this;
            pmm.AnimatedPart = animatedPart;
        }
    }
}
```

This is why GLB node names **must match the SubPart instance IDs** (e.g. `CoreStructuralA_Subpart_DriveRotorB1`), not the template IDs.

### 3.3 PartModelModule — Rendering

In `UpdateRenderData()`, if `KeyframeAnimationModule` is wired:
```csharp
// Get the animation time for this frame
float t = typeStates.GetStateByIdx(KeyframeAnimationModule.StatesIdx).TimeCurrent;

// Evaluate world matrix from the GLB data at time t
float4x4 animMatrix = KeyframeAnimationModule.Shared.EvaluateWorldMatrix(AnimatedPart, t);

// The parent FULL PART's transform (not the subpart's own transform — the animation replaces it)
double4x4 parentToEgo = Parent.FullPart.MatrixAsmb2Ego(in matrixVehicleAsmb2Ego);

// Final render matrix = animated_matrix * parent_to_ego
renderMatrix = double4x4.Unpack(animMatrix) * parentToEgo;
```

**Critical:** When a subpart is animated via `KeyframeAnimationModule`, the render ignores `Parent.MatrixAsmb2Ego()` (the subpart's own assembly-space transform from the XML). The animation fully replaces the subpart's position/orientation for rendering. The XML `<Transform>` on each subpart instance is used at part load time to set initial positions, but **once the GLB animation is active the GLB-driven matrix takes over for rendering.**

### 3.4 Right-Click Menu (ShowContextMenu)

`Part.ShowContextMenu(Vehicle vehicle)` renders the ImGui popup when a player right-clicks a part. It explicitly handles known module types by calling `SubtreeModules.Get<T>()`:

- `Decoupler` → "Decouple" menu item
- `DockingPort` → delegates to `dockingPort.ShowContextMenu()`
- `EVADoor` → delegates to `eVADoor.ShowContextMenu()`
- `ThrusterController` / `EngineController` → Active checkbox + fuel flow
- `Tank` → fill level progress bars
- `Battery` → charge level progress bar
- `Generator` → status info
- **`SolarPanel` → `solarPanel.DrawStateInfo()` (active, occluded, distance, AoA, efficiency, power)**

There is **no built-in handling for `KeyframeAnimationModule`** in `ShowContextMenu`. The solar panel animation is driven automatically (the game deploys/retracts based on SolarPanel module state, not user input). For manually-controlled animations you must add UI yourself.

---

## 4. GLB File Structure Requirements

### Node Hierarchy

```
Root (no animation, name doesn't matter)
  ├── AnimNodeA  (has translation/rotation/scale keyframes → drives a joint)
  │     └── "SubPartInstanceId_1"  (NO keyframes, named after KSA SubPart instance ID)
  ├── AnimNodeB  (has keyframes)
  │     ├── "SubPartInstanceId_2"
  │     └── "SubPartInstanceId_3"
  └── AnimNodeC  (has keyframes, parent of compound chain)
        └── AnimNodeD  (has keyframes, child of C)
              └── "SubPartInstanceId_4"  (driven by C then D composition)
```

**Rules:**
1. The GLB file must contain at least one animation. Only `Animations[0]` is used.
2. Animated nodes (with channels) do NOT need names — they are never looked up by name.
3. "Subpart binding nodes" (the ones that map to KSA SubPart instances) must:
   - Have a non-empty `name` matching the SubPart instance ID exactly
   - Have **no** animation channels of their own
   - Be a descendant (direct or indirect) of at least one animated node
4. A subpart binding node's own `translation/rotation/scale` in the GLB = its rest-pose offset within the animated parent (equivalent to the `<Transform>` in the Assets XML, in parent animation space).
5. If a subpart binding node has multiple animated ancestors, all ancestors are composed (inner → outer): `subpart_static × animated_child × animated_parent × ...`

### Coordinate Space

All transforms in the GLB are in the **parent part's assembly space** (same as the `<Transform>` values in the Assets XML). The evaluated matrix is then composed with `parent.FullPart.MatrixAsmb2Ego()` for world rendering.

### Supported Interpolation Types

| GLB `interpolation` | `SampleType` | Behaviour |
|---|---|---|
| `LINEAR` | `Linear` | Linear lerp / slerp between keyframes |
| `STEP` | `Step` | Snap to keyframe value, no interpolation |
| `CUBICSPLINE` | `CubicSpline` | Loaded but currently falls through to linear (see code note below) |

> **Note:** `CubicSpline` is parsed from the GLB but the interpolation functions only handle `Step` specially and otherwise use linear math. Use `LINEAR` for smooth animations.

### Axis Convention

Blender (Y-up, right-hand) exports GLTF in Y-up. KSA uses a Y-up coordinate system too based on the SubPart `Position` values. When creating the GLB in Blender, export with the default GLTF settings (Y-up, +Z forward) to match the part coordinate space.

---

## 5. Implementing Custom Animated Parts

### 5.1 XML — Assets Definition

In your mod's `Assets.xml`:

```xml
<!-- Reuse structural meshes from CoreStructuralA -->
<Part Id="MyMod_Prefab_HingeArm">

    <!-- Static base housing -->
    <SubPart Id="CoreStructuralA_Subpart_DriveHousingB1"
             InstanceOf="CoreStructuralA_Subpart_DriveHousingB" />

    <!-- The animated rotor (will be driven by GLB) -->
    <SubPart Id="CoreStructuralA_Subpart_DriveRotorB1"
             InstanceOf="CoreStructuralA_Subpart_DriveRotorB">
        <Transform>
            <Position X="0.07287" />
        </Transform>
    </SubPart>

    <!-- Hinge pairs at the fold joint -->
    <SubPart Id="CoreStructuralA_Subpart_HingeOuterB1"
             InstanceOf="CoreStructuralA_Subpart_HingeOuterB">
        <Transform><Position X="1.0" Y="0.4" /></Transform>
    </SubPart>
    <SubPart Id="CoreStructuralA_Subpart_HingeInnerB1"
             InstanceOf="CoreStructuralA_Subpart_HingeInnerB">
        <Transform><Position X="1.0" Y="0.4" Z="-0.02" /></Transform>
    </SubPart>

    <!-- Custom mesh payload (define its SubPart template too) -->
    <SubPart Id="MyMod_Subpart_ArmPanelA1"
             InstanceOf="MyMod_Subpart_ArmPanelA">
        <Transform><Position X="2.0" /></Transform>
    </SubPart>

    <Connector Id="_connector0">
        <Transform><Rotation X="3.14159" Z="3.14159" /><Scale X="0.5" Y="0.5" Z="0.5" /></Transform>
    </Connector>
</Part>

<!-- Custom mesh subpart template -->
<SubPart Id="MyMod_Subpart_ArmPanelA">
    <PartModel Id="MyMod_Subpart_ArmPanelA_Model">
        <Mesh Id="MyMod_ArmPanel_Mesh" />
        <Material Id="CoreStructuralA_Material" />
    </PartModel>
    <MeshView>
        <Mesh Id="MyMod_ArmPanel_VM" />
    </MeshView>
</SubPart>
```

### 5.2 XML — GameData Definition

In your mod's `GameData.xml` (or `Assets.xml` — game data and assets can be in the same file):

```xml
<PartGameData Id="MyMod_Prefab_HingeArm">
    <EditorTag Value="Structural" />
    <KeyframeAnimationModule Id="HingeArmAnimation">
        <KeyframeAnimation Path="Animations/MyMod_HingeArm_Anim.glb"
                           Id="MyMod_HingeArm_Anim" />
    </KeyframeAnimationModule>
</PartGameData>
```

### 5.3 GLB File — Node Structure

In Blender (or any GLTF authoring tool), create the following node tree and export to `.glb`:

```
Root
  ├── HingeJoint  (add a rotation animation 0→90° on Y axis, e.g. 0s→2s)
  │     ├── "CoreStructuralA_Subpart_HingeOuterB1"   ← named exactly as in Assets XML
  │     │     (position at rest: 0, 0, 0 relative to HingeJoint)
  │     ├── "CoreStructuralA_Subpart_HingeInnerB1"   ← named exactly
  │     └── "MyMod_Subpart_ArmPanelA1"               ← named exactly
  └── RotorSpin   (add a rotation animation 0→360° on X axis)
        └── "CoreStructuralA_Subpart_DriveRotorB1"   ← named exactly
```

**Blender workflow:**
1. Create empty objects or simple meshes for each animated joint
2. Name the child objects (that represent KSA subparts) exactly as their SubPart instance IDs
3. Add an `Object Constraint → Limit Rotation` or use keyframe animation for the joint objects
4. Export: File → Export → glTF 2.0 → Binary (.glb)
   - Check: "Apply Transform", "Include: Selected Objects" if needed
   - Animations: "Active Actions Merged" or "NLA Tracks"
5. Place the `.glb` in your mod's `Animations/` folder

> **Tip:** The GLB only contains node transforms. The actual mesh geometry for each subpart is provided by the KSA `SubPart` template (loaded from the game's own mesh assets). The GLB just drives where each subpart renders.

### 5.4 Animation Duration

`Duration` is automatically computed from the final keyframe timestamp across all channels. A 2-second animation has keyframes at `t=0` and `t=2.0`. The TimeGoal range is `[0, Duration]`.

---

## 6. Controlling Animations from Mod Code

### 6.1 Accessing the Module

```csharp
using KSA;

// Access on a specific part
Span<KeyframeAnimationModule> modules = part.SubtreeModules.Get<KeyframeAnimationModule>();
if (modules.Length == 0) return;
KeyframeAnimationModule module = modules[0];
```

### 6.2 Deploying and Retracting

```csharp
// Deploy (play forward to end)
module.TimeGoal = module.Shared.Duration;

// Retract (play backward to start)
module.TimeGoal = 0f;

// Toggle
bool isDeployed = module.TimeGoal >= module.Shared.Duration;
module.TimeGoal = isDeployed ? 0f : module.Shared.Duration;

// Go to a specific position (e.g., 50% open)
module.TimeGoal = module.Shared.Duration * 0.5f;
```

### 6.3 Checking Animation State

`TimeCurrent` lives in the physics state list, not on the module object directly. Read it via:

```csharp
// Reading TimeCurrent requires the state list pattern:
var stateList = part.Tree.States.TryGetTypeList(
    out ModuleStateful<KeyframeAnimationModule,
                       KeyframeAnimationModule.State,
                       EmptyStruct, EmptyStruct>.StateList typeStates)
    ? typeStates : null;

if (stateList != null)
{
    float currentTime = stateList.GetStateByIdx(module.StatesIdx).TimeCurrent;
    bool isAtStart    = currentTime <= 0f;
    bool isAtEnd      = currentTime >= module.Shared.Duration;
    bool isMoving     = currentTime != module.TimeGoal;
}
```

Or via reflection if the generic type is cumbersome:

```csharp
var statesField = part.Tree.States.GetType()
    .GetMethod("TryGetTypeList", BindingFlags.Public | BindingFlags.Instance);
// ... walk the state object ...
```

For most use cases (deploy/retract toggle), only `TimeGoal` needs to be set — no need to read `TimeCurrent`.

### 6.4 Vehicle Update

The animation is **visual-only** (affects rendering transforms only). It does **not** update physics, collision, or mass properties. Unlike Flexo's `Part.Asmb2ParentAsmb` approach, no call to `Vehicle.UpdateAfterPartTreeModification()` is needed.

If you need physics-accurate animated parts (e.g., a moving landing leg that must interact with terrain), continue using Flexo's approach of manipulating `Asmb2ParentAsmb` directly.

---

## 7. Adding Deploy/Retract to the Right-Click Menu

Since `ShowContextMenu` has no built-in `KeyframeAnimationModule` handling, use a Harmony postfix:

```csharp
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;

[HarmonyPatch(typeof(Part), "ShowContextMenu")]
public static class AnimationContextMenuPatch
{
    public static void Postfix(Part __instance, Vehicle? vehicle, ref bool __result)
    {
        if (vehicle == null) return;

        Span<KeyframeAnimationModule> modules =
            __instance.SubtreeModules.Get<KeyframeAnimationModule>();
        if (modules.Length == 0) return;

        KeyframeAnimationModule module = modules[0];
        float duration = module.Shared.Duration;

        // Show "Deploy" item when retracted or partially retracted
        bool isDeployed = module.TimeGoal >= duration;
        if (!isDeployed)
        {
            if (ImGui.MenuItem("Deploy"u8))
            {
                module.TimeGoal = duration;
                __result = true;
            }
        }
        else
        {
            if (ImGui.MenuItem("Retract"u8))
            {
                module.TimeGoal = 0f;
                __result = true;
            }
        }
    }
}
```

Apply this patch in your `Patcher.Patch()`:
```csharp
_harmony.PatchAll(typeof(AnimationContextMenuPatch));
```

> **Scope:** If you only want the context menu patch for your specific part type, filter by template ID:
> ```csharp
> if (__instance.TemplateId != "MyMod_Prefab_HingeArm") return;
> ```

---

## 8. Comparison: KSA KeyframeAnimationModule vs Flexo's Approach

| Aspect | KeyframeAnimationModule (GLB) | Flexo Hinge (Asmb2ParentAsmb) |
|---|---|---|
| **Animation source** | Pre-authored .glb keyframes | Runtime quaternion math |
| **Physics** | Visual only — no physics update | Full physics: CoM, aero, bounding box |
| **Complexity** | Requires GLB authoring tool | Pure C# math |
| **Flexibility** | Limited to pre-baked keyframe curves | Fully dynamic (any angle, speed, easing) |
| **Performance** | Efficient (matrix eval, no physics calls) | Per-frame `UpdateAfterPartTreeModification()` |
| **Suitable for** | Decorative animations (deploy/retract, spin) | Physics-relevant joints (landing legs, arms) |
| **Multiple joints** | Hierarchical GLB chains — free | Manual per-part update loop |
| **State persistence** | `State.TimeCurrent` in sim state | Managed by `HingeController` |

For a **solar panel** or **decorative rotating antenna** → use `KeyframeAnimationModule`.  
For a **landing leg** or **robotic arm** that must interact with terrain/physics → use Flexo's approach.

---

## 9. Available CoreStructuralA Subpart Meshes

These are already defined in the game's `CoreStructuralAAssets.xml` and can be reused via `InstanceOf`:

| Template ID | Description |
|---|---|
| `CoreStructuralA_Subpart_DriveRotorB` | Rotating drive hub (the part that spins) |
| `CoreStructuralA_Subpart_DriveHousingB` | Static drive housing / mount |
| `CoreStructuralA_Subpart_HingeOuterB` | Outer hinge shell (static half) |
| `CoreStructuralA_Subpart_HingeInnerB` | Inner hinge pin (animated half) |
| `CoreStructuralA_Subpart_TrussB` | Cross-member truss element |
| `CoreStructuralA_Subpart_TrussFrameB` | Truss frame section |
| `CoreStructuralA_Subpart_TrussFrameArmB` | Truss frame arm / root connector |

Usage in Assets XML:
```xml
<SubPart Id="MyUniqueName" InstanceOf="CoreStructuralA_Subpart_HingeInnerB">
    <Transform>...</Transform>
</SubPart>
```

The `Id` (instance ID) must be unique within the part and **must match the GLB node name** for animation binding to work.

---

## 10. Complete Minimal Example

A minimal single-hinge part that folds open over 2 seconds:

### Assets.xml
```xml
<SubPart Id="MyMod_Subpart_FlapA_Template">
    <PartModel Id="MyMod_Subpart_FlapA_Model">
        <Mesh Id="MyMod_Flap_Mesh" />
        <Material Id="CoreStructuralA_Material" />
    </PartModel>
</SubPart>

<Part Id="MyMod_Prefab_FoldingFlap">
    <SubPart Id="CoreStructuralA_Subpart_HingeOuterB1"
             InstanceOf="CoreStructuralA_Subpart_HingeOuterB" />
    <SubPart Id="CoreStructuralA_Subpart_HingeInnerB1"
             InstanceOf="CoreStructuralA_Subpart_HingeInnerB">
        <Transform><Position Z="-0.02" /></Transform>
    </SubPart>
    <SubPart Id="MyMod_Subpart_FlapA1" InstanceOf="MyMod_Subpart_FlapA_Template">
        <Transform><Position X="0.5" /></Transform>
    </SubPart>
    <Connector Id="_conn0" />
</Part>
```

### GameData.xml
```xml
<PartGameData Id="MyMod_Prefab_FoldingFlap">
    <KeyframeAnimationModule Id="FlapAnimation">
        <KeyframeAnimation Path="Animations/FoldingFlap_Anim.glb" Id="FoldingFlap_Anim" />
    </KeyframeAnimationModule>
</PartGameData>
```

### GLB (Blender node tree)
```
Root
  └── HingeJoint
        Keyframe t=0.0s: Rotation (0, 0, 0)    ← folded
        Keyframe t=2.0s: Rotation (0, -90°, 0) ← deployed
        ├── "CoreStructuralA_Subpart_HingeInnerB1"  (position: 0, 0, -0.02 from hinge)
        └── "MyMod_Subpart_FlapA1"                  (position: 0.5, 0, 0 from hinge)
```
Note: `CoreStructuralA_Subpart_HingeOuterB1` is NOT in the GLB because it doesn't animate — it stays at its XML `<Transform>` position.

### Mod code to deploy/retract
```csharp
// In your mod's ImGui window or right-click patch:
Span<KeyframeAnimationModule> mods = part.SubtreeModules.Get<KeyframeAnimationModule>();
if (mods.Length > 0)
{
    var m = mods[0];
    if (ImGui.Button("Toggle Flap"u8))
        m.TimeGoal = (m.TimeGoal >= m.Shared.Duration) ? 0f : m.Shared.Duration;
}
```

---

## 11. Notes & Gotchas

- **Only `Animations[0]` is used.** If the GLB has multiple animations, only the first is loaded. Author one animation per GLB file.
- **SubPart `Id` ≠ `InstanceOf`.** The `InstanceOf` attribute selects the template (mesh/material). The `Id` attribute is the instance identifier and is what gets matched to the GLB node name.
- **Static subparts don't need GLB entries.** If a subpart should never move (e.g., the outer hinge shell), just leave it out of the GLB. It renders at its XML `<Transform>` position normally.
- **Physics are not affected.** Colliders, bounding boxes, and mass properties are computed from the XML rest pose, not the animated pose. Do not use this system for landing legs or anything that must physically collide in its animated position.
- **Animation speed is fixed at 1× real-time.** `UpdateModules` advances `TimeCurrent` by `DeltaTime` per frame. There is no speed multiplier in the current API. To change effective speed, bake faster/slower keyframes in the GLB.
- **`TimeGoal` is not clamped automatically.** The code clamps `TimeCurrent` when it reaches `TimeGoal`, but nothing prevents you from setting `TimeGoal` outside `[0, Duration]`. Clamp it yourself: `module.TimeGoal = Math.Clamp(goal, 0f, module.Shared.Duration)`.
- **`EvaluateWorldMatrix` caches bounds.** At `time <= 0` it returns `WorldMatrixStart` (no interpolation). At `time >= Duration` it returns `WorldMatrixEnd`. These are pre-cached at load time.
- **GLB coordinate space must match part assembly space.** The evaluated matrix is multiplied with `Parent.FullPart.MatrixAsmb2Ego()`, so the GLB world space = parent part assembly space. Design your GLB with the part origin at world origin.
