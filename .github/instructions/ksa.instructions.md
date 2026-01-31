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
