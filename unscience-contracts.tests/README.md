# Unscience persistence checks

Dependency-free executable tests for normalized-name collisions, stable overwrite IDs, backups, malformed/newer saves, unknown features, full resets, failed-restore rollback and live-state isolation using independent participant state.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.
