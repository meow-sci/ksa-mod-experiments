I want to investigate how to implement a mod in KSA that allows for "painting" parts.

I do not know if this is possible in the games current state.

Use the ksa skill for relevant info about how this repo is laid out.

The `decomp/ksa` folder holds decompiled sources for KSA that can be analyzed.

Some of the mods here have examples of how to debug vehicle part trees and doing some things with them (postioning, scaling, etc), but currently no mods do anything with the rendering of the parts.

The exception is blinky.lib which has a feature to disable rendering of engine part meshes (to save on GPU rendering for the purposes of that mod), by harmony patching `PartModelModule.UpdateRenderData` to not run in those conditions.  Unsure if this could be helpful or not.

To "paint" the parts, this would mean somehow at runtie apply some kind of coloring to the parts in some way.   I have no idea how that would work, if it could affect a shader input, or if there is something in the C# codebase to affect it, I have no idea.

Do a deep dive analysis of the KSA source code under `decomp/ksa` to see if you can find some way to "paint" parts in some reasonable way to influence a color applied to part rendering.

Also note that `decomp/ksa/Content/Core` contains KSA game data files (a lot of part data is defined as XML data and read into the game at startup) and textures and shader sources under `decomp/ksa/Content/Core/Shaders`

Ask me for any clarifications, and if you want to implement any test code to validate ideas or theories or test things