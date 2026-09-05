# repository maintenance

- `REPOSITORY_INDEX.md` - MUST be maintained with high level information about every csharp project and what it does to act as a discovery guide for ai coding agents
- a README.md file MUST be maintained in each csharp project folder with more detailed information about the mod, its features, and how to use it. Always refer to these README.md files for in-depth understanding of each mod's capabilities and implementation details.
- MUST when creating or modifying mods and mod.lib libraries, ensure that `REPOSITORY_INDEX.md` is updated and the repositories README.md is updated accordingly

# game integration scope (scope/)

`scope/` is the authoritative map of how every unscience feature integrates with the KSA game (Harmony patches, reflection, game types, shaders, assets), used to detect game-update breakage. See `AGENTS.md` → "scope/ maintenance" for the full rules.

- MUST update the relevant `scope/` file in the SAME change whenever you add/remove/modify any game integration point (Harmony patch, reflection lookup, game type/member reference, render-pass/shader/byte-offset dependency, game asset, or StarMap/ISubmod surface)
- MUST add new mods/features to the correct `scope/` area file, the master index `scope/game-integration-surface.md`, and the ToC + status summary in `scope/FULL_SCOPE.md`
- MUST start from `scope/FULL_SCOPE.md` before changing anything that touches the game, and follow its game-update workflow when a new KSA build lands
- MUST keep `scope/FULL_SCOPE.md` concise (entrypoint/ToC + high-level status); push depth into the adjacent area files

# existing funcionality discovery

Use `REPOSITORY_INDEX.md` as an initial place to discover existing mods and their functionality

Each csharp project folder contains a `README.md` with more detailed information about the mod, its features, and how to use it. Always refer to these README files for in-depth understanding of each mod's capabilities and implementation details.

# glossary

- `KSA` - Kitten Space Agency (a game)
- `mod` - modification (a user-created add-on for a game)

# technology

- KSA game mods are written in dotnet C# 10
- KSA game mods use ImGui for user interface
- KSA ImGui bindings are provided by a custom ImGui wrapper via Brutal.ImGuiApi.ImGui
- KSA game mods can optionally use HarmonyLib for runtime method patching
- KSA game mods use StarMap library to load into the game lifecycle with C# attributes

# code conventions

- use `Console.WriteLine` for logging

# hotkey guard (required for every mod)

Every top-level mod project MUST apply `HotkeyGuard` from `MeowSci.KsaAbstractions` in its `Patcher.cs`. This blocks game hotkeys while the player is typing in any ImGui text input.

- MUST add `using MeowSci.KsaAbstractions;` to `Patcher.cs`
- MUST call `HotkeyGuard.Patch(_harmony)` inside `Patch()` after the harmony instance is created
- MUST call `HotkeyGuard.Unpatch(_harmony)` inside `Unload()` before nulling the harmony instance (or inside the existing null-check block)
- The mod's `.csproj` must reference `ksa-abstractions.lib` either directly or transitively through its `.lib` project
- See `fixme-mod-name/Patcher.cs` for a canonical example of a minimal mod applying this pattern

# decompiled sources

KSA game decompiled sources for reference can be found in the `decomp/ksa` directory. These sources are decompiled from the game assemblies and may not be perfectly accurate, but they can be useful for understanding the game's internal workings and for mod development.

DO NOT attempt to load them all blindy, many are quite large.  Make strategic reads into the code base as needed to answer questions, or ask me to tell you which files are relevant for a particular task.

