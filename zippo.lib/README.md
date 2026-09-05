# Zippo

Apply light appearance and queued animations. This feature is hosted by the single **Unscience** mod. Its separate
`zippo.lib.csproj` remains the compilation and ownership boundary; there is no standalone entry project.

## Use

1. Open Unscience with **F11** (or its game-menu entry), show this feature in **Features**, and select it.
2. Configure the authoring form. Target selectors retain exact identities; choose **Controlled vehicle** explicitly where offered.
3. Use the feature’s Apply/Create/Arm action to affect the game.
4. Open **Live State**, select an item, and use its feature-specific controls.
5. **Save settings as preset** stores a reusable recipe; **Save** in the menu stores the complete workspace. **Load** replaces every authoring form and visibility setting, without changing applied effects.

## Disco party lights

Use the vehicle/light target selectors, then open **Disco — party lights**. Choose a single light or **All lights on selected craft**. Enable any combination of color, actuation and beam spread, then **Start Disco**. The existing light switches must be on; the Live State inspector exposes the assembly switch.

Color cycles through an ordered editable palette (1–32 colors) or independently seeded random rainbow hues. Each channel has its own transition duration, hold duration and easing. Actuation alternates between normalized minimum/maximum goals; beam spread alternates between two inner/outer cone half-angle pairs in degrees. Point lights skip spread. Lights without a matching keyframe animation skip actuation. KSA moves toward the requested actuation goal at its own animation rate, so very short cycles can outpace the mechanism.

Each light has a **Disco light** item in Live State with elapsed time, channel/driver status, pause, recipe copy and stop/restore. Pause freezes the recipe clock; a mechanism can still finish moving to its last goal. Color/spread use module-local template copies, leaving other instances alone. Actuation belongs to the full assembly: a driver is owned by one Disco item, and a later Apply takes ownership. A craft-wide Apply claims each driver once. A light whose actuation is shared reports that in its inspector.

Starting again replaces Disco on the selected lights and clears their old Zippo queues. Applying ordinary appearance or queuing a normal animation stops Disco on that exact light first. Stop/unload restores the original light template and the captured actuator goal if still owned. External template replacement is reported rather than overwritten. Disco does not change the legacy shared-template scope of ordinary Zippo appearance/queues.

## Saved authoring state

Exact/controlled vehicle and light part, enabled state, intensity/color, animation endpoints, timing and easing; Disco scope, channel toggles, palette/random mode, ranges and independent timing. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact.

## Live state

Disco runtime records plus each managed ordinary light and queue; appearance uses the game’s shared template scope. These objects remain owned by this feature and are never serialized into workspace files.
Hiding the feature, changing a preset, or loading a workspace does not dispose, re-apply, stop or recreate them.
An explicit live control or a game lifecycle event can change them.

## Implementation

- `zippo.lib.csproj`: feature assembly and shared infrastructure references. Feature-to-feature project references are prohibited.
- The `*Submod.Workspace.cs` participant explicitly binds authoring fields. `PrepareRestore` validates/decodes before returning setters.
- The `*Submod.Live.cs` provider projects typed runtime records through `ILiveStateItem`; the host owns list layout, while the feature owns inspector behavior.
- The existing controllers, renderer hooks and solver timing remain in this library. See the [game-integration map](../scope/FULL_SCOPE.md) for their KSA/Harmony dependencies.

`DiscoRecipe.cs` and `DiscoTiming.cs` own the recipe and cycle evaluation; `DiscoLight.cs` owns per-instance templates and actuator goals; `ZippoSubmod.Disco.cs` owns Disco authoring and inspectors.

Authoring/runtime entry files: `ZippoSubmod.cs`, `ZippoSubmod.Workspace.cs`, `ZippoSubmod.Live.cs`, `ZippoSubmod.Authoring.cs`.

## Persistence and validation

Workspace files and shared feature presets live below the KSA user-data directory’s `.unscience` folder.
Legacy feature presets remain accessible where the feature has a legacy picker. See [workspace behavior](../docs/WORKSPACE.md)
for target resolution, schema/overwrite handling, migration and the in-game smoke checklist.

Build from the repository root with `dotnet build ksa-mod-experiments.slnx`.

## Runtime release

Ordinary light/animation entries now acquire shared-template and switch-baseline leases. Removing the last owner restores original color, indexed-color identity, intensity and switch state. Transitioning to Disco releases ordinary ownership first. Disco also restores inspector switch edits; pause retains ownership, while stop releases it.

`ReleaseLiveState` is feature-owned and is used by the host’s explicit release control and unload. Hiding or loading authoring settings never calls it. Feature patch groups are registered through `ConfigureRuntime` with independent Harmony owners; host menu/input/HUD hooks remain resident.
