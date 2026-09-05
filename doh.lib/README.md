# DOH

Spawn kitten characters with customized materials. This feature is hosted by the single **Unscience** mod. Its separate
`doh.lib.csproj` remains the compilation and ownership boundary; there is no standalone entry project.

## Use

1. Open Unscience with **F11** (or its game-menu entry), show this feature in **Features**, and select it.
2. Configure the authoring form. Target selectors retain exact identities; choose **Controlled vehicle** explicitly where offered.
3. Use the feature’s Apply/Create/Arm action to affect the game.
4. Open **Live State**, select an item, and use its feature-specific controls.
5. **Save settings as preset** stores a reusable recipe; **Save** in the menu stores the complete workspace. **Load** replaces every authoring form and visibility setting, without changing applied effects.

## Saved authoring state

Exact/controlled reference vehicle, character or random choice, count, offset and tint settings. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact.

## Live state

Each spawned kitten with its material controls. These objects remain owned by this feature and are never serialized into workspace files.
Hiding the feature, changing a preset, or loading a workspace does not dispose, re-apply, stop or recreate them.
An explicit live control or a game lifecycle event can change them.

## Implementation

- `doh.lib.csproj`: feature assembly and shared infrastructure references. Feature-to-feature project references are prohibited.
- The `*Submod.Workspace.cs` participant explicitly binds authoring fields. `PrepareRestore` validates/decodes before returning setters.
- The `*Submod.Live.cs` provider projects typed runtime records through `ILiveStateItem`; the host owns list layout, while the feature owns inspector behavior.
- The existing controllers, renderer hooks and solver timing remain in this library. See the [game-integration map](../scope/FULL_SCOPE.md) for their KSA/Harmony dependencies.

Authoring/runtime entry files: `DohSubmod.Live.cs`, `DohSubmod.Workspace.cs`, `DohSubmod.cs`.

## Persistence and validation

Workspace files and shared feature presets live below the KSA user-data directory’s `.unscience` folder.
Legacy feature presets remain accessible where the feature has a legacy picker. See [workspace behavior](../docs/WORKSPACE.md)
for target resolution, schema/overwrite handling, migration and the in-game smoke checklist.

Build from the repository root with `dotnet build ksa-mod-experiments.slnx`.

## Runtime release

Cloned materials are tracked by exact GPU asset reference. Despawn releases a material set after its last kitten; unused allocations from unsuccessful creation are swept. Stock materials are never owned. Cleanup waits for the GPU after consumers are detached.

`ReleaseLiveState` is feature-owned and is used by the host’s explicit release control and unload. Hiding or loading authoring settings never calls it. Feature patch groups are registered through `ConfigureRuntime` with independent Harmony owners; host menu/input/HUD hooks remain resident.
