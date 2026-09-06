---
name: imgui-design
description: preferred imgui layout patterns
---

# ImGui Layout Conventions

These patterns are extracted from the KSA mod codebase and represent the preferred style for building ImGui UIs in submods.

---

## Content Area (outermost wrapper)

Every submod's `RenderContent()` wraps its body with `SubmodUI.BeginContentArea` / `EndContentArea` from `MeowSci.KsaAbstractions`. This applies a consistent `WindowPadding = float2(20f, 20f)` and extra bottom padding. Always call this — never render content directly.

```csharp
SubmodUI.BeginContentArea("##my_content");
// ... content ...
SubmodUI.EndContentArea();
```

---

## Layout Tables

All layout tables use `NoPadOuterX` so content stretches edge-to-edge with the window border (the window padding from `BeginContentArea` handles outer inset).

Apply `CellPadding = float2(6f, 6f)` before every layout table and pop after:

```csharp
ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
if (ImGui.BeginTable("##id", columns, flags))
{
    // ...
    ImGui.EndTable();
}
ImGui.PopStyleVar(); // CellPadding
```

### 4-column equal-width table (parameter grid)

Use for pairs of label+widget side by side (e.g. Columns/Rows, Spacing/Scale):

```csharp
var flags = ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX;
if (ImGui.BeginTable("##params", 4, flags))
{
    ImGui.TableNextRow();
    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Label A");
    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##a", ref valA, ...);
    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Label B");
    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##b", ref valB, ...);
    ImGui.EndTable();
}
```

### 2-column proportional table (label + full-width widget)

Use for rows where the label gets ¼ width and the widget gets ¾, matching the 4-column table's visual alignment:

```csharp
var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
if (ImGui.BeginTable("##select", 2, flags))
{
    ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
    ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

    ImGui.TableNextRow();
    ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Label");
    ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1); /* widget */
    ImGui.EndTable();
}
```

### 3-column fixed-label + stretch-widget + fixed-buttons table

Use when rows need a label, a fill widget, and a fixed-width button group:

```csharp
var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
if (ImGui.BeginTable("##form", 3, flags))
{
    ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, labelW);
    ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch);
    ImGui.TableSetupColumn("##btns", ImGuiTableColumnFlags.WidthFixed, btnColW);
    // ...
    ImGui.EndTable();
}
```

### Table text alignment

Always call `ImGui.AlignTextToFramePadding()` immediately before any label text that sits in the same row as a widget, to vertically center it.

---

## Spacing Between Sections

Use `ImGui.Spacing()` between logical groups. Use `ImGui.SeparatorText("label")` for named section dividers (e.g. between a creation form and a list of items).

---

## Collapsing Headers

- Append `(?)` to the label when a tooltip is needed, then call `ImGui.SetItemTooltip(...)` immediately after.
- Use `ImGuiTreeNodeFlags.DefaultOpen` for primary/important sections.

```csharp
if (!ImGui.CollapsingHeader("My Section (?)", ImGuiTreeNodeFlags.DefaultOpen))
    return;
ImGui.SetItemTooltip("Explanation of this section.");
```

---

## Bordered Child Windows for Repeated List Items

When rendering a list of items where each has its own `CollapsingHeader`, put the content inside a bordered child window. The `CollapsingHeader` renders flush to the window edges (ignoring `WindowPadding`), so the child must be manually expanded to match and its cursor pulled left:

```csharp
if (!ImGui.CollapsingHeader($"Item: {name}##id"))
    return;

var wpadX = ImGui.GetStyle().WindowPadding.X;
float childW = ImGui.GetContentRegionAvail().X + wpadX * 2;
ImGui.SetCursorPosX(ImGui.GetCursorPosX() - wpadX);
ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(20f, 10f)); // inner padding
ImGui.BeginChild($"child_{id}", new float2(childW, 0),
    ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding,
    ImGuiWindowFlags.NoScrollbar);
ImGui.PopStyleVar(); // WindowPadding

// ... content ...

ImGui.Spacing();
ImGui.EndChild();
```

The `WindowPadding` push/pop must happen **before** `BeginChild` — ImGui captures it at Begin time.

---

## Buttons

- Add a leading and trailing space in button labels to give them natural padding when no explicit size is set:
  `" Apply "`, `" Delete "`, `" Create "`, `" Off "`, `" Rows "`, etc.
- For dangerous/destructive actions (e.g. Destroy), push red button color:

```csharp
ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(KSAColor.Xkcd.Scarlet));
ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(KSAColor.Xkcd.PaleGrey));
if (ImGui.Button($" Destroy ##{id}"))
    DoDestroy();
ImGui.PopStyleColor();
ImGui.PopStyleColor();
```

- When a row has multiple sibling buttons, separate with `ImGui.SameLine(0, 8)`.
- When a button is followed by inline text, use `ImGui.SameLine(0, 12)` then `ImGui.AlignTextToFramePadding()` before the text.

---

## Combos with Filter

All combo dropdowns that may have many items follow this pattern:

```csharp
if (ImGui.BeginCombo("##id", preview))
{
    if (ImGui.IsWindowAppearing())
    {
        ImGui.SetKeyboardFocusHere();
        _filter.Clear();
    }
    _filter.Draw("##filter", -1f);
    for (int i = 0; i < items.Length; i++)
    {
        if (!_filter.PassFilter(items[i])) continue;
        bool sel = _selectedIndex == i;
        if (ImGui.Selectable(items[i], sel))
            _selectedIndex = i;
        if (sel) ImGui.SetItemDefaultFocus();
    }
    ImGui.EndCombo();
}
```

---

## Status / Feedback Messages

- Errors: `ImGui.TextColored(new float4(1f, 0.3f, 0.3f, 1f), message)`
- Success: `ImGui.TextColored(new float4(0.4f, 1f, 0.4f, 1f), message)`
- Neutral/info: `ImGui.TextDisabled(message)`
- Always guard with `if (!string.IsNullOrEmpty(_message))` and add `ImGui.Spacing()` before.

---

## Disabled Controls

Wrap unavailable controls with the standard ImGui disable guard:

```csharp
if (!canAct) ImGui.BeginDisabled();
if (ImGui.Button(" Act ##id")) DoAct();
if (!canAct) ImGui.EndDisabled();
```

