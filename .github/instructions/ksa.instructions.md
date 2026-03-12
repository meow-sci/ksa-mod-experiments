---
applyTo: '**'
---

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

# decompiled sources

KSA game decompiled sources for reference can be found in the `decomp/ksa` directory. These sources are decompiled from the game assemblies and may not be perfectly accurate, but they can be useful for understanding the game's internal workings and for mod development.

DO NOT attempt to load them all blindy, many are quite large.  Make strategic reads into the code base as needed to answer questions, or ask me to tell you which files are relevant for a particular task.

