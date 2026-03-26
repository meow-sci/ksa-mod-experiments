# MAJOR

using "mod-a"/"mod-a.lib" as an example mod name for illustrative purposes which represents all mods and their libs

- the IGrantSubmod interface should've been put into ksa-abstractions.lib as a generic interface pattern (not tied to "grant" at all), and the submods should've been defined in each mod-a.lib csproj and reused from both mod-a's ImGui code and grant supermod ImGui code so that we're not largely duplicating the ImGui code per mod ui behavior
- the the harmony patch behavior should be defined in mod-a.lib and reused and called from mod-a and grant supermod

the goal here is that each mod-a and mod-a.lib pair largely contain all the logic for that mod, and mod-a + mod-a.lib can still be used standalone where mod-a provides a ImGui window that includes the mod-a.lib ui code

but grant supermod contains *all* of the submod functionality together in a single ImGui window collected under collapsible headers that can be toggled on/off for visibility

# MINOR
