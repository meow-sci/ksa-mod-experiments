# Unscience data contracts

Data-only workspace/feature snapshots, participant interface, schema validation, atomic named stores and transactional workspace restore. No KSA, ImGui or Harmony reference. Restoring invokes prepared authoring setters only. Unknown feature payloads round-trip through the host.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.
