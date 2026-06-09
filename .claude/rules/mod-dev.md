---
applyTo: '**'
name: Mod Development Instructions
description: Instructions for developing KSA game mods.
---

- MUST compile solution with `dotnet build`
- MUST pass compilation before a task is complete
- MUST prefer good code hygiene and readability over cleverness
- MUST write code that is maintainable
- MUST attempt to keep files relatively small (target 300 lines max, but this is a soft limit and can be exceeded if it makes sense for the code).  prefer splitting code into multiple files if it helps keep file sizes down AND if it improves readability and maintainability