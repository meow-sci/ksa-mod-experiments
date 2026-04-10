use a fleet of 6

we have a ksa game mod called "space-tape" which is a Part editor.

in ksa, the lowest-level primitive in the game is a SubPart which has meshes and textures for rendering.

a Part is a collection of SubPart's arranged in 3d space positioning, rotation and scale through XML defined data.

a Vehicle, similarly, is a collection of Part's that are either surface connected or attached at pre-defined connector points.

Vehicles represent (generally) rockets in the KSA space simulation game

additionally, there is GameData XML which defines physical attributes of SubParts and Parts (mass, metadata, etc)

space-tape is a mod that has been AI generated which mimicked the games built-in vehicle editor to fulfill the role
of a visual Part editor letting you take the existing corpus of low-level SubParts and visually layout and design Parts
then save those as XML for the game to use

the mod is working well, but there are some major shortcomings and gaps, which are:

- in our space-tape Part editor, mouse interactions with the subparts don't work directly.  e.g.
    - when I hover a SubPart there is no highlight shader applied
    - when i click a SubPart part it's not selected for editing (although clicking outside does deselect the current SubPart)
    - when using the gizmos (scale, translate, rotate), there's a very annoying oddity.  when click + dragged, it only works if the mouse stays above the rendered gizmo lines, if you move it outside the mouse movements stop registering.  this should not be the case, the gizmos work fine in the Vehicle editor without this issue.
- i want some quick-flip hotkeys which will rotate parts in 45 degree snaps in two axis (i forget which axis is which, but i i was looking at a part from "front" i want "d" to rotate it on the horizontal axis and "f" to rotate on the vertical axis)
- i want a click-and-drag movement feature, but it should be toggled on/off by a hot key, and when enabled, i only want it to move on one plane, not in 3d space.  pressing "p" should toggle between the "pan" modes, where we switch through x, y, z planes and the back to normal, when in x, y, or z pan mode, clicking + dragging a SubPart should let me move it around but be stuck to that plane

do deep research on our current space-tape mod implementation for any bugs and gaps and how it behaves now, and do very deep and thorough analysis on the games current vehicle editor to find out how it does certain things like the gizmos, part highlighting, click+drag parts, etc.

make a plan and place it in plans/SPACE_TAPE_FLEET_GAPS1_PLAN.md

use ksa, imgui, imgui-design, harmony skills as needed

ask me for any clarifications needed
