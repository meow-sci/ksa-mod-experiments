# ksa-mod-experiments

Silly Kitten Space Agency mods, available as standalone projects and through the `unscience`
umbrella mod. Start with [`REPOSITORY_INDEX.md`](REPOSITORY_INDEX.md) for the complete catalog and
[`scope/FULL_SCOPE.md`](scope/FULL_SCOPE.md) for the game-integration map.

The current camera experiments include `hot-pursuit`: click a vehicle part to mount a live feed in
one of KSA's stock secondary viewports, then tune its part-local pose, FOV, and resolution.

The parachute experiments include `free-fallin`: globally tint the stock canopy, replace its albedo
with a PNG or composite a centered decal onto it, and tune its PBR response.

To install dependencies:

```bash
bun install
```

To run:

```bash
bun run 
```

This project was created using `bun init` in bun v1.3.10. [Bun](https://bun.com) is a fast all-in-one JavaScript runtime.

## building

Every project compiles against the proprietary KSA game assemblies, which are
never committed here. `Directory.Build.props` resolves them (first match wins):

1. `KSA_DLL_DIR` env var (or `-p:KSA_DLL_DIR=...`) — what CI uses.
2. A `ksa-game-assemblies` checkout cloned next to this repo (`../ksa-game-assemblies/current/dll/`).
3. Per-OS defaults (game install dir on Windows, `~/repos/meow-sci/ksa-game-assemblies/current/dll/` elsewhere).

If none resolve, the build fails with a single actionable error instead of a
wall of missing-type errors.

```bash
dotnet build ksa-mod-experiments.slnx
```

Each mod deploys its folder to the KSA user mods dir; set `UNSCIENCE_DIST_DIR`
to redirect all of them (CI does this and zips `<dir>/unscience`).

## releases (GitHub Actions)

`.github/workflows/release.yml` builds the whole solution and publishes ONLY the
`unscience` umbrella mod (which bundles every submod `.lib`) as a zip:

- push to `main` → prerelease `tip-<UTC stamp>`; the 5 newest tip builds are kept, older ones pruned
- push to `release/<version>` → release `v<version>` (re-pushing the branch rebuilds/moves it)
- `feature/**`, `fix/**`, `chore/**` branches and PRs into `main` → build only

The private assemblies come from `meow-sci/ksa-game-assemblies` via the
`KSA_GAME_ASSEMBLIES_PAT` repo secret (fine-grained PAT, read-only Contents on
that repo).
