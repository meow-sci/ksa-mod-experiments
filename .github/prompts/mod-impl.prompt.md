---
name: mod-impl
description: Describe when to use this prompt
---

use #runSubagent  subagents to implement this plan with a task per subagent, provide sufficient context for the agent to do the implemetation well and unambiguously

use the ksa, mod-impl, rpc, harmony, genhttp skills as needed

you MUST after each task is complete use the git-commit skill to create a git commit commit before moving onto the next task

find ksa decompiled sources under `decomp/ksa` as needed

find the ksa game data Core mod files under `decomp/ksa/Content/Core` as needed

ask me for any clarifications needed

do a good job
