# idea overview

the inanimate-carbon-rod ksa mod was created first which can render SubPart thumbnails, show them in an ImGui scrollable pane and has a ImGui popup window to render high-res version with a rotation animation.

the space-tape ksa mode was added afterwards which is a Part editor (create Part's from SubPart's by arranging them visually and then serializing the data to game XML files)

space-tape currently leverages the SubPart thumbnails created by inanimate-carbon-rod, but I want to eliminate inanimate-carbon-rod and combine all it's functionality into space-tape.

additionally, I want to refactor some of space tape functionality

research the current mods and make a detailed implementation plan into plans/SPACE_TAPE_COMBINE_PLAN.md

the things I want to do are:

- move the inanimate-carbon-rod functionality into space-tape (bring over the generate)
- change the Space Tape Unscience Submod UI to just be:
  - "Load SubParts" button (see next for functionality)
  - "Open Part Editor" (change to "Close Part Editor" while it's open)
- change the space-tape Load SubParts button to open a modal which contains the inanimate-carbon-rod Generator collapsible header functionality: Images Per SubPart, image size
  - put in a "Generate" button in the modal, when pressed, generate thumbs (disable the generate and close buttons on the modal)
  - change "Generate" button text to "Re-generate" if thumbs have already been generated
  - put in a "Close" butto non the modal which just dismisses it
- move the existing SubPart display in the space-tape Submod UI into a new separate window titled "SubParts" that is tied to the Part editor being active
  - include thum size, anim delay, filter and the visual display functionality
  - add a "view subparts" checkbox after the thumb size/anim/filter (false by default), and when true, when a subpart thumb is clicked, show the large image viewer window instead of adding the subpart to the part editor
- consolidate the Load/Import space-tape functionality
  - automatically scan built-in game parts AND custom saved parts whenever the editor is opened
  - change this to a simpler display which is a two filterable comboboxes in a 2x2 table using our preferred imgui-design style layout
    - row 1 is "Custom Parts" [combo]
    - row 2 is "Stock Parts" [combo]
  - fix the comboboxes, currently the filters are outside the combos, we have an imgui skill and lots of examples of doing proper filterable comboboxes
  - put the import button on a line after the 2x2 table of labels and combos
- At the top of the editor window before "Undo" put a "Save" button, when pressed open a modal popup/window
  - move the file combo box to the save window
  - when the modal/window is opened, refresh the file list
  - fix the file combobox to be filterable
  - only show the File label and text input if (new file) is selected
  - add a Save and Cancel button to the modal, save actually saves the file to disk
- Remove the existing "Save" area once all the save functionality is moved to the modal
