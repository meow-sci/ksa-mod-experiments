# KSA light infrastructure

Owns LightController: discover light parts and read/write the game’s light templates. Used independently by Zippo and It’s So Shiny. It contains no feature preset, workspace view or feature lifecycle policy.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.

## Runtime ownership

`LightStateLease` coordinates reference-identity ownership of shared light-template color/intensity and per-assembly switch fields. Multiple feature owners share a baseline; the last successful release restores it. Disco can lease only the switch while retaining its own module-local templates.
