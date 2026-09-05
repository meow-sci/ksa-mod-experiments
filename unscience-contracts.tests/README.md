# Unscience persistence checks

Managed executable tests for normalized-name collisions, stable overwrite IDs, backups, malformed/newer saves, unknown features, full resets, failed-restore rollback and live-state isolation using independent participant state.

The executable also source-links the game-independent `DiscoTiming` and `SprayCadence` implementations from their owning features. It checks independent channel timing, easing, serialization, immediate spray presses, interval pacing, UI capture, release/cancellation and dropped-frame behavior. It does not reference feature assemblies or call native/game APIs.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.

## Runtime ownership

Runtime checks now exercise partial activation rollback, retry suppression, failed-release retention, shared baseline ownership, out-of-order mesh-tail reclamation, external-allocation protection and detached malformed-value rejection. They execute no KSA/Harmony/native UI/GPU APIs.

Pebbles source-links its pure recipe validation, Workshop state and camera/manipulator math. Checks cover finite values, five-LOD structure, collision-compatible placement, compound primitive dimensions, detached snapshots, transform persistence, projection/unprojection, Euler conventions, mirroring and undo/redo. They do not exercise native resource transactions or GPU pipelines.
