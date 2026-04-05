Ok.  It's time to unify things inside the "grant" mod

use the ksa, imgui skills as needed for reference

what I want to do is combine the following mods functionality all into grant:

- average-twr
- blinky
- eternal-flame
- garrys-torch
- glass
- i-feel-seen
- kiwis-marbles
- ksa-abstractions.lib
- skittles
- unladen-swallow
- zippo

we should be pulling the functionality from the ".lib"

i also want to do a smart sharing of ImGui code, so each project will need a refactor to facilitate this

i want grant to have a top-level ImGui window with a context button (top right corner) which shows a popup menu where each item is one of our supported submods and has a checkmark shown or hidden if they are displayed in the main imgui window

each submods functionality should be under a collapsible header with that submods name

each submods content that it currently has should be the same or as close as possible to the same as current behavior.  but should be refactored so that its lifted to the mod.lib folder and reusable, and the remaining mod folder for that mod ls mostly just an imgui window that reuses that same functionality

additional popup windows (like skittles theme manager window) can continue to be their own additional windows as-is like now

do a deep analysis of these projects, make a refactor plan of tasks and a plan for the grant mod ImGui to encapsulate all the submod behaviors and place it in plans/GRANT_MOD_PLAN.md

each task in the plan should be sufficiently detailed that its implementation can be delegated to a future ai coding agent in isolation and be able to effectively implement it with no ambiguity

ask me if you have any questions for clarifications

