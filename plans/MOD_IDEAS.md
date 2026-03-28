# con-man

game ui layout manager.  can save the current game ui state to disk under a inputtable name.

can select saved layouts from a combobox and apply them.

allow selecting a "startup default" value, when the mod initially starts, apply it if set. should also be un-settable.

all comboboxes should be filtered and values sorted case-insensitive alphanumerically (cache this so its not re-computed on every game tick render)

# soundboard (name TBD)

- honk


# bugs

- in blinky, destroying a grid should first ensure all engines are shutdown.  bug in KSA that engine sound keeps playing.


 
# action hero

this mod will reference many other mod libraries to reuse their functionality

this mod will be an orchestrator/runner of other mod functionality

this is only scoped to be relatively small scale interactions and workflows

some ideas:

- will need a way to encapsulate invocation of some mod functionality into a standalone, not-shared data structure.  this will be called a "Trick".  some kind of object instance with metadata about its name, purpose, and lambda(s) or some kind of Runnable instance that when run will be responsible for executing arbitrary code (which will be invoking other mod lib code)
- will want to be able to create Tricks with a custom UI per Trick, not something generic overall.  These should be streamlined UIs for each Trick we support tailored to the functionality encapsulated in the Trick.
- the Trick creation UI's should be their own ephemeral ImGui Windows that get created.  An example flow would be the main ActionHero Window would have a Combobox with all known trick types, user selects one, then presses a "Create" button which launches the trick-specific Window to configure the given trick. 
- When a trick is created, it should be added to an in-memory library of tricks. Tricks must have a unique name, when saving a trick if there is name collision display an error message next to the save button about the name collision and maintain all the UI state so the end user can simply fix the name and hit save again
- Tricks should be assignable to hotkeys.  We need a Hotkey Assignments feature that pops up a Window to manage hotkey assignments.  When a hotkey assignment is active the mod needs to have code in its game loop code to detect keypresses using the imgui skill for reference how to do that


