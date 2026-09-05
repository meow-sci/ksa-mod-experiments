# Repository discovery and documentation

Start with [REPOSITORY_INDEX.md](REPOSITORY_INDEX.md), then the relevant project README and [docs/WORKSPACE.md](docs/WORKSPACE.md). Each maintained C# project must have a README covering its purpose, behavior, usage and implementation ownership. The index must cover every maintained project, including nonshipping experiments/templates; vendored decompilation projects are reference material, not maintained Unscience projects.

When adding, removing or changing a feature or shared library, update its README, the repository README and index in the same change. Keep current instructions accurate; put dated investigations in explicitly labeled history instead of leaving contradictory advice in active documentation. `CLAUDE.md` points here rather than duplicating these rules.

# Workspace architecture (required)

- Unscience is **one shipping StarMap mod** with **25 feature libraries**. Retain separate `.lib.csproj` files for code demarcation. Do not create standalone entry projects for bundled features or restore independent distribution as a goal.
- Feature libraries must not reference one another. Use `unscience-contracts.lib` for game-independent data contracts; `ksa-abstractions.lib` for shared game/UI infrastructure; `ksa-lights.lib` and `ksa-rings.lib` for their shared domains. Keep feature-specific game access in its owning library.
- Each feature implements `IWorkspaceFeature`: explicit `DraftBindings`, `CaptureDraft`, `PrepareRestore`, and typed `GetLiveItems`. The host owns navigation/save/load/live-list layout; features own their recipes, runtime records and inspectors.
- Every authoring setting, target/asset choice and durable UI selection must be bound explicitly. Whole-workspace loads replace all drafts and visibility, resetting absent features to defaults. Feature presets preserve the destination's targets while replacing recipe/settings data.
- Save/load must **never** apply settings, mutate game objects, stop playback, clear telemetry, allocate/dispose GPU resources, cancel running jobs or call feature Initialize/Dispose. Prepare validates detached data before returning authoring-only setters. A load may cancel an uncommitted placement gesture; already applied effects continue.
- Main feature forms contain next-action configuration and explicit Apply/Create/Start controls. Applied state and its management belong in **Live State**. Singleton Apply replaces the relevant scoped live state; additive operations create items according to feature semantics.
- Persist target identities, not list indexes. Exact targets are the default; controlled vehicle is an explicit option. Missing targets stay unresolved and block dependent actions. Use the existing topology/path part identity and document its cross-session limits; do not silently select a replacement.
- Hide/show controls authoring visibility, never runtime lifecycle. All features continue updating while hidden. Keep Garry's Torch's solver-safe after-GUI phase, Parts Now's GPU load/purge before GUI and the hidden-HUD fallback.
- Retired features and the Unladen Swallow HTTP/RPC client/server must remain absent. Nonshipping experiments/templates are explicitly listed in the index.

# scope/ maintenance (required)

`scope/` is the authoritative current map of game integration. Read [scope/FULL_SCOPE.md](scope/FULL_SCOPE.md) before changing game-facing behavior.

- Update the relevant area file in the SAME change as any added/removed/modified Harmony target, reflection lookup, game type/member, shader/render-pass/byte-layout dependency, game asset, StarMap hook or feature/lifecycle interface.
- Update [scope/game-integration-surface.md](scope/game-integration-surface.md) and the ToC/status in FULL_SCOPE when the integration inventory changes.
- Keep FULL_SCOPE concise; put technical detail in adjacent area pages. Dated archived findings are evidence, not current feature ownership or verification claims.
- Follow the game-update workflow in FULL_SCOPE when reference assemblies change. Compilation does not verify reflection, native ImGui, shader behavior or GPU ownership.

# Technology and game references

KSA means Kitten Space Agency. The repository currently targets **.NET 10 / C# 13**, as configured in `Directory.Build.props`. UI uses the game's `Brutal.ImGuiApi.ImGui` wrapper; Harmony patches runtime methods; StarMap supplies the host lifecycle attributes.

Authoritative game references are in the sibling `../ksa-game-assemblies/current/`: `dll`, `decomp` and `Content`. Check the current build baseline in FULL_SCOPE. The repository's `decomp/ksa` is stale historical reference. Read targeted files; never load the whole decompiled tree blindly. `KSA_DLL_DIR` can override assembly discovery; verify it points at the intended build.

Use the repository KSA, ImGui, ImGui-design and Harmony skills when applicable. Their older standalone/RPC scaffolding examples are historical; the workspace ownership and distribution requirements above govern current bundled features.

# UI and code conventions

- Use `Console.WriteLine` for logging.
- Prefer readable, maintainable code and explicit data ownership over cleverness. Aim for files around 300 lines when splitting improves readability.
- Use full-width inputs, consistent gaps and padded table cells, and responsive grids. Prefer existing `SubmodUI`, `FormField`, `FormGrid` and feature layout helpers. Pair ImGui Begin/End and style pushes/pops correctly, including collapsed windows.
- Keep business logic and feature-specific UI in the owning feature library. The shipping host handles shared workspace UX and lifecycle wiring.

# HotkeyGuard (required for every real mod entry)

Every top-level mod entry must apply `HotkeyGuard` from `MeowSci.KsaAbstractions` in `Patcher.cs`: add its using, call `HotkeyGuard.Patch(_harmony)` after creating Harmony and call `HotkeyGuard.Unpatch(_harmony)` during unload before clearing the instance. The project must reference `ksa-abstractions.lib` directly or transitively.

Bundled features share the guard installed once by `unscience/Patcher.cs`; they do not create their own hosts/guards. `fixme-mod-name/Patcher.cs` remains a nonshipping standalone example.

# Verification

Compilation must pass before completing a task. Build the solution and run checks relevant to the change:

```sh
dotnet build ksa-mod-experiments.slnx --disable-build-servers -m:1 -p:UNSCIENCE_DIST_DIR=/tmp/unscience-dist
dotnet run --project unscience-contracts.tests --no-build
python3 scripts/check-workspace-boundaries.py
python3 scripts/check-docs.py
git diff --check
```

The isolated distribution path avoids installing a verification build into a live game. Contract tests verify managed storage/restore behavior; they do not execute feature game APIs. Report native UI/gameplay/GPU checks separately and do not claim they passed without running them. The in-game acceptance checklist is in docs/WORKSPACE.md.
