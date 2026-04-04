the existing "blinken" mod loads a vehicle with engine parts with name/ids in a specific format to act as x/y coordinate reference for that engines location in a LCD grid in ksa

in the end, this mod is just controlling the engines on/off in a vehicle that is already pre-defined outside the context of this mod.

i want blinky to have the same runtime control features as blinken does for a grid of engines, however, I want blinky to dynamically build an LCD grid of engines at runtime that does not pre-exist in a vehicle

to do this the mod will need to attach engine parts to existing vehicles.

the KSA game has a vehicle editor built-in now but I'm not sure how that works, but it might be useful to look at that code for how to add parts to an existing vehicle.

the mod should let me select a vehicle to attach the lcd grid to, the part/subpart for the engine to use for the lcd grid, take in parameters like width/height of the lcd grid pixels to generate, and then the spacing (in meters) between them when placed in 3d space, as well as an initial offset to use from the source vehicle that the lcd grid is attached to.

do a deep dive analysis on blinken mod for how it currently behaves, and dive into the ksa decompiled sources under decomp/ksa intelligently to discover how vehicle building works and how to dynamically add parts to vehicles at runtime

a consider to also investigate when doing the analysis is that right now I am aware that adding engine parts (which are resource consumers) causes the ksa game code to recompute a resource graph for the entire vehicle, and with a large number of engines, this gets expensi1ve after a certain point (probably a exponential complexity), do an analysis of if it's possible to use a runtime harmony patch to disable the resource graph calculation during part addition and then re-enable it when adding the last engine so its only computed one time.  i dont know if this is possible or would break anything, so do an analysis of that to see.

collate all the information you gather and make a sensible plan for implementing this  to make a plan to implement this mod.  The plan shoud be a comprehensive task list of things to implement with sufficient context for a future ai coding agent to implement it in isolation, so include code references, example snippets and detailed instructions on what to implement for each task.

Ask me for any questions or clarifications needed before writing the plan
