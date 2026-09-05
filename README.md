# Unscience

A configurable workspace for Kitten Space Agency experiments. Unscience ships as one mod;
25 separate feature library projects keep the code and runtime ownership boundaries clear.

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

The existing GitHub Actions workflow builds the solution and packages only the `unscience` folder.
Main-branch pushes produce tip prereleases; `release/<version>` pushes produce versioned releases.
PRs and configured development branches build without publishing. Proprietary game assemblies
come from the private `ksa-game-assemblies` checkout configured in CI.
