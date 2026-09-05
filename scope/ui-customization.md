# UI / Customization Mods — Game Integration Scope

## Workspace integration (current)

Active bundled features: **skittles, con-man, kitchen-sink**. Each implements `IWorkspaceFeature` with explicit draft bindings and typed `ILiveStateItem` providers; its old standalone entry project is retired. See [workspace contract](../docs/WORKSPACE.md).

Skittles edits a detached ThemeDefinition; only Apply mutates global ImGui style. Its native style editor is in Live State. Con Man reads legacy layout files into detached GaugeState data; ApplyDefinition writes the same GaugeCanvas fields and calls ReinitializeDerivedValues. Active gauge layout/startup controls live in the inspector. Kitchen Sink retains explicit Force IVA policy; the old Flexo experiments are removed. Global style/layout changes can affect both workspace windows, but workspace load does not apply either policy.

The tables below describe retained game touchpoints. Dated upgrade investigations are preserved in the historical reference linked below; current UI and persistence ownership is stated explicitly here.


Permanent reference for detecting when KSA game updates break the UI/customization
mods (`skittles`, `con-man`, `kitchen-sink`). Every game-facing member these mods
touch is enumerated and verified against decompiled sources.

**@5402** unless a cell says otherwise. **Mod code** paths are relative to the repo root
`~/repos/meow-sci/unscience`.

**Host lifecycle** — The single Unscience host initializes and updates these feature libraries, independently of authoring visibility. HotkeyGuard remains in `unscience/Patcher.cs`; feature Harmony groups are registered by their owning libraries through `ConfigureRuntime`. See [architecture](00-architecture-and-abstractions.md).

**Distinction — ImGui is third-party, not KSA.** skittles and parts of con-man drive
`Brutal.ImGuiApi` (the bundled Dear ImGui wrapper), which is shipped with the game but
is *not* KSA game code. Those rows are tagged `(ImGui)` in the Risk column. The only
genuinely KSA-owned surfaces here are `GaugeCanvas` (con-man), the
editor/part/vehicle/`PartModel` types (kitchen-sink), and `KSAColor` (button accents).

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

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Named theme template or complete detached style/color definition. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 3 | `skittles.lib/ThemeDefinition.cs`; `ThemeManager.cs` | `ImGui.GetStyle() : ImGuiStylePtr` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None (`:5431`) | (ImGui) core of the whole mod |
| 2 | 3 | `ThemeDefinition.cs` | `ImGuiStylePtr.Colors[i] : float4` (backing `ImGuiStyle.Colors : float4_60`) | `Brutal.ImGuiApi/ImGuiStyle.cs` | Yes | None | (ImGui) 60-slot inline array; index = `ImGuiCol` |
| 3 | 3 | `ThemeDefinition.cs` (capture) / `:182-238` (apply); `ThemeManager.cs` | `ImGuiStylePtr` style vars (Alpha, WindowRounding, WindowBorderHoverPadding, TabCloseButtonMinWidth*, FramePadding, ItemSpacing, AntiAliased*, … 72 members) | `Brutal.ImGuiApi/ImGuiStylePtr.cs` (72) / `ImGuiStyle.cs` | Yes | None (member-set diff empty; 72==72) | (ImGui) all read+write fields present |
| 4 | 3 | `ThemeDefinition.cs`; `ThemeSerializer.cs` | `ImGuiCol` enum, 60 slots `Text`(0)…`ModalWindowDimBg`(59), then `COUNT` | `Brutal.ImGuiApi/ImGuiCol.cs` | Yes | None (Text:5…ModalWindowDimBg:64, COUNT:65) | (ImGui) hard-coded `60` count matches |
| 5 | 3 | `SkittlesSubmod.cs` | `ImGui.ShowStyleEditor(ImGuiStylePtr ref = default)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) |
| 6 | 3 | `ThemeManager.cs` | `ImGui.StyleColorsDark/Light/Classic(ImGuiStylePtr dst = default)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) |
| 7 | 3 | `SkittlesSubmod.cs` | `ImGui.GetColorU32(ImGuiCol, float) : ImColor8` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) feeds PushStyleColor |
| 8 | 3 | `SkittlesSubmod.cs` | `KSAColor.Xkcd.Scarlet`/`.PaleGrey : Color.Preset` | `KSA/KSAColor.cs` (class `Xkcd`:23) | Yes | None (same lines+RGB) | **KSA type** — delete-button accent only |
| 9 | 3/6 | `unscience/Mod.cs` | `ImGui.IsKeyPressed(ImGuiKey.F11)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) window toggle |
| 10 | 1 | `unscience/Patcher.cs` | `HotkeyGuard.Patch` (abstraction; patches `Brutal.ImGuiApi` IO) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard, not game-typed |

**Game assets referenced** — None.

## con-man

**Purpose** — HUD gauge layout manager. Saves/restores per-`GaugeCanvas` visibility,
drag offset, and scale as named `.toml` layouts, with an optional startup-default
layout. Reads/writes private `GaugeCanvas` fields via reflection (the game exposes no
public setters), then force-repositions the live ImGui windows by title.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Detached gauge visibility, offsets and scales; selected legacy layout. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 2 | `con-man.lib/GaugeStateAccessor.cs` | `GaugeCanvas._canvases : private static List<GaugeCanvas>` (NonPublic+Static) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:92, byte-identical) | **string-based reflection — required field** (IsValid gate). Public read-only mirror `AllCanvases` exists at `:177` |
| 2 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._enabled : private bool = true` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:143) | **string-based — required field**. Public `SetEnabled(bool)`/`ToggleEnabled()` at `:664/:669` OR in `AlwaysEnabled` |
| 3 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._customOffset : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:134) | **string-based — required field** |
| 4 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._customScale : private float2 = float2.One` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:136) | **string-based — required field** |
| 5 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._windowPosition : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:130) | string-based — **optional** (not in IsValid; degrades to `float2.Zero`) |
| 6 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._windowSize : private float2` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:132) | string-based — **optional** (degrades to `(100,100)`) |
| 7 | 2 | `GaugeStateAccessor.cs` | `GaugeCanvas._windowTitle : protected string` (NonPublic+Instance) | `KSA/GaugeCanvas.cs` | Yes | None (OLD:115; `protected` since 5018, still NonPublic) | string-based — **optional** (skips reposition if null) |
| 8 | 3 | `LayoutManager.cs`; `ConManSubmod.cs` | `GaugeCanvas.Id : string` (inherited `SerializedId.Id { get; set; }`) | `KSA/SerializedId.cs` | Yes | None | layout dictionary key; GaugeCanvas→GaugeBase→SerializedId |
| 9 | 3 | `LayoutManager.cs` | `ImGui.SetWindowPos/SetWindowSize(ImString name, in float2, ImGuiCond)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) reposition live window by title; `string`→`ImString` |
| 10 | 3 | `ConManSubmod.cs` | `ImGui.GetStyle()` + `ImGuiStylePtr.ItemSpacing/FramePadding` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) layout math |
| 11 | 3 | `ConManSubmod.cs` | `KSAColor.Xkcd.Scarlet`/`.PaleGrey` + `ImGui.GetColorU32` | `KSA/KSAColor.cs` | Yes | None | **KSA type** — delete-button accent |
| 12 | 3/6 | `unscience/Mod.cs` | `ImGui.IsKeyPressed(ImGuiKey.F11)` | `Brutal.ImGuiApi/ImGui.cs` | Yes | None | (ImGui) window toggle |
| 13 | 1 | `unscience/Patcher.cs` | `HotkeyGuard.Patch` (abstraction) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard |

**Game assets referenced** — None.

## kitchen-sink

**Purpose** — Retained Force IVA authoring/apply policy and editor derived-value repair helper. Flexo transform experiments and their solver patch are removed. `IvaForceRender` owns the shared template and render-instance changes.

**Unscience integration** — The feature implements `IWorkspaceFeature` in its own library. Unscience owns initialization, update, disposal and shared Harmony wiring. Draft restore changes authoring data only; typed live items expose applied state independently.

**Wiring** — `unscience/Patcher.cs` applies/removes `IvaForceRender`. `KitchenSinkSubmod.Dispose()` restores the override; no Kitchen Sink vehicle-solver patch remains.

**UI / hotkeys** — F11 opens the shared Unscience workspace. Select this feature to author settings; use Live State for applied-item controls. There is no standalone feature window or feature-specific top-level hotkey.

**Persistence** — Whether to enable forced IVA rendering. Disclosure and authoring scroll state are saved too.
Feature presets retain settings and asset choices while leaving the current target selections intact. Runtime data is excluded.

**Integration points**

| # | Kind | Mod code (file) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | 3 | `KitchenSinkLib.cs` | `Program.Editor : static VehicleEditor?` | `KSA/Program.cs` | Yes | None (OLD:207) | null-guarded |
| 2 | 3 | `KitchenSinkLib.cs` | `VehicleEditor.EditingSpace : VehicleEditingSpace` (field) | `KSA/VehicleEditor.cs` | Yes | None (OLD:545) | |
| 3 | 3 | `KitchenSinkLib.cs` | `VehicleEditingSpace.Parts : PartTree?` (field) | `KSA/VehicleEditingSpace.cs` | Yes | None (OLD:16; file diff is 3 `Viewport`→`IViewport` draw signatures) | null-guarded |
| 4 | 3 | `KitchenSinkLib.cs` | `PartTree.States : ModuleStateList` (field) | `KSA/PartTree.cs` | Yes | None (OLD:39) | passed as `oldStates` |
| 5 | 3 | `KitchenSinkLib.cs` | `PartTree.ReinitializeDerivedValues(ModuleStateList oldStates) : void` | `KSA/PartTree.cs` | Yes | None (OLD:308; 0-arg overload `:302`) | `ModuleStateList.cs` byte-identical |
| 6 | 1 | `kitchen-sink.lib/IvaForceRender.cs` | `PartModel..ctor(PartModelModule.Template)` **protected** (Harmony postfix via `AccessTools.Constructor`) | `KSA/PartModel.cs` | Yes | None (OLD:383; body identical, only ctor) | catches parts built after toggle |
| 7 | 1 | `IvaForceRender.cs` (lookup), `:98` (postfix sig) | `PartModel.AddInstance(PerInstanceData, IViewport, int frameIndex) : void` (Harmony postfix; captures `__0`,`__1` only) | `KSA/PartModel.cs` | Yes | **RETYPED @5402** `Viewport`→`IViewport` (OLD:407) — postfix `__1` updated; **NEW GATE @5402** `:410-413` early-returns unless `viewport.HasAny(ViewportOptionFlags.RenderPartModels)`; IVA/raytracing gate `:415` now per-viewport | method is **3-arg**; postfix ignores `frameIndex` (`__2`). Postfix mirrors the RenderPartModels and per-viewport IVA gates. |
| 8 | 3 | `IvaForceRender.cs` | `PartModel.PerInstanceData` (struct) | `KSA/PartModel.cs` | Yes | None (OLD:331) | postfix param `__0` |
| 9 | 3 | `IvaForceRender.cs` | `PartModel.ViewportData.Get(PartModel, IViewport) : ViewportData` → `.InstanceList : List<PerInstanceData>` `.Add` | `KSA/PartModel.cs` | Yes | **RETYPED @5402** param `Viewport`→`IViewport` (OLD:313/309); lookup keyed by `viewport.Id : ViewportId` | nested class `ViewportData`:308 |
| 10 | 3 | `IvaForceRender.cs` | `PartModel.Instances : static List<PartModel>` | `KSA/PartModel.cs` | Yes | None (OLD:357) | enumerated on toggle-on |
| 11 | 3 | `IvaForceRender.cs` | `PartModel.Template : PartModelModule.Template` (field) | `KSA/PartModel.cs` | Yes | None (OLD:361) | |
| 12 | 3 | `IvaForceRender.cs` | `PartModelModule.Template.Internal : bool` (field) | `KSA/PartModelModule.cs` | Yes | None (OLD:40) | the field flipped to force visibility |
| 13 | 3 | `IvaForceRender.cs` | `PartModelModule.Template.RayTracing : RaytracingMode` + `RaytracingMode.ShadowProxy` | `KSA/PartModelModule.cs` | Yes | None (OLD:32/15) | shadow-proxy skip in editor postfix |
| 14 | 3 | `IvaForceRender.cs` | `Program.Editor` (null check, editor-preview gate) | `KSA/Program.cs` | Yes | None | |
| 15 | 3 | `IvaForceRender.cs` | `Program.MainViewport : IGameViewport` `.Mode : CameraMode { get; }` `== CameraMode.IVA` | `KSA/Program.cs`; `IViewport.cs` (impl `ViewportBase.cs`); `CameraMode.cs` | Yes | **RETYPED @5402** — `MainViewport` was `Viewport` (OLD Program:468), `Mode` was a public field (OLD `Viewport.cs`); `CameraMode.cs` identical | compile-bound read; no code change |
| 16 | 1 | `unscience/Patcher.cs` | `Universe.ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` (Harmony prefix; captures `dtPlayer` by name) | `KSA/Universe.cs` | Yes | None (OLD:1767; body identical) | method is **2-arg**; prefix declares only `dtPlayer` — valid; single overload so `AccessTools.Method` is unambiguous |
| 28 | 1 | `unscience/Patcher.cs` | `HotkeyGuard.Patch` (abstraction) | `ksa-abstractions.lib/HotkeyGuard.cs` | n/a | None | shared guard |

**Game assets referenced** — None (operates on already-loaded `PartModel`/`PartTree`
instances; no `Content/` paths).

## Historical evidence

See [dated integration and upgrade reference](history/ui-customization.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Current runtime release behavior

Applying a layout captures the affected gauge objects’ original enabled flags, offsets and scales. Release/unload restores those fields and window geometry. A saved preferred layout is not applied at startup. Startup does not apply the saved theme. The first actual style mutation captures a baseline; release/unload restores that baseline. The native style editor is available only while style ownership exists. IVA integration is owned here, not in ksa-abstractions. Both render hooks are gated by the applied policy. Disable/unload restores mutated Internal flags before releasing hooks.

Feature hook targets retain their existing signatures; patch ownership now follows explicit demand through the shared runtime coordinator. Native acceptance remains outstanding.
