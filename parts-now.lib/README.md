# Parts Now

Load new part definitions during play. This feature is hosted by the single **Unscience** mod. Its separate
`parts-now.lib.csproj` remains the compilation and ownership boundary; there is no standalone entry project.

## Use

1. Open Unscience with **F11** (or its game-menu entry), show this feature in **Features**, and select it.
2. Configure the authoring form. Target selectors retain exact identities; choose **Controlled vehicle** explicitly where offered.
3. Use the feature’s Apply/Create/Arm action to affect the game.
4. Open **Live State**, select an item, and use its feature-specific controls.
5. **Save settings as preset** stores a reusable recipe; **Save** in the menu stores the complete workspace. **Load** replaces every authoring form and visibility setting, without changing applied effects.

## Saved authoring state

Mod metadata, three XML documents and selected tab, folder selection/filter, headroom settings. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact.

## Live state

Each runtime-loaded mod plus loader progress, results and GPU budget; gated reload/unload. These objects remain owned by this feature and are never serialized into workspace files.
Hiding the feature, changing a preset, or loading a workspace does not dispose, re-apply, stop or recreate them.
An explicit live control or a game lifecycle event can change them.

## Implementation

- `parts-now.lib.csproj`: feature assembly and shared infrastructure references. Feature-to-feature project references are prohibited.
- The `*Submod.Workspace.cs` participant explicitly binds authoring fields. `PrepareRestore` validates/decodes before returning setters.
- The `*Submod.Live.cs` provider projects typed runtime records through `ILiveStateItem`; the host owns list layout, while the feature owns inspector behavior.
- The existing controllers, renderer hooks and solver timing remain in this library. See the [game-integration map](../scope/FULL_SCOPE.md) for their KSA/Harmony dependencies.

Authoring/runtime entry files: `PartsNowSubmod.Workspace.cs`, `PartsNowSubmod.Live.cs`, `PartsNowSubmod.cs`.

## Persistence and validation

Workspace files and shared feature presets live below the KSA user-data directory’s `.unscience` folder.
Legacy feature presets remain accessible where the feature has a legacy picker. See [workspace behavior](../docs/WORKSPACE.md)
for target resolution, schema/overwrite handling, migration and the in-game smoke checklist.

Build from the repository root with `dotnet build ksa-mod-experiments.slnx`.

## Runtime release

Startup no longer reserves 48/12 MiB of GPU headroom. Shared raster buffers grow on demand before Bind, within the configured budget, and freed contiguous tails are reclaimed. Releasing the last pack shrinks storage while preserving external allocations. Buffer relocation is refused while a ray-tracing renderer owns cached GPU addresses: launch without ray tracing for runtime mesh growth. Texture-only loads can use the existing buffers. Release runs before GUI and uses the existing unload gate; it does not add cross-feature asset leases.

`ReleaseLiveState` is feature-owned and is used by the host’s explicit release control and unload. Hiding or loading authoring settings never calls it. Feature patch groups are registered through `ConfigureRuntime` with independent Harmony owners; host menu/input/HUD hooks remain resident.
