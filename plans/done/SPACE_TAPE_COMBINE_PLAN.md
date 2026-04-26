# Space Tape Combine Plan

Combine the `inanimate-carbon-rod` (ICR) mod into `space-tape`, and refactor the Space Tape UI so the Grant submod panel is minimal, SubPart browsing lives in a dedicated floating window tied to the Part Editor, Load/Import is consolidated into a compact 2x2 table of filterable combos, and Save moves into a modal popup.

## Confirmed design decisions

- **ICR removal:** Full delete. Migrate ICR classes into `space-tape.lib`, delete both `inanimate-carbon-rod/` (mod) and `inanimate-carbon-rod.lib/` projects, remove `InanimateCarbonRodSubmod` registration from `grant/Mod.cs`, update `ksa-mod-experiments.slnx`, update `REPOSITORY_INDEX.md`.
- **SubParts window lifecycle:** Auto-opens when Part Editor opens, auto-closes when Part Editor closes. Additionally exposes an independent toggle button (similar to the existing "Editor Window" button) so the user can re-open it while the editor is still active.
- **Save modal semantics:** Use existing [PartModWriter.SavePart](space-tape.lib/PartModWriter.cs) merge semantics (appends/replaces-by-Id within the target file). No behavior change on the I/O side; only the UI wrapping moves into a modal.
- **Large viewer:** Reuse the existing [SubpartViewerWindow](inanimate-carbon-rod.lib/SubpartViewerWindow.cs), migrated into `space-tape.lib`, rendered as a floating window from `SpaceTapeSubmod.RenderFloatingWindows()`.
- **Thumbnail timing:** Opening the editor auto-scans SubParts via reflection (cheap `SubPartCatalog.LoadSubParts()`). GPU thumbnail generation stays manual, initiated by the Load SubParts modal's `Generate` button.

## Baseline files (single source of truth)

Read these before any task. Do NOT paraphrase or invent signatures — copy from source.

- Mods & libs:
  - [space-tape/Mod.cs](space-tape/Mod.cs)
  - [space-tape/Patcher.cs](space-tape/Patcher.cs)
  - [space-tape/space-tape.csproj](space-tape/space-tape.csproj)
  - [space-tape.lib/space-tape.lib.csproj](space-tape.lib/space-tape.lib.csproj)
  - [space-tape.lib/SpaceTapeSubmod.cs](space-tape.lib/SpaceTapeSubmod.cs)
  - [space-tape.lib/SubPartCatalog.cs](space-tape.lib/SubPartCatalog.cs)
  - [space-tape.lib/PartEditorUi.cs](space-tape.lib/PartEditorUi.cs)
  - [space-tape.lib/PartEditorScene.cs](space-tape.lib/PartEditorScene.cs)
  - [space-tape.lib/PartEditorState.cs](space-tape.lib/PartEditorState.cs)
  - [space-tape.lib/PartModWriter.cs](space-tape.lib/PartModWriter.cs)
  - [space-tape.lib/PartCatalog.cs](space-tape.lib/PartCatalog.cs)
  - [space-tape.lib/PartImporter.cs](space-tape.lib/PartImporter.cs)
  - [inanimate-carbon-rod/Mod.cs](inanimate-carbon-rod/Mod.cs)
  - [inanimate-carbon-rod/inanimate-carbon-rod.csproj](inanimate-carbon-rod/inanimate-carbon-rod.csproj)
  - [inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj](inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj)
  - [inanimate-carbon-rod.lib/InanimateCarbonicRodSubmod.cs](inanimate-carbon-rod.lib/InanimateCarbonicRodSubmod.cs)
  - [inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs](inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs)
  - [inanimate-carbon-rod.lib/SubpartThumbnailCache.cs](inanimate-carbon-rod.lib/SubpartThumbnailCache.cs)
  - [inanimate-carbon-rod.lib/SingleSubpartGenerator.cs](inanimate-carbon-rod.lib/SingleSubpartGenerator.cs)
  - [inanimate-carbon-rod.lib/SubpartViewerWindow.cs](inanimate-carbon-rod.lib/SubpartViewerWindow.cs)
- Integration points:
  - [grant/Mod.cs](grant/Mod.cs) lines ~60–82 — submod registration
  - [grant/grant.csproj](grant/grant.csproj) line 27 — ICR project reference
  - [ksa-mod-experiments.slnx](ksa-mod-experiments.slnx) — solution entries
  - [REPOSITORY_INDEX.md](REPOSITORY_INDEX.md) — catalog
- Patterns & conventions:
  - [.github/skills/imgui-design/SKILL.md](.github/skills/imgui-design/SKILL.md) — **all new UI must follow this**
  - [.github/skills/imgui/SKILL.md](.github/skills/imgui/SKILL.md)
  - [.github/skills/mod-impl/SKILL.md](.github/skills/mod-impl/SKILL.md)
  - Filterable combo reference implementations:
    - [garrys-torch.lib/GarrysTorchSubmod.cs](garrys-torch.lib/GarrysTorchSubmod.cs) preset combo (search for `BeginCombo`)
    - Modal popup reference: [garrys-torch.lib/GarrysTorchSubmod.cs](garrys-torch.lib/GarrysTorchSubmod.cs) lines 365–430 (`RenderDeletePresetModal`, `RenderSavePresetModal`)
    - Modal popup reference: [con-man.lib/ConManSubmod.cs](con-man.lib/ConManSubmod.cs) line 238 (`OpenPopup`), line 257 (`BeginPopupModal`)

## High-level task order

Each task is atomic, compiles, and preserves existing functionality between tasks so behavior is never broken. Each task ends with `dotnet build` passing and a conventional commit via the `git-commit` skill.

1. Migrate ICR library code into `space-tape.lib` (behavior-preserving move).
2. Remove `inanimate-carbon-rod` and `inanimate-carbon-rod.lib` projects; update grant, slnx, repo index.
3. Introduce `SubpartGenerationController` static API in `space-tape.lib` (encapsulates generator + config).
4. Simplify Grant submod panel (Load SubParts + Open/Close Part Editor only).
5. Implement Load SubParts modal (Images per SubPart, Image Size, Generate/Re-generate, Close).
6. Build new "SubParts" floating window tied to editor lifecycle with view-subparts toggle.
7. Wire SubpartViewerWindow as floating window owned by `SpaceTapeSubmod`.
8. Consolidate Load/Import into 2x2 filterable combo table (auto-scan on editor open).
9. Add Save button to editor toolbar + Save modal popup (filterable file combo).
10. Remove legacy Save section from editor window.
11. Final pass: README updates, REPOSITORY_INDEX.md, dead-code sweep.

---

## Task 1 — Migrate ICR library classes into `space-tape.lib`

### Goal

Move all reusable thumbnail-generation/viewer classes from `inanimate-carbon-rod.lib` into `space-tape.lib` under namespace `MeowSci.SpaceTapeLib`. Do NOT yet delete `inanimate-carbon-rod/` or `inanimate-carbon-rod.lib/`. Do NOT yet touch grant. This task is a behavior-preserving code move that leaves the ICR mod still functional as a thin shim referencing the new classes through a re-exported namespace alias.

### Why first

ICR and space-tape both reference `SubpartThumbnailCache`. We need one canonical copy in `space-tape.lib`. Keep ICR working mid-refactor to preserve behavior until Task 2 physically removes it.

### Files to move

Physically move (not copy) the following files from `inanimate-carbon-rod.lib/` to `space-tape.lib/Thumbnails/` (new subdirectory):

| Source | Destination | Change |
|--------|-------------|--------|
| `inanimate-carbon-rod.lib/SubpartThumbnailCache.cs` | `space-tape.lib/Thumbnails/SubpartThumbnailCache.cs` | namespace → `MeowSci.SpaceTapeLib` |
| `inanimate-carbon-rod.lib/SubpartThumbnailGenerator.cs` | `space-tape.lib/Thumbnails/SubpartThumbnailGenerator.cs` | namespace → `MeowSci.SpaceTapeLib` |
| `inanimate-carbon-rod.lib/SingleSubpartGenerator.cs` | `space-tape.lib/Thumbnails/SingleSubpartGenerator.cs` | namespace → `MeowSci.SpaceTapeLib` |
| `inanimate-carbon-rod.lib/SubpartViewerWindow.cs` | `space-tape.lib/Thumbnails/SubpartViewerWindow.cs` | namespace → `MeowSci.SpaceTapeLib` |

Do NOT move `InanimateCarbonicRodSubmod.cs` — its UI is being redesigned and integrated into space-tape natively in later tasks.

### csproj changes

1. [space-tape.lib/space-tape.lib.csproj](space-tape.lib/space-tape.lib.csproj) — copy any game assembly references that existed only in [inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj](inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj) and aren't already present (expected additions: `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `Planet.Render.Core`). Compare both files side by side; don't remove anything from space-tape.lib.
2. **Leave** the existing `<ProjectReference Include="..\inanimate-carbon-rod.lib\inanimate-carbon-rod.lib.csproj" />` in `space-tape.lib.csproj` for now — we'll remove it in Task 2 after ICR's own references are cut.
3. [inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj](inanimate-carbon-rod.lib/inanimate-carbon-rod.lib.csproj) — add `<ProjectReference Include="..\space-tape.lib\space-tape.lib.csproj" />` so the moved classes are still reachable from the now-shim `InanimateCarbonicRodSubmod.cs`. This creates a **temporary reverse dependency** that exists only until Task 2 deletes the ICR projects.

### `InanimateCarbonicRodSubmod.cs` edits (temporary)

Add `using MeowSci.SpaceTapeLib;` at the top so it can still resolve `SubpartThumbnailGenerator`, `SubpartThumbnailCache`, `SubpartViewerWindow`. Delete any stale `using MeowSci.InanimateCarbonRodLib;` it might have on moved types. The submod should still compile and behave identically — it is the F10 ICR window + grant collapsing header.

### Update all consumers

1. [space-tape.lib/SubPartCatalog.cs](space-tape.lib/SubPartCatalog.cs) line 9: remove `using MeowSci.InanimateCarbonRodLib;` (no longer needed since `SubpartThumbnailCache` lives in the same namespace now).
2. [inanimate-carbon-rod.lib/InanimateCarbonicRodSubmod.cs](inanimate-carbon-rod.lib/InanimateCarbonicRodSubmod.cs) line 9: change `namespace MeowSci.InanimateCarbonRodLib;` stays BUT add `using MeowSci.SpaceTapeLib;` for the moved types.
3. Any other reference to `MeowSci.InanimateCarbonRodLib.Subpart*` anywhere in the workspace: replace with `MeowSci.SpaceTapeLib`. Run `grep_search` for `MeowSci.InanimateCarbonRodLib` to find them all.

### Acceptance

- `dotnet build` clean.
- Running the game: ICR F10 window still works; grant panel still shows "Inanimate Carbon Rod" submod with Generator UI; space-tape still uses animated thumbnails.
- Commit: `refactor(space-tape): move thumbnail generator/cache/viewer from icr into space-tape.lib`.

---

## Task 2 — Delete `inanimate-carbon-rod` and `inanimate-carbon-rod.lib` projects

### Goal

Remove ICR entirely from the repo and workspace. After this task, only `space-tape.lib` owns thumbnail-related code.

### Steps

1. Remove from [grant/Mod.cs](grant/Mod.cs):
   - Line ~72: delete `_submods.Add(new InanimateCarbonRodSubmod());`
   - Delete the `using MeowSci.InanimateCarbonRodLib;` statement near the top of the file.
2. Remove from [grant/grant.csproj](grant/grant.csproj):
   - Line 27: delete `<ProjectReference Include="..\inanimate-carbon-rod.lib\inanimate-carbon-rod.lib.csproj" />`.
3. Remove from [space-tape.lib/space-tape.lib.csproj](space-tape.lib/space-tape.lib.csproj):
   - Delete `<ProjectReference Include="..\inanimate-carbon-rod.lib\inanimate-carbon-rod.lib.csproj" />`.
4. Delete directories (user confirmed full delete):
   - `inanimate-carbon-rod/` (the entire folder).
   - `inanimate-carbon-rod.lib/` (the entire folder).
5. Remove from [ksa-mod-experiments.slnx](ksa-mod-experiments.slnx): find any `<Project>` entries pointing at either deleted folder and remove.
6. [REPOSITORY_INDEX.md](REPOSITORY_INDEX.md): remove the ICR rows/sections. Add a note under space-tape: "Owns SubPart thumbnail generation (migrated from inanimate-carbon-rod)."
7. [space-tape/README.md](space-tape/README.md): add a Thumbnail Generation section (high-level, user-facing).

### Verification grep checklist

Before committing, run these searches and resolve any hits (should be zero after the task):

- `InanimateCarbonRod` in any `.cs` or `.csproj` → 0 results.
- `inanimate-carbon-rod` in any file except memory/skills/docs/archives → 0 results.
- `MeowSci.InanimateCarbonRodLib` anywhere → 0 results.

### Acceptance

- `dotnet build` clean.
- Grant main window no longer lists "Inanimate Carbon Rod" submod.
- Space Tape still works (animated thumbnails still visible after clicking Load SubParts).
- Commit: `refactor(icr): remove inanimate-carbon-rod mod and lib (functionality merged into space-tape)`.

---

## Task 3 — Introduce `SubpartGenerationController` static facade in `space-tape.lib`

### Goal

Give Space Tape a single, process-wide controller encapsulating the thumbnail generator plus its user-visible configuration (Images per SubPart, Image Size index). The Load SubParts modal, the submod lifecycle, and the SubParts window will all read/drive this controller.

### Rationale

The existing `SubpartThumbnailGenerator` is a plain instance class that needs per-frame `Update()` calls and knowledge of UI state (e.g. "has generation ever completed at least once?" — drives the "Generate" vs "Re-generate" button label). Centralizing this avoids sprinkling state across UI classes.

### New file: `space-tape.lib/Thumbnails/SubpartGenerationController.cs`

```csharp
using System;

namespace MeowSci.SpaceTapeLib;

/// <summary>
/// Process-wide facade around SubpartThumbnailGenerator for Space Tape UI.
/// Owns the generator instance, user-facing settings, and "has ever generated"
/// flag used by the Load SubParts modal to switch Generate/Re-generate labels.
/// Instantiated once by SpaceTapeSubmod; driven per-frame via Update(double).
/// </summary>
public sealed class SubpartGenerationController : IDisposable
{
    public static readonly int[] ImageSizes      = { 64, 128, 256, 512, 1024 };
    public static readonly string[] ImageSizeLabels = { "64", "128", "256", "512", "1024" };

    private readonly SubpartThumbnailGenerator _generator = new();

    public int ViewCount { get; set; } = 32;
    public int ImageSizeIndex { get; set; } = 1; // 128 by default
    public bool HasGeneratedAtLeastOnce { get; private set; }

    public GenerationState State => _generator.State;
    public int ProgressCurrent   => _generator.ProgressCurrent;
    public int ProgressTotal     => _generator.ProgressTotal;
    public string? LastError     => _generator.LastError;

    public bool IsBusy => _generator.State == GenerationState.Generating;

    public void Update() => _generator.Update();

    public void Generate()
    {
        _generator.ViewCount          = ViewCount;
        _generator.ThumbnailImageSize = ImageSizes[ImageSizeIndex];
        _generator.GenerateAll();
    }

    /// <summary>Clears cache + resets generator so a Re-generate starts from scratch.</summary>
    public void Reset() => _generator.Reset();

    internal void MarkGeneratedOnce() => HasGeneratedAtLeastOnce = true;

    public void Dispose() => _generator.Dispose();
}
```

Additionally: detect completion transitions in `Update()` to set `HasGeneratedAtLeastOnce`. Use a stored previous-state field inside the controller:

```csharp
private GenerationState _lastObservedState = GenerationState.Idle;
public void Update()
{
    _generator.Update();
    if (_generator.State == GenerationState.Done && _lastObservedState != GenerationState.Done)
        HasGeneratedAtLeastOnce = true;
    _lastObservedState = _generator.State;
}
```

### Integration

In [SpaceTapeSubmod.cs](space-tape.lib/SpaceTapeSubmod.cs):

1. Add field `private readonly SubpartGenerationController _generation = new();`.
2. In `Update(double dt)` call `_generation.Update();` **before** `_catalog.Update(dt);`.
3. In `Dispose()` call `_generation.Dispose();`.
4. Expose the controller to UI via constructor parameters passed down to the Load SubParts modal renderer (Task 5) and the SubParts window (Task 6).

### Acceptance

- Compiles.
- Running the game: nothing UI-visible yet; this is pure plumbing.
- Commit: `feat(space-tape): add SubpartGenerationController facade`.

---

## Task 4 — Simplify the Grant submod panel

### Goal

Reduce `SpaceTapeSubmod.RenderContentInner()` / the existing `SubPartCatalog` render block to just two buttons:

- `Load SubParts` — opens the Load SubParts modal (Task 5).
- `Open Part Editor` when editor is inactive; `Close Part Editor` when active.

Remove the old thumb-size slider, anim delay, filter text, grid display, selection handling, and the `Editor Window` button from the grant panel. All SubPart browsing moves to the dedicated floating window (Task 6).

### Files to edit

- [space-tape.lib/SpaceTapeSubmod.cs](space-tape.lib/SpaceTapeSubmod.cs) `RenderContentInner()` (~line 83).
- [space-tape.lib/SubPartCatalog.cs](space-tape.lib/SubPartCatalog.cs) — split responsibilities (see below).

### `SubPartCatalog.cs` refactor

Current `SubPartCatalog` mixes three responsibilities: (1) loading the SubPart list via reflection; (2) rendering the control panel + grid; (3) tracking selection. After this task:

- **Keep** `LoadSubParts()`, `_subparts`, `_animTimer`, `Update(dt)`, and a new pure-data accessor `IReadOnlyList<PartTemplate>? SubParts => _subparts;`
- **Keep** `SelectedSubPartId` + `TakeSelectedSubPartId()`.
- **Remove** everything inside `Render(...)` related to the control panel and grid. The grid moves to the new `SubPartsWindow` class in Task 6. Delete the whole `Render()` method (and `_thumbDisplaySize`, `_animTickMs`, `_filter`, `_filtered`, `_registeredViews` — these relocate to the new window class).

After refactor, `SubPartCatalog` becomes a thin catalog-and-selection holder.

### `SpaceTapeSubmod.RenderContentInner()` new body

Replace the current body (roughly):

```csharp
SubmodUI.BeginContentArea("##space_tape_content");
```

with a two-row layout using the preferred 2-column proportional pattern from the imgui-design skill. Target layout:

```
[ Load SubParts ]
[ Open Part Editor ]  (or [ Close Part Editor ] when scene.IsActive)
```

Each button full-width via `new float2(-1, 0)`. Implementation sketch:

```csharp
private void RenderContentInner()
{
    // Load SubParts modal trigger
    if (ImGui.Button(" Load SubParts ##st_load_modal", new float2(-1, 0)))
        ImGui.OpenPopup("Load SubParts##st_load_popup");

    // Render the modal body (Task 5)
    _loadSubPartsModal.Render(_generation);

    ImGui.Spacing();

    // Editor open/close
    if (_scene.IsActive)
    {
        if (ImGui.Button(" Close Part Editor ##st_editor_close", new float2(-1, 0)))
            CloseEditor();
    }
    else
    {
        if (ImGui.Button(" Open Part Editor ##st_editor_open", new float2(-1, 0)))
            OpenEditor();
    }
}
```

`OpenEditor()` and `CloseEditor()` are new helpers on `SpaceTapeSubmod`:

```csharp
private void OpenEditor()
{
    _scene.Enter();
    if (_scene.IsActive)
    {
        _catalog.LoadSubParts();      // auto-scan SubParts (cheap)
        _ui.WindowOpen = true;         // show editor window
        _subPartsWindow.IsOpen = true; // show SubParts window (Task 6)
        // Auto-load saved + stock parts for the Load/Import combos (Task 8)
        _ui.AutoLoadSavedAndStockParts(_writer);
    }
}

private void CloseEditor()
{
    _scene.Exit();
    _ui.WindowOpen = false;
    _subPartsWindow.IsOpen = false;
}
```

### Also handle selection dispatch (still needed)

Keep the "selected subpart → AddSubPart to editor" flow. Move it to after the submod's per-frame update OR invoke it right after the window renders in `RenderFloatingWindows` (Task 6). A single canonical spot:

```csharp
// In Update(double dt) AFTER _generation.Update() and _catalog.Update(dt):
string? selected = _catalog.TakeSelectedSubPartId();
if (selected != null && _scene.IsActive && !_subPartsWindow.ViewSubPartsMode)
{
    _controller.AddSubPart(selected);
    _scene.SyncParts(_controller.CurrentPart);
}
else if (selected != null && _subPartsWindow.ViewSubPartsMode)
{
    // Handled in Task 7 — open SubpartViewerWindow
}
```

### Acceptance

- Grant panel shows only the two buttons.
- Clicking Open Part Editor opens the editor window AND the SubParts window (once Task 6 ships).
- `dotnet build` clean.
- Commit: `refactor(space-tape): minimize grant submod panel to Load SubParts + Open Editor`.

---

## Task 5 — Load SubParts modal

### Goal

A modal popup (triggered by the "Load SubParts" button) replicating the Generator section of the old ICR submod:

- `Images per SubPart` integer input (range 2–32).
- `Image Size` combo (64 / 128 / 256 / 512 / 1024).
- `Generate` button → becomes `Re-generate` once `_generation.HasGeneratedAtLeastOnce` is true.
- `Close` button.
- While generating: disable Generate and Close buttons; show a progress bar with `current/total` text and disable the two setting widgets.
- Status text below: green "Done (N subparts)" / red "Failed: {msg}" / gray "Ready to generate".

Modal sizing: `ImGuiWindowFlags.AlwaysAutoResize`.

### New file: `space-tape.lib/LoadSubPartsModal.cs`

```csharp
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

public sealed class LoadSubPartsModal
{
    public const string PopupId = "Load SubParts##st_load_popup";

    public void Render(SubpartGenerationController gen)
    {
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        bool busy = gen.IsBusy;

        // Settings table: 2-col SizingFixedFit | Stretch
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
        if (ImGui.BeginTable("##st_load_tbl", 2,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, 160f);
            ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch);

            // Row 1: Images per SubPart (?)
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Images per SubPart");
            ImGui.SameLine(); ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);
                ImGui.TextWrapped("Generating subpart thumbnails is GPU-intensive. " +
                    "Reduce this and Image Size on lower-end hardware.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            ImGui.TableNextColumn();
            if (busy) ImGui.BeginDisabled();
            int views = gen.ViewCount;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("##st_load_views", ref views, 0.1f, 2, 32))
                gen.ViewCount = views;
            if (busy) ImGui.EndDisabled();

            // Row 2: Image Size (?)
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding(); ImGui.Text("Image Size");
            ImGui.SameLine(); ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetItemTooltip("Higher resolution = sharper thumbs, more VRAM.");
            ImGui.TableNextColumn();
            if (busy) ImGui.BeginDisabled();
            int sizeIdx = gen.ImageSizeIndex;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##st_load_imgsize", ref sizeIdx,
                    SubpartGenerationController.ImageSizeLabels,
                    SubpartGenerationController.ImageSizeLabels.Length))
                gen.ImageSizeIndex = sizeIdx;
            if (busy) ImGui.EndDisabled();

            ImGui.EndTable();
        }
        ImGui.PopStyleVar();

        ImGui.Spacing();

        // Progress bar OR status line
        if (busy && gen.ProgressTotal > 0)
        {
            float progress = (float)gen.ProgressCurrent / gen.ProgressTotal;
            ImGui.ProgressBar(progress, new float2(-1, 0),
                $"{gen.ProgressCurrent}/{gen.ProgressTotal}");
        }
        else
        {
            string status = gen.State switch
            {
                GenerationState.Done   => $"Done ({SubpartThumbnailCache.All.Count} subparts)",
                GenerationState.Failed => $"Failed: {gen.LastError}",
                _                      => "Ready to generate"
            };
            float4 color = gen.State switch
            {
                GenerationState.Done   => new float4(0.3f, 1f, 0.3f, 1f),
                GenerationState.Failed => new float4(1f, 0.3f, 0.3f, 1f),
                _                      => new float4(0.7f, 0.7f, 0.7f, 1f)
            };
            ImGui.TextColored(color, status);
        }

        ImGui.Spacing();

        // Buttons row
        string genLabel = gen.HasGeneratedAtLeastOnce ? " Re-generate ##st_load_gen" : " Generate ##st_load_gen";
        if (busy) ImGui.BeginDisabled();
        if (ImGui.Button(genLabel))
        {
            if (gen.HasGeneratedAtLeastOnce) gen.Reset();
            gen.Generate();
        }
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Close ##st_load_close"))
            ImGui.CloseCurrentPopup();
        if (busy) ImGui.EndDisabled();

        ImGui.EndPopup();
    }
}
```

### Plumb into `SpaceTapeSubmod`

- Field: `private readonly LoadSubPartsModal _loadSubPartsModal = new();`
- `RenderContentInner()`: after `ImGui.OpenPopup(LoadSubPartsModal.PopupId)`, call `_loadSubPartsModal.Render(_generation);` unconditionally each frame (ImGui rule: `BeginPopupModal` must be called every frame while the modal is open).

### Acceptance

- Clicking Load SubParts opens the modal. Clicking Generate kicks off thumbnail generation (observable via progress bar).
- Close dismisses the modal; if user closes mid-generation they can re-open and still see progress.
- After one successful generation, button reads "Re-generate".
- Commit: `feat(space-tape): load subparts modal with generator controls`.

---

## Task 6 — New "SubParts" floating window

### Goal

A new floating window titled `SubParts##st_subparts_window` that owns the control panel + thumbnail grid formerly in the grant panel. Lifecycle coupled to the Part Editor (Task 4 `OpenEditor`/`CloseEditor`).

### UI layout (follow imgui-design skill)

All within the floating window body:

1. 2-col proportional table (`SizingStretchProp`, 1f:3f):
   - Row 1: `Thumb Size` | `DragFloat 32..256`.
   - Row 2: `Anim Delay` | `DragInt 16..500 ms`.
   - Row 3: `Filter` | `InputText`.
2. `ImGui.Checkbox(" View SubParts ##st_sp_view", ref _viewSubPartsMode);` (default **false**). Tooltip: "When checked, clicking a thumbnail opens the full viewer instead of adding the part to the editor."
3. `ImGui.Spacing(); ImGui.SeparatorText($"SubParts ({filteredCount})");`
4. Scrollable child with virtual rendering, copied verbatim from the current `SubPartCatalog.Render` grid block (lines ~140–285 of [SubPartCatalog.cs](space-tape.lib/SubPartCatalog.cs)). Keep selection-highlight color `new float4(0.2f, 0.6f, 1f, 0.8f)`, the `_registeredViews` descriptor cleanup, and the fallback chain (animated → static `template.Thumbnail` → text button).

### New file: `space-tape.lib/SubPartsWindow.cs`

Skeleton:

```csharp
using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Thumbnails;

namespace MeowSci.SpaceTapeLib;

public sealed class SubPartsWindow
{
    public bool IsOpen { get; set; }
    public bool ViewSubPartsMode { get; private set; }

    private float _thumbDisplaySize = 128f;
    private int _animTickMs = 100;
    private double _animTimer;
    private readonly ImInputString _filter = new(256);
    private readonly List<PartTemplate> _filtered = new();
    private readonly HashSet<ThumbnailReference> _registeredViews = new();

    public void Update(double dt) => _animTimer += dt;

    public void Render(SubPartCatalog catalog)
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new float2(520, 520), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin("SubParts##st_subparts_window", ref open))
        {
            RenderControls();
            ImGui.Spacing();
            RenderGrid(catalog);
        }
        ImGui.End();
        IsOpen = open;
    }

    // RenderControls + RenderGrid implementations — paste + adapt from current SubPartCatalog
}
```

Port the grid code **byte-for-byte** from the current `SubPartCatalog.Render()` (lines ~140–285) except:

- Replace `_subparts`/`_filter` with calls into the `SubPartCatalog` argument (use a new public accessor `IReadOnlyList<PartTemplate>? Parts => _subparts;` on `SubPartCatalog`).
- Item IDs: change `##st_cat_` prefix to `##st_sp_` so ImGui IDs remain unique if both systems coexist during incremental migration.
- On click:
  ```csharp
  if (clicked)
      catalog.SetSelectedSubPartId(template.Id);
  ```
  where `SetSelectedSubPartId` is a new helper on `SubPartCatalog` replacing direct field assignment.

### Integrate in `SpaceTapeSubmod`

- Field: `private readonly SubPartsWindow _subPartsWindow = new();`
- `Update(double dt)` — add `_subPartsWindow.Update(dt);`
- `RenderFloatingWindows()` — add `_subPartsWindow.Render(_catalog);`
- `OpenEditor`/`CloseEditor` already toggle `_subPartsWindow.IsOpen` from Task 4.

### Dispatch logic change

From Task 4 the per-frame selection handler must distinguish modes:

```csharp
string? selected = _catalog.TakeSelectedSubPartId();
if (selected != null)
{
    if (_subPartsWindow.ViewSubPartsMode)
        _subpartViewer.OpenFor(selected); // wired in Task 7
    else if (_scene.IsActive)
    {
        _controller.AddSubPart(selected);
        _scene.SyncParts(_controller.CurrentPart);
    }
}
```

### Acceptance

- Opening the Part Editor also opens a separate "SubParts" window with the thumbnail grid.
- Clicking a thumb in non-view mode adds a subpart to the editor.
- Closing the editor closes the SubParts window.
- Commit: `feat(space-tape): extract SubParts grid into dedicated floating window`.

---

## Task 7 — Wire `SubpartViewerWindow` (large image viewer) into Space Tape

### Goal

When `View SubParts` is checked in the SubParts window and the user clicks a thumbnail, open the migrated [SubpartViewerWindow](space-tape.lib/Thumbnails/SubpartViewerWindow.cs) for that subpart. The viewer is rendered as a floating window owned by `SpaceTapeSubmod` (not the SubParts window, so it survives closure).

### Add a small adapter in `SpaceTapeSubmod`

Pick a sensible default image size that matches what's in cache — use `SubpartGenerationController.ImageSizes[_generation.ImageSizeIndex]` as the `imageSize` arg.

```csharp
private readonly SubpartViewerWindow _subpartViewer = new();

public void Update(double dt)
{
    _generation.Update();
    _catalog.Update(dt);
    _subPartsWindow.Update(dt);
    _subpartViewer.Update(dt);

    // Selection dispatch (moved out of RenderContentInner)
    string? selected = _catalog.TakeSelectedSubPartId();
    if (selected != null)
    {
        if (_subPartsWindow.ViewSubPartsMode)
        {
            var entry = SubpartThumbnailCache.Get(selected);
            if (entry != null)
                _subpartViewer.Open(selected, entry,
                    SubpartGenerationController.ImageSizes[_generation.ImageSizeIndex]);
        }
        else if (_scene.IsActive)
        {
            _controller.AddSubPart(selected);
            _scene.SyncParts(_controller.CurrentPart);
        }
    }
}

public void RenderFloatingWindows()
{
    _ui.RenderEditorWindow(_controller, _scene, _gizmos, _interaction, _catalog, _writer, _cameraSnap, _lighting);
    _subPartsWindow.Render(_catalog);
    _subpartViewer.Render();
}

public void Dispose()
{
    // ... existing ...
    _subpartViewer.Dispose();
    _generation.Dispose();
}
```

### Acceptance

- With View SubParts checked, clicking a thumb opens a "Subpart Viewer" floating window showing the hi-res viewer.
- Unchecking View SubParts restores click-to-add behavior.
- Commit: `feat(space-tape): open large viewer from SubParts window when view mode enabled`.

---

## Task 8 — Consolidate Load/Import into 2x2 filterable combo table

### Goal

Replace the current [PartEditorUi.RenderLoadImportSection](space-tape.lib/PartEditorUi.cs#L459-L615) (separate SeparatorText sections, external filter inputs, two combos, separate Refresh/Load buttons) with a compact 2x2 table of filterable combos:

```
Custom Parts | [filterable combo]
Stock  Parts | [filterable combo]
```

A single `Import` button sits on the line below the table. Both lists auto-refresh when the editor is opened (called from `OpenEditor` via the `AutoLoadSavedAndStockParts` helper introduced in Task 4).

### Rewrite plan for `RenderLoadImportSection`

Exact target layout, following the imgui-design skill:

```csharp
private void RenderLoadImportSection(PartEditorController controller, PartEditorScene scene, PartModWriter writer)
{
    if (!ImGui.CollapsingHeader("Load / Import##st_loadimport", ImGuiTreeNodeFlags.DefaultOpen))
        return;

    ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new float2(6f, 6f));
    var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX;
    if (ImGui.BeginTable("##st_li_tbl", 2, flags))
    {
        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##widget", ImGuiTableColumnFlags.WidthStretch, 3f);

        // Row 1: Custom Parts
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Custom Parts");
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        RenderCustomPartsCombo();

        // Row 2: Stock Parts
        ImGui.TableNextRow();
        ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding(); ImGui.Text("Stock Parts");
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        RenderStockPartsCombo();

        ImGui.EndTable();
    }
    ImGui.PopStyleVar();

    ImGui.Spacing();

    bool canImport = _selectedSavedPartIndex >= 0 || _selectedGamePartIndex >= 0;
    if (!canImport) ImGui.BeginDisabled();
    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new float4(0.1f, 0.5f, 0.7f, 1f)));
    if (ImGui.Button(" Import ##st_li_import"))
        DoImport(controller, scene, writer);
    ImGui.PopStyleColor();
    if (!canImport) ImGui.EndDisabled();

    if (_loadImportStatusMessage != null)
    {
        ImGui.Spacing();
        ImGui.TextColored(_loadImportStatusColor, _loadImportStatusMessage);
    }
}
```

### Filterable combo pattern (INSIDE the combo per imgui-design skill)

`RenderCustomPartsCombo()` and `RenderStockPartsCombo()` both use the imgui-design convention: filter input **inside** the combo, auto-focus on open. Pattern (copy for both):

```csharp
private void RenderCustomPartsCombo()
{
    string preview = _selectedSavedPartIndex >= 0 && _selectedSavedPartIndex < _savedParts.Count
        ? $"{_savedParts[_selectedSavedPartIndex].partId}  [{_savedParts[_selectedSavedPartIndex].fileName}]"
        : _savedParts.Count == 0 ? "(no custom parts)" : "(select one)";

    if (ImGui.BeginCombo("##st_li_custom", preview))
    {
        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
            _loadFilter.Clear();
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##st_li_custom_filter", _loadFilter);

        string filterText = _loadFilter.ToString().Trim();
        for (int i = 0; i < _savedParts.Count; i++)
        {
            var (pId, fName) = _savedParts[i];
            if (filterText.Length > 0
                && !pId.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                && !fName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                continue;

            bool sel = i == _selectedSavedPartIndex;
            if (ImGui.Selectable($"{pId}  [{fName}]##st_li_c{i}", sel))
            {
                _selectedSavedPartIndex = i;
                _selectedGamePartIndex = -1;
            }
            if (sel) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }
}
```

Mirror the same structure for `RenderStockPartsCombo()` using `_gameParts.Parts` / `_gamePartFilter` / `_selectedGamePartIndex`.

### `DoImport` method

Lift the import body (currently an inline `if/else if` block) into its own method matching the current logic:

- `hasSavedSel` → `writer.LoadPart`, `controller.LoadPart`, `scene.SyncParts`, set file name and status.
- `hasGameSel` → `PartImporter.ImportFromTemplate`, etc.

Copy from lines ~622–690 of [PartEditorUi.cs](space-tape.lib/PartEditorUi.cs) verbatim (only the imports/body inside the `if (ImGui.Button(" Import ##st_loadimport_btn"))` block).

### Auto-load helper

New public method on `PartEditorUi`, called from `SpaceTapeSubmod.OpenEditor`:

```csharp
public void AutoLoadSavedAndStockParts(PartModWriter writer)
{
    writer.RefreshFileList();
    _savedParts = writer.ListSavedParts();
    _selectedSavedPartIndex = -1;

    _gameParts.Load();
    _selectedGamePartIndex = -1;
}
```

### Remove now-dead fields and methods

Delete these no-longer-used fields from `PartEditorUi`:

- `_filteredSavedPartIndices` and `_filteredGamePartIndices` (filtering now happens inline inside each combo).
- None of the `Refresh`/`Load Game Parts` helpers (now subsumed by auto-load).

### Acceptance

- Opening the Part Editor populates both combos.
- Filtering inside each combo narrows the visible list; selecting one clears the other.
- Import button disabled until a selection is made; imports correctly for both sources.
- Commit: `refactor(space-tape): consolidate load/import into 2x2 filterable combo table`.

---

## Task 9 — Save button in toolbar + Save modal

### Goal

- Add a `Save` button at the very start of the editor toolbar (before Undo).
- Clicking it opens a modal popup `Save Part##st_save_popup`.
- Modal refreshes the file list on open, shows a filterable combo of existing files + `(new file)` entry.
- If `(new file)` is selected, show a `File:` label + text input for the new filename. Otherwise hide the input (editing an existing file reuses its name).
- Modal buttons: `Save` (performs `PartModWriter.SavePart`, closes on success) and `Cancel`.

### Files to edit

- [space-tape.lib/PartEditorUi.cs](space-tape.lib/PartEditorUi.cs) `RenderToolbar` (~line 108) — insert Save button before Undo.
- New class `SavePartModal` in `space-tape.lib/SavePartModal.cs`.

### Toolbar insertion

In `RenderToolbar`, at the start of the method body (before the Undo block):

```csharp
bool canSave = controller.CurrentPart.Placements.Count > 0
               && !string.IsNullOrWhiteSpace(controller.CurrentPart.PartId)
               && !string.IsNullOrWhiteSpace(controller.CurrentPart.GameData.DisplayName);

if (!canSave) ImGui.BeginDisabled();
if (ImGui.Button(" Save "))
{
    writer.RefreshFileList();
    _savePartModal.OnOpen(writer);
    ImGui.OpenPopup(SavePartModal.PopupId);
}
if (!canSave) ImGui.EndDisabled();
if (!canSave && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
    ImGui.SetItemTooltip("Add at least one SubPart, a Part ID, and a Display Name to save.");
ImGui.SameLine();
// existing Undo/Redo/New Part follow
```

Thread `PartModWriter writer` into `RenderToolbar` (it's already in `RenderEditorWindow` signature at line 84) — update the method signature and the call site accordingly.

Also render the modal body within the editor window scope once per frame:

```csharp
_savePartModal.Render(controller, writer, OnSaveSuccess: () =>
{
    controller.MarkSaved();
    _saveStatusMessage = "Saved!";
    _saveStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
});
```

### New file: `space-tape.lib/SavePartModal.cs`

```csharp
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace MeowSci.SpaceTapeLib;

public sealed class SavePartModal
{
    public const string PopupId = "Save Part##st_save_popup";

    private readonly ImInputString _newFileNameInput = new(128);
    private readonly ImInputString _filter = new(128);
    private int _selectedFileIndex = -1;   // -1 => "(new file)"
    private string? _lastStatusMessage;
    private float4 _lastStatusColor;

    /// <summary>Called immediately before OpenPopup to initialize state.</summary>
    public void OnOpen(PartModWriter writer)
    {
        _selectedFileIndex = writer.ExistingFiles.Count > 0
            ? writer.ExistingFiles.IndexOf(writer.CurrentFileName)
            : -1;
        _newFileNameInput.SetValue(writer.CurrentFileName.AsSpan());
        _filter.Clear();
        _lastStatusMessage = null;
    }

    public void Render(PartEditorController controller, PartModWriter writer, Action? onSaveSuccess = null)
    {
        bool open = true;
        if (!ImGui.BeginPopupModal(PopupId, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        RenderFileCombo(writer);

        bool isNewFile = _selectedFileIndex < 0;
        if (isNewFile)
        {
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding(); ImGui.Text("File:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##st_save_newname", _newFileNameInput))
            {
                // nothing special; just captures user input
            }
        }

        ImGui.Spacing();

        // Save / Cancel
        string fileName = isNewFile
            ? _newFileNameInput.ToString().Trim()
            : writer.ExistingFiles[_selectedFileIndex];
        bool canSave = !string.IsNullOrWhiteSpace(fileName);

        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button(" Save ##st_save_confirm"))
        {
            writer.CurrentFileName = fileName;
            bool ok = writer.SavePart(controller.CurrentPart);
            if (ok)
            {
                _lastStatusMessage = $"Saved to {fileName}.xml";
                _lastStatusColor = new float4(0.3f, 1f, 0.3f, 1f);
                onSaveSuccess?.Invoke();
                ImGui.CloseCurrentPopup();
            }
            else
            {
                _lastStatusMessage = $"Save failed: {writer.LastError}";
                _lastStatusColor = new float4(1f, 0.3f, 0.3f, 1f);
            }
        }
        if (!canSave) ImGui.EndDisabled();
        ImGui.SameLine(0, 8);
        if (ImGui.Button(" Cancel ##st_save_cancel"))
            ImGui.CloseCurrentPopup();

        if (_lastStatusMessage != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(_lastStatusColor, _lastStatusMessage);
        }

        ImGui.EndPopup();
    }

    private void RenderFileCombo(PartModWriter writer)
    {
        string preview = _selectedFileIndex < 0 ? "(new file)" : writer.ExistingFiles[_selectedFileIndex];
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##st_save_combo", preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
                _filter.Clear();
            }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##st_save_combo_filter", _filter);

            string filterText = _filter.ToString().Trim();

            // (new file) entry — always shown unless filter excludes
            if (filterText.Length == 0 ||
                "(new file)".Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                "new".Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                bool sel = _selectedFileIndex < 0;
                if (ImGui.Selectable("(new file)##st_save_newf", sel))
                    _selectedFileIndex = -1;
                if (sel) ImGui.SetItemDefaultFocus();
            }

            for (int i = 0; i < writer.ExistingFiles.Count; i++)
            {
                string name = writer.ExistingFiles[i];
                if (filterText.Length > 0 && !name.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool sel = i == _selectedFileIndex;
                if (ImGui.Selectable($"{name}##st_save_f{i}", sel))
                    _selectedFileIndex = i;
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
}
```

### Acceptance

- Save button appears as the leftmost toolbar button; disabled with tooltip until conditions met.
- Clicking opens the modal; existing files are selectable + filterable; `(new file)` shows the filename text input.
- Save writes correctly and closes the modal on success; failure keeps modal open with red error.
- Cancel dismisses without side effects.
- Commit: `feat(space-tape): save button + modal popup in part editor toolbar`.

---

## Task 10 — Remove legacy Save section

### Goal

Delete `RenderSaveSection` in [PartEditorUi.cs](space-tape.lib/PartEditorUi.cs) and its call site in `RenderEditorWindow`. Remove the old `RenderFilePicker` from [PartModWriter.cs](space-tape.lib/PartModWriter.cs) (~line 271) since it's superseded by `SavePartModal`.

### Experimental hot-reload preservation

The old `RenderSaveSection` contained an `Experimental` block with a `Test Hot-Reload` button wired to [HotReloadSpike](space-tape.lib/HotReloadSpike.cs). Preserve this as a new collapsed section at the bottom of the editor window (after `RenderGameDataSection`):

```csharp
private void RenderExperimentalSection(PartEditorController controller)
{
    if (!ImGui.CollapsingHeader("Experimental##st_exp")) return;
    if (ImGui.Button(" Test Hot-Reload ##st_hotreload"))
    {
        var (success, message) = HotReloadSpike.TryRegisterPart(controller.CurrentPart);
        _hotReloadSuccess = success;
        _hotReloadMessage = message + (success ? " (Verified in ModLibrary)" : "");
    }
    if (_hotReloadMessage != null)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(
            _hotReloadSuccess ? new float4(0.3f, 1f, 0.3f, 1f) : new float4(1f, 0.5f, 0.3f, 1f)));
        ImGui.TextWrapped(_hotReloadMessage);
        ImGui.PopStyleColor();
    }
}
```

Keep the existing `_hotReloadMessage`, `_hotReloadSuccess` fields.

### Also remove from `PartEditorUi`

- `_saveStatusMessage`, `_saveStatusColor` fields (now owned by `SavePartModal`, rendered inside it).
  - Exception: the toolbar Save path wires `onSaveSuccess` to set `_saveStatusMessage` on `PartEditorUi` so the editor window can show a brief "Saved!" indicator next to the Save button. Keep these two fields if you want that behavior (recommended).

### Acceptance

- Editor window no longer has a Save section at the bottom.
- `Experimental` section remains but collapsed by default.
- `PartModWriter.RenderFilePicker` is gone (inline combo in `SavePartModal` replaces it).
- Commit: `refactor(space-tape): remove legacy save section from editor window`.

---

## Task 11 — Docs & final sweep

### Goal

Bring README/REPOSITORY_INDEX in sync with the new reality.

### Edits

- [REPOSITORY_INDEX.md](REPOSITORY_INDEX.md) — remove any ICR rows; expand the space-tape entry to describe new capabilities (thumbnail generation, subparts window, save/load modal).
- [space-tape/README.md](space-tape/README.md) — add user-facing sections:
  - "Grant submod panel" (two buttons).
  - "Load SubParts modal" (Images per SubPart / Image Size / Generate).
  - "SubParts window" (filter, anim, view-subparts toggle).
  - "Part Editor" (unchanged except the new Save button in toolbar).
  - "Saving parts" (modal flow).
- Check all `.github/skills/*` and memory for stale ICR references — none expected but grep to confirm.

### Final verification

- `dotnet build` clean across the solution.
- Smoke test in game: open grant panel, click Load SubParts, generate, close; click Open Part Editor; verify SubParts window appears; toggle View SubParts and click a thumb (verify viewer); click Import to load a stock part; edit then click Save in the toolbar; verify modal save flow to both new and existing files.
- Commit: `docs(space-tape): update readme and repository index after icr merge`.

---

## Risk register

| Risk | Mitigation |
|------|-----------|
| Moving `SubpartThumbnailGenerator.cs` drops a private Vulkan resource pool reference | The class is fully self-contained per the analysis — no static state aside from `_commandPool` fields on the instance. Namespace change only. |
| `SubpartThumbnailCache.Store` is `internal` and must remain reachable from `SubpartThumbnailGenerator` after the move | Both classes end up in the same assembly (`space-tape.lib`) and same namespace, so `internal` still works. |
| ICR and space-tape both calling `CreateImGuiThumbnail` on the same `ThumbnailReference` could double-register descriptors | Not a concern after Task 2 deletes ICR — the reference-counted descriptor registry inside KSA handles idempotent registration but we prefer single-owner usage in the consolidated codebase. |
| Modal popups need `BeginPopupModal` every frame while open | `RenderContentInner` always calls `_loadSubPartsModal.Render(...)`; `PartEditorUi.RenderEditorWindow` always calls `_savePartModal.Render(...)`. Ensure both sit outside any conditional. |
| `HotkeyGuard` must still be applied on the space-tape Patcher | [space-tape/Patcher.cs](space-tape/Patcher.cs) is untouched by this plan; confirm after final build. |
| Filterable combo autofocus might steal keyboard on every frame while combo open | Gated behind `ImGui.IsWindowAppearing()` as in the imgui-design skill reference — only focuses the filter input on first frame. |

## Out of scope

- Rewriting the 3D editor (scene, gizmos, interaction).
- Changing [PartModWriter.SavePart](space-tape.lib/PartModWriter.cs) file format or merge semantics.
- Changing [PartImporter](space-tape.lib/PartImporter.cs).
- Touching other mods besides grant + space-tape + inanimate-carbon-rod.

