this is an idea for a ksa game mod.

the game uses an ImGui ui framework, use the ksa and imgui skills for information.

the `decomp/ksa` folder holds the decompiled ksa sources, but the game code is probably not useful, however the following spots under the decomp folder contain decompiled info speific to Brutals csharp ImGui just in case its useful

- Brutal.ImGuiApi
- Brutal.ImGuiApi.Abstractions
- Brutal.ImGuiApi.Extensions
- Brutal.ImGuiApi.InlineArrays
- Brutal.ImGuiApi.Internal
- Brutal.ImGuiApi.Internal.InlineArrays

What i want skittles to do is a global theming of ImGui, make some change that will affect all ImGui default theme values across the whole program instance.

I'm not sure how to accomplish this.

Do a deep dive and ask me for any clarifications