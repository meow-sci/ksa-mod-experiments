# Unscience game integration scope

Start here before modifying game integration. This folder maps Harmony targets, lifecycle hooks, reflection, game members, shader layouts and assets to their current owners. Keep the corresponding area file and master index updated in the same change.

## Baseline and current status

Reference build: **KSA 2026.9.7.5402**, sibling `ksa-game-assemblies/current/{dll,decomp,Content}`. The repository's `decomp/ksa` is stale. The previous upgrade comparison was 5348 → 5402; only revision 5401 had a local changelog entry, so the earlier upgrade used source/asset diffs. Dated findings are preserved in [history](history/README.md); active area pages and the [master index](game-integration-surface.md) describe current owners.

The workspace redesign retains **26 feature library projects**, adds game-independent contracts plus shared lights/rings infrastructure, and ships one Unscience host. Every feature exposes detached authoring capture/restore and typed runtime items. All retained bundled standalone entries, RPC, and the user-designated obsolete features are retired. The project graph and complete list are in [REPOSITORY_INDEX.md](../REPOSITORY_INDEX.md).

The solution compiles against 5402. Automated contract checks cover save/overwrite/recovery, transactional draft restoration and live-state isolation; project checks enforce independent feature references. Native ImGui, game simulation and GPU behavior still require an in-game acceptance pass. See [workspace verification](../docs/WORKSPACE.md#verification).

Outstanding game-specific checks carried forward: Hot Pursuit secondary viewport omissions/lease contention; Pyro refraction (game-side `_hasRefractionInstances` is not set in 5402); Garry's Torch collision/debris behavior; Graffiti terrain/deployed-canopy picking; Parts Now loader/thumbnail/on-demand mesh relocation invariants; Free Fallin projection/PBR/restore; editor scale/parachute limits and symmetry; Kiwi near deployed chutes; Force IVA viewport gating; Thug/Shiny rendering; Humble Arteest paint/material paths; Kitten animation driver and Con Man HUD scaling. Prior compile success never cleared these behavioral risks.

Current runtime changes: all 26 features expose explicit release; initialization failures are isolated; original-value restoration and owned GPU cleanup are implemented; detached validation and native ImGui recovery are strengthened. Managed ownership tests cover rollback, shared baselines and tail reclamation. Parts Now now grows raster mesh buffers on demand, refusing relocation with a live ray-tracing renderer. The requested cross-feature asset-unload lease mechanism remains excluded. Native acceptance is pending.

Current creative-tool additions: Zippo Disco uses per-instance LightModule templates and shared assembly actuator ownership; Humble Arteest adds rendered-mesh cursor picking and mesh paint scopes; Graffiti adds held-input spray cadence. See the area pages and master surface for exact members. No new Harmony targets or GPU layouts; managed checks do not establish native acceptance.

Pebbles provides mesh/import, linked scale/collider setup and selected/all clutter-type replacement with automatic GLB material assignment and warned fallbacks for common Blender appearance extensions. Its simplified authoring retains the existing per-celestial override and preview integration inventory. Its runtime GLB importer, new runtime hooks, private GPU resources and physics invalidation require native acceptance; see [ground clutter](ground-clutter.md).

## Integration model

- `unscience/Mod.cs` is the StarMap entry and owns the 26 feature lifecycles. Visibility affects authoring navigation only. `IWorkspaceFeature` extends `ISubmod`; each feature owns its recipes and live records.
- `unscience/Patcher.cs` retains host-only menu/input/HUD hooks. Feature-defined groups use independent Harmony owners with first-demand activation and final-release teardown. Garry's Torch uses a solver-safe after-GUI phase, not an added solver prefix.
- `HiddenUiFrameHook` replays non-UI updates while the game's HUD is hidden. Parts Now keeps GPU load/purge before GUI. The host drains GameThread independently of RPC.
- `unscience-contracts.lib` has no game references. `ksa-abstractions.lib` owns draft bindings, exact target identity, common UI/live contracts and cross-cutting game helpers. `ksa-lights.lib` and `ksa-rings.lib` own shared domain integration; feature libraries never reference one another.
- Saving/loading only replaces detached authoring state. Explicit Apply invokes the existing game operations. Renderer handles, welds, players, samples, jobs and live item data are never serialized as workspace state.

## Area index

| Area | Active features / integration |
|---|---|
| [Master surface](game-integration-surface.md) | Game types, reflection watchlist, shader/asset dependencies, dated upgrade findings |
| [Architecture](00-architecture-and-abstractions.md) | Host, shared contracts, selectors/part identity, StarMap, HotkeyGuard, hidden-HUD fallback |
| [Vehicle physics](vehicle-physics.md) | eternal-flame, i-feel-seen, garrys-torch; resource and solver/render tracking |
| [Celestials and lights](celestial-and-lights.md) | kiwis-marbles, zippo; celestial welds, shared LightController, exact live light identity, Disco templates/actuation |
| [Camera](camera.md) | camera-controller-override, glass, hot-pursuit; separate draft/player and viewport leases |
| [Telemetry](telemetry.md) | average-twr, geeforce; live recorders and monitoring policy |
| [Pixel grids and render](pixel-grids-and-render.md) | its-so-shiny, thug-life; grid state and custom pass |
| [Characters and materials](character-and-materials.md) | doh, humble-arteest, kitten-animations; EVA, material/paint/engine overrides, mesh-instance cursor paint, animation driver |
| [Part editor](part-editor-and-robotics.md) | parts-now, dont-stifle-me; loading jobs, on-demand mesh storage, editor policy |
| [Exhaust](exhaust-plumes.md) | pyro; plume instances and explicit shared-template recipes |
| [Decals](decals.md) | graffiti; DecalEntry, picking, custom pass and global policy |
| [Parachutes](parachutes.md) | free-fallin; detached recipe and applied material resources |
| [Ground clutter](ground-clutter.md) | pebbles; private per-body ecotype graphs, material buffers, collision rebuilds and Workshop preview |
| [Ground clutter GLB import](ground-clutter-glb-materials.md) | pebbles; local GLB geometry/scene decoding, native embedded PNG/JPEG conversion, private texture upload and retirement |
| [Rings](rings.md) | rocky-mcrock-face, bloomin-onion; shared catalog/mesh infrastructure and outgoing-reference coordination |
| [UI customization](ui-customization.md) | skittles, con-man, kitchen-sink; explicit style/layout/IVA policies |
| [Retired RPC](rpc.md) | Removed server/client; no active network surface |
| [Nonshipping experiments](standalone-mods.md) | Music experiment outside the shipping solution; removed standalone owners |

## Game-update workflow

1. Read this index and the relevant area before editing. Rebuild against the new DLLs to identify typed signature changes.
2. Diff string reflection names, member kind/type and every active Harmony target signature/body against the previous reference build. A compile-clean string lookup can still fail at runtime.
3. Check shader sources, binary layouts, assets and semantic assumptions: especially free StateBitFlag bits, renderer ownership, viewport gates and StarMapAllModsLoaded occurring before ModLibrary.Bind for Parts Now's mesh reservation.
4. Read source changes when changelogs are incomplete. Preserve unresolved behavioral checks explicitly; do not equate a build with in-game verification.
5. Update the master and affected area in the same change, bump the baseline, and record remediation/validation. Keep this file concise; detailed evidence belongs in adjacent pages or the dated upgrade plan.
