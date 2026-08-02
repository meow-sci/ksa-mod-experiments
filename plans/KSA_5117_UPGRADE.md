# KSA Upgrade — `2026.7.9.5018` → `2026.8.3.5117`

Impact review + remediation record for the game update landed on **2026-08-01**.
Supersedes [`KSA_5018_UPGRADE.md`](KSA_5018_UPGRADE.md) as the current baseline record; that document
remains the history for the `4750 → 5018` hop.

Authoritative per-touchpoint detail lives in [`../scope/`](../scope/FULL_SCOPE.md).

---

## Inputs

| Input | Value |
|---|---|
| **NEW** | `2026.8.3.5117` (2026-08-01), revs 5057–5117 — `…/ksa-game-assemblies/current` |
| **OLD / baseline** | `2026.7.9.5018` — git tag `2026.7.9.5018` in the same repo |
| **Intermediate build** | `2026.7.10.5056` (revs 5019–5056) — present as `ksa-game-assemblies_prev`, not separately verified, so the real span is **5018 → 5117** |
| **Live install** | `C:\Program Files\Kitten Space Agency\` — `KSA.dll` reports `2026.8.3.5117`, **matching NEW**, so the default `dotnet build` compiled against the right DLLs with no `KSAFolder` override |

✅ **The changelog covers the whole span** — unlike 4750→5018. The two `version.json` files on disk
cover revs **5019–5056** and **5057–5117** contiguously from the 5018 baseline, so no revision went
unreviewed. Source diff: **223 decomp files** (~11.7k inserted / 4.6k deleted lines) and **103
Content files**.

## Framing — what this update actually changed

Five game-side themes, none of which is a rewrite on the scale of the 4750→5018 combustion/resource
work:

1. **Crew & roster system** (revs 5068/5070/5074/5083/5085/5087/5101–5103) — `KittenRoster`,
   `KittenRosterEntryData`, `IVASeat`, EVADoor↔seat linking via a new `SeatId`, a crew-assignment
   window, vehicle recovery, and a memorial tab. This is the largest behavioral surface for unscience.
2. **Burn / orbit UX** (revs 5086/5092/5093/5106/5110/5111/5113) — a new `BurnContextMenu` (807 lines),
   danger-arc markers, orbit sampling changes.
3. **Launch pads** (revs 5090/5094) — new `LaunchPad` mesh + renderer, collider interaction, ground
   contact.
4. **Vehicle structural destruction** (rev 5115) — vehicles destroyed by structural g-limit or
   dynamic-pressure limit.
5. **Cloud shadows on vessels** (rev 5100) — `GetCloudShadow()` added to the mesh fragment shaders;
   atmosphere push-constants replaced by a per-planet UBO.

Plus the two API cleanups that broke the build: `Double3Ex` direction vectors (5067) and the
flight-computer vacuum-thrust aggregates (5114).

---

## Findings and remediation

### 🔴 1. `Double3Ex.{Up,Down,Left,Right,Forward,Backward}` removed — **FIXED**

- **Class:** removed member (compile break). 15 × `CS0117` in `space-tape.lib`.
- **Evidence:** `KSA/Double3Ex.cs` — 12 lines deleted between the two tags. Changelog rev 5067:
  *"Removed Double3Ex Up/Forward/etc. vectors as they were misleading and often misused"* /
  *"Added named vectors to Camera as they were used legitimately for this purpose in a few cases"* /
  *"Clarified reference frame for camera vectors."*
- **Blast radius:** space-tape only (19 call sites in `PartEditorGizmos.cs`,
  `PartEditorInteraction.cs`, `Thumbnails/SingleSubpartGenerator.cs`,
  `Thumbnails/SubpartThumbnailGenerator.cs`). `Double3Ex.One`/`Zero`/`NaN` survive and are still used.
- **Also in this rev:** `Camera.GetForward/GetRight/GetUp` → `GetForwardEcl/GetRightEcl/GetUpEcl`.
  **No mod in the repo calls the renamed accessors** — verified by repo-wide grep.
- **Fix:** new `ksa-abstractions.lib/Directions.cs` holding the six constants with identical values
  (`Up = double3.UnitY`, `Forward = -double3.UnitZ`, …), so behavior is unchanged. Call sites use
  `Directions.*`.
- **Why not alias `Camera.ForwardView`/`RightView`/`UpView`:** the game kept those specifically for
  camera-view-frame use and rev 5067 explicitly *narrowed* their meaning. Routing frame-agnostic
  gizmo/thumbnail axes through a camera type would re-introduce the ambiguity the game just removed.
  Putting them in `ksa-abstractions.lib` matches that library's stated job — concentrating a game
  update's blast radius in one place.

### 🔴 2. `VehicleConfigInfo.TotalEngineVacuumThrust` removed — **FIXED**

- **Class:** removed member (compile break) **+ semantic drift**. 1 × `CS1061` in `average-twr.lib`.
- **Evidence:** `KSA/FlightComputer.cs` lost `TotalEngineVacuumThrust`,
  `TotalEngineVacuumMassFlowRate`, `TotalEngineExhaustVelocity`, `TotalEngineIsp` **and** the loop in
  `UpdateVehicleConfig` that filled them; gained `AmbientPressure`, `ActiveEngineThrust`,
  `ActiveEngineMassFlowRate`, `LastThrustTime` and `UpdateActiveEnginePerformance()`. Changelog rev
  5114: *"Made the flight computer aware when engines run out of propellant and stop taking credit
  for the thrust they produce"* / *"…dV and TWR ratings reflect the engines that are actually capable
  of producing thrust. **TWR also takes atmospheric pressure into account.**"*
- **Fix:** `TwrDataReader.ComputeMaxAcceleration` now calls
  `vehicle.ComputeActiveThrust(vehicle.FlightComputer.AmbientPressure)` (`KSA/Vehicle.cs:6069`,
  public), which is exactly what the game's own navball TWR uses (`KSA/Vehicle.cs:2454`).
- **Behavior change, accepted deliberately:** readings become ambient-corrected and propellant-aware.
  `NavBallData.ThrustWeightRatio` (what `ReadTwr` returns) **already** changed this way in the same
  rev with no code change on our side, so following the game keeps `ReadTwr` and
  `ComputeMaxAcceleration` measuring the same quantity — as they always did. Reconstructing vacuum
  thrust from `EngineController.VacuumData` was the alternative and was rejected because it would
  have silently desynced the two numbers shown side by side in the UI.
- **Also renamed:** `NavBallData.DeltaVInVacuum` → `DeltaV`. Not referenced by any mod.
- Documented for users in `average-twr/README.md`.

### 🔴 3. `Part` matrix-cache invalidation sentinel changed — **FIXED** (silent; compile-clean)

The highest-value catch of this upgrade: no build error, no renamed symbol, and the mod code
*looked* correct.

- **Evidence:** rev 5112 (*"Added caching for Part.MatrixAsmb2VehicleAsmb, the calculation of which
  was a significant cost at high time warp"*) changed the uncached sentinel:
  - **5018:** `private double4x4 _matrixAsmb = double4x4.Identity;` and the miss test was
    `if (_matrixAsmb == double4x4.Identity)`.
  - **5117:** `private double4x4 _matrixAsmb = UncachedMatrix;` (all-NaN) and the miss test is
    `if (_matrixAsmb.M11.Equals(double.NaN))` (`KSA/Part.cs:536-552,688,732,1035`). Three more cached
    fields were added: `_positionVehicleAsmb`, `_asmb2VehicleAsmb`, `_matrixAsmb2VehicleAsmb`.
- **Impact:** space-tape reflected `_matrixAsmb`/`_matrixAsmb2Parent` and wrote `double4x4.Identity`
  **to invalidate** them, at `PartEditorInteraction.cs:415` and `PartEditorUi.cs:807-808`. On 5117
  `Identity` no longer means "invalid" — it means *"the cached transform is identity"*, so the part's
  transform collapses. The guard went from harmless-redundant to actively corrupting.
- **Fix:** all three sites now call the public **`Part.ResetCachedPosMatrixValues()`**
  (`KSA/Part.cs:1047`), which resets all five caches. It was **already public on 5018**, so the
  reflection was never necessary in the first place. `using System.Reflection` dropped from both
  files; two entries leave the string-reflection watchlist permanently.
- **flexo was never exposed.** Its similar-looking `HingeController.ApplyRotation` re-assigns the
  **property setters** to force invalidation, and those call `ResetCachedPosMatrixValues()` internally
  (`KSA/Part.cs:706,720,758`). Risk `R-flexo-2` in
  [`../scope/part-editor-and-robotics.md`](../scope/part-editor-and-robotics.md) stands, but this rev
  did not trip it.

### ⚠️ 4. Behavioral — open, need a live in-game pass

None of these is a build failure; none can be cleared statically.

| # | Mod | Finding | Status |
|---|---|---|---|
| 4a | **space-tape** | `EVADoorTemplate` gained `[XmlAttribute("SeatId")]` (rev 5085) and the EVA button now only appears when the door's aligned `IVASeat` is occupied. `GameDataXmlSerializer.SerializeEVADoor` emits no `SeatId`, so authored EVA doors will be **inert**. Its existing `ConnectorId` attribute was never an `EVADoorTemplate` member on 5018 either — **pre-existing** silent no-op. | **Open.** Needs `SeatId` on `EVADoorState` + editor UI + writer; deferred because it needs a UI decision on how the user picks a seat id. |
| 4b | **doh** | `KittenSpawner` replicates the *old* `EVADoor.CreateKittenEva()`; the game now resolves the kitten from `Universe.KittenRoster` via the aligned `IVASeat`, and `Vehicle` disposal finalizes kitten mission stats (revs 5074/5083/5085/5102/5103). `KittenEva`'s ctor is signature-identical so doh still runs, but spawns **roster-less** kittens. | **Open** — live pass. |
| 4c | **con-man** | New `GaugeCanvas.PlaceBesideActiveBurnGizmo()` (revs 5092/5113) **writes `_customOffset`** — the exact private field con-man reflects and owns — to park the burn window beside the burn gizmo. | **Open** — live pass. |
| 4d | **garrys-torch, kiwis-marbles** | Rev 5115 added vehicle destruction on structural g-limit / dynamic-pressure limit. Torch teleports the vehicle every frame; marbles rewrites orbits. | **Open** — live pass. |
| 4e | **parts-now** | `EditorTag` gained `Booster`/`Coupling`/`Cargo`; `BuiltInEditorTags` still lists six. Harmless today — none of the three is registered in `VehicleEditor._editorTagLookup` (which force-registers only the original six) nor declared in `Content/Core/PartGameData.xml`. | **Watch item.** |

---

## Verified clean

- **All 33 Harmony patch-target signatures byte-identical**, including every shared chokepoint:
  `GameSettings.OnKeyAll(GlfwKeyEvent) → bool` (HotkeyGuard → **every** top-level mod),
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)` (still a single overload),
  `Program.DrawProgramMenusHook()`, `Program.DrawMenuBar(Viewport,int)`, all three
  `*Module.UpdateRenderData(in double4x4, bool, Viewport, int)`, `PartModel(.Dynamic).AddInstance`,
  `PartModel..ctor(PartModelModule.Template)`, `SuperMeshRenderSystem.RenderMainPass(CommandBuffer)`,
  `ShaderModuleUtils.FromFile`, `OrbitController/FlyController.OnFrame`,
  `Camera.ChangeFieldOfView/UpdateProjection`, `Vehicle.GetWorldMatrix/UpdateRenderData/Teleport/
  RefillConsumables`, `GaugeCanvas.OnDrawMenuBar`, `Celestial.SetOrbit/UpdatePerFrameData`,
  `IOrbiter.ShowOrbit`, `EngineController.SetIsActive`, `PartTree.CreateFromNewPartTree`.
- **The entire string-reflection watchlist resolves** — `Camera._fovRadians`, all 7 `GaugeCanvas`
  fields, the `KittenEva._renderable` → `_characterAvatar` → `CharacterAvatar.Core` →
  `CharacterCore.Scale` chain, `LightModule+TemplateData.ColorRgb`, `CatExpressionAnim._expressionPose`,
  all six parts-now `ModLibrary.All*` registries, `SerializedCollection<T>._collection`/`GetList`/`Find`,
  `VehicleEditor._editorTagLookup`, `PartTree.RecomputeStaticMass`,
  `ResourceManagerBase.NearestToFurtherest*`. **`ModLibrary.cs`'s 42-line diff is _only_ log
  line-number churn.**
- 🔶 **Both standing invariants HOLD.** `PerInstanceData.StateBitFlag` bits 11..31 remain unused by
  the game (it uses ≤ bit 6 in the mesh shaders) → humble-arteest Vehicle Paint safe.
  `ModLibrary.LoadAll()` (`Program.cs:965`) still precedes `ModLibrary.Bind()` (`:994`) →
  parts-now's `[StarMapAllModsLoaded]` mesh-headroom reservation still lands in the right window.
- **humble-arteest's GLSL anchors survive.** `MeshIndirect.frag` / `MeshIndirectRaytraced.frag` did
  change (rev 5100 cloud shadows) but only inside `getLightColor()`; the `vec3 sampledColor` anchor
  (`:114` / `:156`) and the `inStateFlags` varying (`:30` / `:20`) are intact, and the new
  `GetCloudShadow` resolves from `Common/Lighting.glsl:52` through the game's own include callback,
  which the mod passes straight through.
- **Byte-identical decomp files:** `PartModel`, `PartModelDynamic`, `MaterialData`,
  `GpuMaterialSystem`, `SuperMeshRenderSystem`, `PartModelRenderer`, `LightModule`, `LightSwitch`,
  `CatExpressionAnim`, `CharacterAvatar`, `CharacterCore`, `KittenRenderable`, `GenericGizmo`,
  `OrbitLinePass`, `Controller`, `KeyframeAnimationModule`, `DeviceMeshInterleaved`, `AssetManager`,
  `GpuObjectSystem`, `SerializedCollection`, `ShaderModuleUtils`, `Situation`,
  `KinematicMeasurements`, `KSAColor`, `MusicPlayList`.
- **thug-life:** `UnlitMesh*` shaders byte-identical and both ids still in `DefaultAssets.xml`. The
  MSAA / alpha-to-coverage rework (revs 5057/5058) is absorbed because the mod reads
  `Program.OffScreenPass.SampleCount` dynamically rather than hard-coding a sample count.
- **mesh-deform:** `MeshIndirect.vert` byte-identical → still self-disabled, unchanged; **not** a new
  regression.
- **Assets removed in revs 5077/5096** — `IconSymmetry*`, `IconPlaceGizmo`, `IconRotateGizmo`,
  `IconTranslateGizmo`, `IconScaleGizmo`, `IconAngleSnapping*`, `PlanetMeshVertexDataComp` — are
  referenced by **no** mod in this repo.
- **Toolchain:** builds clean with `TreatWarningsAsErrors` and **0 warnings**, so the Brutal packages
  shipped no nullability/signature shift in the ImGui surface actually used (contrast the rev-4729
  bump, which cost `garrys-torch.lib` a CS8604).

## Verification performed

- `dotnet build ksa-mod-experiments.slnx` against the live `5117` install — **all 55 projects,
  0 warnings, 0 errors.**
  - ⚠️ Worth recording: the *first* build failed in only `space-tape.lib` and `average-twr.lib`, which
    skipped `space-tape`, `average-twr` **and the `unscience` supermod**. A green count of "52 of 55
    projects" is **not** proof the rest is clean — the full build after the fixes is what confirmed no
    further errors were hiding behind them.
- Full string-reflection watchlist re-grepped against `<NEW>`.
- Every Harmony patch target signature-diffed `git show 2026.7.9.5018:… ` vs working tree.
- `PerInstanceData` / `MaterialData` byte layouts and every referenced GLSL + asset id diffed.
- Both `version.json` changelogs read and filtered against the coupling table.
- **There is no test suite in this repo** — `dotnet build` plus a live in-game pass is the whole
  verification story.

## Still required — live in-game pass

A green build covers a small fraction of the risk. Outstanding:

1. The five behavioral items in §4 (space-tape EVA doors, doh kitten spawning, con-man layout
   offsets, torch/marbles vs structural destruction, parts-now tag validation).
2. Render correctness: thug-life's quad (MSAA/alpha-to-coverage rework), humble-arteest's paint +
   engine emissive (shader anchors moved lines), blinky / its-so-shiny grid scale and lighting,
   space-tape / flexo gizmos and grid lines.
3. Whether any `ISSUES.md` entry is explained or closed by this build.

Practical route: launch KSA, open the unscience window (**F11**) — 23 submods load through it — then
exercise the implicated mods. `unladen-swallow`'s HTTP endpoints (`0.0.0.0:7887`) drive blinky /
its-so-shiny / glass / camera / torch without UI clicking.
