# kiwis-marbles

- add a vertical spacer above "Create Weld" and "Active Welds" sections
- re-do the comboboxes and their labels to use imgui horizontal layout patterns so they are properly dynamic with full width occupied, where the comboboxes stretch and the labels take up their natural size
- change the Target/Source/Center dist (surface) label and values to be aligned in a table (no headers) layout of two columns and three rows (first col is label second is value)
- change the three place buttons to each be on a new line (they have long labels)
- under each weld collapsible header
    - re-arrange the label/data for surface dist, target r (is that radius?), source r (is that radius?) into a 2 col 3 row table
    - when re-enabling surface mode, if the current position of the weld source is not at 0 surface distance, it is snapped back to 0 surface distance.  i don't want that, i want there to be no data changes when turning surface mod on/off, whenever one mode changes values, the values tracked by the other mode should be reflected.  it mostly works for the position data already, its just the surface distance thats a problem

# zippo

- add a vertical spacer before the "Zippo Light Control" line and before the end of the mod ui for nicer look-n-feel
- make the combobox / label use imgui horizontal layout tools instead of absolute offsets, the label should be natural size and combobox flex grow

# unladen-swallow

- add a vertical spacer before the "Unladen Swallow" line and before the end of the mod ui for nicer look-n-feel
- add an input box for the bind host and an input box for the port, if it's possible to have data masking for ipv4 for the host and int only for the port do that.  arrange these in a 2x2 table.  use their values when the server is activated

# skittles

- add a vertical spacer before the "Skittles Global Theme Manager" line and before the end of the mod ui for nicer look-n-feel

# i-feel-seen

- add a vertical spacer before the "Vehicle Render Distance Override" line and before the end of the mod ui for nicer look-n-feel
- make the combobox / label use imgui horizontal layout tools instead of absolute offsets, the label should be natural size and combobox flex grow
- add a "active vehicles" section below the existing content and move the table of vehicles down below that

# glass

- add a vertical spacer before the "Glass" line and before the end of the mod ui for nicer look-n-feel

# garrys-torch

- add a vertical spacer before the "Create Weld" line and before the end of the mod ui for nicer look-n-feel
- make the comboboxes / labels use imgui horizontal layout tools instead of absolute offsets, the label should be natural size and combobox flex grow

# eternal-flame

- add a vertical spacer before the "Eternal Flame (refill every 500ms)" line and before the end of the mod ui for nicer look-n-feel
- default the refill rate to 50ms
- remove the "Active" table column header, the text is cutoff anyways. just leave it blank


# blinky

- add a vertical spacer before the "blinky (Dynamic LCD engine pixel grid)" line and before the end of the mod ui for nicer look-n-feel
- make the drag float slides / labels pairs use imgui horizontal layout tools instead of absolute offsets, the label should be natural size and combobox flex grow


# average-twr

- add a vertical spacer before the collapsible header content and before the end of the mod ui for nicer look-n-feel
