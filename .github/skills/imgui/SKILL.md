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

## Full ImGui API Reference

The entire ImGui API should be exposed via this Brutal C# wrapper, so use your knowledge of the official ImGui

## Examples

These are some examples using Brutal ImGui API calls to demonstrate common ImGui features.

### Colored text

```csharp
float4 currentColor = GetGForceColor(recorder.Latest.Magnitude);
ImGui.TextColored(currentColor, $"Current: {recorder.Latest.Magnitude:F2} g");
ImGui.SameLine(0, 20);
ImGui.TextColored(ColorRed, $"Peak: {recorder.PeakG:F2} g");
ImGui.SameLine(0, 20);
ImGui.Text($"Avg: {recorder.AvgG:F2} g");

```

### Horizontal line separator

```csharp
ImGui.Separator();
```

### Indentation

```csharp
ImGui.Indent();
ImGui.Text("abc");
ImGui.Unindent();
```

### Collapsing Header

```csharp
if (ImGui.CollapsingHeader("thing", ImGuiTreeNodeFlags.DefaultOpen))
{
  ImGui.Text("content");
}
```

### Detect Keypresses

```csharp
if (ImGui.IsKeyPressed(ImGuiKey.F11))
{
  _windowVisible = !_windowVisible;
}

```

### Float value slider

```csharp
// Speed slider arguments are: (label, ref to value, min, max)
if (ImGui.SliderFloat("Speed (m/s)", ref _actualValue, 1.0f, 250.0f))
{
  // Value updated
}
```

### Combobox

```csharp
string[] easingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
// Combo box arguments are: (label, ref to selected index, array of options, number of options)
if (ImGui.Combo("Easing##ZoomOut", ref _selectedValue, easingNames, easingNames.Length))
{
  // Value updated
}
```

### Add a spacing gap

```csharp
ImGui.Spacing();
```

### Progress bar

```csharp
progress = Math.Clamp(progress, 0.0f, 1.0f);
ImGui.ProgressBar(progress, new float2(-1, 0));
```
