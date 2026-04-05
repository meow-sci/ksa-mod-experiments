# SubmodUI Conformance & UI Redesign Plan

## Background

`SubmodUI` is a static helper class defined in `ksa-abstractions.lib/SubmodUI.cs`.
Every submod's `RenderContent()` **must** wrap its entire body with:

```csharp
SubmodUI.BeginContentArea("##<unique_id>");
// ... all ImGui content here ...
SubmodUI.EndContentArea();
```

`BeginContentArea` pushes `WindowPadding = float2(20, 20)`, opens a borderless
auto-height child window, and pops the style var. `EndContentArea` adds a 20 px
bottom dummy and closes the child. This guarantees uniform inset padding on every
side for all submods rendered inside the Grant unified window.

This plan covers two layers of work for each non-conformant submod:
1. **Conformance** — add the `SubmodUI` wrapper and remove ad-hoc padding substitutes.
2. **UI Redesign** — apply the layout patterns established in the conformant mods
   (`glass`, `con-man`, `garrys-torch`, `i-feel-seen`, `vehicle-paint`) to improve
   usability, visual clarity, and consistency for game players.

---

## Conformance Status

| Submod lib | File | Status |
|---|---|---|
| `average-twr.lib` | `AverageTwrSubmod.cs` | ❌ Non-conformant |
| `camera-controller-override.lib` | `CameraControllerOverrideSubmod.cs` | ❌ Non-conformant |
| `geeforce.lib` | `GeeForceSubmod.cs` / `GForceUI.cs` | ❌ Non-conformant |
| `kitten-animations.lib` | `KittenAnimationsSubmod.cs` | ❌ Non-conformant |
| `kiwis-marbles.lib` | `KiwisMarblesSubmod.cs` | ❌ Non-conformant |
| `skittles.lib` | `SkittlesSubmod.cs` | ❌ Non-conformant |
| `unladen-swallow.lib` | `UnladenSwallowSubmod.cs` | ❌ Non-conformant |
| `zippo.lib` | `ZippoSubmod.cs` | ❌ Non-conformant |

---

## UI Design Pattern Reference

These patterns are observed in the fully-conformant mods and **must** be applied
consistently during redesign. An implementing agent should study these files
before writing any ImGui code:

| Reference file | Key pattern demonstrated |
|---|---|
| `eternal-flame.lib/EternalFlameSubmod.cs` | Simple flat content area, table with RowBg + ScrollY |
| `glass.lib/GlassSubmod.cs` | 2-column proportional table (1fr label + 3fr widget), `AlignTextToFramePadding` |
| `con-man.lib/ConManSubmod.cs` | 3-column fixed-fit table, `BeginDisabled/EndDisabled`, modal popup inside content area |
| `garrys-torch.lib/GarrysTorchSubmod.cs` | Bordered child-window per list item, preset modal, `SeparatorText` section dividers |
| `i-feel-seen.lib/IFeelSeenSubmod.cs` | 2-column proportional table for selector, fixed-fit table for list, `PushID(i)` in loops |
| `humble-arteest.lib/VehiclePaintSubmod.cs` | Filtered combo helper, per-part table with `ScrollY`, status message pattern |
| `blinky.lib/BlinkySubmod.cs` | Menu bar, `SeparatorText` with dynamic counts, `CollapsingHeader` with `DefaultOpen` |

### Core Layout Rules (apply to every redesigned submod)

1. **Table for label+widget pairs.** Whenever a control needs a label, put both
   in a 2-column `ImGui.BeginTable` with `SizingStretchProp | NoPadOuterX` and
   `CellPadding (6,6)`. Never use raw `ImGui.Combo("Label##id", ...)` — use a
   hidden-label id + a table label column instead.

2. **`AlignTextToFramePadding()`** before every `ImGui.Text(...)` in a label column
   so text baselines align with the adjacent widget.

3. **`SetNextItemWidth(-1)`** on every widget that should fill its column.

4. **`SeparatorText("Section Name")`** to divide logical sections within the submod.
   Never use `TextColored + Separator` as a section divider — that was a pre-convention workaround.

5. **`CollapsingHeader(..., ImGuiTreeNodeFlags.DefaultOpen)`** for sections that
   should be expanded by default.

6. **`BeginDisabled() / EndDisabled()`** instead of hiding controls — always show
   unavailable controls grayed out with a `SetItemTooltip` explaining why.

7. **`SetItemTooltip("...")`** immediately after any non-obvious control to provide
   player-facing help text. Call `ImGui.SetItemTooltip` (not `BeginTooltip`) for
   simple single-string tips.

8. **Filtered combos.** Any combo with more than ~5 items should use `ImGuiTextFilter`
   with `IsWindowAppearing` auto-focus (see `garrys-torch` / `i-feel-seen` for the
   exact pattern).

9. **Status messages** should use `TextColored(red, msg)` for errors and
   `TextDisabled(msg)` for informational/hint text. Never silently suppress errors.

10. **Button label padding.** Use `" Label ##id"` (space-padded) for important
    action buttons to give them a wider hit target. Reserve compact labels for
    table icon buttons (` del `, ` X `).

11. **`PushID(i) / PopID()`** inside every loop that renders controls, to guarantee
    unique ImGui IDs and avoid stale-widget bugs.

12. **`CellPadding` push/pop** around every `BeginTable` call:
    ```csharp
    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
    if (ImGui.BeginTable(...)) { ... ImGui.EndTable(); }
    ImGui.PopStyleVar();
    ```

---

## General Conformance Rules

1. Add `using MeowSci.KsaAbstractions;` if not already present.
2. Wrap the **entire body** of `RenderContent()` in
   `SubmodUI.BeginContentArea("##<short_id>")` … `SubmodUI.EndContentArea()`.
3. **Remove** any manual padding substitutes that the convention now handles:
   - Leading `ImGui.TextColored(...)` title headers (Grant collapsible header already labels the section).
   - Leading `ImGui.Separator()` / `ImGui.Spacing()` calls compensating for missing top padding.
   - `ImGui.Indent()` / `ImGui.Unindent()` pairs that exist only for indentation, not section grouping.
4. **Keep** separators and spacing that logically divide content sections.
5. Do not change any non-UI logic, field declarations, helper methods, or other files in the lib unless the redesign explicitly calls for a new helper.
6. After each submod edit, run `dotnet build` from the repository root and confirm zero errors.

---

## Task 1 — `average-twr.lib` / `AverageTwrSubmod`

**File:** `average-twr.lib/AverageTwrSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- Stats are rendered as a wall of raw `ImGui.Text()` lines separated by ASCII art
  (`"── TWR ──────────────────────────────"`).
- Sample count is buried in plain text at the top; there's no visual status indicator.
- Start/Pause and Reset buttons are at the bottom with no visual separation.
- No indication to the player of what the statistics mean.

### Conformance changes

Wrap `RenderContent()` in `SubmodUI.BeginContentArea("##atwr_content")` …
`SubmodUI.EndContentArea()`. Remove the raw `ImGui.Text($"Samples: {n}"); ImGui.Separator();`
from the top (samples count will be integrated into the redesigned layout).

### UI redesign

Replace the ASCII-art stat display with a properly structured layout:

**Status bar (top of content)**
```
[ Status: Recording  |  Samples: 1234 ]
```
Use a 2-column table (`SizingStretchProp`) with label "Status" → colored text
("Recording" in green if collecting, "Paused" in yellow if not) and label
"Samples" → sample count.

**Stat tables (one collapsing header per metric group)**
Use `CollapsingHeader("TWR", ImGuiTreeNodeFlags.DefaultOpen)` and
`CollapsingHeader("Max Acceleration (m/s²)", ImGuiTreeNodeFlags.DefaultOpen)`.

Inside each header render a 2-column table (`SizingStretchProp | NoPadOuterX`,
`CellPadding (6,4)`, `RowBg | BordersInnerH`) with rows:

| Statistic | Value |
|---|---|
| Mean | 1.2345 |
| Std Dev | 0.0123 (1.0%) |
| Harmonic mean | 1.2100 |
| Brachi eff | 1.1980 |

- Left column: `AlignTextToFramePadding(); ImGui.Text("Mean")` etc.
- Right column: `ImGui.Text($"{value:F4}")` (or formatted string as today).
- Std Dev row shows the percentage inline: `$"{stdDev:F4}  ({pct:F1}%)"`
- When `SampleCount == 0`, show `ImGui.TextDisabled("No samples yet.")` inside
  each header body instead of the table.

**Control row (bottom of content)**
```
[ ▶ Start | ■ Pause ]  [ ↺ Reset ]
```
Render a single `ImGui.SeparatorText("Controls")` then a button row:
- `ImGui.Button(_isCollecting ? "■ Pause##atwr" : "▶ Start##atwr")`
- `ImGui.SameLine(0, 8)`
- `ImGui.Button("↺ Reset##atwr")` — disable when `SampleCount == 0`.

**No new fields or non-UI logic changes are needed.** All statistics are computed
the same way from the existing accumulator.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##atwr_content");` as the first statement in `RenderContent()`.
- [ ] Remove the existing `ImGui.Text($"Samples: {n}"); ImGui.Separator();` from the top of `RenderContent()`.
- [ ] Add a 2-column status table at the top showing "Status" (colored) and "Samples" (count).
- [ ] Replace the ASCII-art TWR text block with a `CollapsingHeader("TWR", DefaultOpen)` containing a `RowBg | BordersInnerH` 2-column stat table.
- [ ] Replace the ASCII-art Acceleration text block with a `CollapsingHeader("Max Acceleration (m/s²)", DefaultOpen)` containing the same style stat table.
- [ ] Show `ImGui.TextDisabled("No samples yet.")` inside each header body when `SampleCount == 0` (instead of the table).
- [ ] Replace the Start/Pause/Reset buttons with `ImGui.SeparatorText("Controls")` followed by `▶ Start` / `■ Pause` toggle and `↺ Reset` (disabled when 0 samples).
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 3).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 2 — `camera-controller-override.lib` / `CameraControllerOverrideSubmod`

**File:** `camera-controller-override.lib/CameraControllerOverrideSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- `RenderContent()` is ~500 lines of near-identical code repeated 8 times, one block
  per animation type. Each block re-declares `string[] easingNames = { ... }` — this
  is both a performance waste and a readability problem.
- Every slider uses a raw `if (SliderFloat(...)) { // Value updated }` with an
  empty comment body — pure noise.
- There is no visual grouping of related animation types. Zoom In, Zoom Out,
  Spiral Zoom In, Spiral Zoom Out, and Zoom In To Offset are all in the same flat
  list as Orbit, Loopy Orbit, and Shake.
- Each section's controls are a vertical list of sliders with no label/widget
  alignment — the label text is part of the slider's display label, producing a
  ragged appearance.
- The `KeyframeSequencePanel.Render()` call is buried at the bottom with the same
  styling as animation config sections.

### Conformance changes

- Wrap `RenderContent()` in `SubmodUI.BeginContentArea("##cco_content")` … `SubmodUI.EndContentArea()`.
- Remove the `ImGui.Indent()` / `ImGui.Unindent()` pairs inside each
  `CollapsingHeader` block — the SubmodUI wrapper provides the outer inset and
  a single `Indent` level inside the header is sufficient.

### UI redesign

**Eliminate the repetition with a private helper method**

Extract a shared `RenderAnimationParamsTable(string idSuffix, ref float speed, ...)` helper
that accepts the parameters for a zoom-style animation and renders the common
table (speed, duration, easing, easing power sliders) and the "Add to Sequence"
button. For animations that don't have a `speed` parameter (Orbit, Shake), use
overloads or separate helpers.

Define the easing names **once** as a static readonly field:
```csharp
private static readonly string[] EasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };
```

**Group animations into categories using `SeparatorText`**

Restructure `RenderContent()` to render three groups inside the content area:

```
SeparatorText("Zoom Animations")
  CollapsingHeader("Zoom Out")        { table }
  CollapsingHeader("Zoom In")         { table }
  CollapsingHeader("Zoom In To Offset") { table }
  CollapsingHeader("Spiral Zoom Out") { table }
  CollapsingHeader("Spiral Zoom In")  { table }

SeparatorText("Orbit Animations")
  CollapsingHeader("Orbit")           { table }
  CollapsingHeader("Loopy Orbit")     { table }

SeparatorText("Effects")
  CollapsingHeader("Shake")           { table }

SeparatorText("Keyframe Sequence")
  KeyframeSequencePanel.Render(...)
```

**Table layout for animation parameters (inside each CollapsingHeader)**

Replace the flat `SliderFloat("Label##id", ...)` calls with a 2-column table
(`SizingStretchProp | NoPadOuterX`, `CellPadding (6,6)`), columns: label (1fr)
/ widget (3fr). Example for a Zoom section:

```
| Speed (m/s)       | [slider 1..250 ] |
| Duration (s)      | [slider 1..30  ] |
| Easing            | [combo         ] |
| Easing Power Start| [slider 1..6   ] |  ← only shown for EaseIn / EaseInOut
| Easing Power End  | [slider 1..6   ] |  ← only shown for EaseOut / EaseInOut
```

The last row of each table is a full-width "Add to Sequence" button spanning both
columns (`ImGui.TableSetColumnSpan` or render it outside the table below).

**Remove empty `// Value updated` comment bodies** throughout. The `if (Slider...)` idiom is already idiomatic C# — no comment is needed.

**`KeyframeSequencePanel` improvements** (file: `UI/KeyframeSequencePanel.cs`):
- The return-to-start controls use raw `ImGui.Indent/Unindent` and loose sliders
  with long implicit-label text. Convert to a 2-column table matching the pattern above.
- The keyframe list items use `ImGui.Selectable` + a side-rendered colored overlay
  (a workaround hack that re-renders the title). Replace with a simple table row:
  `bool isPlaying` → push text color → `ImGui.Text(title)` → pop color, giving
  clean highlighting without the `SameLine(-textWidth)` trick.
- Move the `▶ Play / ⏸ Pause / ▶ Resume / ⏹ Stop / Clear All` buttons into a
  compact table row with `SizingStretchSame` so they distribute evenly.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##cco_content");` as the first statement in `RenderContent()`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()` (after the Keyframe Sequence panel call).
- [ ] Add `private static readonly string[] EasingNames = { "Linear", "Ease In", "Ease Out", "Ease In-Out" };` to the class, and replace all 8 local declarations.
- [ ] Extract a private `RenderZoomParamsTable(string id, ref float speed, ref float duration, ref int easing, ref float powerStart, ref float powerEnd)` helper that renders the 5-row 2-column parameter table and returns `true` if "Add to Sequence" was clicked.
- [ ] Extract a private `RenderOrbitParamsTable(string id, ref float degrees, ref float duration, ref int easing, ref float powerStart, ref float powerEnd)` helper.
- [ ] Extract a private `RenderShakeParamsTable(string id, ref float duration, ref int count, ref float amplitude, ref float speed, ref int easing, ref float powerStart, ref float powerEnd)` helper.
- [ ] Restructure `RenderContent()` to use `ImGui.SeparatorText` for the three categories: "Zoom Animations", "Orbit Animations", "Effects".
- [ ] Remove `ImGui.Indent()` / `ImGui.Unindent()` inside each CollapsingHeader block.
- [ ] Remove all empty `// Value updated` comment bodies.
- [ ] In `KeyframeSequencePanel.cs`, convert the return-to-start controls to a 2-column table layout.
- [ ] In `KeyframeSequencePanel.cs`, replace the `Selectable + SameLine(-textWidth) + TextColored` hack with `ImGui.PushStyleColor / ImGui.Text / ImGui.PopStyleColor` for current-keyframe highlighting.
- [ ] In `KeyframeSequencePanel.cs`, put the five playback buttons in an evenly-distributed row using `ImGui.SetNextItemWidth` or a fixed-width per-button calculation.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 3 — `geeforce.lib` / `GeeForceSubmod` + `GForceUI`

**Files:**
- `geeforce.lib/GeeForceSubmod.cs`
- `geeforce.lib/GForceUI.cs`

### Context

`GeeForceSubmod.RenderContent()` delegates entirely to `GForceUI.RenderContent()`.
`GForceUI.RenderContent()` is also called by the standalone `GForceUI.Render()`
window and must remain wrapper-free.

### Conformance change only

Wrap the delegation call in `GeeForceSubmod.RenderContent()`. Do **not** change
`GForceUI.cs` — its existing content layout is already well-structured (tabular
stats row, progress bar, graph, scrub slider, controls).

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##gf_content");
    GForceUI.RenderContent(_recorder, SampleIntervalSec);
    SubmodUI.EndContentArea();
}
```

No UI redesign is required for this submod. `GForceUI.RenderContent` already
follows good patterns.

### Checklist

- [ ] In `GeeForceSubmod.cs`, add `SubmodUI.BeginContentArea("##gf_content");` before the `GForceUI.RenderContent(...)` call.
- [ ] Add `SubmodUI.EndContentArea();` after the `GForceUI.RenderContent(...)` call.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present in `GeeForceSubmod.cs` (line 5).
- [ ] Do **not** modify `GForceUI.cs`.
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 4 — `kitten-animations.lib` / `KittenAnimationsSubmod`

**File:** `kitten-animations.lib/KittenAnimationsSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- Buttons inside each `CollapsingHeader` are stacked one-per-row — on a wide
  panel this wastes horizontal space and requires scrolling.
- The "Expressions" section has the `SliderFloat("Expression Duration (s)", ...)`
  slider **above** the expression buttons, but there's no visual separation
  between the slider and the buttons, making it unclear that the slider affects
  the buttons below it.
- "Walking Animations" has only two buttons, stacked vertically.
- No disabled state when no avatar is present — the method just silently returns.

### Conformance changes

- The early-return guard (`if (null == avatar) return;`) must stay **before** `BeginContentArea`.
- Add `SubmodUI.BeginContentArea("##ka_content")` after the guard.
- Add `SubmodUI.EndContentArea()` as the last statement.

### UI redesign

**Avatar unavailable state**
Replace the silent early return with a `SubmodUI.BeginContentArea` block that
renders `ImGui.TextDisabled("No avatar detected in scene.")` and then `EndContentArea`.
This gives the player feedback instead of a mysteriously empty section.

```csharp
public void RenderContent()
{
    var avatar = KittenAvatarAccessor.GetKittenAvatar();

    SubmodUI.BeginContentArea("##ka_content");

    if (avatar == null)
    {
        ImGui.TextDisabled("No avatar detected in scene.");
        SubmodUI.EndContentArea();
        return;
    }

    // ... sections ...

    SubmodUI.EndContentArea();
}
```

**MMU Animations section**
Render buttons in a 2-column equal-width grid using a table with
`SizingStretchSame | NoPadOuterX`, `CellPadding (6,4)`. Each button fills its
column: `ImGui.SetNextItemWidth(-1); ImGui.Button("Idle Default##ka")`.

Buttons in order, left-to-right across the rows:
```
| Idle Default  | Move Left    |
| Move Right    | Move Forward |
| Move Backward | Move Up      |
| Move Down     |              |
```

**Expressions section**
Place the duration slider inside a 2-column table row:
```
| Duration (s)  | [slider 1..5 ] |
```
Then use `ImGui.SeparatorText("Expressions")` to introduce the expression buttons,
which are rendered in a 3-column equal-width grid:
```
| Angry  | Awe    | Happy  |
| Sad    | Scared |        |
```

**Walking Animations section**
Put "Running" and "Walking" side by side using `SameLine(0, 8)`.

### Checklist

- [ ] Change the early-return guard to render `SubmodUI.BeginContentArea("##ka_content")` before the check, show `TextDisabled("No avatar detected in scene.")` if null, call `EndContentArea` and return.
- [ ] Render MMU animation buttons in a 2-column `SizingStretchSame` table.
- [ ] In the Expressions section, place the duration slider in a 2-column table row with `AlignTextToFramePadding` label.
- [ ] Add `ImGui.SeparatorText("Expressions")` between the slider row and the expression buttons.
- [ ] Render expression buttons in a 3-column `SizingStretchSame` table.
- [ ] In Walking Animations, put "Running" and "Walking" `SameLine(0, 8)`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 5 — `kiwis-marbles.lib` / `KiwisMarblesSubmod`

**File:** `kiwis-marbles.lib/KiwisMarblesSubmod.cs`

### Current problems

- No `SubmodUI` wrapper; double-indent substitutes for it.
- The Create Weld section uses `ImGui.PushStyleColor` to override text color to
  green for the Source/Target combos, but this approach requires `PopStyleColor`
  and intermixes style logic with layout logic — it is fragile.
- Source and Target dropdowns both have inline `ImGui.TextColored("Source (...)")`
  rendered via `SameLine` after the combo — non-standard layout.
- The CCI offset (`DragFloat3`) and unit scale combo are laid out with manual
  `SetNextItemWidth(-100f)` / `SameLine` / `SetNextItemWidth(82f)` — fragile against
  window resizing.
- The "Place on Surface" helper buttons are three separate `SameLine` buttons with
  verbose labels, hard to scan at a glance.
- Active welds use `double Indent/Unindent` — reduces to single after wrapper added.
- Active weld edit state is tracked in `Dictionary<int, ...>` keyed by list index,
  which requires complex re-keying logic in `RemoveWeld`. This is a non-UI concern
  but the redesign should not make it worse.

### Conformance changes

- Wrap `RenderContent()` in `SubmodUI.BeginContentArea("##km_content")` / `EndContentArea()`.
- Replace the double `Indent/Unindent` in the Create Weld section with `SeparatorText("Create Weld")`.
- Replace the double `Indent/Unindent` inside each active-weld `CollapsingHeader` body with a single pair.
- Replace `TextColored + Separator` for "Active Welds" with `SeparatorText("Active Welds")`.

### UI redesign

**Create Weld section — use a 3-column table for Source + Target**

Replace the manual `PushStyleColor / BeginCombo / SameLine / TextColored /
PopStyleColor` pattern with a standard 3-column table:
- Column 1: label (fixed width from `CalcTextSize("Source")`)
- Column 2: filtered combo (stretch)
- Column 3: (empty — reserved for future buttons or align with active-weld table)

Use `ImGuiTextFilter` combos following the `garrys-torch` pattern (auto-focus on
`IsWindowAppearing`, `filter.Draw(...)` before items).

Remove the `PushStyleColor(Text, RadioactiveGreen)` calls. The standard combo text
color is sufficient — using radioactive green for both Source and Target makes
neither stand out meaningfully.

Add `ImGui.SetItemTooltip(...)` after the Source combo:
```
"Source: the celestial body (planet or moon) that will be moved and locked\nto the target's position each frame."
```
And after the Target combo:
```
"Target: any orbiter (vehicle or another celestial) that the source will follow."
```

**CCI Offset row — use a 2-column table**

Replace the manual `SetNextItemWidth(-100f)` / `SameLine` layout with a
2-column table row:
- Column 1: `AlignTextToFramePadding(); ImGui.Text("CCI Offset")` with tooltip explaining CCI
- Column 2: a nested sub-row — `DragFloat3` taking most of the space, then `Combo` for unit selector at fixed width

A clean implementation keeps them on the same row using `SetNextItemWidth` with
calculated widths, or uses an inner 2-column sub-table.

**Surface placement helper — compact button row with tooltips**

Replace the three verbose "Place on Surface (along X+/Y+/Z+)" buttons with
three compact axis buttons and a tooltip per button:
```
[ +X ]  [ +Y ]  [ +Z ]
```
Add `SetItemTooltip("Place source on surface of target along X+ axis")` etc.

**Active Welds — follow Garry's Torch bordered-child pattern**

In `RemoveWeld`, each active weld currently renders inside a `CollapsingHeader`
with a double-indent body. Replace with the `garrys-torch` bordered-child-window
pattern:
- `CollapsingHeader("Weld N: Source → Target##km_weld_N", DefaultOpen)` opens the header.
- Immediately after, open a bordered auto-height child window (same style as
  `garrys-torch`'s `gt_child_{index}`) with `WindowPadding (20,10)`.
- Inside the child: source → target info row, offset controls, Unweld button.
- This makes each active weld visually self-contained.

Inside each active-weld child:
- Use a 2-column table for "Offset unit" label + `Combo` (unit scale) + `DragFloat3`.
- Keep "Surface Orbit Mode" checkbox and its lon/lat/altitude sliders in a 2-column
  table following the label-widget pattern.
- Put the "Unweld" button at the bottom with `PushStyleColor(Button, Scarlet)` /
  `PopStyleColor()` as in Garry's Torch for a clear destructive-action indicator.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##km_content");` as the first statement in `RenderContent()`.
- [ ] Replace `TextColored + Separator` + double-`Indent` for "Create Weld" with `ImGui.SeparatorText("Create Weld")`.
- [ ] Replace Source/Target combo layout with a 3-column `SizingFixedFit | NoPadOuterX` table (`CellPadding (6,6)`) following the `garrys-torch` `RenderFilteredCombo` pattern.
- [ ] Remove `PushStyleColor(Text, RadioactiveGreen)` / `PopStyleColor()` calls on the Source and Target combos.
- [ ] Add `SetItemTooltip(...)` after the Source and Target combos explaining each role.
- [ ] Replace CCI offset `SetNextItemWidth(-100f) + SameLine + SetNextItemWidth(82f)` layout with a 2-column table row with the `DragFloat3` in the widget column and unit `Combo` at the right edge.
- [ ] Add a `SetItemTooltip` on the offset control explaining CCI offset.
- [ ] Replace the three "Place on Surface" buttons with compact `[ +X ]`, `[ +Y ]`, `[ +Z ]` buttons with `SetItemTooltip`.
- [ ] Replace `TextColored + Separator` for "Active Welds" with `ImGui.SeparatorText($"Active Welds ( {_welds.Count} )")`.
- [ ] Replace double-indent inside each active-weld `CollapsingHeader` body with the Garry's Torch bordered-child-window pattern.
- [ ] Inside each active-weld child, put offset controls in a 2-column table with `AlignTextToFramePadding` labels.
- [ ] Inside each active-weld child, put surface-mode lon/lat/altitude sliders in a 2-column table.
- [ ] Style the "Unweld" button with `PushStyleColor(Button, Scarlet)` + `PushStyleColor(Text, PaleGrey)`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 6).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 6 — `skittles.lib` / `SkittlesSubmod`

**File:** `skittles.lib/SkittlesSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- `RenderEditorWindow()` opens a separate `ImGui.Begin/End` window and is currently
  called at the end of `RenderContent()` — it must be moved outside the child window.
- The branded title block at the top (`TextColored + SameLine + TextDisabled + Separator`)
  is display noise.
- The "Active: {name}" display is a plain `ImGui.Text` — it blends into the rest
  and is easy to miss.
- The theme selector combo is full-width. There is no Apply button (selection
  applies on click inside the dropdown), but the active-theme status is not aligned
  with the selector.
- The "Quick Apply" row puts 5 buttons on one line with `SameLine`. On narrower
  windows this can overflow or look cramped.

### Conformance changes

- Add `SubmodUI.BeginContentArea("##sk_content")` as the first statement.
- Remove the branded title block (4 lines: `TextColored`, `SameLine`, `TextDisabled`, `Separator`).
- Add `SubmodUI.EndContentArea()` **before** `if (_editorVisible) RenderEditorWindow()`.

### UI redesign

**Active theme display — use a 2-column info table**

Replace the plain `ImGui.Text($"Active: {active}")` + `ImGui.Separator()` with a
2-column table row:
```
| Active  | Game Default         |
| Select  | [combo dropdown    ] |
```
- "Active" label → `AlignTextToFramePadding(); ImGui.Text("Active")`.
- Right column: `ImGui.TextColored(green, active)` — colored so active theme is visually prominent.
- "Select" label → standard aligned label.
- Right column: full-width theme combo (`SetNextItemWidth(-1)`).

This eliminates the `ImGui.Separator()` between active status and the dropdown and
makes both controls visually related in the same table.

**Theme selector combo — filterable**

The current combo already has a filter input inside it. Keep this behavior — it is
a good UX pattern. Ensure it follows the standard `IsWindowAppearing` / auto-focus
/ `filter.Draw` pattern.

**Quick Apply buttons — 2-column grid**

Replace the 5-button `SameLine` row with a 2-column `SizingStretchSame` table so
buttons wrap gracefully:
```
| Dark    | Light   |
| Classic | Rod     |
| Reset   |         |
```

Add `SetItemTooltip` to "Reset" explaining it restores game defaults, and to "Rod"
explaining it applies the Inanimate Carbon Rod theme.

**Open Theme Editor button**

Keep the "Open Theme Editor##sk" button below the Quick Apply grid, separated by
`ImGui.Spacing()`.

**`RenderEditorWindow()` positioning**

`RenderEditorWindow()` calls `ImGui.Begin/End` — this must remain outside the
`SubmodUI` child window. Place it after `SubmodUI.EndContentArea()`:

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##sk_content");
    // ... all content ...
    SubmodUI.EndContentArea();

    if (_editorVisible)
        RenderEditorWindow();
}
```

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##sk_content");` as the first statement in `RenderContent()`.
- [ ] Remove `ImGui.TextColored(...); ImGui.SameLine(); ImGui.TextDisabled("Global Theme Manager"); ImGui.Separator();`.
- [ ] Replace `ImGui.Text($"Active: {active}"); ImGui.Separator();` with a 2-column table row that uses `ImGui.TextColored(green, active)` in the widget column.
- [ ] Add the "Select" row to the same table, with the theme combo using `SetNextItemWidth(-1)`.
- [ ] Replace the 5-button `SameLine` row with a 2-column `SizingStretchSame` table.
- [ ] Add `SetItemTooltip` to "Reset" and "Rod" buttons.
- [ ] Keep "Open Theme Editor" button below the grid.
- [ ] Add `SubmodUI.EndContentArea();` immediately before `if (_editorVisible) RenderEditorWindow();`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 7 — `unladen-swallow.lib` / `UnladenSwallowSubmod`

**File:** `unladen-swallow.lib/UnladenSwallowSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- The colored title and `SeparatorText("HTTP RPC Server")` are top-padding substitutes.
- The UI is just a checkbox followed by a status line — technically minimal but
  the status line only says "Running on http://0.0.0.0:7887" (no visual emphasis)
  or "Server: Stopped" (`TextDisabled`). There is no indication of the port or URL
  in a clickable/copyable form.
- No indication what the server does (i.e., what endpoints are available).

### Conformance changes

- Add `SubmodUI.BeginContentArea("##us_content")` as the first statement.
- Remove `ImGui.TextColored(..., "Unladen Swallow");`.
- Remove `ImGui.SeparatorText("HTTP RPC Server");`.
- Add `SubmodUI.EndContentArea()` as the last statement.

### UI redesign

**Server control row — 2-column table**

Replace the raw checkbox + status text with a 2-column table (`SizingStretchProp | NoPadOuterX`, `CellPadding (6,6)`):

```
| Server    | [☑ Enabled]       |
| Status    | ● Running  / ○ Stopped  |
| Endpoint  | http://0.0.0.0:7887     |
```

- "Server" label → checkbox `Enable HTTP Server##us`.
- "Status" label → `TextColored(green, "● Running")` or `TextColored(grey, "○ Stopped")`.
- "Endpoint" label → `TextDisabled("http://0.0.0.0:7887")` — only show this row when running.

**Endpoints reference section**

Below the table, add a `CollapsingHeader("Available Endpoints")` (default closed
so it doesn't clutter the compact view). Inside it, list the RPC endpoints as a
read-only text block. This helps the player/developer know what they can call.
The list can be a static string and updated to match `SwallowServer`'s actual
routes. Example:

```
GET  /fov            — read current FOV
POST /fov            — set FOV
GET  /blinky/list    — list Blinky grids
POST /blinky/static  — set static pixel
POST /blinky/animate — set animated scroll
POST /blinky/off     — clear grid
```

Render each row as a single `ImGui.TextDisabled(line)`.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##us_content");` as the first statement in `RenderContent()`.
- [ ] Remove `ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");`.
- [ ] Remove `ImGui.SeparatorText("HTTP RPC Server");`.
- [ ] Add a 2-column `SizingStretchProp | NoPadOuterX` table with `CellPadding (6,6)`.
- [ ] Add "Server" row: label + Enable checkbox (keep existing enable/disable logic).
- [ ] Add "Status" row: label + colored status indicator (`● Running` green / `○ Stopped` grey).
- [ ] Add "Endpoint" row (visible only when running): label + `TextDisabled("http://0.0.0.0:7887")`.
- [ ] Add `CollapsingHeader("Available Endpoints")` (default closed) below the table with a static text list of RPC routes from `SwallowServer`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 4).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 8 — `zippo.lib` / `ZippoSubmod`

**File:** `zippo.lib/ZippoSubmod.cs`

### Current problems

- No `SubmodUI` wrapper.
- The branded title is top-padding noise.
- Vehicle and Light Part combos use `ImGui.Combo("Label##id", ...)` which places
  the label on the same line after the combo — non-standard layout.
- "Light Part" combo has a `SameLine` debug button (`Dbg##zp`) appended, making the
  part selector narrower in an unpredictable way.
- Intensity is a `DragFloat` full-width above an `ImGui.Text("Emissive Intensity")`
  label — the label is **below** the widget, backwards from convention.
- The Color preset combo uses the same raw label-after-combo pattern.
- The manual color picker is full-width but sandwiched between the combo and the end
  of the method — no visual separation.

### Conformance changes

- Add `SubmodUI.BeginContentArea("##zp_content")` as the first statement.
- Remove `ImGui.TextColored(..., "Zippo  Light Control"); ImGui.Separator(); ImGui.Spacing();`.
- Add `SubmodUI.EndContentArea()` as the last statement.

### UI redesign

**Vehicle and Light Part selectors — 2-column table**

Consolidate the two selectors into a single 2-column `SizingStretchProp | NoPadOuterX`
table (`CellPadding (6,6)`):

```
| Vehicle     | [combo: vehicles       ] |
| Light Part  | [combo: light parts    ] |
```

Move the `Dbg##zp` debug button outside the table. Place it after the table as a
small helper button: `ImGui.Button(" Dbg ##zp")` at the bottom of the vehicle-selection
area, perhaps inside a `CollapsingHeader("Debug", default-closed)` to keep it out
of the way during normal use.

Use `ImGuiTextFilter` filterable combos for both Vehicle and Light Part since
vehicles/parts lists can be long.

**Light Controls section — 2-column table**

When a light part is selected, show a `ImGui.SeparatorText("Light Controls")` and
then a 2-column table for all per-light parameters:

```
| On / Off    | [ Turn Off / Turn On button ] |
| Intensity   | [slider 0..1               ] |
| Color Preset| [combo presets             ] |
| Color       | [ColorEdit4 picker         ] |
```

- "On/Off" row: single button in widget column.
- "Intensity" row: `DragFloat` widget column with `SetNextItemWidth(-1)`. Label is
  "Intensity" in label column. **The label is now on the left**, which is standard.
- "Color Preset" row: `Combo` in widget column.
- "Color" row: `ColorEdit4` in widget column.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##zp_content");` as the first statement in `RenderContent()`.
- [ ] Remove `ImGui.TextColored(...); ImGui.Separator(); ImGui.Spacing();` from the top.
- [ ] Add a 2-column `SizingStretchProp | NoPadOuterX` table with `CellPadding (6,6)` for Vehicle and Light Part selectors.
- [ ] Replace raw `ImGui.Combo("Vehicle##zp", ...)` with a filterable combo following the `garrys-torch` `RenderFilteredCombo` pattern in the Vehicle row.
- [ ] Replace raw `ImGui.Combo("Light Part##zp", ...)` with a filterable combo in the Light Part row.
- [ ] Move `Dbg##zp` button outside the selector table; place it inside a `CollapsingHeader("Debug", default-closed)`.
- [ ] Add `ImGui.SeparatorText("Light Controls")` before the per-light-part controls (only shown when a part is selected).
- [ ] Add a 2-column `SizingStretchProp | NoPadOuterX` table for On/Off, Intensity, Color Preset, and Color picker rows.
- [ ] "Intensity" label on left, `DragFloat` filling the widget column — remove the below-widget `ImGui.Text("Emissive Intensity")` line.
- [ ] Verify `using MeowSci.KsaAbstractions;` is present (line 5).
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Run `dotnet build` — expect zero errors.

---

## Verification Checklist (run after all 8 tasks are complete)

- [ ] `dotnet build` from repo root produces zero errors and zero warnings introduced by these changes.
- [ ] In the Grant supermod window, every refactored submod's collapsible section renders with consistent top, left, and right inset matching the conformant submods (Eternal Flame, Blinky, etc.).
- [ ] No submod produces a visible nested child-window artifact or double-scroll.
- [ ] Standalone mod versions are unaffected — the SubmodUI child window nests cleanly inside any parent `ImGui.Begin/End`.
- [ ] `GForceUI.Render()` (standalone geeforce window) is visually unchanged.
- [ ] The Skittles Theme Editor floating window (`RenderEditorWindow`) opens and functions correctly after being moved outside the SubmodUI child.
- [ ] In each redesigned submod, every label/widget pair is rendered via a 2-column table — no raw `ImGui.Combo("Label##id")` pattern remains.
- [ ] No `TextColored + Separator` section-divider pattern remains — all section dividers use `ImGui.SeparatorText(...)`.
- [ ] All combos with more than 5 items have a filterable input with `IsWindowAppearing` auto-focus.
- [ ] No `ImGui.Indent() / ImGui.Unindent()` pairs remain that exist solely for horizontal inset rather than logical section grouping.
- [ ] Disabled controls use `BeginDisabled / EndDisabled` and have `SetItemTooltip` explanations.

---

## Notes for Implementing Agents

- **SubmodUI ID uniqueness:** The string passed to `SubmodUI.BeginContentArea(id)` is the ImGui child-window ID. It must be unique within the parent window. The `##` prefix hides it. Suggested IDs: `##<abbrev>_content`.
- **`EndContentArea` position:** Always the very last line of `RenderContent()`, except for top-level `ImGui.Begin/End` blocks (e.g., `RenderEditorWindow`) which must live outside the child.
- **Do not nest `BeginContentArea` calls.** Each submod gets exactly one `BeginContentArea` / `EndContentArea` pair.
- **Redesign scope:** UI changes are confined to `RenderContent()` and any private `Render*` helper methods. Non-UI fields, logic classes, and other files in the lib are out of scope unless a specific checklist item says otherwise.
- **Split large `RenderContent` methods into private helpers** (like `RenderCreateSection`, `RenderWeldSection` in Garry's Torch) so each method stays under ~80 lines.
- **Conformant reference files to study before implementing:**
  - `eternal-flame.lib/EternalFlameSubmod.cs` — simple flat layout
  - `glass.lib/GlassSubmod.cs` — minimal 2-column table
  - `con-man.lib/ConManSubmod.cs` — 3-column fixed table, popups
  - `garrys-torch.lib/GarrysTorchSubmod.cs` — bordered child-windows, filtered combos, modals
  - `i-feel-seen.lib/IFeelSeenSubmod.cs` — compact selector + list table
  - `humble-arteest.lib/VehiclePaintSubmod.cs` — scrollable per-part table, status messages