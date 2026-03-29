---
name: imgui
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

## ImGui demo app examples

For the imgui c++ demo app examples you can find the table of contents at [./examples-toc.md](./examples-toc.md) which links to the individual example files in [./examples/](./examples/).  These examples can be used as a reference for how to use ImGui to achieve various UI features and patterns.

IMPORTANT NOTE - these are the original C++ examples.  The examples must be adapted to the KSA Brutal C# ImGui wrapper which is 1:1 but using C# syntax and conventions instead of C++.



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

### Horitonzal line separator with text

```csharp
ImGui.SeparatorText("One-liner variants");
```

### Indentation

```csharp
ImGui.Indent();
ImGui.Text("abc");
ImGui.Unindent();
```

### Color widget - large - with RGB floats

```csharp
ImGui.Text("Color widget with Float Display:");
float4 color = new float4(1.0f, 0.5f, 0.2f, 1.0f);
ImGui.ColorEdit4("MyColor##2f", ref color, ImGuiColorEditFlags.Float);
```

### Color button only with Picker popup - with RGB floats

```csharp
ImGui.Text("Color button with Picker:");
// With the ImGuiColorEditFlags.NoInputs flag you can hide all the slider/text inputs
// With the ImGuiColorEditFlags.NoLabel flag you can pass a non-empty label which will only be used for the tooltip and picker popup
float4 color = new float4(0.2f, 0.8f, 0.4f, 1.0f);
ImGui.ColorEdit4("MyColor##3", ref color, ImGuiColorEditFlags.Float | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
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

### Float value drag slider

There are also 2/3/4 value slider widget variants (see later)

Prefer `ImGui.DragFloat` over `ImGui.SliderFloat` since it allows for both dragging and manual input

```csharp
// Speed slider arguments are: (label, ref to value, min, max)
if (ImGui.DragFloat("Speed (m/s)", ref _actualValue, 1.0f, 250.0f))
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

### Combobox with filter example

```csharp
// Note: _itemFilter should be a class-level field initialized as: private ImGuiTextFilter _itemFilter = new ImGuiTextFilter(); or some stateful field that has a lifetime scoped to the combo box usage or broader
string[] items = { "Item 1", "Item 2", "Item 3" };
int selectedItemIndex = 0;
string previewValue = items[selectedItemIndex];

if (ImGui.BeginCombo("Select Item##combo", previewValue))
{
    if (ImGui.IsWindowAppearing())
    {
        ImGui.SetKeyboardFocusHere();
        _itemFilter.Clear();
    }
    ImGui.SetNextItemShortcut(ImGuiMod_Ctrl | ImGuiKey_F);
    _itemFilter.Draw("##Filter", -1);

    for (int n = 0; n < items.Length; n++)
    {
        bool isSelected = selectedItemIndex == n;
        if (_itemFilter.PassFilter(items[n]))
        {
            if (ImGui.Selectable(items[n], isSelected))
            {
                selectedItemIndex = n;
            }
        }
    }
    ImGui.EndCombo();
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

### Multi-value slider inputs

These are multi-input sliders for float2/3/4 and int2/3/4.  The arguments are (label, ref to value array, min, max) for the drag and slider variants, and (label, ref to value array) for the input variant.  The drag variant allows both dragging and manual input, while the slider variant only allows dragging.

```csharp
static float vec4f[4] = { 0.10f, 0.20f, 0.30f, 0.44f };
static int vec4i[4] = { 1, 5, 100, 255 };

ImGui.SeparatorText("2-wide");
// manual input
ImGui.InputFloat2("input float2", vec4f);
// drag OR manual input
ImGui.DragFloat2("drag float2", vec4f, 0.01f, 0.0f, 1.0f);
// drag only
ImGui.SliderFloat2("slider float2", vec4f, 0.0f, 1.0f);
// manual input
ImGui.InputInt2("input int2", vec4i);
// drag OR manual input
ImGui.DragInt2("drag int2", vec4i, 1, 0, 255);
// drag only
ImGui.SliderInt2("slider int2", vec4i, 0, 255);


ImGui.SeparatorText("3-wide");
// manual input
ImGui.InputFloat3("input float3", vec4f);
// drag OR manual input
ImGui.DragFloat3("drag float3", vec4f, 0.01f, 0.0f, 1.0f);
// drag only
ImGui.SliderFloat3("slider float3", vec4f, 0.0f, 1.0f);
// manual input
ImGui.InputInt3("input int3", vec4i);
// drag OR manual input
ImGui.DragInt3("drag int3", vec4i, 1, 0, 255);
// drag only
ImGui.SliderInt3("slider int3", vec4i, 0, 255);

ImGui.SeparatorText("4-wide");
// manual input
ImGui.InputFloat4("input float4", vec4f);
// drag OR manual input
ImGui.DragFloat4("drag float4", vec4f, 0.01f, 0.0f, 1.0f);
// drag only
ImGui.SliderFloat4("slider float4", vec4f, 0.0f, 1.0f);
// manual input
ImGui.InputInt4("input int4", vec4i);
// drag OR manual input
ImGui.DragInt4("drag int4", vec4i, 1, 0, 255);
// drag only
ImGui.SliderInt4("slider int4", vec4i, 0, 255);
```

### Focus Trap — Blocking Game Hotkeys During Text Input

When a mod has `InputText` or other text-entry widgets, typing in them will also trigger game hotkeys (e.g. `\` toggles the in-game console, `Enter` submits console commands). To prevent this, use a **scoped focus trap** pattern.

**Critical rules:**

1. **Never use `ImGui.GetIO().WantTextInput` globally** — it is `true` whenever *any* ImGui text input has focus, including the game's own in-game console. Blocking hotkeys based on this flag will break the console (can't close it, can't press Enter).
2. **Check focus per-window** using `ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)` — this scopes the check to only your mod's windows.
3. **The focus check must be inside the `Begin`/`End` block** — after `ImGui.End()` there is no valid window context and `IsWindowFocused` won't return correct results.

**Pattern — render method returns focus state:**

```csharp
// In the Mod class, expose a static flag for the Harmony patch to read
internal static bool ModHasFocusedTextInput;

[StarMapAfterGui]
public void OnAfterUi(double dt)
{
    bool anyTextInput = false;

    if (_windowVisible)
        anyTextInput |= RenderMyWindow();

    ModHasFocusedTextInput = anyTextInput;
}

// Each render method checks focus INSIDE the Begin/End block and returns it
private bool RenderMyWindow()
{
    bool hasFocusedText = false;
    ImGui.SetNextWindowSize(new float2(400, 300), ImGuiCond.FirstUseEver);
    if (ImGui.Begin("My Window", ref _windowVisible))
    {
        // Check INSIDE Begin/End — this is where the window context is valid
        hasFocusedText = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows)
                      && ImGui.GetIO().WantTextInput;

        ImGui.InputText("##name", _nameInput);
        // ... other widgets ...
    }
    ImGui.End();
    return hasFocusedText;
}
```

**Harmony patch — block game hotkeys only for this mod:**

```csharp
[HarmonyPatch(typeof(GameSettings), nameof(GameSettings.OnKeyAll))]
static class PatchGameSettingsOnKeyAll
{
    static bool Prefix(ref bool __result)
    {
        if (Mod.ModHasFocusedTextInput)
        {
            __result = true;
            return false; // skip original — hotkey is consumed
        }
        return true; // run original
    }
}
```

**Why this works:** `GameSettings.OnKeyAll` is the first handler in the game's input chain (`Program.cs`). If it returns `true`, all downstream handlers (including `ConsoleWindow.OnKey`) are short-circuited. By scoping the check to only your mod's windows, the in-game console and other input handlers remain functional.