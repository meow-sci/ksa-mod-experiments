# SubmodUI Conformance Refactor Plan

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

**Reference implementation (fully conformant):**
`eternal-flame.lib/EternalFlameSubmod.cs` – a simple but complete example.

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

## General Rules for Every Refactor

1. Add `using MeowSci.KsaAbstractions;` if not already present.
2. Wrap the **entire body** of `RenderContent()` in
   `SubmodUI.BeginContentArea("##<short_id>")` … `SubmodUI.EndContentArea()`.
3. **Remove** any manual padding substitutes that the convention now handles:
   - Leading `ImGui.TextColored(...)` title headers (these are display chrome, not
     content — the Grant collapsible header already labels the section).
   - Leading `ImGui.Separator()` / `ImGui.Spacing()` calls that were compensating
     for missing top padding.
   - `ImGui.Indent()` / `ImGui.Unindent()` pairs that only exist because there was
     no content-area inset.
4. **Keep** separators and spacing that logically divide content sections within
   the submod.
5. Do not change any non-UI logic, field declarations, helper methods, or other
   files in the lib.
6. After each submod edit, run `dotnet build` from the repository root and confirm
   zero errors.

---

## Task 1 — `average-twr.lib` / `AverageTwrSubmod`

**File:** `average-twr.lib/AverageTwrSubmod.cs`

### What to change

`RenderContent()` currently renders content directly with no wrapper. The first
two lines are `ImGui.Text(...)` and `ImGui.Separator()` acting as ad-hoc top
padding.

**Current `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    int n = _accumulator.SampleCount;
    ImGui.Text($"Samples: {n}");
    ImGui.Separator();
    // ... stats rows ...
    if (ImGui.Button(...)) ...
    ImGui.SameLine();
    if (ImGui.Button("Reset##atwr")) ...
}
```

**Target `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##atwr_content");

    int n = _accumulator.SampleCount;
    ImGui.Text($"Samples: {n}");
    ImGui.Separator();
    // ... stats rows (unchanged) ...
    if (ImGui.Button(...)) ...
    ImGui.SameLine();
    if (ImGui.Button("Reset##atwr")) ...

    SubmodUI.EndContentArea();
}
```

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##atwr_content");` as the first statement
  in `RenderContent()`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 3).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 2 — `camera-controller-override.lib` / `CameraControllerOverrideSubmod`

**File:** `camera-controller-override.lib/CameraControllerOverrideSubmod.cs`

### What to change

`RenderContent()` is a long method (~400+ lines) composed of 8 collapsing-header
sections (one per animation type) plus a Keyframe Sequence panel at the bottom.
Each collapsing-header section uses `ImGui.Indent()` / `ImGui.Unindent()` for
visual inset, and sections are separated by `ImGui.Spacing(); ImGui.Separator();`.
None of this content is inside a `SubmodUI` child window.

**Current `RenderContent()` opener:**

```csharp
public void RenderContent()
{
    // Zoom Out Animation Configuration
    if (ImGui.CollapsingHeader("Zoom Out Animation"))
    {
        ImGui.Indent();
        // ... controls ...
        ImGui.Unindent();
    }
    ImGui.Spacing();
    ImGui.Separator();
    // ... more sections ...
}
```

**Target `RenderContent()` opener/closer:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##cco_content");

    // Zoom Out Animation Configuration
    if (ImGui.CollapsingHeader("Zoom Out Animation"))
    {
        ImGui.Indent();
        // ... controls (unchanged) ...
        ImGui.Unindent();
    }
    ImGui.Spacing();
    ImGui.Separator();
    // ... remaining sections unchanged ...

    SubmodUI.EndContentArea();
}
```

The `ImGui.Indent()` / `ImGui.Unindent()` pairs inside each collapsing-header
block serve a legitimate visual purpose (indenting the controls *under* the
header), so they should **remain**. Only the outer wrapper is missing.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##cco_content");` as the first statement
  in `RenderContent()`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`
  (after the `KeyframeSequencePanel` render call and any trailing separators).
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 3 — `geeforce.lib` / `GeeForceSubmod` + `GForceUI`

**Files:**
- `geeforce.lib/GeeForceSubmod.cs`
- `geeforce.lib/GForceUI.cs`

### Context

`GeeForceSubmod.RenderContent()` is a one-liner that delegates to the static
helper:

```csharp
public void RenderContent()
{
    GForceUI.RenderContent(_recorder, SampleIntervalSec);
}
```

The static `GForceUI.RenderContent(...)` renders all content directly without a
wrapper. Both the submod and the static helper need to participate.

### What to change

The cleanest approach is to add the wrapper in `GeeForceSubmod.RenderContent()`
so that `GForceUI.RenderContent` stays a reusable rendering helper (it is also
called by `GForceUI.Render()` for the standalone window, where the wrapper must
**not** be applied).

**Target `GeeForceSubmod.RenderContent()`:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##gf_content");
    GForceUI.RenderContent(_recorder, SampleIntervalSec);
    SubmodUI.EndContentArea();
}
```

`GForceUI.RenderContent` itself is **not** changed — it must remain wrapper-free
for its use inside the standalone `GForceUI.Render()` window.

### Checklist

- [ ] In `GeeForceSubmod.cs`, wrap the delegation call:
  - Add `SubmodUI.BeginContentArea("##gf_content");` before the delegation.
  - Add `SubmodUI.EndContentArea();` after the delegation.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present in
  `GeeForceSubmod.cs` (it is — line 5).
- [ ] Do **not** modify `GForceUI.cs`.
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 4 — `kitten-animations.lib` / `KittenAnimationsSubmod`

**File:** `kitten-animations.lib/KittenAnimationsSubmod.cs`

### What to change

`RenderContent()` starts with an early-exit guard (`if (null == avatar) return;`)
followed by three collapsing-header blocks. There is no wrapper. The early return
must remain before the `BeginContentArea` call — there is nothing to render if
there is no avatar, and opening a child window only to close it empty is wasteful.

**Current `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    var avatar = KittenAvatarAccessor.GetKittenAvatar();
    if (null == avatar) return;

    if (ImGui.CollapsingHeader("MMU Animations"))
    { ... }

    if (ImGui.CollapsingHeader("Expressions"))
    { ... }

    if (ImGui.CollapsingHeader("Walking Animations"))
    { ... }
}
```

**Target `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    var avatar = KittenAvatarAccessor.GetKittenAvatar();
    if (null == avatar) return;

    SubmodUI.BeginContentArea("##ka_content");

    if (ImGui.CollapsingHeader("MMU Animations"))
    { ... }

    if (ImGui.CollapsingHeader("Expressions"))
    { ... }

    if (ImGui.CollapsingHeader("Walking Animations"))
    { ... }

    SubmodUI.EndContentArea();
}
```

The early-return guard stays **before** `BeginContentArea`. There are no
manual indent/padding substitutes to remove here — the collapsing headers render
cleanly.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##ka_content");` after the avatar null check
  and before the first `ImGui.CollapsingHeader` call.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 5 — `kiwis-marbles.lib` / `KiwisMarblesSubmod`

**File:** `kiwis-marbles.lib/KiwisMarblesSubmod.cs`

### What to change

`RenderContent()` uses `ImGui.TextColored(...)` section headers and a heavy
`ImGui.Indent(); ImGui.Indent();` / `ImGui.Unindent(); ImGui.Unindent();` pattern
as a manual padding substitute. The method has two main sections: **Create Weld**
and **Active Welds**.

**Current `RenderContent()` opener:**

```csharp
public void RenderContent()
{
    // --- Create Weld ---
    ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld");
    ImGui.Separator();
    ImGui.Indent();
    ImGui.Indent();
    // ... weld creation UI ...
    ImGui.Unindent();
    ImGui.Unindent();

    // --- Active Welds ---
    ImGui.Spacing();
    ImGui.Separator();
    ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Active Welds");
    ImGui.Separator();
    // ... active weld entries ...
}
```

**Target `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##km_content");

    // --- Create Weld ---
    ImGui.SeparatorText("Create Weld");
    // ... weld creation UI (remove the two Indent/Unindent pairs) ...

    // --- Active Welds ---
    ImGui.Spacing();
    ImGui.SeparatorText("Active Welds");
    // ... active weld entries (unchanged) ...

    SubmodUI.EndContentArea();
}
```

Specific changes inside the Create Weld section:
- Replace the `ImGui.TextColored(..., "Create Weld")` + `ImGui.Separator()` pair
  with `ImGui.SeparatorText("Create Weld")`.
- Remove both `ImGui.Indent();` lines immediately after.
- Remove both `ImGui.Unindent();` lines at the end of the Create Weld block.
- The double-indent on each active-weld collapsing-header body
  (`ImGui.Indent(); ImGui.Indent();` / `ImGui.Unindent(); ImGui.Unindent();`)
  should also be reduced to a single `ImGui.Indent()` / `ImGui.Unindent()` pair,
  since the SubmodUI wrapper now provides the outer inset.
- Replace `ImGui.TextColored(..., "Active Welds")` + `ImGui.Separator()` with
  `ImGui.SeparatorText("Active Welds")`.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##km_content");` as the first statement in
  `RenderContent()`.
- [ ] Replace `ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Create Weld"); ImGui.Separator();`
  with `ImGui.SeparatorText("Create Weld");`.
- [ ] Remove the two `ImGui.Indent();` calls immediately after the section header.
- [ ] Remove the two `ImGui.Unindent();` calls at the end of the Create Weld block
  (just before the Active Welds section).
- [ ] Replace `ImGui.TextColored((float4)KSAColor.Xkcd.Custard, "Active Welds"); ImGui.Separator();`
  with `ImGui.SeparatorText("Active Welds");`.
- [ ] Inside each active-weld collapsing-header block, reduce
  `ImGui.Indent(); ImGui.Indent();` to a single `ImGui.Indent();` and
  `ImGui.Unindent(); ImGui.Unindent();` to a single `ImGui.Unindent();`.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 6).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 6 — `skittles.lib` / `SkittlesSubmod`

**File:** `skittles.lib/SkittlesSubmod.cs`

### What to change

`RenderContent()` begins with a branded colored title and separator that were
acting as visual top-padding / section identification. Inside Grant, the
collapsing header already identifies the submod, so these serve no purpose.

**Current `RenderContent()` opener:**

```csharp
public void RenderContent()
{
    // --- Main content (inside grant collapsible header) ---
    ImGui.TextColored(new float4(0.17f, 0.98f, 0.12f, 1.0f), "Skittles");
    ImGui.SameLine();
    ImGui.TextDisabled("Global Theme Manager");
    ImGui.Separator();

    string active = _themeManager.ActiveThemeName ?? "Game Default";
    ImGui.Text($"Active: {active}");
    // ... rest of the method ...

    if (_editorVisible)
        RenderEditorWindow();
}
```

**Target `RenderContent()` skeleton:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##sk_content");

    string active = _themeManager.ActiveThemeName ?? "Game Default";
    ImGui.Text($"Active: {active}");
    // ... rest of the method unchanged ...

    SubmodUI.EndContentArea();

    // Editor window is a separate top-level ImGui window — render outside the child
    if (_editorVisible)
        RenderEditorWindow();
}
```

Key points:
- The `ImGui.TextColored(..., "Skittles")` + `ImGui.SameLine()` +
  `ImGui.TextDisabled("Global Theme Manager")` + `ImGui.Separator()` block at the
  top is removed (replaced by the SubmodUI wrapper).
- `RenderEditorWindow()` opens a separate top-level ImGui window with
  `ImGui.Begin(...)` — it must be called **outside** the `SubmodUI.EndContentArea()`
  child window to avoid nesting windows.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##sk_content");` as the first statement in
  `RenderContent()`.
- [ ] Remove the lines:
  ```csharp
  ImGui.TextColored(new float4(0.17f, 0.98f, 0.12f, 1.0f), "Skittles");
  ImGui.SameLine();
  ImGui.TextDisabled("Global Theme Manager");
  ImGui.Separator();
  ```
- [ ] Add `SubmodUI.EndContentArea();` immediately **before** the
  `if (_editorVisible) RenderEditorWindow();` call (not after it), so that the
  editor window is opened outside the SubmodUI child.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 7 — `unladen-swallow.lib` / `UnladenSwallowSubmod`

**File:** `unladen-swallow.lib/UnladenSwallowSubmod.cs`

### What to change

`RenderContent()` is short (~10 active lines). It begins with a branded colored
title and a `SeparatorText` that act as top padding / branding, followed by a
checkbox and a status line.

**Current `RenderContent()`:**

```csharp
public void RenderContent()
{
    ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");
    ImGui.SeparatorText("HTTP RPC Server");

    if (ImGui.Checkbox("Enable HTTP Server##us", ref _serverEnabled))
    { ... }

    if (_server is not null && _server.IsRunning)
        ImGui.TextColored(..., "Server: Running on http://0.0.0.0:7887");
    else
        ImGui.TextDisabled("Server: Stopped");
}
```

**Target `RenderContent()`:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##us_content");

    if (ImGui.Checkbox("Enable HTTP Server##us", ref _serverEnabled))
    { ... }

    if (_server is not null && _server.IsRunning)
        ImGui.TextColored(..., "Server: Running on http://0.0.0.0:7887");
    else
        ImGui.TextDisabled("Server: Stopped");

    SubmodUI.EndContentArea();
}
```

Key points:
- Remove `ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");`.
- Remove `ImGui.SeparatorText("HTTP RPC Server");` — the Grant header already
  identifies this as Unladen Swallow; a redundant sub-header adds clutter.
  If the agent judges that a section label inside the content area adds value
  (e.g. for standalone-mod context), `ImGui.SeparatorText("HTTP RPC Server");`
  may be retained after the `BeginContentArea` call.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##us_content");` as the first statement in
  `RenderContent()`.
- [ ] Remove `ImGui.TextColored(new float4(1.0f, 0.84f, 0.0f, 1.0f), "Unladen Swallow");`.
- [ ] Remove `ImGui.SeparatorText("HTTP RPC Server");` (or keep it after the
  `BeginContentArea` call at the implementing agent's discretion).
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 4).
- [ ] Run `dotnet build` — expect zero errors.

---

## Task 8 — `zippo.lib` / `ZippoSubmod`

**File:** `zippo.lib/ZippoSubmod.cs`

### What to change

`RenderContent()` begins with a colored title + `Separator()` + `Spacing()` block
used as top-padding branding. The rest of the method is well-structured and needs
only the wrapper added around it.

**Current `RenderContent()` opener:**

```csharp
public void RenderContent()
{
    RefreshVehicles();

    ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "Zippo  Light Control");
    ImGui.Separator();
    ImGui.Spacing();

    // Vehicle combobox
    ...
}
```

**Target `RenderContent()` opener/closer:**

```csharp
public void RenderContent()
{
    SubmodUI.BeginContentArea("##zp_content");

    RefreshVehicles();

    // Vehicle combobox
    ...

    SubmodUI.EndContentArea();
}
```

Key points:
- Remove `ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "Zippo  Light Control");`.
- Remove `ImGui.Separator();` and `ImGui.Spacing();` that immediately follow the
  title (they were compensating for missing top padding / section identification).
- `RefreshVehicles()` stays immediately after `BeginContentArea` — it is a data
  refresh, not a padding concern.

### Checklist

- [ ] Add `SubmodUI.BeginContentArea("##zp_content");` as the first statement in
  `RenderContent()`.
- [ ] Remove `ImGui.TextColored(new float4(1.0f, 0.85f, 0.0f, 1.0f), "Zippo  Light Control");`.
- [ ] Remove the `ImGui.Separator();` and `ImGui.Spacing();` immediately following
  the removed title line.
- [ ] Add `SubmodUI.EndContentArea();` as the last statement in `RenderContent()`.
- [ ] Verify `using MeowSci.KsaAbstractions;` is already present (it is — line 5).
- [ ] Run `dotnet build` — expect zero errors.

---

## Verification Checklist (run after all 8 tasks are complete)

- [ ] `dotnet build` from repo root produces zero errors and zero warnings
  introduced by these changes.
- [ ] In the Grant supermod window, each refactored submod's collapsible section
  renders with consistent top, left, and right inset matching the conformant
  submods (Eternal Flame, Blinky, etc.).
- [ ] No submod produces a visible nested child-window artifact or double-scroll.
- [ ] The standalone versions of each mod (not through Grant) are unaffected —
  standalone mods call `RenderContent()` inside their own `ImGui.Begin/End`, and
  the SubmodUI child window nests cleanly inside any parent window.
- [ ] `GForceUI.Render()` (standalone geeforce window) is visually unchanged.
- [ ] The Skittles Theme Editor floating window (`RenderEditorWindow`) still
  opens and functions correctly.

---

## Notes for Implementing Agents

- **ID uniqueness:** The string passed to `SubmodUI.BeginContentArea(id)` is used
  as an ImGui child-window ID. It must be unique within the parent window. The
  `##` prefix hides it from the visible label. The suggested IDs above follow the
  pattern `##<abbrev>_content`.
- **`EndContentArea` position:** Always the very last line of `RenderContent()`,
  except for any top-level `ImGui.Begin/End` blocks (like `RenderEditorWindow`)
  that must live outside the child.
- **Do not nest `BeginContentArea` calls.** Each submod gets exactly one
  `BeginContentArea` / `EndContentArea` pair.
- **Conformant reference files to study before implementing:**
  - `eternal-flame.lib/EternalFlameSubmod.cs` — simple flat layout
  - `blinky.lib/BlinkySubmod.cs` — complex layout with tables and collapsing
    headers inside the content area
  - `con-man.lib/ConManSubmod.cs` — modal/popup interaction inside content area
