# Unscience repository index

Unscience is one shipping mod with 26 feature libraries. Each library retains its own `.csproj` and source folder.
Contributor rules are in [AGENTS.md](AGENTS.md). Documentation inventory/link checks run through `python3 scripts/check-docs.py`.
Link checks exclude ignored build leftovers so retired project directories cannot mask broken links in a clean checkout.
Start with [workspace architecture](docs/WORKSPACE.md) and [integration scope](scope/FULL_SCOPE.md).

## Shipping host and infrastructure

| Project | Responsibility |
|---|---|
| [unscience](unscience/README.md) | StarMap/Harmony lifecycle, feature catalog, authoring workspace, named Save/Load dialogs and Live State window. |
| [ksa-abstractions.lib](ksa-abstractions.lib/README.md) | Game discovery, part identity, solver scheduling, HotkeyGuard, hidden-HUD lifecycle, shared ImGui layout, explicit draft bindings and typed live-item interfaces. Depends on data-only Unscience contracts. PartIdentity verifies vehicle topology and root/subpart path because KSA regenerates runtime instance IDs; editor-only parts use a session identity. |
| [ksa-lights.lib](ksa-lights.lib/README.md) | Owns LightController: discover light parts and read/write the game’s light templates. Used independently by Zippo and It’s So Shiny. It contains no feature preset, workspace view or feature lifecycle policy. |
| [ksa-rings.lib](ksa-rings.lib/README.md) | Owns RingAssetCatalog, RingMeshFactory, RockyUi and RingOwnership. Bloomin’ Onion and Rocky McRock Face use these without referencing one another. BeforeReplace notifies overlays to restore/release the outgoing ring reference before replacement; converted meshes remain alive until a successful rebuild or disposal. |
| [unscience-contracts.lib](unscience-contracts.lib/README.md) | Data-only workspace/feature snapshots, participant interface, schema validation, atomic named stores and transactional workspace restore. No KSA, ImGui or Harmony reference. Restoring invokes prepared authoring setters only. Unknown feature payloads round-trip through the host. |
| [unscience-contracts.tests](unscience-contracts.tests/README.md) | Managed feature timing/spray checks plus persistence tests for normalized-name collisions, stable overwrite IDs, backups, malformed/newer saves, unknown features, full resets, failed-restore rollback and live-state isolation using independent participant state. |

## Feature libraries

| Project | Function | Live ownership |
|---|---|---|
| [average-twr.lib](average-twr.lib/README.md) | Record and inspect thrust-to-weight statistics. | A recorder with pause, reset and measurements. |
| [bloomin-onion.lib](bloomin-onion.lib/README.md) | Create custom planetary rings. | One applied custom ring per body; copy recipe, remove or remove all. |
| [camera-controller-override.lib](camera-controller-override.lib/README.md) | Compose cinematic camera motion. | A separate camera sequence player; play, pause, resume, stop and copy recipe. |
| [con-man.lib](con-man.lib/README.md) | Configure the game gauge layout. | The live console layout, original-value restoration and legacy saving controls. |
| [doh.lib](doh.lib/README.md) | Spawn kitten characters with customized materials. | Each spawned kitten with its material controls. |
| [dont-stifle-me.lib](dont-stifle-me.lib/README.md) | Relax editor scale and value limits. | The applied editor policy and restore controls. |
| [eternal-flame.lib](eternal-flame.lib/README.md) | Continuously refill selected vehicles. | Each monitored vehicle with refill toggles and removal; shared interval. |
| [free-fallin.lib](free-fallin.lib/README.md) | Customize parachute canopy materials. | Global canopy material override, editable settings, copy and stock restore. |
| [garrys-torch.lib](garrys-torch.lib/README.md) | Weld vehicles onto a target part. | Each vehicle weld with transform, animation queue, preset export and unweld. |
| [geeforce.lib](geeforce.lib/README.md) | Record acceleration and jerk. | The recorder, breach state and measurements. |
| [glass.lib](glass.lib/README.md) | Set the main camera field of view. | The global FOV override with copy and disable controls. |
| [graffiti.lib](graffiti.lib/README.md) | Click or hold-to-spray PNG decals onto parts, terrain and canopy cloth. | Each decal; visibility, transform/appearance and removal; bulk selection and render policy. |
| [hot-pursuit.lib](hot-pursuit.lib/README.md) | Mount secondary cameras on clicked vehicle parts. | Each camera with its own viewport lease, pose, visibility, copy, reopen and removal. |
| [humble-arteest.lib](humble-arteest.lib/README.md) | Paint by list or cursor: mesh instances/shared meshes, parts, kitten materials and engine glow. | Mesh-instance/shared-mesh and per-part/type paint, shared material colors, engine overrides and global policies. |
| [i-feel-seen.lib](i-feel-seen.lib/README.md) | Keep vehicles visible through visibility overrides. | Each tracked vehicle with visibility toggle and removal. |
| [its-so-shiny.lib](its-so-shiny.lib/README.md) | Build and control grids of light parts. | Each light grid, patterns/appearance/destruction and global mesh policy. |
| [kitchen-sink.lib](kitchen-sink.lib/README.md) | Control the retained IVA rendering experiment. | The global IVA override and restore control. Flexo experiments are retired. |
| [kitten-animations.lib](kitten-animations.lib/README.md) | Author kitten body animation, expressions and locomotion tuning. | The explicitly applied kitten identity/mode and captured locomotion tuning. |
| [kiwis-marbles.lib](kiwis-marbles.lib/README.md) | Weld celestial bodies to orbiters. | Each celestial weld with Cartesian/surface controls and solver-safe unweld. |
| [parts-now.lib](parts-now.lib/README.md) | Load new part definitions during play. | Each runtime-loaded mod plus loader progress, results and GPU budget; gated reload/unload. |
| [pebbles.lib](pebbles.lib/README.md) | Per-celestial ground clutter recipes, meshes/materials, runtime GLB import, placement and a detached collider Workshop. | Per-body applied override, original-state restoration and retained GLB assets. |
| [pyro.lib](pyro.lib/README.md) | Attach independent engine plumes to vehicle parts. | Each plume, bulk toggles and each applied shared exhaust-template override. |
| [rocky-mcrock-face.lib](rocky-mcrock-face.lib/README.md) | Replace planetary ring meshes and textures. | One applied swap per body with edit/copy/restore; resources retained independently of the form. |
| [skittles.lib](skittles.lib/README.md) | Configure ImGui colors and style. | The global applied ImGui style with live editor, legacy export and default restore. |
| [thug-life.lib](thug-life.lib/README.md) | Attach and animate sunglasses. | Each sunglasses attachment and its transform/removal controls. |
| [zippo.lib](zippo.lib/README.md) | Apply light appearance, queued animations and Disco party-light recipes. | Per-light Disco with independent channels and restore; each ordinary light/queue; ordinary appearance uses the game’s shared template scope. |

## Templates and experiments outside the shipping solution

- [byo-music](byo-music/README.md): Standalone audio experiment; not included in the Unscience distribution.
- [byo-music.lib](byo-music.lib/README.md): Audio playback library for the nonshipping BYO Music experiment.
- [jplrepo](jplrepo/README.md): Nonshipping diagnostics/experiments project.
- [fixme-mod-name](fixme-mod-name/README.md): Minimal standalone mod scaffold showing the required HotkeyGuard lifecycle; retained as a template.
- [fixme-mod-name.lib](fixme-mod-name.lib/README.md): Library scaffold paired with the mod template.

## Retired

Standalone wrappers for the 25 bundled features and the unladen-swallow RPC/client surface were retired.
Blinky, Red Alert, Mesh Deform, Marque, Stampy and Steely Eyed Missile Kitten were removed.
Space Tape, Grant and Inanimate Carbon Rod entry projects were already absent; remaining Kitchen Sink Flexo experiments were removed.
Historical implementation details remain in Git history.

## Reference material

`decomp/` contains vendored historical decompilation projects, not maintained Unscience projects. Use the sibling `ksa-game-assemblies/current` tree for the current game. Dated plans, issue triage and scope snapshots are labeled as history; current behavior is defined by the guides above.

## Runtime ownership

All 26 libraries implement explicit release of applied state. The shared lifecycle coordinator manages feature-defined Harmony demand, rollback and retries; it never infers lifecycle from visibility or live-row count. `ksa-lights.lib` coordinates light baseline leases; `ksa-abstractions.lib` tracks explicitly owned GPU assets and isolates native UI scopes. Parts Now uses on-demand raster buffer growth with a ray-tracing relocation guard. See [workspace lifecycle](docs/WORKSPACE.md#runtime-release-and-failure-handling).
