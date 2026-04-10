use a fleet of 6

the space-tape mod currently has an editor that is very similar to the vehicle editor.

the camera controls also behave the same, defaulting to an orbit mode.

i'm not sure exactly what it's orbiting though, possibly an arbitrarily fixed point?

i would like to add some helper functionality for camera controls that snap the camera to particular vantage points:

- front (no rotation, aligned directly facing the origin with the origin centered in the screen)
- left (same idea, but looking straight on from 90deg to left)
- right (same idea, but looking straight on from 90deg to right)
- back (same idea, but looking straight on from 180deg)
- top (same idea, but looking down from top)
- bottom (same idea, but looking down from bottom)

ideally pressing one of the buttons for these mods just snaps the camera to that but leaves it in orbit mode so if the end-user moves the camera it moves freely again.

secondarily, it would be really nice to have a visual aide like graph paper when in one of the snapped modes.

i have no idea how this might be possible in ksa with what we have available without implementing a custom shader.  it may not be possible.  but investigate if it is possible to have some kind of visual indicator that would be kind of like graph paper or some kind of translucent plane rendered that represents the flat plane that the snapped camera direction is facing. 

if we can do this, i would like the translucent/plane to become visible when one of the snap modes is clicked.  it can remain visible even if the end-user pivots the camera, and stay visible until the end-user toggles off the snap view modes.

use the ksa, imgui, imgui-design, harmony skills as needed

when done place a detailed implementation plan with sufficient task information with unambiguous fine detail for future coding agents to have precise information and references to decompiled sources or other mod code so that future coding agents can take the task list and competently and accurately implement the feature as intended, you MUST include sufficient detail to make this possible

place the detailed plan into a file named `plans/SPACE_TAPE_CAMERA_CONTROLS_PLAN.md`