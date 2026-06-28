# AGENTS.md — operating rules for AI coding agents in this repo

This repo is the **unscience** mod suite for Kitten Space Agency (KSA): a set of C# 10 mods + `.lib`
libraries, unified by the `unscience` supermod. Read this before making changes.

General repo conventions live in **[`CLAUDE.md`](CLAUDE.md)** and the discovery index in
**[`REPOSITORY_INDEX.md`](REPOSITORY_INDEX.md)** — this file does not repeat them; it adds the rules
specific to working safely against the game and to keeping the integration scope docs current.

## Discovery (read before writing code)

- Start from [`REPOSITORY_INDEX.md`](REPOSITORY_INDEX.md) for what each project does.
- Read the target project's `README.md` for feature/implementation detail.
- For **anything touching the game** (Harmony, reflection, game types, shaders, assets), read
  **[`scope/FULL_SCOPE.md`](scope/FULL_SCOPE.md)** and the relevant area file under `scope/` first. It
  maps every feature to the exact game members it depends on, with decompiled-source paths.

## scope/ maintenance — MANDATORY

`scope/` is the authoritative record of how unscience integrates with KSA and the basis for assessing
game-update breakage. It must never drift from the code.

- **MUST** update the relevant `scope/` file in the **same change** whenever you add, remove, or modify
  any *game integration point* — i.e. any of:
  - a Harmony patch (new/removed/retargeted, or a changed patch kind or overload param array);
  - a reflection lookup (`AccessTools.*`, `System.Reflection`, `Traverse`) — especially **string-named**
    private members;
  - a direct reference to a game type/method/field/property/enum (KSA.* or game-shipped Brutal.*);
  - a render-pass hook, runtime-recompiled shader, or per-instance/byte-offset struct dependency;
  - a game asset reference (template/part/shader/character/sound by id or path);
  - a StarMap lifecycle attribute or `ISubmod` surface change.
- **MUST** keep each touchpoint row accurate: mod-code `file:line`, the game `Type.Member(signature)`,
  the decomp path, and the "in current build / Δ" status.
- **MUST**, when adding a **new mod/feature** to the suite, add its integration points to the correct
  area file (or create a new area file) **and** add it to the master index
  [`scope/game-integration-surface.md`](scope/game-integration-surface.md), the contents table and
  status summary in [`scope/FULL_SCOPE.md`](scope/FULL_SCOPE.md), and — if it touches the game —
  the string-reflection watchlist and/or shaders-&-assets tables in the master index.
- **MUST** keep [`scope/FULL_SCOPE.md`](scope/FULL_SCOPE.md) small: it is the entrypoint/ToC + high-level
  status only. Push depth into the area files; if a topic grows, give it its own `scope/` file and link it.
- **MUST**, when a fix resolves a gap, update the touchpoint's status in `scope/` (and the
  corresponding `plans/` doc) so the record reflects reality.
- **MUST NOT** let `scope/`, `REPOSITORY_INDEX.md`, or a project `README.md` describe behavior the code
  no longer has. If you find such drift while working nearby, correct it.

## Working against a game update

When the game has been updated (a new `ksa-game-assemblies` version is provided):

1. **MUST** `dotnet build` against the live install first — typed breaks surface immediately.
2. **MUST** diff every string/reflection touchpoint in `scope/game-integration-surface.md` against the
   new decomp (compile-clean does **not** mean safe — string lookups fail silently at runtime).
3. **MUST** scan the new build's `version.json` changelog for behavioral changes and match them to the
   "Update-risk findings" sections in the area files.
4. **MUST** re-check runtime-recompiled shaders and byte-offset struct hacks against the new shader
   sources under the game's `Content` tree.
5. **MUST** update the `scope/` version baseline + touchpoint statuses, and record remediation in a
   `plans/` document (e.g. `plans/FIX_CURRENT_GAPS_PLAN.md`).
6. Decompiled game source for reference is in `decomp/` (see [`decomp/AGENTS.md`](decomp/AGENTS.md)) and
   in the provided `ksa-game-assemblies*/current/decomp` trees — **the provided trees are
   authoritative** for version diffing (the in-repo `decomp/ksa` copy may be older). Do not load them
   wholesale; grep strategically.

## Core build & code rules (see CLAUDE.md for the full list)

- **MUST** compile the solution with `dotnet build`; a task is not complete until it builds.
- **MUST** apply `HotkeyGuard` from `MeowSci.KsaAbstractions` in every top-level mod's `Patcher.cs`
  (see `fixme-mod-name/Patcher.cs`).
- **MUST** keep `REPOSITORY_INDEX.md` and each project `README.md` updated when creating/modifying mods.
- Prefer readable, maintainable code over cleverness; target ~300 lines/file (soft limit).
- Use `Console.WriteLine` for logging.
