# Humble Arteest

Paint parts, kitten materials and engine glow. This feature is hosted by the single **Unscience** mod. Its separate
`humble-arteest.lib.csproj` remains the compilation and ownership boundary; there is no standalone entry project.

## Use

1. Open Unscience with **F11** (or its game-menu entry), show this feature in **Features**, and select it.
2. Configure the authoring form. Target selectors retain exact identities; choose **Controlled vehicle** explicitly where offered.
3. Use the feature’s Apply/Create/Arm action to affect the game.
4. Open **Live State**, select an item, and use its feature-specific controls.
5. **Save settings as preset** stores a reusable recipe; **Save** in the menu stores the complete workspace. **Load** replaces every authoring form and visibility setting, without changing applied effects.

## Paint at cursor

Set the vehicle paint **Brush** and **Blend**, open **Paint at cursor**, choose a range/scope, and arm the next world click. This is a flight-scene tool. Esc cancels; a miss keeps it armed. The armed brush is a detached snapshot, and loading a workspace cancels the gesture without changing applied paint.

- **Individual mesh instance** (default) colors only the clicked render mesh on that exact subpart instance.
- **Whole clicked subpart** uses the existing per-part override for all its render meshes.
- **All instances of clicked mesh** colors the matching render mesh asset across current and future instances, including other craft.

The picker tests every primitive of static/dynamic part render meshes, including dynamic gimbal transforms. It does not paint terrain, skinned kittens or parachute cloth. Paint changes albedo color using the existing shader; it does not replace texture files. The priority is mesh instance → part instance → shared mesh asset → part template → global. Removing a specific override reveals any broader override underneath. Blend remains one shader-wide policy, so applying a new blend changes how all paint is combined.

Mesh-instance/shared-mesh overrides are separate Live State items with color, copy-brush and removal controls. `PaintPicker.cs` owns raycasts, `VehiclePaint.Meshes.cs` owns mesh overrides, and `HumbleArteestSubmod.ClickPaint.cs` owns gesture/UI/inspectors. Existing Harmony handoffs now carry the resolved render mesh ID and verify the submitting model identity; no new patch target or GPU layout is added.

## Saved authoring state

Paint brush/blend/scope, cursor scope/range, exact part and engine sets, part types/material names, tints and emissive parameters. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact.

## Live state

Per-mesh-instance/shared-mesh and per-part/type paint, shared material colors, engine overrides and global policies. These objects remain owned by this feature and are never serialized into workspace files.
Hiding the feature, changing a preset, or loading a workspace does not dispose, re-apply, stop or recreate them.
An explicit live control or a game lifecycle event can change them.

## Implementation

- `humble-arteest.lib.csproj`: feature assembly and shared infrastructure references. Feature-to-feature project references are prohibited.
- The `*Submod.Workspace.cs` participant explicitly binds authoring fields. `PrepareRestore` validates/decodes before returning setters.
- The `*Submod.Live.cs` provider projects typed runtime records through `ILiveStateItem`; the host owns list layout, while the feature owns inspector behavior.
- The existing controllers, renderer hooks and solver timing remain in this library. See the [game-integration map](../scope/FULL_SCOPE.md) for their KSA/Harmony dependencies.

Authoring/runtime entry files: `EngineEmissiveSubmod.cs`, `VehiclePaintSubmodTables.cs`, `KittenColorSubmod.cs`, `HumbleArteestSubmod.cs`, `HumbleArteestSubmod.Workspace.cs`, `VehiclePaintSubmod.cs`, `HumbleArteestSubmod.Live.cs`, `HumbleArteestSubmod.Authoring.cs`.

## Persistence and validation

Workspace files and shared feature presets live below the KSA user-data directory’s `.unscience` folder.
Legacy feature presets remain accessible where the feature has a legacy picker. See [workspace behavior](../docs/WORKSPACE.md)
for target resolution, schema/overwrite handling, migration and the in-game smoke checklist.

Build from the repository root with `dotnet build ksa-mod-experiments.slnx`.
