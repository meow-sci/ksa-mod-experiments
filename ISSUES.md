# Current issues and validation

The workspace redesign builds successfully and managed save/restore, dependency-boundary and distribution checks pass. Native game/UI/GPU behavior is not yet verified by those checks.

- **Workspace acceptance:** run the multi-effect save A/save B/load A/B sequence in [WORKSPACE.md](docs/WORKSPACE.md#verification), including missing targets, session recovery, singleton replacement and live-item removal.
- **Eternal Flame:** the previously reported refill-during-burn behavior still needs in-game verification. Electricity refill is already supported here; it is not a pending Zippo feature.
- **Garry's Torch:** recheck weld behavior, error logging and debris/structural-failure interactions with the existing disposed guard and solver-safe timing.
- **Humble Arteest:** verify the current paint/material/engine paths in-game before treating older “dead by design” reports as current findings.
- **Kitten Animations:** verify the reworked driver, clips and expression controls; previous reports predate the current implementation.
- **Other game integration risks:** the current list is maintained in [FULL_SCOPE](scope/FULL_SCOPE.md#baseline-and-current-status), including camera, ring, decal, parachute and runtime-loader checks.

Blinky, Red Alert, Flexo and the other removed features are retired, not open repair tasks. Old reports and per-build investigations remain in [historical triage](docs/notes/ISSUE_TRIAGE_HISTORY.md). Feature ideas are tracked separately in [IDEAS.md](IDEAS.md).
