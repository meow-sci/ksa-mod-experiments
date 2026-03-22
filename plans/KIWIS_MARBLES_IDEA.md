# Overview

The mod will be named "kiwis-marbles" and the "kiwis-marbles" and "kiwis-marbles.lib" csharp projects are already created and added to the solution

This mod should allow one to move around celestial bodies (planets, moons)

We have a mod already which moves around vehicles (garys-torch), I think celestial bodies might be able to be done similarly.

Once welded, just like garys-torch, on every game tick we would reposition it so we're effectively throwing away physics for that object (and thats intentional), so the target of the weld remains normal (if it itself is not welded).

The welds should be possible to have multiple (like garys-torch), and they should be ordered in a DAG when added to the list of all welds so that the first in a potential chain of welds is applied first, and the subsequent objects are then relative to that one.

Do an analysis of garys-torch, any relevant ksa decompiled sources you need, etc to make a plan to implement this mod.  The plan shoud be a comprehensive task list of things to implement with sufficient context for a future ai coding agent to implement it in isolation, so include code references, example snippets and detailed instructions on what to implement for each task.

Ask me for any questions or clarifications needed before writing the plan

