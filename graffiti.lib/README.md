# Graffiti

Project PNG decals onto parts, terrain and canopy cloth. This feature is hosted by the single **Unscience** mod. Its separate
`graffiti.lib.csproj` remains the compilation and ownership boundary; there is no standalone entry project.

## Use

1. Open Unscience with **F11** (or its game-menu entry), show this feature in **Features**, and select it.
2. Configure the authoring form. Target selectors retain exact identities; choose **Controlled vehicle** explicitly where offered.
3. Use the feature’s Apply/Create/Arm action to affect the game.
4. Open **Live State**, select an item, and use its feature-specific controls.
5. **Save settings as preset** stores a reusable recipe; **Save** in the menu stores the complete workspace. **Load** replaces every authoring form and visibility setting, without changing applied effects.

## Hold-to-spray

Select a PNG, configure placement dimensions and appearance, enable **Spray while holding mouse**, and set **Spray interval (ms)** (10–60000). Press **Spray at cursor...**, then hold the left mouse button over the world. Release pauses the stroke; another world press starts immediately. Esc or Cancel ends placement. Single-click mode remains available.

Arming snapshots the placement recipe and interval. Each tick creates an ordinary, independently managed decal in Live State. Timing uses a monotonic wall clock; it emits at most one decal per GUI frame and skips missed ticks rather than bursting after a stall. A press over UI never starts a world stroke, and entering UI cancels the held stroke until a fresh world press. Missing geometry produces no decal. Workspace/preset load cancels only the pending gesture, preserving already placed decals.

`SprayCadence.cs` owns the managed timing/input gate; `GraffitiSubmod.Placement.cs` owns native input and placement. No additional render hook, texture allocator or shader is introduced.

## Saved authoring state

Image, size/depth/roll, opacity, brightness, range, renderer policy, spray mode/interval and import-browser view. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact.

## Live state

Each decal; visibility, transform/appearance and removal; bulk selection and render policy. These objects remain owned by this feature and are never serialized into workspace files.
Hiding the feature, changing a preset, or loading a workspace does not dispose, re-apply, stop or recreate them.
An explicit live control or a game lifecycle event can change them.

## Implementation

- `graffiti.lib.csproj`: feature assembly and shared infrastructure references. Feature-to-feature project references are prohibited.
- The `*Submod.Workspace.cs` participant explicitly binds authoring fields. `PrepareRestore` validates/decodes before returning setters.
- The `*Submod.Live.cs` provider projects typed runtime records through `ILiveStateItem`; the host owns list layout, while the feature owns inspector behavior.
- The existing controllers, renderer hooks and solver timing remain in this library. See the [game-integration map](../scope/FULL_SCOPE.md) for their KSA/Harmony dependencies.

Authoring/runtime entry files: `GraffitiSubmod.Live.cs`, `GraffitiSubmod.Workspace.cs`, `GraffitiSubmod.Placement.cs`, `GraffitiSubmod.cs`, `GraffitiSubmod.Ui.cs`.

## Persistence and validation

Workspace files and shared feature presets live below the KSA user-data directory’s `.unscience` folder.
Legacy feature presets remain accessible where the feature has a legacy picker. See [workspace behavior](../docs/WORKSPACE.md)
for target resolution, schema/overwrite handling, migration and the in-game smoke checklist.

Build from the repository root with `dotnet build ksa-mod-experiments.slnx`.

## Runtime release

Release cancels placement, stops submissions and frees decal GPU resources. Render hooks are installed only while decals are owned. Draft loading still only cancels an uncommitted gesture.

`ReleaseLiveState` is feature-owned and is used by the host’s explicit release control and unload. Hiding or loading authoring settings never calls it. Feature patch groups are registered through `ConfigureRuntime` with independent Harmony owners; host menu/input/HUD hooks remain resident.
