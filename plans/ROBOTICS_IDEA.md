i have a mod idea for ksa, you can use the ksa, harmony, imgui, imgui-design skills as needed for references to what context this is about

the decomp/ksa folder holds the decompiled sources for the game which can be browsed and analyzed for behvavior

I want to create a "robotics" mod called "flexo", the over-arching goal of this is to take
the currently static SubPart/Part system and introduce what kerbal space program called "robotics", but for us, I want to start simple with a hinge.

I don't want to attempt to change how the game actually defines SubPart/Part data even if
that would be possible because i just want this to be additive temporarily (the game will eventually have this as a built-in feature), so I want to manage it external to the existing SubPart/Part system.

What this means is we'll need to define our own data which identifies which part(s) make up  a robotic part like a "hinge" or a "rotor", and then store some metadata the user can input using an imgui window about e.g. speed the "motor" runs at when it moves the part, maximum extends for the hinges in degrees, how far a rotor spins (freespin, limited rotation, etc)

We have an existing Part editor mod called space-tape (which lets you build Parts from SubParts)

I want to make a new Flexo editor, which should be much simpler overall in terms of total functionality, but do some similar things to our space-tape editor (having a editor scene setup, lighting help, SubPart hover/selection, etc).

But our flexo editor will do something like the following:

- load and render existing Part into the scene (space-tape can do this already)
- work on one Part at a time
- this is NOT a part editor, the user cannot MOVE SubParts etc in this editor
- let the end user manipulate the camera and lighting with front/back/top/bottom/left/right snaps, grid view, etc
- let the user select subparts by clicking on them (with hover indicators and highlights when selected, same as space-tape) or by clicking on them in a imgui window with a list of subparts for the current Part
- we will need specific behavior for each kind of robotic part we support
    - for hinge
        - enter a "hinge" creator mode
        - user must select two parts to be hinged