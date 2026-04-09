Use a fleet of 6 for this

In KSA, parts are made of SubParts.

SubParts are the lowest level "part" in the game which have meshes and textures etc, and "parts" are arrangements of SubParts, and vehicles are built from parts.

The game is designed with heavy GPU instancing of meshes for SubParts for performance, so things like panels and screws and pipes and hinges and switches are SubParts, and then parts reuse them judiciously with very little performance cost.

Right now parts are defined in XML in the game Core/ mod, see files like these as an example for XML which contains `<SubPart>` and `<Part>` nodes:

- decomp\ksa\Content\Core\CoreCommandAAssets.xml
- decomp\ksa\Content\Core\CoreFairingAAssets.xml

The game currently has a vehicle editor (where you build a vehicle from Parts), but there is no Part editor (where you would build Parts from SubParts)

I want to investigate how to add a Part editor which allows you to pick from the available SubParts and build a Part.

I don't want snapping like the vehicle editor has right now or advanced features like that, I just want the Part editor to support placing the SubParts in 3d space by position and rotation with some nice controls and imgui windows to make this easy to do.  Ideally this could reuse the existing rotation and position 3d gizmos in addition to having imgui windows with data inputs.

Do a deep give into the KSA decompiled sources under decomp/ksa and formulate a plan on how we could create a Part editor in-game.

The existing vehicle editor, I think (but am not sure), when going into the vehicle editor, simply moves the camera to some far away place in space away from any celestials and then lets you arrange the parts around (I might be wrong about this, though.  But we could do this potentially?)

Create a plan in file plans/SPACE_TAPE_PLAN.md

Ask me for any clarifications needed