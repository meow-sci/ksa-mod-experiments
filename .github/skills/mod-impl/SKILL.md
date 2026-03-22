---
name: mod-impl
description: details about how to implement a mod
---


- MUST use fixme-mod-name and fixme-mod-name.lib folders to start with for each mod + mod.lib csproj (unless the folders were pre-created before this agent run)
- when using the fixme-mod-name and fixme-mod-name.lib templates, replace "fixme-mod-name" with the hyphenated name of the mod and replace "FixmeModName" with the PascalCase name of the mod in the csproj files and any code files, the hypenated name also must be fixed in the csproj filename
- MUST update the ksa-mod-experiments.slnx file to reference the new mod and mod.lib csproj files
- MUST follow the task instructions precisely for implemting the mod
- MAY use exploratory learning from the `decomp/ksa` folder which contains decompiled ksa dll assemblies.  some of these files are large so be careful not to fill up context, use #runSubagent agents to do exploration as needed
- MUST ask me for any clarifications needed about the task instructions or any other aspect of the mod implementation
- when making a decision about how to implement something, consider that the end-user of the mod is a human interacting with a game.  make decisions that are easy to work with, easy to control, and can provide a good and fun user experience
- when doing ImGui UI work, use the /ksa and /imgui skills for details about how to use ImGui with KSA for UI components
- MUST implement mods with core functionality contained in the .lib csproj and primarily UI code in the main mod csproj.  this is so that other mods will be able to reuse the functionality in aggregate mods and RPC mods etc.
- MUST design mod code with good hygiene and C# practices in mind, such as good naming, modularity, and maintainability
- MUST make smart decisions about utilizing the ksa-abstractions.lib library and potentially add new abstractions to it.  this library is meant to act as a buffer to insulate and protect mod code from game dll/assemblies in a single place where they can be fixed.  do not overengineer there, is a mod needs a hyper local ksa dll assembly interaction, just code it in the mod/mod.lib; but if there is a generic pattern that already exists in ksa-abstractions.lib or could be added there that would be useful for multiple mods, then use/add it there
