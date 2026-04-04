# overview

unladen-swallow mod name comes from monty python and the holy grail, its a joke about birds carrying coconuts.

the mod will provide an RPC mechanism over HTTP AND wire it directly to functionality from other mods.

i already created the `unladen-swallow` and `unladen-swallow.lib` csproj projects and added them to the dotnet solution

# how to implement

I already built a separate standalone mod called kitten-remote-operations-control (or KROC), which is available in this workspace as reference, that code and setup can be used in the new unladen-swallow mod as needed.

- `unladen-swallow` csproj will be a VERY simple ImGui mod window which has an enable/disable checkbox which turns the http server on/off, and thats it
- `unladen-swallow.lib` csproj will
    - contain the HTTP server and API endpoints which provide functionality from other mods
    - link in other mod.lib projects to make their functionality available
- if other mod functionality requested isn't nicely abstracted into its mod.lib, refactoring those mod/mod.lib projects should be planned as part of the tasks
- the HTTP server MUST use `GenHTTP` library (see the `server` project in kroc)
- the HTTP endpoints MUST set `.Serializers(Serialization.Default())` for serializers (see for example AcctionRefill.cs).  If we don't, it may end up trying to use some serializers from ASP.NET which is NOT AVAILABILE in our KSA runtime setup and cannot be added.
- bring over just the ignite and shutdown actions from kroc for now, build these into unladen-swallow.lib

# the plan

make a detailed implementation plan which is a set of tasks to implement for future AI coding agents

each task must have extensive details of what must be done and implemented for that task with enough context that an ai agent coding it in isolation will have sufficient and unambiguous instructions and context to properly implement the tasks

