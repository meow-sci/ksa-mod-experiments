# Unscience

A configurable workspace for Kitten Space Agency experiments. Unscience ships as one mod;
26 separate feature library projects keep the code and runtime ownership boundaries clear.

Open with **F11** or the game-menu entry. Use **Features** to show or hide tools, then configure
and apply their settings. **Save** names a complete authoring workspace; its existing-save dropdown
selects an overwrite. **Load** opens a saved-state browser: double-click a state or select it and
press **Load selected**. Loading replaces all authoring settings, selections and feature visibility.
Applied effects continue unchanged.

**Live State** opens an independent window containing managed effects. Select a weld, plume, ring,
light, camera, decal, loaded mod or other record to edit or remove it using feature-specific controls.
Feature forms also offer **Save settings as preset** for reusable recipes.

- [User behavior and architecture](docs/WORKSPACE.md)
- [Every project and its purpose](REPOSITORY_INDEX.md)
- [KSA integration and game-update workflow](scope/FULL_SCOPE.md)
- [Contributor instructions](AGENTS.md)
- [Current issues and validation](ISSUES.md)
- [Original redesign plan](plans/UNSCIENCE_WORKSPACE_REDESIGN.md)

## Creative tools

[Zippo](zippo.lib/README.md) includes **Disco** with independent color, actuation and beam-spread cycles for one light or a whole craft. [Graffiti](graffiti.lib/README.md) supports timed hold-to-spray decals. [Humble Arteest](humble-arteest.lib/README.md) paints by world click, targeting an individual mesh instance, a whole subpart or every instance of a mesh. Their recipes are saved with the workspace; applied effects are managed in Live State.

## Build and verify

Requires .NET 10 and licensed KSA reference assemblies. `Directory.Build.props` resolves them from
`KSA_DLL_DIR`, a sibling `ksa-game-assemblies/current/dll` checkout, or the configured OS default.

```sh
dotnet build ksa-mod-experiments.slnx
dotnet run --project unscience-contracts.tests --no-build
python3 scripts/check-workspace-boundaries.py
python3 scripts/check-docs.py
```

The host copies its distribution to the configured KSA mod directory. For an isolated build:

```sh
dotnet build ksa-mod-experiments.slnx -p:UNSCIENCE_DIST_DIR=/tmp/unscience-dist
```

Install the resulting `unscience` folder as one mod. Delete the previous installed `unscience` folder before copying the new distribution so retired DLLs are not left behind. Remove previously installed standalone copies
of the same features to avoid duplicate lifecycle hooks. Named workspace JSON files live in
`<KSA user data>/.unscience/workspaces`; feature recipes live in `.unscience/feature-presets`.
Legacy feature preset files are preserved.

## Releases

GitHub Actions builds the solution, runs the managed/architecture/documentation/release-policy checks, and packages only the `unscience` folder. Licensed game assemblies come from the private `ksa-game-assemblies` checkout configured in CI.

All checks must pass before packaging and publication. The documentation check resolves links against tracked and new nonignored source files; ignored build leftovers from retired projects cannot satisfy a link that would fail in a clean CI checkout.

| Branch/event | Published build | Retention |
|---|---|---|
| `main` push or manual run | `tip-<UTC timestamp>-<run id>-<attempt>` prerelease | Latest 5 tip builds |
| `feature/*` push or manual run | `feature-<UTC timestamp>-<run id>-<attempt>` prerelease | Latest N feature builds across **all** feature branches |
| `release/<version>` push or manual run | `v<version>` stable release | Kept; rebuilding the branch replaces that version |
| Pull requests, `fix/*`, `chore/*`, other manual refs | Build/check only | No release |

N defaults to **5**. Set the repository Actions variable `FEATURE_BUILD_RETENTION` to a positive integer to change it. Feature builds share one naming/retention pool; no branch-specific release series is created. Their embedded mod version is `<base>-feature.<timestamp>-<run id>-<attempt>`, and their ZIP is `unscience-feature-<timestamp>-<run id>-<attempt>.zip`. The release points to the originating commit. Prereleases are not marked Latest.

Cleanup is serialized per channel while feature builds run concurrently. Retention reads every release API page and only deletes older published prereleases in its own channel, including their tags. Draft releases, stable releases and the other rolling channel are excluded. Run `python3 scripts/check-release-policy.py` to check branch publication rules and retention selection offline.

## Runtime restoration

Each feature now owns an explicit release path shared by Live State management and unload. Applied patches activate on demand and disappear after their final active consumer or pending cleanup. Restorable game fields use captured originals, and owned GPU assets are released after consumers and GPU work are drained. Authoring loads remain detached from runtime lifecycle. See [runtime lifecycle and acceptance](docs/WORKSPACE.md#runtime-release-and-failure-handling). Parts Now allocates raster mesh capacity on demand; mesh-buffer relocation requires launching without ray tracing. Its existing cross-feature asset-unload policy is unchanged.

[Pebbles](pebbles.lib/README.md) offers a simple mesh/import → scale and colliders → selected planet clutter types workflow. GLB embedded textures are assigned automatically, common Blender material extensions fall back with appearance warnings, and collider scale stays aligned with the preview. Apply and original-state restoration use per-body live ownership; saving and loading only affect authoring recipes.
