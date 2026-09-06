# Unscience persistence checks

Managed executable tests for normalized-name collisions, stable overwrite IDs, backups, malformed/newer saves, unknown features, full resets, failed-restore rollback and live-state isolation using independent participant state.

The executable also source-links the game-independent `DiscoTiming` and `SprayCadence` implementations from their owning features. It checks independent channel timing, easing, serialization, immediate spray presses, interval pacing, UI capture, release/cancellation and dropped-frame behavior. It does not reference feature assemblies or call native/game APIs.

See [workspace architecture](../docs/WORKSPACE.md), [repository index](../REPOSITORY_INDEX.md), and [integration scope](../scope/FULL_SCOPE.md).

Build with `dotnet build ksa-mod-experiments.slnx`. Run persistence checks with
`dotnet run --project unscience-contracts.tests --no-build`; run dependency checks with
`python3 scripts/check-workspace-boundaries.py`.

## Runtime ownership

Runtime checks now exercise partial activation rollback, retry suppression, failed-release retention, shared baseline ownership, out-of-order mesh-tail reclamation, external-allocation protection and detached malformed-value rejection. They execute no KSA/Harmony/native UI/GPU APIs.

Pebbles source-links its pure recipe validation, Workshop state and camera/manipulator math. Checks cover finite values, five-LOD structure, collision-compatible placement, compound primitive dimensions, detached snapshots, transform persistence, projection/unprojection, Euler conventions, mirroring and undo/redo. Simple-authoring checks cover selected/all target isolation, exact slot identities, linked mesh/collider scaling and automatic material-map propagation through every LOD. They do not exercise native resource transactions or GPU pipelines.

GLB checks use generated binary fixtures to exercise container/accessor bounds, unsigned index widths, normalized UVs, generated normals, scene instances, node matrices/quaternions, mirrored winding, source material-slot isolation and exact path/hash identity through detached recipe cloning. Malformed data, external buffers, unknown required extensions, cycles, skins, morph targets, missing textured UVs and nonfinite/overflowing geometry are rejected. Compatibility checks accept known appearance extensions with warnings, preserve source base-color references/factors and verify baked texture transforms/secondary UVs and specific errors for genuinely unsupported main texture encodings. The detached material reader uses an injected synthetic decoder to verify main pixel preservation, skipped incompatible detail maps, PNG/JPEG fallback source selection, legacy specular/glossiness diffuse/UV/alpha conversion, wrapping warnings, blended cutouts and cached recipe isolation. Synthetic pixels verify linear color-factor baking, packed PBR factors, normal scaling and alpha-mask threshold conversion. These checks do not invoke native PNG/JPEG decoding, file-browser UI, image upload or bindless texture APIs.
