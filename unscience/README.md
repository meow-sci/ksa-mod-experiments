# Unscience host

The only shipping StarMap entry project. It references 25 independent feature libraries and shared infrastructure.
`Mod.cs` owns lifecycle and the feature catalog; `Patcher.cs` applies Harmony features independently and includes
HotkeyGuard and the hidden-HUD frame hook. `WorkspaceWindow.cs` owns authoring navigation and persistence;
`WorkspaceDialogs.cs` implements named workspace/feature saves and loading; `LiveStateWindow.cs` collects
feature-owned typed runtime records and dispatches their inspectors.

Feature visibility changes presentation only. Every feature continues receiving its required updates.
Workspace restore prepares every participant before applying any authoring change and never calls feature
Initialize, Update, Dispose or game-application methods. The host drains the shared game-thread queue even
though the RPC feature was retired. Parts Now retains its dedicated loader/purge phase.

Use **F11**, **Features**, **Save**, **Load**, and **Live State** as described in the [workspace guide](../docs/WORKSPACE.md).
Build from the root with `dotnet build ksa-mod-experiments.slnx`; see [root build instructions](../README.md).
