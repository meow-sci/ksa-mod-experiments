# KSA abstractions

Game discovery, part identity, solver scheduling, HotkeyGuard, hidden-HUD lifecycle, shared ImGui layout (including responsive FormGrid and full-width label-above-input FormField), explicit draft bindings and typed live-item interfaces. Depends on data-only Unscience contracts. PartIdentity verifies vehicle topology and root/subpart path because KSA regenerates runtime instance IDs; editor-only parts use a session identity.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.
