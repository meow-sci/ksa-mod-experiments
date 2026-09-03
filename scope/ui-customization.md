# UI / Customization Mods — Game Integration Scope

Permanent reference for detecting when KSA game updates break the UI/customization
mods (`skittles`, `con-man`, `kitchen-sink`). Every game-facing member these mods
touch is enumerated and verified against decompiled sources.

**Verified game versions**

- NEW decomp `2026.9.7.5402` root: `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD decomp `2026.8.22.5348` root: `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root
(namespace-foldered, e.g. `KSA/GaugeCanvas.cs`, `Brutal.ImGuiApi/ImGuiStyle.cs`); line numbers are
**@5402** unless a cell says otherwise. **Mod code** paths are relative to the repo root
`~/repos/meow-sci/unscience`.

**How these mods are hosted (all three)**

Each mod ships as a thin standalone StarMap host (`<mod>/Mod.cs` + `<mod>/Patcher.cs`)
whose logic lives in a `*.lib` exposing a `MeowSci.KsaAbstractions.ISubmod`. The same
submod instances are also embedded in the **unscience** supermod
(`unscience/Mod.cs:65,72,83` create `SkittlesSubmod`, `ConManSubmod`,
`KitchenSinkSubmod`). Both hosts toggle a window with **F11** and call
`SubmodUI.BeginContentArea` / `EndContentArea` for the body.

Important hosting caveat for kitchen-sink: as of Phase 4 the supermod's `unscience/Patcher.cs`
**now calls** `IvaForceRender.Patch()` (so the IVA ctor/`AddInstance` postfixes are live in the
supermod too), but it still does **not** dispatch the vehicle-solver prefix
(`KitchenSinkSolverPatch`) to `KitchenSinkSubmod`. The Flexo "Update Physics" path is therefore
still only live in the **standalone** kitchen-sink host (`kitchen-sink/Patcher.cs`). This is a
mod-wiring detail, not a game-update risk, but it is part of the integration picture.

**Distinction — ImGui is third-party, not KSA.** skittles and parts of con-man drive
`Brutal.ImGuiApi` (the bundled Dear ImGui wrapper), which is shipped with the game but
is *not* KSA game code. Those rows are tagged `(ImGui)` in the Risk column. The only
genuinely KSA-owned surfaces here are `GaugeCanvas` (con-man), the
editor/part/vehicle/`PartModel` types (kitchen-sink), and `KSAColor` (button accents).

**Summary of 4680 -> 4750 risk: NO breaking deltas.** Every typed member, enum slot,
private reflected field, and patched method these three mods use is byte-for-byte
identical in signature between OLD and NEW; only source line numbers shifted. The two
changelog items that looked relevant — "Update KSA to use the latest Brutal packages"
(rev 4729) and the mesh/shader churn (MeshIndirect merge, ModelGlass/ModelEye combine,
IVA ambient-occlusion/raytracing fixes) — left the `ImGuiStyle`/`ImGuiCol` surface and
the `PartModel` public API untouched. Details per mod.

**Integration-point "Kind" legend**

1. Harmony patch  2. Reflection (`AccessTools.*` / string-based field/method)
3. Direct typed API  4. Render/GPU  5. Asset  6. Lifecycle

---

## skittles

**Purpose** — Global ImGui theme manager. Mutates the shared `ImGuiStyle`
(`ImGui.GetStyle()`) so every game window/control re-themes live. Ships built-in
schemes (Game Default captured at startup, Dark/Light/Classic via ImGui presets,
"Inanimate Carbon Rod") and user `.toml` themes; wraps `ImGui.ShowStyleEditor()` for
live editing; restores the captured game default on unload. No KSA game types beyond
`KSAColor` button accents — this is almost entirely a `Brutal.ImGuiApi` (third-party)
integration.

**Unscience integration** — `SkittlesSubmod : ISubmod`
(`skittles.lib/SkittlesSubmod.cs:10`). `Initialize()` builds a `ThemeManager`, captures
the current style as "Game Default", ships the Carbon Rod preset, loads config, and
applies the saved startup theme. `RenderContent()` draws the picker; `RenderFloatingWindows()`
hosts the editor window. `Dispose()` calls `ThemeManager.RestoreDefaults()` to re-apply
the captured default. Created standalone (`skittles/Mod.cs:27`) and in the supermod
(`unscience/Mod.cs:65,90`).

**UI/hotkeys** — Standalone window "Skittles — Theme Manager", 420x360, **F11** toggle
(`skittles/Mod.cs:48,75`). Picker: Active label, filterable theme combobox
(applies on select), "Open Theme Editor" button, red "Delete" button (custom themes
only). Editor window "Skittles — Theme Editor###sk_editor", 700x800, hosts
`ImGui.ShowStyleEditor()` plus Save / Save-as-New controls (`SkittlesSubmod.cs:156-227`).

**Persistence** — Tomlyn TOML under `%USERPROFILE%\Documents\My Games\Kitten Space Agency\skittles\`
(`ThemeManager.cs:31-35`): `config.toml` (`active_theme`, `ModConfig.cs`) and
`themes\*.toml` (per-theme: `[meta]`,`[colors]` 60 named slots,`[style]` vars —
`ThemeSerializer.cs`). `inanimate-carbon-rod.toml` auto-shipped on first run
(`ThemeManager.cs:49-55`). No StarMap save hooks; no game assets.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 3 | `skittles.lib/ThemeDefinition.cs:84,172`; `ThemeManager.cs:224` | `ImGui.GetStyle() : ImGuiStylePtr` | `Brutal.ImGuiApi/ImGui.cs:5431` | Yes | None (`:5431`) | (ImGui) core of the whole mod |
| 2 | 3 | `ThemeDefinition.cs:89-90,177-178` | `ImGuiStylePtr.Colors[i] : float4` (backing `ImGuiStyle.Colors : float4_60`) | `Brutal.ImGuiApi/ImGuiStyle.cs:188` | Yes | None | (ImGui) 60-slot inline array; index = `ImGuiCol` |
| 3 | 3 | `ThemeDefinition.cs:94-165` (capture) / `:182-238` (apply); `ThemeManager.cs:225-259` | `ImGuiStylePtr` style vars (Alpha, WindowRounding, WindowBorderHoverPadding, TabCloseButtonMinWidth*, FramePadding, ItemSpacing, AntiAliased*, … 72 members) | `Brutal.ImGuiApi/ImGuiStylePtr.cs` (72) / `ImGuiStyle.cs` | Yes | None (member-set diff empty; 72==72) | (ImGui) all read+write fields present |
| 4 | 3 | `ThemeDefinition.cs:87,175`; `ThemeSerializer.cs:12-31,55` | `ImGuiCol` enum, 60 slots `Text`(0)…`ModalWindowDimBg`(59), then `COUNT` | `Brutal.ImGuiApi/ImGuiCol.cs:5-65` | Yes | None (Text:5…ModalWindowDimBg:64, COUNT:65) | (ImGui) hard-coded `60` count matches |
| 5 | 3 | `SkittlesSubmod.cs:224` | `ImGui.ShowStyleEditor(ImGuiStylePtr ref = default)` | `Brutal.ImGuiApi/ImGui.cs:5521` | Yes | None | (ImGui) |
| 6 | 3 | `ThemeManager.cs:89,93,99` | `ImGui.StyleColorsDark/Light/Classic(ImGuiStylePtr dst = default)` | `Brutal.ImGuiApi/ImGui.cs:5552,5557,5562` | Yes | None | (ImGui) |
| 7 | 3 | `SkittlesSubmod.cs:108-109` | `ImGui.GetColorU32(ImGuiCol, float) : ImColor8` | `Brutal.ImGuiApi/ImGui.cs:5960` | Yes | None | (ImGui) feeds PushStyleColor |
| 8 | 3 | `SkittlesSubmod.cs:108-109` | `KSAColor.Xkcd.Scarlet`/`.PaleGrey : Color.Preset` | `KSA/KSAColor.cs:1561,837` (class `Xkcd`:23) | Yes | None (same lines+RGB) | **KSA type** — delete-button accent only |
| 9 | 3/6 | `skittles/Mod.cs:48` | `ImGui.IsKeyPressed(ImGuiKey.F11)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) window toggle |
| 10 | 1 | `skittles/Patcher.cs:13` | `HotkeyGuard.Patch` (abstraction; patches `Brutal.ImGuiApi` IO) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard, not game-typed |

**Game assets referenced** — None.

**Update-risk findings (4680 -> 4750)**

- No breaking deltas detected. `ImGuiStyle`/`ImGuiStylePtr` member set is identical
  (member-name diff empty; 72 public members both revs), `ImGuiCol` is identical
  (60 slots + `COUNT`), and all driven `ImGui.*` methods are present with unchanged
  signatures. The rev-4729 "latest Brutal packages" update did not alter the style or
  color surface.
- Standing fragility (version-independent, not a 4750 regression): `ThemeDefinition`,
  `ThemeSerializer.ColorNames`, and the `BuiltInThemes.CarbonRod()` index map all
  hard-code **60** colors and a fixed style-var list. If a future Brutal/Dear ImGui
  bump adds a color slot (raising `ImGuiCol.COUNT`) or a style var, skittles silently
  drops the new field rather than crashing — watch `ImGuiCol.cs` slot count and
  `ImGuiStyle.cs` members on every Brutal update.

---

## con-man

**Purpose** — HUD gauge layout manager. Saves/restores per-`GaugeCanvas` visibility,
drag offset, and scale as named `.toml` layouts, with an optional startup-default
layout. Reads/writes private `GaugeCanvas` fields via reflection (the game exposes no
public setters), then force-repositions the live ImGui windows by title.

**Unscience integration** — `ConManSubmod : ISubmod` (`con-man.lib/ConManSubmod.cs:10`).
`Initialize()` builds a `GaugeStateAccessor` (resolves the reflected fields) and a
`LayoutManager`; logs a warning if reflection fails. `Update(dt)` applies the startup
default once canvases exist (`ConManSubmod.cs:43-55`). `RenderContent()` is the whole
UI; `Dispose()` is a no-op. Created standalone (`con-man/Mod.cs:27`) and in the supermod
(`unscience/Mod.cs:72`). If `GaugeStateAccessor.IsValid` is false the UI shows a red
"game may have been updated" message instead of operating (`ConManSubmod.cs:59-64`) —
the built-in breakage canary.

**UI/hotkeys** — Standalone window "Con-Man — Layout Manager", 500x600, **F11** toggle
(`con-man/Mod.cs:53,80`). Rows: Layout (filterable combo) + Apply/Delete; Save-current
(name field) + Save; Startup-default (filterable combo, `(None)` option) + Reset;
delete-confirm modal; collapsible "Gauge Data Debug" table listing every live canvas's
Id/Enabled/Offset/Scale (`ConManSubmod.cs:287-333`).

**Persistence** — Tomlyn TOML under
`%USERPROFILE%\Documents\My Games\Kitten Space Agency\.con-man\`
(`LayoutManager.cs:29-33`): `config.toml` (`[settings] startup_default`) and
`layouts\<name>.toml` (`[gauges.<canvasId>]` with `enabled`,`offset_x/y`,`scale_x/y` —
`LayoutSerializer.cs`). Layout keys are `GaugeCanvas.Id` (stable `SerializedId.Id`), so
they survive sessions. No game assets.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 2 | `con-man.lib/GaugeStateAccessor.cs:28` | `GaugeCanvas._canvases : private static List<GaugeCanvas>` (NonPublic+Static) | `KSA/GaugeCanvas.cs:92` | Yes | None (OLD:92, byte-identical) | **string-based reflection — required field** (IsValid gate). Public read-only mirror `AllCanvases` exists at `:177` |
| 2 | 2 | `GaugeStateAccessor.cs:29` | `GaugeCanvas._enabled : private bool = true` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:143` | Yes | None (OLD:143) | **string-based — required field**. Public `SetEnabled(bool)`/`ToggleEnabled()` at `:664/:669` OR in `AlwaysEnabled` |
| 3 | 2 | `GaugeStateAccessor.cs:30` | `GaugeCanvas._customOffset : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:134` | Yes | None (OLD:134) | **string-based — required field** |
| 4 | 2 | `GaugeStateAccessor.cs:31` | `GaugeCanvas._customScale : private float2 = float2.One` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:136` | Yes | None (OLD:136) | **string-based — required field** |
| 5 | 2 | `GaugeStateAccessor.cs:32` | `GaugeCanvas._windowPosition : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:130` | Yes | None (OLD:130) | string-based — **optional** (not in IsValid; degrades to `float2.Zero`) |
| 6 | 2 | `GaugeStateAccessor.cs:33` | `GaugeCanvas._windowSize : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:132` | Yes | None (OLD:132) | string-based — **optional** (degrades to `(100,100)`) |
| 7 | 2 | `GaugeStateAccessor.cs:34` | `GaugeCanvas._windowTitle : protected string` (NonPublic+Instance) | `KSA/GaugeCanvas.cs:115` | Yes | None (OLD:115; `protected` since 5018, still NonPublic) | string-based — **optional** (skips reposition if null) |
| 8 | 3 | `LayoutManager.cs:119,147`; `ConManSubmod.cs:316` | `GaugeCanvas.Id : string` (inherited `SerializedId.Id { get; set; }`) | `KSA/SerializedId.cs:13` | Yes | None | layout dictionary key; GaugeCanvas→GaugeBase→SerializedId |
| 9 | 3 | `LayoutManager.cs:169-170` | `ImGui.SetWindowPos/SetWindowSize(ImString name, in float2, ImGuiCond)` | `Brutal.ImGuiApi/ImGui.cs:5756,5767` | Yes | None | (ImGui) reposition live window by title; `string`→`ImString` |
| 10 | 3 | `ConManSubmod.cs:68-73` | `ImGui.GetStyle()` + `ImGuiStylePtr.ItemSpacing/FramePadding` | `Brutal.ImGuiApi/ImGui.cs:5431` | Yes | None | (ImGui) layout math |
| 11 | 3 | `ConManSubmod.cs:125-126` | `KSAColor.Xkcd.Scarlet`/`.PaleGrey` + `ImGui.GetColorU32` | `KSA/KSAColor.cs:1561,837` | Yes | None | **KSA type** — delete-button accent |
| 12 | 3/6 | `con-man/Mod.cs:53` | `ImGui.IsKeyPressed(ImGuiKey.F11)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) window toggle |
| 13 | 1 | `con-man/Patcher.cs:14` | `HotkeyGuard.Patch` (abstraction) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard |

**Game assets referenced** — None.

**Update-risk findings (5117 → 5261)**

- ✅ **All seven reflected fields still resolve**, still declared on `GaugeCanvas` itself, so
  `GaugeStateAccessor.IsValid` stays true and `_canvases` remains a working canary.
  `GaugeCanvas.OnDrawMenuBar()` is signature-identical (marque's patch target).
- ⚠️ **BEHAVIORAL BREAK — `_enabled` is no longer sufficient to show a gauge (rev 5201).**
  The commit added *"a per-canvas gauge visibility context system. A canvas with one or more flags
  only draws when every flag is true for the currently controlled vehicle."* `GaugeCanvas` gained:
  - `[XmlElement("VisibleInContext")] public List<GaugeVisibilityFlag> VisibleInContext`
  - `public bool IsContextVisible()` — returns true only if **every** flag matches; the flags are
    `Burn` (`BurnPlan.HasActiveBurns`), `Engines`, `EVA` (`controlledVehicle is KittenEva`),
    `Vehicle` (**not** a `KittenEva`), `Sequence`, `Target`, `IVA` (`GetCameraMode() == IVA`),
    `Thrusters`, `Atmosphere`
  - `public static void ApplyContextOverrides()` — reads
    `GameSettings.Current.GaugeContextOverrides` and **overwrites** each canvas's `VisibleInContext`

  and the draw gate became:
  ```csharp
  if (!_enabled || !IsContextVisible() || (this == Program.CrewPortraitsCanvas && !CrewPortraitPanel.HasOccupants))
  ```
  con-man writes `_enabled` (`GaugeStateAccessor.cs:68`) and reads it (`:63`). Both still work, but
  **the game can now veto the result**: a canvas carrying context flags stays hidden regardless of
  what con-man sets, and the stock assignments are non-empty out of the box (BurnControl→`Burn`,
  EngineControl→`Engines`, RendezvousControl→`Target`, KittenFlightControl→`EVA`,
  AutopilotSettings→`Vehicle`, Sequence→`Sequence`).

  There is also now a **second source of truth** con-man does not know about:
  `GameSettings.Current.GaugeContextOverrides` is persisted to `settings.toml` and re-applied by
  `ApplyContextOverrides()`, and the game ships a stock **"Context Assignments"** HUD window that
  edits it. Likely user-visible symptom: **enabling a gauge in con-man appears to do nothing.**

  **Open — not fixed.** Candidate approaches: have con-man read/clear `VisibleInContext` alongside
  `_enabled`, or surface the context flags in con-man's own UI. Needs a decision on how con-man
  should coexist with the stock system rather than fight it. **Needs a live pass either way.**
- ⚠️ **New canvases this span** — EVA Control (rev 5179), Crew Portraits (5193/5194/5232), Resources
  (5229/5231/5234/5246/5247). con-man enumerates `_canvases` dynamically so it will see them, but
  saved layouts predate them, and Crew Portraits carries the extra `HasOccupants` gate above.
  Rev 5228 also fixed gauge overlay content rendering one frame behind while dragging.
- ✅ **kitchen-sink** — `PartTree.ReinitializeDerivedValues()` and the `PartModel` ctor /
  `PartModel.AddInstance` IvaForceRender targets are signature-identical.
- ✅ **skittles** — builds clean with `TreatWarningsAsErrors` and 0 warnings, so no `ImGui.GetStyle()`
  surface churn landed in the Brutal bump this span.

**Update-risk findings (4750 -> 5018)**

- ✅ **All seven reflected fields still resolve** — and critically, all seven are still declared
  **on `GaugeCanvas` itself** (lines 92/112/127/129/131/133/140), none lifted into `GaugeBase`.
  That matters because `GaugeStateAccessor` calls `typeof(GaugeCanvas).GetField(name, NonPublic|…)`,
  which does **not** walk base types for non-public members — a move to `GaugeBase` would have been a
  silent total failure.
- ⚠ **`_windowTitle` changed `private` → `protected`.** Still `NonPublic`, so
  `BindingFlags.NonPublic | BindingFlags.Instance` still finds it. No code change needed, but this is
  the kind of drift that a `private`-only lookup would have missed.
- 🔴 **BEHAVIORAL: the game grew a first-party version of con-man's feature.** Unlike 4680→4750, this
  span is dense with gauge/HUD work and none of it moves a symbol con-man binds to:
  - **rev 4940** — added a **Hud dropdown to the file bar**, and **moved the gauge enable/disable
    toggles out of the View dropdown into it**. Also added **HudLayouts**: save any arrangement of
    gauges as a named layout, mark one default, serialized to a `HUDLayouts` folder alongside
    Saves/Vehicles. This is a native re-implementation of con-man's layout manager — expect UI overlap
    and possible fights over canvas offset/scale state.
  - **rev 4919** — the in-flight Sequence UI is now drawn on a GaugeCanvas dressing; navball canvas
    padding fixed.
  - **rev 4959** — the burn UI was reworked into the gauge-canvas system, and an **`AlwaysEnabled`
    flag** was added for canvases whose enabled state is driven by gameplay rather than the user.
    con-man does not know about that flag, so toggling `_enabled` on such a canvas may be overridden
    or produce an inconsistent UI.
  - **rev 5003** — all UI pop-ups moved to Gauge Window styling.
  - **rev 4970** — `ImGauge`/`ImGaugeWindow` helper classes added (new `ImGauge*.cs`,
    `SerializedCanvas`, `LayoutSave`/`LayoutSaves`, `PopupGaugeLayout` types).
  → **Needs a live pass**: verify the canvas list con-man enumerates, that its saved layouts still
  apply, and how it interacts with the game's own HudLayouts.

#### Carried over from the 4680 -> 4750 review

- No breaking deltas detected. **This is the highest-risk integration in the suite**
  (seven string-named private fields on `GaugeCanvas`), and all seven exist unchanged
  in NEW — only line numbers shifted ~1. `GaugeCanvas : GaugeBase : SerializedId`
  inheritance and the `string Id` key are intact.
- Field semantics still match the apply math: the decomp confirms
  `_windowPosition = GetWindowPos() - _customOffset` and
  `_windowSize = GetWindowSize() / _customScale` (`GaugeCanvas.cs:505-506`), which is
  exactly what `LayoutManager.ApplyLayout` assumes (`LayoutManager.cs:149-170`).
- Failure mode if a future update renames any of fields #1-#4: `GaugeStateAccessor.IsValid`
  goes false, the UI shows the red warning, and no reflection writes occur — con-man
  fails safe rather than corrupting state. Fields #5-#7 degrade silently to defaults.

---

## kitchen-sink

**Purpose** — Grab-bag of one-off editor/render fixes. Two shipped fixes plus two
experimental transform-test panels: (a) **Fix Invisible Subparts** —
`PartTree.ReinitializeDerivedValues` on the editor's part tree; (b) **Force IVA
Rendering** — `IvaForceRender` (in `ksa-abstractions.lib`) flips
`PartModelModule.Template.Internal` to false and Harmony-postfixes the `PartModel` ctor
and `PartModel.AddInstance` so interior meshes render outside IVA camera mode and in the
editor preview; (c/d) **Flexo Part/Subpart Test** — interactive `Part` transform nudging
with deferred physics resync.

**Unscience integration** — `KitchenSinkSubmod : ISubmod` (`kitchen-sink.lib/KitchenSinkLib.cs:12`),
holding a `FlexoPartTest` and `FlexoSubpartTest`. Created standalone
(`kitchen-sink/Mod.cs:30`) and in the supermod (`unscience/Mod.cs:83`).
`UpdateBeforeVehicleSolvers` is driven by a Harmony prefix on
`Universe.ExecuteNextVehicleSolvers` (`kitchen-sink/Patcher.cs:52-75`, priority First).
**Wiring (Phase 4):** the supermod now applies `IvaForceRender.Patch` (`unscience/Patcher.cs`), so
the IVA ctor/`AddInstance` postfixes (parts spawned after toggle + editor-preview fix) are live in
supermod mode as well as standalone. **Still standalone-only:** `KitchenSinkSolverPatch.Apply`
(`kitchen-sink/Patcher.cs:23-24,52-75`) — so the Flexo "Update Physics" button only fires in the
standalone kitchen-sink host. `Mod.Unload` forces `IvaForceRender.Enabled = false` to restore
templates (`kitchen-sink/Mod.cs:72`).

**UI/hotkeys** — Standalone window "Kitchen Sink", 420x300, **F11** toggle
(`kitchen-sink/Mod.cs:55,85`). Sections: "Force IVA Rendering" checkbox; "Fix Invisible
Subparts" → "Refresh Vehicle" button; "Flexo Part Test" and "Flexo Subpart Test"
(vehicle/part[/subpart] combos + Pos/Rot drag tables + Reset / Update-Physics).

**Persistence** — None. No disk I/O, no config, no StarMap save hooks. `IvaForceRender`
state is in-memory (`_mutatedTemplates`) and reset on toggle-off / unload.

**Integration points**

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 3 | `KitchenSinkLib.cs:56` | `Program.Editor : static VehicleEditor?` | `KSA/Program.cs:226` | Yes | None (OLD:207) | null-guarded |
| 2 | 3 | `KitchenSinkLib.cs:57` | `VehicleEditor.EditingSpace : VehicleEditingSpace` (field) | `KSA/VehicleEditor.cs:545` | Yes | None (OLD:545) | |
| 3 | 3 | `KitchenSinkLib.cs:57,59` | `VehicleEditingSpace.Parts : PartTree?` (field) | `KSA/VehicleEditingSpace.cs:16` | Yes | None (OLD:16; file diff is 3 `Viewport`→`IViewport` draw signatures) | null-guarded |
| 4 | 3 | `KitchenSinkLib.cs:59` | `PartTree.States : ModuleStateList` (field) | `KSA/PartTree.cs:39` | Yes | None (OLD:39) | passed as `oldStates` |
| 5 | 3 | `KitchenSinkLib.cs:60` | `PartTree.ReinitializeDerivedValues(ModuleStateList oldStates) : void` | `KSA/PartTree.cs:308` | Yes | None (OLD:308; 0-arg overload `:302`) | `ModuleStateList.cs` byte-identical |
| 6 | 1 | `ksa-abstractions.lib/IvaForceRender.cs:42` | `PartModel..ctor(PartModelModule.Template)` **protected** (Harmony postfix via `AccessTools.Constructor`) | `KSA/PartModel.cs:384` | Yes | None (OLD:383; body identical, only ctor) | catches parts built after toggle |
| 7 | 1 | `IvaForceRender.cs:46` (lookup), `:98` (postfix sig) | `PartModel.AddInstance(PerInstanceData, IViewport, int frameIndex) : void` (Harmony postfix; captures `__0`,`__1` only) | `KSA/PartModel.cs:408` | Yes | **RETYPED @5402** `Viewport`→`IViewport` (OLD:407) — postfix `__1` updated; **NEW GATE @5402** `:410-413` early-returns unless `viewport.HasAny(ViewportOptionFlags.RenderPartModels)`; IVA/raytracing gate `:415` now per-viewport | method is **3-arg**; postfix ignores `frameIndex` (`__2`). ⚠ postfix does not yet mirror the new gate — see 5348→5402 summary |
| 8 | 3 | `IvaForceRender.cs:98,105` | `PartModel.PerInstanceData` (struct) | `KSA/PartModel.cs:332` | Yes | None (OLD:331) | postfix param `__0` |
| 9 | 3 | `IvaForceRender.cs:105` | `PartModel.ViewportData.Get(PartModel, IViewport) : ViewportData` → `.InstanceList : List<PerInstanceData>` `.Add` | `KSA/PartModel.cs:314,310` | Yes | **RETYPED @5402** param `Viewport`→`IViewport` (OLD:313/309); lookup keyed by `viewport.Id : ViewportId` | nested class `ViewportData`:308 |
| 10 | 3 | `IvaForceRender.cs:111` | `PartModel.Instances : static List<PartModel>` | `KSA/PartModel.cs:358` | Yes | None (OLD:357) | enumerated on toggle-on |
| 11 | 3 | `IvaForceRender.cs:87,89,113,116,125` | `PartModel.Template : PartModelModule.Template` (field) | `KSA/PartModel.cs:362` | Yes | None (OLD:361) | |
| 12 | 3 | `IvaForceRender.cs:87,89,101,113,116,125` | `PartModelModule.Template.Internal : bool` (field) | `KSA/PartModelModule.cs:40` | Yes | None (OLD:40) | the field flipped to force visibility |
| 13 | 3 | `IvaForceRender.cs:103` | `PartModelModule.Template.RayTracing : RaytracingMode` + `RaytracingMode.ShadowProxy` | `KSA/PartModelModule.cs:32,15` | Yes | None (OLD:32/15) | shadow-proxy skip in editor postfix |
| 14 | 3 | `IvaForceRender.cs:100` | `Program.Editor` (null check, editor-preview gate) | `KSA/Program.cs:226` | Yes | None | |
| 15 | 3 | `IvaForceRender.cs:102` | `Program.MainViewport : IGameViewport` `.Mode : CameraMode { get; }` `== CameraMode.IVA` | `KSA/Program.cs:485`; `IViewport.cs:29` (impl `ViewportBase.cs:36`); `CameraMode.cs:14` | Yes | **RETYPED @5402** — `MainViewport` was `Viewport` (OLD Program:468), `Mode` was a public field (OLD `Viewport.cs:14`); `CameraMode.cs` identical | compile-bound read; no code change |
| 16 | 1 | `kitchen-sink/Patcher.cs:56` | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` (Harmony prefix; captures `dtPlayer` by name) | `KSA/Universe.cs:1834` | Yes | None (OLD:1767; body identical) | method is **2-arg**; prefix declares only `dtPlayer` — valid; single overload so `AccessTools.Method` is unambiguous |
| 17 | 3 | `FlexoPartTest.cs:184`; `FlexoSubpartTest.cs:193` (via `VehicleProvider.cs:15`) | `Universe.CurrentSystem : static CelestialSystem?` `.All : LookupCollection<Astronomical>` `.UnsafeAsList()` | `KSA/Universe.cs:94`; `CelestialSystem.cs:64` | Yes | None (OLD:94/57) | Flexo vehicle enumeration |
| 18 | 3 | `FlexoPartTest.cs:84,91`; `VehicleProvider.cs:22` | `Vehicle.Id : string` (inherited `Astronomical.Id { get; protected set; }`) | `KSA/Astronomical.cs:104` | Yes | None (OLD:104) | read-only to mod |
| 19 | 3 | `FlexoPartTest.cs:201`; `FlexoSubpartTest.cs:214` | `Vehicle.Parts : PartTree { get; set; }` → `PartTree.Parts : ReadOnlySpan<Part>` | `KSA/Vehicle.cs:604`; `PartTree.cs:95` | Yes | None (OLD:598/95; `Parts` is a property, not a field) | |
| 20 | 3 | `FlexoPartTest.cs:108,115` | `Part.Template : PartTemplate` `.Id` | `KSA/Part.cs:576` | Yes | None (OLD:568) | combo labels |
| 21 | 3 | `FlexoPartTest.cs:216,250,263` | `Part.PositionParentAsmb : double3 { get; set; }` | `KSA/Part.cs:752` | Yes | None (OLD:744; property body diffed identical) | written by Flexo |
| 22 | 3 | `FlexoPartTest.cs:217,251,264` | `Part.Asmb2ParentAsmb : doubleQuat { get; set; }` | `KSA/Part.cs:766` | Yes | None (OLD:758; property body diffed identical) | written by Flexo |
| 23 | 3 | `FlexoPartTest.cs:227` | `Part.TreeChildren : List<Part>` (field) | `KSA/Part.cs:666` | Yes | None (OLD:658) | descendant snapshot |
| 24 | 3 | `FlexoPartTest.cs:302`; `FlexoSubpartTest.cs:230` | `Part.SubParts : ReadOnlySpan<Part>` | `KSA/Part.cs:1079` | Yes | None (OLD:1052) | cache invalidation walk |
| 25 | 3 | `FlexoPartTest.cs:253,266,279,286,306` | `Part.BoundingBoxVehicleAsmb : (double3,double3) { get; set; }` + `ComputeBoundingBoxVehicleAsmb() : (double3 Min, double3 Max)` | `KSA/Part.cs:831,1464` | Yes | Property none (OLD:823). Method **body refactored @5402** (OLD:1424): now `ComputeSubPartBoundingBox(inVehicleAsmb: true)` (`:1484`) accumulating **all** `MeshViewModule`s per sub-part via `AccumulateMeshBounds` (`:1504`) instead of only `span[0]`; signature unchanged | recompute after move; bounds may grow slightly for multi-mesh subparts |
| 26 | 3 | `FlexoPartTest.cs:320`; `FlexoSubpartTest.cs:291` | `Vehicle.UpdateAfterPartTreeModification() : void` | `KSA/Vehicle.cs:1881` | Yes | None (OLD:1727; body identical) | deferred to solver prefix |
| 27 | 2 | `FlexoPartTest.cs:319`; `FlexoSubpartTest.cs:290` | `PartTree.RecomputeStaticMass() : void` **private** (HarmonyLib `Traverse.Method("RecomputeStaticMass")`) | `KSA/PartTree.cs:778` | Yes | None (OLD:778; public `RefreshStaticMass()` wrapper at `:773`) | **string-based reflection** — silently caught if renamed |
| 28 | 1 | `kitchen-sink/Patcher.cs:22` | `HotkeyGuard.Patch` (abstraction) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard |

**Game assets referenced** — None (operates on already-loaded `PartModel`/`PartTree`
instances; no `Content/` paths).

**Update-risk findings (4680 -> 4750)**

- No breaking deltas detected. `KSA/PartModel.cs` and `KSA/PartModelModule.cs` are
  byte-identical between revs (same members, same line numbers), so the IVA force-render
  feature (#6-#15) is fully intact despite the rev-4693/4745-era mesh/shader churn —
  that churn (MeshIndirect merge, ModelGlass/ModelEye combine, IVA AO/raytracing fixes)
  touched shaders and GPU paths, not the `PartModel` C# API or `Template.Internal` gate.
- `PartTree.ReinitializeDerivedValues(ModuleStateList)` and the private
  `RecomputeStaticMass()` are unchanged; `ModuleStateList` still exists. Fix-Invisible-
  Subparts (#4-#5) and the Flexo physics resync (#26-#27) are safe.
- `Universe.ExecuteNextVehicleSolvers` is still the single 2-arg
  `(double dtPlayer, SimStep simStep)` overload in both revs — the by-name `dtPlayer`
  prefix and the name-only `AccessTools.Method` resolution remain unambiguous.
- Watch items (version-independent): the protected `PartModel..ctor` resolved by
  parameter-type array (#6) breaks if a `PartModelModule.Template` overload is added or
  the param type changes; `Traverse.Method("RecomputeStaticMass")` (#27) is the only
  string-named member in this mod and fails *silently* (caught, logged) if renamed.
```

---

## Area summary — Update-risk findings (5261 → 5348)

- ⚠️ **con-man vs the new global Hud Scale (rev 5293) — the headline finding this span.**
  All seven reflected `GaugeCanvas` fields still resolve and their declarations are **byte-identical at
  the same line numbers** (`KSA/GaugeCanvas.cs:92,115,130,132,134,136,143`); `_windowTitle` is still
  `protected` on `GaugeCanvas` itself. The **arithmetic around them changed**:

  | `KSA/GaugeCanvas.cs` | 5261 | 5348 |
  |---|---|---|
  | `:534` → `:536` | `ScreenReference.PixelsToUv(pixelsSize)` | `PixelsToUv(pixelsSize / GameSettings.GetGaugeScale())` |
  | `:815` → `:817` | `SetNextWindowSizeConstraints(2f, 2048f, …)` | `… 2048f * MathF.Max(1f, GetGaugeScale()), …` |
  | `:856` → `:859-866` | — | new `ConsoleStyle.BeginGaugeHostScope(GetGaugeScale() * clamp(ContentScale, 0.6, 3))` around the draw, closed at `:884` |

  con-man captures and restores `_windowPosition` / `_windowSize` / `_customScale` / `_customOffset`
  (`GaugeStateAccessor.cs:28-34`; arithmetic documented at `LayoutManager.cs:151-152`), so **layouts
  saved at one Hud Scale restore at the wrong size and position at another.** Layouts saved and
  restored at the same Hud Scale should be fine. **Open — needs a live pass before any code change.**
  Suggested approach: record `GameSettings.GetGaugeScale()` alongside each saved layout and normalise
  on restore.
- ⚠️ **Still open from 5261:** the rev-5201 per-canvas `IsContextVisible()` gate. The crew-portraits
  canvas gained a **third** gate this span — `GameSettings.ShowCrewPortraitCameras()` (rev 5276), so
  the draw condition is now
  `!_enabled || !IsContextVisible() || (CrewPortraits && (!ShowCrewPortraitCameras() || !HasOccupants))`
  (`:719`).
- ℹ️ **Also new on `GaugeCanvas`:** `RecalculateAll()` → `RecalculateAll(bool forceReattach = false)`
  (optional param, source-compatible; con-man does not call it); `Detached = false` reset on reattach
  (`:954`); `RegisterOpaqueCoverage(...)` and `DrawWidgetText(...)` added for the rev-5283 UI coverage
  culling. `GaugeCanvas.OnDrawMenuBar()` — marque's patch target — is **unchanged**.
- ⚠️ **skittles — `GameSettings.Interface.FontSize` was removed** (rev 5277) and `GetInterfaceScale()`
  redefined from `FontSize / 20f` to a dedicated 50–200 % `Interface.Scale`; `GetFontSize()` is now
  `max(1, round(20 * GetInterfaceScale()))`. skittles touches only `ImGui.GetStyle()` and reads neither
  setting, so **no code change** — but rev 5277 also made `ConsoleStyle` ImGui windows scale with the new
  setting, so themed sizes/paddings should get a live eyeball at a non-100 % Interface Scale.
- ✅ **kitchen-sink clean.** `PartTree.ReinitializeDerivedValues()` still exists (call sites moved from
  `Vehicle.cs` to `PartArchetypes.cs`/`EVADoor.cs`); `IvaForceRender`'s targets — `PartModel..ctor`,
  `PartModel.AddInstance`, `PartModel.Instances`, `PartModelModule.Template.Internal`, `CameraMode.IVA` —
  are all unchanged. `PartTree.RecomputeStaticMass` still private and `Traverse`-able.
  Rev 5312 added raytracing to IVA kittens — worth a live look at IVA force-render.
- ✅ **`IvaForceRender.Patch` IS wired in the supermod** (`unscience/Patcher.cs:74`, unpatch `:114`) — the
  "still open" wording that used to sit here was stale; the Phase-4 wiring predates 5348.

---

## Area summary — Update-risk findings (5348 → 5402)

Span note: only rev **5401** ("Fixed crash for incorrect data stride for thumbnail rendering") is
logged; revisions **5349–5400 are unlogged**, so the source diff is the only evidence. Headline
game change in the span: the `Viewport` class was replaced by `IViewport`/`IGameViewport`/
`ViewportBase`/`ViewportRegistry` (`Program.Viewports` removed), plus parachutes and part
structural limits.

- ✅ **con-man — all seven reflected `GaugeCanvas` fields are byte-identical at the same lines**
  (`KSA/GaugeCanvas.cs:92,143,134,136,130,132,115`), still declared on `GaugeCanvas` itself (nothing in
  `GaugeBase.cs`), same kinds (fields) and types; `_windowTitle` still `protected`. The whole file diff
  is two `Viewport`→`IViewport` render signatures (`:1347`, `:1367`). The apply math con-man assumes
  (`_windowPosition = GetWindowPos() - _customOffset`, `_windowSize = GetWindowSize() / _customScale`,
  `:828-837`), the `IsContextVisible()` draw gate (`:719`) and the rev-5293 Hud-Scale arithmetic
  (`:536`, `:817`, `:861`) are unchanged — so the two **open behavioral items from 5261/5348
  (context-visibility veto, Hud-Scale-relative layouts) carry over untouched**. `GaugeStateAccessor.IsValid`
  stays true; `SerializedId.Id` (`:13`) identical.
- ✅ **skittles clean.** Zero `Brutal*` files in the diff list; `Brutal.ImGuiApi/ImGuiCol.cs` (60 slots +
  `COUNT`) and `ImGuiStyle.cs` byte-identical; `KSAColor.cs` byte-identical (`Xkcd` `:23`, `PaleGrey`
  `:837`, `Scarlet` `:1561`). The Brutal DLLs changed hash at identical size (rebuild only). Solution
  builds with 0 warnings against 5402.
- 🔴 **kitchen-sink / IvaForceRender — one compile break, fixed.** `PartModel.AddInstance` (`:408`) and
  `PartModel.ViewportData.Get` (`:314`) now take `IViewport`; the postfix's `__1` in
  `ksa-abstractions.lib/IvaForceRender.cs:98` was retyped `Viewport`→`IViewport`. `Program.MainViewport`
  is now `IGameViewport` (`KSA/Program.cs:485`) and `.Mode` an interface property (`KSA/IViewport.cs:29`)
  — compile-bound reads, no code change. `PartModel..ctor` (`:384`), `Instances` (`:358`), `Template`
  (`:362`), `PartModelModule.Template.Internal/RayTracing/ShadowProxy` (`:40/:32/:15`) and
  `CameraMode.IVA` are unchanged.
- ⚠️ **Open recommendation — mirror `AddInstance`'s new gate in the postfix.** `PartModel.AddInstance`
  now early-returns unless `viewport.HasAny(ViewportOptionFlags.RenderPartModels)` (`:410-413`), and its
  IVA/raytracing branch is per-viewport (`viewport.HasAll(UseRaytracing) && viewport.Mode == IVA`,
  `:415`). A Harmony postfix runs even after that early return, so `AddInstancePostfix` would add an
  internal instance to a list the game never drains for a flag-less viewport. Dead today (every viewport
  the game creates has the flag: `KSA/Program.cs:948,949,952,956`), but cheap to harden:
  `if (!__1.HasAny(ViewportOptionFlags.RenderPartModels)) return;` and read `__1.Mode` rather than
  `Program.MainViewport.Mode`. The stock `(!Template.Internal || viewport.Mode == IVA)` gate (`:424`) the
  feature works around is unchanged. **Needs a live pass** in the editor with Force IVA on.
- ✅ **kitchen-sink Fix-Invisible-Subparts / Flexo clean.** `PartTree.States` (`:39`),
  `ReinitializeDerivedValues(ModuleStateList)` (`:308`), private `RecomputeStaticMass` (`:778`),
  `VehicleEditor.EditingSpace` (`:545`), `VehicleEditingSpace.Parts` (`:16`), `Vehicle.Parts` (`:604`,
  property), `UpdateAfterPartTreeModification` (`:1881`, body identical), `Part.PositionParentAsmb` /
  `Asmb2ParentAsmb` (`:752/:766`, bodies identical), `TreeChildren` (`:666`), `SubParts` (`:1079`),
  `Universe.ExecuteNextVehicleSolvers` (`:1834`, body identical, single overload) — all unchanged.
  `PartTree.cs`'s only diff is `UpdateRenderData` (`IViewport`) plus parachute line rendering.
- ℹ️ **Semantic drift, no action:** `Part.ComputeBoundingBoxVehicleAsmb()` (`:1464`) was refactored into
  `ComputeSubPartBoundingBox(inVehicleAsmb: true)` (`:1484`) and now accumulates **every**
  `MeshViewModule` of each sub-part (`AccumulateMeshBounds`, `:1504`) instead of only the first, so the
  bounds Flexo writes back into `BoundingBoxVehicleAsmb` (`:831`) may be slightly larger for multi-mesh
  subparts. Signature/return type unchanged.
- ✅ **`IvaForceRender.Patch` is wired in the supermod** (`unscience/Patcher.cs:74`); the
  `KitchenSinkSolverPatch` prefix remains standalone-only (`kitchen-sink/Patcher.cs:24`), as before.
- **Live pass wanted:** con-man Apply/Save at a non-100 % Hud Scale (carried over); Force IVA in the
  editor after the `IViewport` retype; skittles theme apply at a non-100 % Interface Scale (carried
  over from 5348).
