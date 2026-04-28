use a fleet of 6

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
- each robotic part will start off from an existing Part, we will define metadata about the robotic behavior we want, then we will save it as a new Part with a new ID (the resulting part should be identical to the existing Part when serialized to XML, just with a new part id and editor metadata tags)
- we will need specific behavior for each kind of robotic part we support
    - for hinge
        - enter a "hinge" creator mode
        - user must select three parts to be hinged:
            - plateA - SubPart that is one of the hinge (other parts will be attached to this with connectors, managed in the Part definition)
            - plateB - SubPart that is the other side of the hinge  (other parts will be attached to this with connectors, managed in the Part definition)
            - hinge - SubPart that is the pivot point
        - define the extents of the hinge range of movement as min/max in degrees
        - define what the resting position is in the part (e.g. the part might be at 0 deg or might be at 90deg or 180deg by default, for example)
- define other required metadata: new part ID, optionally new editor metadata tags
- once a flexo robotic part has been fully designed and all required data is present, allow the user to save it as a new part
    - saves a regular Part XML, GameData XML, etc (copies the existing Part data, with new ID and editor metadata tags, other things if needed)
    - saves a new `$HOME/Documents/My Games/Kitten Space Agency/.flexo/flexo_part_[part_id].toml` TOML file which encodes the flexo specific data for that part (the Part ID its associated with, the type of robotic part, the attributes for that part like the subpart IDs for the behaviors, degree settings etc etc)
- the robotic movement should, i think, be rotating subparts.  which i hope will make things attached with connectors to them
- we'll need a "flexo" unscience submod which allows opening the editor decsribed above, but also provides the runtime functionality to trigger flexo parts to activate and do things (move)
    - at startup, read all flexo part data from `$HOME/Documents/My Games/Kitten Space Agency/.flexo/flexo_part_*.toml`
    - this panel should have a "scan" button that when pressed will scan the active vehicle for any matching flexo parts, if found, generate dynamic collapsible header section for each.  there should be a unique implementation of this sub panel per robotic part type (e.g. a hinge will have different control requirements then a rotor)
    - the flexo part sub panels should have imgui widgets to play with the part, for hinge:
        - a drag float slider for fractional seconds
        - "open" - open from current position to max degree opening, pivitong on the pivot point
        - "close" - similar, but min degree setting from current
        - a slider to do it manually from min/max extent with nub at the current position

I am probably wrong about doing this at the SubPart level and probably have to lift the whole idea up one level, because connectors are at part-level, not SubPart level.

This means we would need the editor to load up a Vehicle, and we would pick parts as each robotic component, and the rotation at runtime would apply to the parts, which means things connected to those parts via connectors would naturally move.

Do a deep analysis on how this could work in the KSA decompiled sources at decomp/ksa and check out our existing space-tape part editor mod.

Make a detailed implementation plan and place it into plans/FLEXO_PLAN.md with highly detailed contextual data, references to space-tape and decompiled ksa sources as needed and fine enough unambiguous specific information for the tasks to implement it that a future coding agent that is handed the task will be able to competently and accurately implement the task and features with no ambiguity

Do a good job

Use the ripgrep and fd skills for good ways to search the ksa decompiled sources using `rg` and `fd` utilities, which are richer then find, head, tail, grep etc.  But you can still use those too if you're more familiar with them.
