# KSA ring infrastructure

Owns RingAssetCatalog, RingMeshFactory, RockyUi and RingOwnership. Bloomin’ Onion and Rocky McRock Face use these without referencing one another. BeforeReplace notifies overlays to restore/release the outgoing ring reference before replacement; converted meshes remain alive until a successful rebuild or disposal.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.
