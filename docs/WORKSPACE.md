# Unscience workspaces

Unscience ships as one StarMap mod. Its 26 features remain separate C# library projects. Open the workspace with F11 or the game's Unscience menu. Features → show/hide controls which authoring forms appear; hidden features continue updating their applied effects.

## Save, load, and reusable settings

The main window is an authoring workspace. Configure actions, choose their targets, and press the feature's Apply/Create/Start button to affect the game. Editing a form, loading a workspace, or resetting a form does not apply it.

**Save** opens a name dialog. Select an existing save from the dropdown or enter its name to overwrite it; the button changes to **Overwrite**. Names are trimmed, Unicode-normalized, and matched without case. **Load** opens a separate searchable window. Double-click a save, or select it and press **Load selected**. Loading replaces all feature drafts, including hidden features, visibility, selections, filters, disclosures, scroll offsets and the main/load/live window placements. Features absent from an older document reset to their defaults. Unknown feature payloads survive a load/save cycle.

Every feature also has **Save settings as preset** and a saved-settings dropdown. These presets carry the recipe and form state, leaving the destination form's target selections intact. Loading a settings preset does not apply it. Legacy feature preset libraries remain available where the feature already supported them.

Workspace saves use exact vehicle, body, part and asset identities, never a saved list index. Vehicle selectors offer an explicit **Controlled vehicle** alternative. A missing target remains visibly unresolved and blocks actions requiring it. It is never replaced by the first available item. Asset choices are recipe data, so reusable feature presets retain them.

KSA regenerates part instance IDs when loading a vehicle. Persistent part selections therefore use vehicle ID, a topology fingerprint, and root/subpart path. Changed topology is unresolved. KSA provides no durable per-part GUID: an identical replacement at the same path in an identically named vehicle cannot be distinguished across sessions. Editor-only parts use session identities and must be reselected in a later session. Controlled-vehicle selection does not silently retarget an exact part selection.

## Live State

**Live State** opens an independent window listing each feature's current managed runtime items. Select an item to see its own controls. The list includes additive objects such as welds, plumes, camera mounts, decals and rings, and scoped policies such as the active FOV, editor limits, material overrides, telemetry recorders and global style.

Item inspectors provide the feature's applicable edit, stop, remove, restore, queue or copy-to-form actions. Copying to a form is an explicit operation and does not itself apply changes. Applying a singleton policy from the main form replaces that policy; additive features create entries according to their existing semantics. Runtime controls operate on live objects, independently of draft settings.

Saving/loading workspaces never disposes, recreates, pauses or reapplies live objects. Camera playback has a separate player from the saved sequence recipe. Materials, themes, gauge layouts, lights, editor policies and shared exhaust templates change only on explicit Apply. Placement gestures are canceled on load to prevent a subsequent click from applying a newly restored recipe accidentally. Already placed objects continue running.

Runtime records are session data, not game-save persistence. Workspace loading cannot resurrect destroyed objects, restore historical telemetry or reload assets. Shared template edits affect all game instances using that template, which inspectors identify as shared/global scope. A live selection ID may no longer exist in a later session; this changes the inspector selection, not the game.

## Storage and recovery

Files live under `KsaPaths.UserDataDir/.unscience/`:

| Path | Contents |
|---|---|
| `workspaces/<id>.json` | Named whole-workspace documents |
| `feature-presets/<feature-id>/<id>.json` | Reusable feature settings |
| `session/last-workspace.json` | Optional automatic session recovery |
| `session/before-load.json` | Workspace captured before the latest manual load |

Preferences controls session autosave. Recover workspace before last load restores the authoring recovery document. JSON writes use a temporary file, flush and atomic replacement, retaining a `.bak` of the previous file. Malformed saves appear as errors in the load list. Unsupported schemas or invalid fields fail preparation before changing any feature. If a draft setter fails, the restore transaction rolls back affected participants. Existing legacy visibility/window preferences are used when there is no session document.

The save dialog's transient open state and pending destructive confirmation actions are intentionally not replayed. Live inspector edits/selection caches and game jobs belong to runtime state. Workspace documents store window visibility/placement and selected live ID, not those runtime payloads.

## Architecture and extension rules

`unscience-contracts.lib` contains the game-independent schema, store and transactional restore coordinator. `ksa-abstractions.lib/Workspace` provides explicit typed draft bindings, stable selectors, UI helpers and `IWorkspaceFeature` / `ILiveStateItem`. Each feature owns its recipe types and typed runtime records. The host only collects their common interfaces and renders their inspectors.

Register every authoring field explicitly in the feature's `*.Workspace.cs`. Bind targets separately from settings. Decode and validate complete payloads in `PrepareRestore`; its returned action may only assign detached authoring data. Never call game APIs, Apply, Initialize, Dispose, renderer rebuilds, resource allocation or runtime mutators from a restore setter. Complex sequence data must have a tagged data representation, not serialized delegates or KSA object graphs.

Feature libraries may reference contracts and shared KSA infrastructure, not one another. `ksa-lights.lib` owns shared light access; `ksa-rings.lib` owns shared ring assets and replacement coordination. Bloom announces an outgoing ring replacement so Rocky releases its overlay on the outgoing reference before the new renderer is built. Retiring standalone distribution does not merge these project boundaries.

`Mod` initializes and updates every retained feature. Visibility only controls authoring navigation. Keep parts-now's GPU load/purge work before GUI and Garry's Torch's solver-safe weld phase after GUI, including the hidden-HUD fallback. HotkeyGuard remains installed on the single host Harmony instance.

## Creative recipes and runtime ownership

Disco lives in Zippo's existing feature library. Its saved recipe includes craft/light scope, palette and independently timed color/actuation/spread channels. Each running light owns detached settings and a module-local template. Shared assembly actuators have one owning Disco item; native motion can lag the requested cycle. Pause freezes recipe time; stop restores owned templates/goals. See [Zippo](../zippo.lib/README.md).

[Graffiti](../graffiti.lib/README.md) saves spray mode/interval with placement settings. Arming copies these settings; each successful tick creates a normal decal. [Humble Arteest](../humble-arteest.lib/README.md) saves click scope/range with its brush; the clicked target becomes a mesh-instance, part-instance or shared-mesh live override. Both gestures are ephemeral and cancelled by workspace/preset loads, while existing effects remain untouched.

Creative-tool acceptance (requires the game):

- Run three-channel Disco on identical lights on two craft; confirm instance isolation, all-craft behavior, skipped non-actuated/point-light channels, shared actuator ownership, pause, replace, stop, despawn and unload restoration. Save/load/hide while running and confirm effects continue.
- Spray onto vehicle/terrain/canopy, drag through UI, release/repress, stall a frame and load another workspace. Confirm no catch-up burst, no UI-originated stroke and preservation of placed decals.
- Click identical meshes under all three paint scopes; test overlap precedence/removal, gimballed engines and multi-primitive meshes. Load a workspace and verify existing paint stays. Confirm pending gestures are cancelled and unresolved light/vehicle selectors block dependent actions.

These native checks are separate from managed tests and are not implied by compilation.

## Verification

Run:

```sh
dotnet build ksa-mod-experiments.slnx --disable-build-servers -m:1 -p:UNSCIENCE_DIST_DIR=/tmp/unscience-dist
python3 scripts/check-workspace-boundaries.py
python3 scripts/check-docs.py
dotnet run --project unscience-contracts.tests --no-build
```

Contract checks cover detached snapshots, normalized overwrite collisions, backups, schema failures, missing-feature defaults, unknown feature preservation, prepare-only behavior, rollback and isolation from live state. They do not load KSA or invoke native ImGui/GPU APIs.

In-game acceptance: create a weld, plume, custom ring and animated light; save A; edit forms and feature visibility; save B; load A and B while verifying all live objects and playback continue. Test missing/replaced targets, controlled-vehicle changes, feature presets on a second target, singleton replacement, live copy/edit/remove, narrow/wide layout, F11/F2, session restart and unload. Repeat material/render checks listed in `scope/FULL_SCOPE.md`. Build success does not establish those native runtime results.

## Removed distribution

The requested obsolete features and their entry projects are removed: unladen-swallow (including HTTP client/server), steely-eyed-missile-kitten, stampy, space-tape, red-alert, mesh-deform, marque, inanimate-carbon-rod, grant, flexo and blinky. Already-retired features remain absent. Every retained bundled feature's standalone entry project is retired; its `.lib.csproj` remains. Nonshipping music experiments and scaffolding are listed separately in `REPOSITORY_INDEX.md`.

## Runtime release and failure handling

The workspace offers **Release all applied state for this feature**. Each library implements `ReleaseLiveState`; the host also invokes it before disposal. Release restores reversible captured fields, removes owned objects and stops future effects. It does not rewind already simulated motion or fuel history. Pause may retain objects and resources; release removes that ownership. Parts Now queues release to the pre-GUI phase and reports unload refusals. Kiwi retains pending orbit-restoration rows until a safe solver phase succeeds.

`ConfigureRuntime(FeatureRuntime)` registers each feature’s explicit patch demand, independently from its live row count. Each group receives a distinct `MeowSci.Unscience/<feature>/<group>` Harmony owner. Activation rollback removes partial hooks; release failures remain retryable. Only the host’s menu, HotkeyGuard and hidden-HUD fallback stay resident. Garry’s Torch implements `UpdateAfterGui`; Parts Now continues updating before GUI. Feature initialization failures are shown as unavailable entries while healthy features keep working. Their detached saved payloads round-trip without calling the failed library.

The native feature-render boundary captures ImGui recovery state and restores unbalanced scopes before displaying an exception. This depends on the current game’s internal ImGui recovery bindings and requires native acceptance. Detached bindings reject non-finite values, malformed required shapes and oversized payloads; feature validators enforce indices, dimensions and recipe constraints before setters are returned.

Restoration acceptance: launch without Apply and compare hooks, templates, shaders and allocations; apply/replace/release each feature and compare captured baselines; repeat material spawn/remove cycles and inspect asset maps; unweld nonuniformly scaled craft; switch control away from an exact kitten target; remove grids discovered by scan; inject restore/rebuild failures; repeat hidden-HUD and unload cases. Parts Now should allocate no startup headroom, grow before mesh upload, reclaim out-of-order freed tails, and refuse relocation while ray tracing holds device addresses. These native checks have not been executed by the managed tests.

## Pebbles authoring and acceptance

Pebbles is the 26th bundled library. Its main form captures and edits per-body ground clutter recipes. Workshop Done updates only the draft; Apply queues native replacement. Live State owns per-body restoration. Saved meshes, textures, biome aliases and body identities remain exact; unavailable targets/assets block Apply. Loading does not refresh or destroy the preview, rebuild clutter, or change colliders. See [Pebbles](../pebbles.lib/README.md).

Native acceptance: capture Earth and Luna; replace one variant and then all five LODs; verify texture/shadow routing; build offset/rotated compound primitives in the Workshop; test stationary and moving physics bubbles; preserve destroyed-clutter exclusions through Apply/Restore; confirm Mars remains unchanged when Luna's shared stock rocks are overridden; replace and restore repeatedly; load a different draft while applied; hide all authoring/HUD; release while preview and live clutter are present; change systems/unload. Confirm no camera/editor/control changes and no stale GPU descriptors or Bepu shapes.
