this repository is a set of mods for a game ksa.  i want to do a repository refactor.

right now, this is a multi project dotnet solution.  that will be the same.

however, right now, most of these csharp projects contain their mod code logic AND produce their mod
as an output, these are:

- average-twr
- blinken
- byo-music
- camera-controller-override
- garys-torch
- geeforce
- ifeel-seen
- kitten-animations
- zippo

these other folders are reference data or debugging etc and not part of the code and MUST be ignored in the refactor:

- decomp
- docs
- fixme-mod-name
- logs
- plans
- stampy

i want you to NOT make any changes right now but do a comprehensive analysis of the repository and make a detailed
plan of a sequence of changes to make to accomplish the refactoring goals.

guidelines:

- use example-lib-project as an example library style project.  its output type is library.
- during refactors, everything should get a new set of namespaces that are consistent, always with the root `MeowSci`, for example example-lib-project is `MeowSci.ExampleLib`
- use `zippo` projects `CopyCustomContent` section as an example of how all `MeowSci.*` assemblies can be dynamically referenced by convention with naming patterns for the copy to target output operations to get a fully working mod dist dir referencing internal projects as libraries

the refactoring goals are:

- update the "ksa-abstractions.lib" csharp library project, i already created it as an empty shell, the goals of it are
  - to create an abstraction layer which insulates our custom mod code from KSA game code.  KSA is in alpha state and its codebase/dll assemblies will change over time.  there are likely common things the mods do like get the current vehicle which are all directly referenceing KSA assembnlies, so if that code changes, it breaks in N places.  this abstractions project should contain OBVIOUSLY COMMON abstractions into it, so there is a single place to fix those abstractions KSA assembly references if the game changes the behavior.  DO NOT over-engineer KSA assembly references that are hyper specific to a particular mod behavior, that's fine, this abstractions should be logically common types of operations.
- refactor every mod project to
  - extract all its logic into a new companion csharp project called [modname].lib
  - refactor the [modname] to depend on [modname].lib and refactor [modnam]'s mod code to have the same functionality as before, but referenceing it from the lib
  - the goal of this is that the mods functionality must be reusable from the library for other purposes in addition to being used by the mod itself
  - refactor relevant code to use "ksa-abstractions.lib" as makes sense
  - refactor the mod/library project assembly name and code to have a mod/library project specific name in the pattern of `MeowSci.ProjectName`, for example `MeowSci.Zippo` for the zippo mod, and `MeowSci.ZippoLib` for the (to be created) zippo.lib project
  - DO NOT break the functionality of the mod, use careful analysis and planning to ensure the refactoring does not break anything
  - If possible, when refactoring the mod logic into its library, the new refactored variant of the logic should be follow a best practice of using stateless static functions and have stateful objects passed to them.  This is not a hard requirement, but a preferred pattern.  Another goal is that I want the mod library functionality to be reusable in a later feature that will be a sort of dynamic action invoker.  This would for example let me register an action that would be bound to a hotkey, this hotkey would want to run as an example "run the zippo turn on light feature for vehicle 'rocket' on part name 'light123' with rgb color '(1,1,1)' with emissive intensity `11`", this mod providing the action caller capabilities would register this as a lambda (and the mod which has the action functionality will be compiled/linked against all the other mods, so it can call the mod functions and use its data structures).  just take this into account when doing the mod.lib refactoring that i want the mod functionality to be reusable in such a manner that it can be called by an anonymous lambda (the lambda can be stateful and contain stateful object references to pass to the mod functionality if needed)
- update "grant" top-level mod csproj with references all the new mod.lib projects to be an all-in-one supermod with access to all the mod features.  just update the csproj for compile time linkage, dont implement any mod code.