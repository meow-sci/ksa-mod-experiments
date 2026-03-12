---
name: ImGui
description: ImGui is an immediate mode UI library.  KSA uses ImGui for UI
---

# Overview

KSA (Kitten Space Agency) uses the ImGui UI library for its user interface.

ImGui itself is a C/C++ library, but KSA uses a C# wrapper around it which is custom to the game exposed via an internal framework called Brutal.

The complete ImGui API is exposed and is accessible by a `using` declaration in csharp code:

```csharp
using Brutal.ImGuiApi;
```

This makes ImGui available using a `ImGui` class with static functions for ImGui API calls.  For example:

```csharp
ImGui.Begin("My Window");
if (ImGui.Button("Click me!")) {
  Console.WriteLine("clicked!");
}
```