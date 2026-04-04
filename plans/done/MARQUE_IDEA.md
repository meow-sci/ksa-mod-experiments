use ksa and imgui skills to implement a new feature in the marque mod

`MarqueLib.DrawMarqueMenus()` static function is already setup to be run at the right to to inject items into a imgui menu (the code will be run between `ImGui.BeginMenu("View")` and `ImGui.EndMenu();` so it will be responsible for contributing `ImGui.MenuItem("..")` and whatever else we want suitable for this kind of location inside an imgui menu system

I want to have the following features:

- Marque (imgui MenuItem)
  - Vehicles (imgui MenuItem)
    - All
    - None
    - Separator
    - [dynamic list alphabetically sorted of Vehicles]
  - Celestials
    - All
    - None
    - Separator
    - Earth
      - All
      - None
      - Separator
      - [dynamic list alphabetically sorted celestials under Earth SOI e.g. child celestials]
    - [other celestials under Sol SOI]
      - All
      - None
      - Separator
      - [dynamic list alphabetically sorted celestials under Earth SOI e.g. child celestials]

- The dynamic lists of Vehicles and Celestials should show a checkmark if their orbit lines are enabled

- The All button at a given level should enable orbit lines for everything in that section

- The None button at a given level should disable orbig lines for everything in that section

- The "Earth" node is meant to represent a dynamic list of every Celestial that is a child of Sol

- Each dynamic Celestial should have an all/none/separator then list of children, and if a given child has it's own child celestials, repeat with more sub menus

- Importantly these menus SHOULD NOT close when clicked, so the end-user can click and toggle many lines on/off quickly without having to revisit the menus all over again. this can be done with `ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);` at the start of our code and `ImGui.PopItemFlag()` to disable it after our code


- use `Program.SetHiddenOrbitLines(IOrbiter)` and `Program.SetVisibleOrbitLines(IOrbiter)` to toggl elines on/off.  these are private static so use reflection to get a reference


```
- Marque
  - Vehicles
    - All
    - None
    - Separator
    - [dynamic list alphabetically sorted of Vehicles]
  - Sol
    - All
    - Children
    - None
    - Separator
    - Earth
      - All
      - Children
      - None
      - Separator
      - Earth
      - Separator
      - [dynamic list alphabetically sorted celestials under Earth SOI e.g. child celestials]
    - [other celestials under Sol SOI]
      - All
      - Children
      - None
      - Separator
      - [Self]
      - Separator
      - [dynamic list alphabetically sorted celestials under Earth SOI e.g. child celestials]
  - Everything
    - Combobox (filterable)
    - [alphabetically sorted list of all IOrbiter]
```

- I added "Children" options after each "All" which should enable orbit lines for everything at the current menu items immediately list of children IOrbiter instances

- I added Everything menu item, which should have a combobox w/ filter and the list after should be every IOrbiter, the filter should dynamically filter and clicking on any of the dynamic items should toggle its orbit line setting

- I added a [Self] entry in each submenu area to hold the checkmark data for that given item since as a submenu parent it cant show the checkmark (it should also act as a toggle-able button for that item)
