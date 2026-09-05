# Implementation status

The workspace redesign is implemented across all 25 retained feature libraries. Separate `.lib.csproj` boundaries remain; bundled standalone entries and the requested obsolete features are removed. See [current workspace contract](../docs/WORKSPACE.md), [project index](../REPOSITORY_INDEX.md), and [integration status](../scope/FULL_SCOPE.md) for the implemented behavior. The original analysis and sequencing below are retained as design context. Native in-game acceptance remains outstanding; automated verification is described in the workspace document.

# Unscience workspace and live state redesign

Status: proposed implementation plan; no feature code or projects changed by this analysis.
Baseline: current repository and sibling KSA `current` sources, cataloged as `2026.9.7.5402`.

## Decisions and required behavior

- One Unscience distribution and StarMap entry project. Retire standalone feature entry projects.
- **Keep each retained feature's folder and `.lib.csproj`.** Project boundaries remain the code-demarcation mechanism; do not flatten features into the host.
- A named **workspace save** captures every feature's authoring state, including hidden features, target selections, and Unscience presentation state.
- Loading replaces the whole authoring workspace. It never applies, removes, stops, recreates, or reconfigures existing runtime effects.
- Named **feature presets** capture reusable settings for an individual operation. Loading one populates that operation's form; it does not apply it.
- **Live State** is a separate window, collecting every runtime item Unscience manages and exposing feature-specific controls through a common contract.
- Exact targets are the default, with an explicit controlled-vehicle option. Missing or ambiguous exact targets remain unresolved, never silently substituted.
- Features remain initialized and their runtime work continues when hidden, collapsed, when another workspace loads, or when the main window closes.
- Applying a singleton operation replaces its existing live configuration. Applying an additive operation creates another live item. The feature defines the actual scope of uniqueness.

## Findings in the current code

| Evidence | Implication |
|---|---|
| `unscience/Mod.cs` constructs 28 submods, updates all regardless of visibility, and embeds `RenderContent()` | Preserve hidden-feature lifecycle behavior; replace the monolithic shell orchestration incrementally. |
| `unscience/UnscienceState.cs` saves one `state.toml` and a filtered main-window `window.ini` | Current persistence covers headers, visibility, and a few preferences, not feature fields or named workspaces. |
| Visibility and header state use display `Name`; tooltip text changes the ImGui header label | Introduce immutable feature IDs and stable widget IDs independent of names/tooltips. |
| `ISubmod` has lifecycle, content and floating-window rendering, but no state or runtime enumeration contract | Introduce explicit capabilities; do not serialize the existing submod objects. |
| Garry's Torch, Pyro and Bloomin' Onion already have settings presets and live records | Reuse their settings models and migrate stores instead of inventing another set of effect definitions. |
| `CameraControllerOverrideSubmod` authors directly into `KeyframeSequencePlayer.Keyframes` | Split serializable sequence definitions from executing animation objects, elapsed time and captured camera transforms. |
| `RockyMcRockFaceSubmod.PruneUnusedMeshClones()` retains assets from `_selections` | Runtime ownership must drive retention. A loaded authoring workspace cannot become the resource keep-list. |
| Glass, Zippo, Skittles, Don't Stifle Me, kitten animation controls and Pyro's template editor write live state from widgets | Convert authoring controls to drafts and explicit Apply/Play/Start actions. Moving panels alone is insufficient. |
| `GForceUI.RenderContent()` runs breach checks against the recorder | Separate view state and calculations from mutation of recording state; merely drawing restored UI must not change live records. |
| `UnladenSwallowSubmod.Update()` is the caller of `GameThread.DrainOnGameThread()` | Move queue ownership/draining to the host when retiring RPC. Keep solver/GPU work at its required phase. |
| `its-so-shiny.lib` references `zippo.lib`; `bloomin-onion.lib` references `rocky-mcrock-face.lib` | Extract deliberately shared light and ring infrastructure, then prohibit feature-to-feature project references. |
| `unscience.csproj` references `byo-music.lib` without constructing its submod; contains RPC packaging dependencies | Remove unused host dependencies and rebuild distribution from clean outputs. |
| `scope/FULL_SCOPE.md` and some READMEs disagree with current behavior (e.g. the shell README's feature table is incomplete; paint status is stale) | Use source as the behavior evidence and repair affected discovery/scope documentation during migration. |

Current build settings are `net10.0` / C# 13 in `Directory.Build.props`; retain them during this redesign. Some repository prose calls this C# 10.

## Scope reduction and project layout

Requested removals:

| Current status | Work |
|---|---|
| Bundled: `unladen-swallow`, `red-alert`, `blinky` | Delete feature entry and library projects, registration, patch calls, dependencies and distribution payload. Bundled feature count becomes **25**. |
| Standalone only: `steely-eyed-missile-kitten`, `stampy`, `mesh-deform`, `marque` | Delete entry/library projects where present, solution entries and feature-specific assets. The repository spelling is `steely-eyed-missile-kitten`. |
| No tracked implementation projects: `space-tape`, `flexo`, `grant`, `inanimate-carbon-rod` | Remove stale active references, not historical migration records. Ignored build remnants may exist. The similarly named Skittles theme is not a separate submod. |

Retire the standalone wrappers for the 25 retained features as well: keep implementations and documentation in their library projects. Move any required content/deployment responsibilities before removing a wrapper. Do not delete user preset files, imported assets, mission databases or installed content as part of source cleanup.

The RPC-specific `client/`, OpenAPI files and scripts need a consumer audit and retirement with Unladen Swallow. Remove now-unused GenHTTP/package/copy rules only after checking actual remaining consumers. Clean output matters because the host currently copies `MeowSci.*` from its build directory and stale DLLs can survive source deletion.

`byo-music` and `jplrepo` are not bundled features and were not requested as additions. Keep their experimental source outside the shipping solution unless separately incorporated; do not silently expand the workspace's functionality. The scaffold/template should be updated for the single-host development model.

Proposed dependency structure (names are implementation suggestions):

```text
unscience.csproj                         StarMap host, composition, windows, persistence service
  -> each retained feature.lib.csproj    feature drafts, presets, runtime manager, ImGui panels
       -> unscience-contracts.lib       game/UI-independent IDs, snapshot and live descriptors
       -> unscience-ui.lib              shared Brutal ImGui layout and target/preset widgets
       -> ksa-abstractions.lib          game access, scheduling, target resolution, hotkey guard
       -> ksa-lights.lib                shared light access for Zippo / Its So Shiny
       -> ksa-rings.lib                 shared ring assets, rebuild and ownership infrastructure
```

Feature projects must not reference other feature projects or the host. Shared projects must not reference features. Move generic `RockyUi` form helpers into the UI library; keep ring-specific UI in the ring features. Extract shared infrastructure by moving existing implementations with minimal behavior changes. Enforce allowed project references in CI; do not rely on namespace conventions alone. Keep feature-specific data out of a growing universal settings class.

## Three distinct kinds of state

### 1. Authoring workspace — persisted

Each feature owns a typed `FeatureDraft` (all inputs and intended selections) and `FeatureViewState` (tabs, inner sections, filters, selected draft rows and other meaningful presentation). Compound features can have multiple operation drafts: Humble Arteest's paint/material/emissive tools and Pyro's plume/template editors are examples.

The workspace envelope stores:

- Format version, save ID, display name, timestamps and application version for diagnostics.
- Feature order, visibility, expanded state, selected feature/navigation and tooltip preference.
- Each feature's immutable ID, schema version, complete draft and view state, including hidden features.
- Positions, sizes and visibility of **Unscience-owned** authoring windows and the Live State inspector shell, with offscreen clamping.
- Selected feature-preset identity for presentation, **plus the actual settings values**. Changing/deleting a feature preset later must not change a saved workspace.

Do not persist `Vehicle`, `Part`, avatars, live entry objects, native handles, delegates, ImGui buffers or runtime animation implementations. Serialize text as strings and rebuild `ImInputString` buffers. Persist vector components explicitly. Avoid reflection-based private-field snapshots.

Do not save modal-open state, pending destructive confirmations, input focus, held keys, an armed world-click command or the last success/error message. These are transient interactions, not reusable settings. On workspace load, cancel unfinished placement gestures without touching placed items or submitted jobs. The user explicitly arms placement again.

Live inspector selections/expansions can be restored best-effort by live item ID, but are only view preferences. Missing IDs select nothing. Do not replay inspector edit buffers or operations. Stock Hot Pursuit camera window visibility is live camera state, not an Unscience authoring-window setting.

### 2. Feature presets — persisted settings only

Provide consistent Save Preset / preset picker / Load into Form controls for every bulk-settings operation. A preset contains a feature ID, operation ID, schema version, name and typed settings payload. Normally omit the target so the settings can be reused on another item; the workspace retains exact target selections.

Save directly from a draft, even when nothing has been applied. The Live State inspector can offer **Copy settings to form** and **Save settings as preset**, both deep-copying data. Copying to a form may show that feature; it never modifies the live source item. Loading a feature preset changes only that operation's settings, retaining the current target.

### 3. Managed runtime state — session-owned

Each feature manager owns typed live records containing applied settings, target bindings, status and the resources required to implement the effect. Record settings are independent copies of drafts. Runtime rendering and updates read only the live record/manager.

Runtime includes effects, overrides, active recordings, queued animation playback, loaded runtime content and shared-template mutations. It is not an event log of every button ever pressed. One-shot maintenance actions with no continuing owned state (e.g. recompute editor subparts) need feedback, not a permanent fake live record.

Loading a workspace is never allowed to call feature Initialize/Dispose, assign live globals, invoke Apply/Restore, rebuild a renderer, clear an animation queue, change recorder behavior or unload runtime content. Opening its restored windows must obey the same invariant on subsequent frames.

## Contracts and ownership

Keep lifecycle/rendering game-facing and snapshot data game-independent. Suggested interfaces/capabilities:

| Contract | Responsibility |
|---|---|
| Feature descriptor | Stable ID, display name, category, order and supported operation IDs. |
| Workspace participant | Capture a detached feature snapshot; deserialize/migrate/validate into a prepared draft; replace draft/view state without runtime callbacks. |
| Feature preset participant | Capture/load settings for a named operation, with explicit typed validation and target exclusion rules. |
| Live state provider | Enumerate descriptors and resolve a live item by stable ID; implemented by the existing feature manager or a thin adapter. |
| Live item | ID, feature/kind, scope/target, label, status, summary and supported action descriptors. Its typed record remains feature-owned. |
| Live inspector | Render a selected feature-owned item and submit its supported commands; lives in the feature project. |

The host knows providers, not every concrete effect type. A `LiveStateCatalog` aggregates provider snapshots; it does **not** duplicate the feature managers' ownership lists. Use change versions/events or cached snapshots instead of reallocating/re-enumerating every game object each frame. Do not serialize a live item just because it implements a common interface.

Use stable IDs independent of list index and display label. Additive items get unique session IDs. Singleton items use a stable feature/kind/scope key, so reapply updates the selected item rather than adding duplicate rows. A failed apply leaves the previous applied record and resources intact; publish changes only after success (or report an explicit fault if a game operation cannot be rolled back).

Common actions are capabilities, not mandatory methods that throw: e.g. Remove, Restore, Pause, Resume, Stop, Reopen viewport, Copy to form. Restore and Remove retain their feature-specific semantics. No universal destructive Clear All in the initial implementation. Lists iterate snapshots and defer mutations until iteration completes. Expensive rebuild/resize actions use explicit buttons in the inspector.

Preserve solver timing, GPU retirement and startup ordering. The host drains ordinary game-thread commands before updates; solver-sensitive commands are consumed by their existing solver hooks. Do not tick physics from the Live State window. Keep `HiddenUiFrameHook`, one host Harmony instance, mandatory HotkeyGuard, lazy GPU allocation and isolated unload failures. In particular, preserve Parts Now's pre-`ModLibrary.Bind()` headroom reservation and its existing state-machine timing.

## Target and asset references

- Persist vehicle/body IDs with available system/save context and a display label for unresolved presentation.
- `Part.InstanceId` is assigned with `Universe.GetNextRunningId()` in the current game's `Part` constructor. It is a same-session lookup aid, **not a proven cross-load identity**; `Part.Id` can also repeat.
- Use a session-scoped runtime-ID hint plus a vehicle-relative structural locator and template/name checks. Establish which saved hierarchy data can identify a part across reload before shipping the resolver. If identity cannot be proven, preserve the saved selection as unresolved and offer explicit rebinding; never choose the first same-template part.
- Include subpart ancestry, duplicate-part cases, staging/debris and editor-versus-flight context in resolver tests. Do not silently follow a staged part into a different vessel unless the operation explicitly supports that behavior.
- Controlled-vehicle mode is an explicit authoring selector; resolve it when the player applies an operation. Applied effects retain that resolved target. Preserve dynamic following only for feature behaviors explicitly designed for it (e.g. controlled-vehicle telemetry).
- Store template/mesh/texture/clip IDs and imported library filenames, not combo indices or GPU/material handles. Missing assets leave drafts intact with inline resolution errors and Apply disabled where required.
- Merely loading a workspace cannot invoke catalog-refresh helpers that also hot-swap live resources (e.g. Graffiti's `RefreshLibrary`). Separate read-only catalog scans from explicit runtime refresh operations.

## Feature migration inventory — all 25 retained features

Each row must yield a complete field inventory before its implementation is considered migrated. Existing backing types below are starting points, not a claim that they already implement the proposed contracts.

| Feature | Saved authoring state | Live State representation / special handling |
|---|---|---|
| Average TWR | Target mode and sampling/setup/view options that exist in the UI | Recorder session backed by `TwrSampleAccumulator`; collection status, statistics and reset/stop controls move to inspector. Save/load never resets samples. |
| Bloomin' Onion | Full `RingDefinition`, stripes/LODs, body, preset, editor sections | `AppliedRing` per body from `RingDefinitionController`; inspect/edit/remove/copy. Shared ring ownership with Rocky needs coordination. |
| Camera Animations | All animation-type inputs, group draft, complete ordered sequence definitions and return-to-start settings | Singleton current playback with independent instantiated keyframes; pause/resume/stop/progress. Loading a sequence draft cannot alter its executing copy. |
| Con-Man | Selected layout plus copied gauge-layout draft, filters/name fields | Applied HUD layout singleton; live gauge summary/edit/capture controls. Separate persistent startup-default preference from workspace loading. |
| DOH | Vehicle/character, offset, count, color/random policy, material-sharing choice | Spawned kitten records from `SpawnedKittenRegistry`; recolor/despawn. Preserve shared material ownership for batch spawns. |
| Don't Stifle Me | Intended enabled/snap/expanded-limit values | Editor policy singleton(s), backed by live `EditorScaleSettings`/`EditorLimitSettings`; explicit Apply and inspector Restore. Existing part scale is not rolled back by workspace load. |
| Eternal Flame | Target, intended refill interval, fuel/electricity options | `MonitoredVehicle` entries in `FuelManager`; runtime toggles/removal. Current interval is shared: represent it as manager policy unless deliberately changing that behavior. |
| Free Fallin | Complete `CanopyMaterialSettings`, PNG/mode/color/PBR/rotation and browser preferences | Global canopy appearance singleton; inspector reapply/restore, runtime textures remain retained. |
| Garry's Torch | Source/target/anchor, full `WeldPreset`, filters and animation authoring fields | `WeldEntry` plus its animation queue; modify/unweld/animate/copy. Preserve source uniqueness, weld sorting and solver synchronization. |
| GeeForce | Display settings, threshold draft, view preferences and target mode | Recorder singleton from `GForceRecorder`; graph, live/paused view, stats and clear controls. Move breach bookkeeping out of rendering and separate it from draft thresholds. |
| Glass | Intended FOV and lens choice | FOV override singleton backed by `FovController`; explicit Apply, live adjustment/disable in inspector. |
| Graffiti | Image and placement dimensions/depth/roll/range/alpha/brightness, relevant renderer-policy drafts and library filters | `DecalEntry` per placed decal, including dormant anchors; preserve multi-selection/bulk removal in Live State. Renderer-global policy uses a separate live record where needed. Never restore armed click state. |
| Hot Pursuit | Placement range and new-camera defaults for pose/FOV/resolution | `HotPursuitCamera` per mount; inspect/edit/remove/visibility/reopen/resize. Preserve viewport leases while switching workspaces, including when the feature is hidden. |
| Humble Arteest | Separate paint, kitten-material and emissive drafts; colors/blend mode/targets/filters | Per-part, per-template and global paint overrides; material tint records; per-engine emissive records and global policy. Preserve precedence and shared-material effects; material indices alone are not saved target identity. |
| I Feel Seen | Target and intended render-visibility override | `VehicleTracker` entries; per-vehicle management moves entirely to inspector. |
| Its So Shiny | Grid name/dimensions/layout/offset/scale/color/intensity and pattern/scroll recipe | `ShinyGridManager` grids and current animation; scans register live items only on explicit user action. Destroy removes created parts using existing safeguards. |
| Kitchen Sink | Draft Force IVA choice and inputs to maintenance actions | IVA rendering policy singleton. Refresh Vehicle remains a one-shot action with no continuing managed record. |
| Kitten Animations | Clip selection, playback recipe, expression/variant policy, strengths and locomotion tuning draft | Existing driver/expression ownership, plus global locomotion tuning override; move live readouts/control to inspector. `_context` currently couples draft widgets to the controlled avatar: split it and retain drafts while no kitten is controlled. |
| Kiwi's Marbles | Source/body/target, offset, units and applicable placement modes | `CelestialWeldEntry` per source plus pending restore status; retain topological order, deferred solver restores and live surface controls. |
| Parts Now | Mod metadata, full Assets/Part/GameData text, tab/filter/folder selection and validation-input state | Runtime-loaded mod records and active load jobs; results/reload/unload and unused-part safety gates in inspector. Loading a workspace never installs files, loads content or cancels jobs. Headroom/startup settings remain separate application preferences. |
| Pyro | Complete plume settings, target, nozzle/look parameters; separate copied shared-template draft | `PlumeEntry` per plume; shared-template override record per template ID, explicitly affecting stock engines too. Expose currently preset-only/create-later fields in the full authoring form. |
| Rocky McRock Face | `RingSelection` drafts, body, mesh/texture choices and filters, including per-body draft map if retained | Applied swap per ringed body. Current API is per-body, not one universe-wide singleton. Runtime snapshots own converted mesh retention and coordinate with Bloomin' Onion. |
| Skittles | Selected theme and complete detached `ThemeDefinition` editor draft | Applied global ImGui style singleton. Theme selection/editor must not mutate `ImGui.GetStyle()` until Apply; inspector can edit live style. Startup preference remains separate. |
| Thug Life | Target/part/subpart, transform/size and animation recipe | `ThugLifeEntry` plus optional slide, owned by render manager; live visibility/edit/remove. Keep lazy GPU initialization. |
| Zippo | Target, intensity/enabled/color and full animation recipe | Managed light edits by actual target scope plus queued animation sessions. Current APIs/queues use string part IDs in places: remove ambiguity; light template writes can affect multiple instances and must be labeled/tracked at that scope. |

Ring coordination is a substantive integration issue: Rocky and Bloomin' Onion both mutate the same ring reference graph and keep restoration snapshots. Shared infrastructure must define ownership/ordering and baseline restoration so removing one override cannot erase another. Preserve both authoring tools; do not solve the conflict with duplicated snapshots or assume different project boundaries imply different game resources.

## UX specification

### Main workspace

Use a menu row **Features | Save | Load | Live State**. Show workspace name and a modified indicator. A searchable feature navigation pane and full-width authoring pane make 25 features manageable; allow the navigation pane to collapse at narrow widths. Features controls include show/hide, Show All/Hide All and clear empty-workspace guidance. Visibility is presentation only and is saved.

Keep the feature-specific forms and familiar named sections, but organize each as Target → Settings → explicit Apply/Create/Play. Inner expansion state is explicit and persisted. Show validation and action feedback here; all managed runtime lists, graphs/status panels and editing of applied values belong in Live State. A successful action can offer “Open in Live State” without forcing a focus change. Reset Form only resets draft data.

### Save dialog

- Save opens an ImGui modal with a full-width name input and filtered **Existing save** dropdown (including New save).
- Selecting an existing save fills its name and changes the primary action to **Overwrite**.
- A typed name collision selects/identifies the existing save and offers Overwrite rather than silently creating a duplicate. Use trimmed, case-insensitive normalized-name uniqueness and stable file IDs.
- Empty names cannot save. Save errors remain in the dialog, retaining inputs; success closes it and updates workspace identity/modified state.
- Save writes authoring state only. Overwrite is deliberate in the dialog; no additional routine confirmation chain is needed.

### Load window

- Load opens a separate resizable ImGui window with a filterable saved-state list (name, modified date, visible-feature count, compatibility status).
- Single-click selects; double-click or **Load selected** invokes the exact same load path once. Disable the button with no valid selection.
- Loading fully replaces authoring state, including hidden feature drafts and feature visibility; close the load window after success. On failure retain current workspace and show the error.
- Inline text states that existing live effects continue. Maintain a recovery copy of the outgoing draft so replacement is recoverable without an interrupting save prompt.

### Live State window

- Live State opens/focuses a separate resizable ImGui window, independently visible from the main workspace.
- Search and optional feature/type/status filters above a list of label, feature, target and status. Show active, paused, dormant, pending and failed items as appropriate, including hidden features.
- Selecting a row discloses its feature-owned inspector next to the list; at narrow widths stack the inspector below it. Use stable item IDs to retain selection across insertions/reapply and clear it safely on removal.
- Use bordered disclosure sections for complex settings and existing domain-specific controls. Preserve Graffiti-style multi-select/bulk actions within compatible item types.
- Copy settings to form is explicit. Live inspector edits never silently rewrite authoring drafts or saved presets.
- Closing this window hides the inspector only. Hot Pursuit feed windows and every live operation continue under their normal runtime ownership.

### ImGui conventions

Use the repository KSA/ImGui/ImGui-design/Harmony skills, with current source/scope taking precedence over stale examples. Preserve `SubmodUI.BeginContentArea` / `EndContentArea` behavior when moving it to shared UI infrastructure: 20px content padding, section gaps, `NoPadOuterX`, 6px cell padding, aligned labels and stretch widgets (`SetNextItemWidth(-1)`). Use 1:3 label/widget rows or four-column parameter grids; fall back to two columns on narrow windows. Size adjacent action buttons as an even-width row with gaps where appropriate. Filter long combos. Balance all Begin/End and style stacks, including early/error returns. Scope every form/inspector ID by feature and item identity. Restoring section state requires a one-frame explicit open/close override; `ImGuiCond.Once` alone cannot apply a later workspace load.

## Persistence and restoration design

Use versioned JSON envelopes with explicit typed DTO serialization (System.Text.Json is available on the existing runtime). This supports nested sequences and drafts without adding another package. Keep importing legacy TOML with the already-used Tomlyn code. Runtime contracts never carry serializer-discovered KSA graphs.

Suggested storage under existing `KsaPaths.UserDataDir/.unscience/`:

```text
workspaces/<save-id>.json
feature-presets/<feature-id>/<preset-id>.json
session/last-workspace.json
session/before-load.json
preferences.json
```

Named saves change only through Save/Overwrite. Autosave updates `last-workspace`, never the named save. Preserve auto-save preference and interval separately from named workspace content. Restoring last workspace at launch is the same draft-only path; legacy theme/layout startup policies are separate, explicit application preferences and are not rerun on workspace load.

Load in two phases: (1) parse, schema-migrate, validate and prepare every feature snapshot with no game mutations; (2) replace the complete draft/view-state set at a UI frame boundary. Default/reset fields absent from an older supported schema; never merge with whatever happens to be in the current form. Missing targets/assets are nonfatal unresolved selections. Unsupported newer feature schemas or malformed known payloads block load with an actionable error, leaving current state intact. Known removed features can be skipped with a notice; preserve unknown feature envelopes for forward round-tripping where compatible, without interpreting them as commands.

Use bounded file sizes/collection counts, finite-number checks, stable enum discriminators and deterministic defaults. Capture detached snapshots on the UI thread, write a temporary sibling file, flush and atomically replace; retain a backup on overwrite. No raw save name in a filesystem path. Validate names consistently in Save and Load discovery. Keep corrupt files visible with errors without breaking the whole list.

Migrate existing `state.toml` display-name keys with an explicit old-name-to-ID map and import the main-window `window.ini` only. Import Garry's Torch, Pyro, Bloomin' Onion, Con-Man and Skittles authored saves without applying them. Make migration idempotent, preserve originals and report imported/skipped counts. Do not import active runtime objects. Preserve existing decal/parachute libraries and reference their assets from drafts.

## Implementation sequence and completion gates

1. **Baseline and removal pass.** Record current registrations/dependencies and build; remove requested features and their consumers, move queue drain to host, remove dead packaging dependencies. Rebuild into a clean distribution and confirm no removed DLLs. Update affected scope/docs immediately.
2. **Foundation.** Add narrow contracts/UI projects, immutable feature/operation IDs, typed draft/preset patterns, versioned store and pure load pipeline. Add dependency checks and tests using fake runtime providers. Keep existing host behavior running while participants migrate; do not advertise complete workspace saves with silently omitted features.
3. **Representative end-to-end slice.** Migrate Garry's Torch (additive/unique-source welds), Pyro (additive plus shared-template edits), and Free Fallin (global singleton). Deliver Save/Load dialogs and Live State list/inspector using real state. Prove preset A → apply → load B leaves A's effects intact before broad rollout.
4. **Shared resource boundaries.** Extract light/ring infrastructure; fix ring ownership and GPU retention, then migrate Bloomin' Onion, Rocky, Zippo and Its So Shiny. This phase must precede relying on their saved drafts during renderer rebuilds.
5. **Remaining feature migration.** Move simple tracked objects (I Feel Seen, Eternal Flame, DOH, Thug Life, Kiwi's Marbles), then Graffiti/Hot Pursuit, camera/kitten animations, materials/global policies/theme/HUD, telemetry and Parts Now. Each feature moves its entire runtime management UI in the same change as its state split. Preserve authored settings when targets are absent.
6. **Single-host consolidation and UX completion.** Retire retained standalone wrappers after migrating assets/docs and any patch/startup responsibilities; keep every feature `.lib.csproj`. Complete responsive navigation, inner view-state restoration, recovery/autosave and legacy imports. Update scaffolding and release packaging. Verify all 25 registered features participate.
7. **Release validation.** Full solution build and meaningful persistence/ownership tests, then in-game interaction and rendering checks. Update the host README, repository README/index and all affected library READMEs. Update `scope/00-architecture-and-abstractions.md`, every touched area, the master surface, and the concise `FULL_SCOPE.md` ToC/status in the same implementation changes.

Keep feature files focused (roughly 300 lines where useful): draft DTO, runtime manager/provider, authoring panel and live inspector are natural separate files. Do not combine a mechanical move with a speculative rewrite of working game math/shaders. The migration is architectural; the validated game-specific implementations should survive it.

## Required verification

- Round-trip every feature's non-default settings, nested lists, strings, targets, filters and inner section states, including hidden features and absent targets. Keep explicit fixture/field coverage so new settings cannot silently escape persistence.
- Snapshot/draft/live isolation: edit a draft after applying, load a feature preset and load an entire workspace; none changes the applied settings or shared nested objects. Capture itself must be non-mutating.
- Assert no Apply/Remove/Dispose/Initialize/Play/Stop/renderer/viewport calls during load and during subsequent restored-window rendering. Running simulation may naturally advance; compare command calls, item identity, configuration and continuity rather than requiring clocks to freeze.
- Missing/ambiguous targets, duplicate part templates, changed hierarchy, game reload, controlled-vehicle changes and missing assets cannot silently bind elsewhere.
- Singleton reapply preserves row identity and replaces only the intended scope; additive apply preserves existing items. Failed resource allocation/rebuild retains or clearly faults the correct live record.
- Invalid/newer schemas, malformed JSON, empty names, Unicode/case collisions, failed writes and interrupted overwrite leave existing saves/current draft intact. Legacy imports are repeatable without duplicates.
- Named saves are never autosaved over; complete workspace loads reset omitted supported fields and do not inherit hidden-feature state from the outgoing workspace.
- In-game: weld + Pyro laser eyes + custom rings, save A, apply effects, load B, hide their features, open Live State and edit/remove A's effects. Load A again: no duplicates, stopped playback, lost viewports or restored stock materials.
- Cover camera playback/queues, renderer rebuild and ring override order, dormant targets, editor/flight transitions, F2 hidden HUD, F11 main-window toggle, text-input hotkey guard, closed Live State window, and unload cleanup. Check layout at narrow/wide sizes and UI scales.
- Build the complete shipping solution with `dotnet build`; a green build does not verify the game's Vulkan, solver, viewport or Harmony runtime behavior. Existing KSA 5402 limitations remain separate from this UX migration.

## Analysis validation record

The working tree was clean before analysis. The initial default build stalled without output and was stopped. The full solution then compiled using the existing restored dependencies, with build servers disabled:

```sh
dotnet build ksa-mod-experiments.slnx --no-restore --disable-build-servers -m:1 --nologo -v minimal -p:UNSCIENCE_DIST_DIR=/private/tmp/unscience-workspace-analysis-cached-dist
```

Result: **build succeeded, 0 warnings, 0 errors**, elapsed 1m 5s. This validates compilation against the locally available references and package cache, not a fresh network restore. `git diff --check` passed. Only this plan and discovery links in the repository README/index and host README were changed. This plan does not claim an in-game validation run or implementation completion.
