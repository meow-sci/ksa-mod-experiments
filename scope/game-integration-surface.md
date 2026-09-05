# Game Integration Surface — master index (unscience KSA mod suite)

## Creative tool additions — current

- [Zippo Disco](celestial-and-lights.md): direct instance-local `LightModule.Template` copies with owned ColorRgb/FloatReference channels; `KeyframeAnimationModule.Shared.{Duration,PartLookup}` and `TimeGoal` drive matching light assemblies. Restore is ownership-checked; no new Harmony or shader target.
- [Humble Arteest cursor paint](character-and-materials.md): `Cursor.GetEgoRay`, all render `MeshReference.PositionsCompare` primitives, `Ray.RaycastWatertight` and exact static/dynamic (including gimbal) part matrices. Existing paint Harmony handoffs add mesh ID and submitting-model identity; per-instance/shared-mesh records retain the existing StateBitFlag layout.
- [Graffiti spray](decals.md): held mouse/UI capture gating and monotonic cadence call the existing placement path. No added game render dependency.

All new configuration is authoring-only persistence; pending gestures are cancellable and applied effects remain live. Managed timing/input checks are in `unscience-contracts.tests`; native acceptance remains required.

## Workspace redesign — current ownership

The shipping host now has **25 independent feature libraries**, one StarMap/Harmony host, shared contracts/lights/rings infrastructure and a pure contract-check project. See [architecture](00-architecture-and-abstractions.md), [project index](../REPOSITORY_INDEX.md) and the per-area **Workspace integration (current)** sections. `IWorkspaceFeature` extends the lifecycle with explicit detached draft capture/preparation and typed `ILiveStateItem` collection. Every retained feature is adapted; standalone feature entry projects and the RPC service are retired.

New shared target surface: `PartIdentity` reads `Vehicle.Id`, `Vehicle.Parts.Parts`, `Part.Id`, `Part.Template.Id`, `Part.SubParts`, `Part.InstanceId` and `ImGui.GetFrameCount`. `Part.InstanceId` is regenerated and `PartInstance.GlobalInstanceId` is XmlIgnore; persisted selections use verified topology/path and reject changed topology. `DraftChoice` retains exact missing IDs or an explicit controlled-vehicle selector. `LiveIdentity` is process-only. See the architecture page for persistence limitations.

Moved integration owners: light TemplateData/ColorRgbReference/LightSwitch helpers now live in `ksa-lights.lib/LightController.cs`; ring catalog/mesh reflection now lives in `ksa-rings.lib`. `RingOwnership.BeforeReplace` coordinates outgoing PlanetaryRings references between Bloom and Rocky before rebuild. These moves add no Harmony target. New recipe/inspector files invoke the same typed game members recorded in each area; saving/loading never invokes them.

Removed owners: unladen-swallow, steely-eyed-missile-kitten, stampy, space-tape, red-alert, mesh-deform, marque, inanimate-carbon-rod, grant, flexo and blinky. Rows owned only by those features are removed from the active type table. Their source citations and dated upgrade notes are preserved only in the historical archive. In particular BlinkyPatches, the old mesh-deform shader path and all RPC routes are absent.


Single consolidated lookup of every game-side touchpoint (KSA.* types + risk-bearing game-shipped
Brutal.*/RenderCore.* members) across all unscience mods, aggregated from the per-area `scope/`
files. Use it on every KSA update to find which mods a changed game member puts at risk.

**Verification baseline:** cataloged against KSA build **2026.9.7.5402**
(`~/repos/meow-sci/ksa-game-assemblies/current/decomp`), diffed from the previously verified baseline
**2026.8.22.5348**, which is also what sits on disk as `ksa-game-assemblies_prev`. **Baseline == OLD**,
a single hop — but ⚠ **the changelog gap is 52 revisions**: NEW's `version.json` covers only
`5400 → 5402` (one logged commit, rev 5401), so revisions **5349–5400** were reviewed from the **source
diff only** (197 `KSA/*.cs` changed, 66 added, 2 removed). Full record:
[`../plans/KSA_5402_UPGRADE.md`](../plans/KSA_5402_UPGRADE.md).
Decomp paths are relative to the decomp root (`KSA/…`); Content paths relative to `…/current/Content`.
Per-row detail and the exact 5261↔5348 diff live in the linked area scope files.

---

## How to use on a game update

- **Re-grep each row's Decomp path in the new build.** If a member moved only (line shift), it's fine;
  if it was renamed / removed / re-signatured / moved namespace, every mod in that row's **Used by**
  column is at risk — open the cited area scope file and the **Mod code ref** to assess.
- **Prioritize the string-reflection watchlist (section 4) first.** Those lookups are NOT compile-checked,
  so a game rename breaks them **silently at runtime** (no build error). They are the highest-value checks.
- **Then rebuild the solution (`dotnet build`) to catch typed breaks.** Any row marked *direct API /
  typed* that changed signature surfaces as a compile error; the build is the safety net for everything
  in section 3 that is not string-based.
- **Check the shaders & assets subtable (section 5) by reading the shipped files**, not just the C#:
  several mods edit GLSL by anchor-string and depend on asset ids — a shader refactor breaks them with
  no C# change (humble-arteest Vehicle Paint was rebuilt for 5018 and
  now fails loudly instead of silently if its anchor moves).
- **Re-verify the 🔶 standing invariants.** These are facts about the game that no symbol lookup and
  no compile can check, and each one fails silently:
  - `PerInstanceData.StateBitFlag` bits **11..31** are still unused by KSA (humble-arteest Vehicle
    Paint) — see `KSA.PartModel` below and [`character-and-materials.md`](character-and-materials.md).
  - **Parts Now buffer relocation remains at the pre-GUI boundary.** New offsets must fit the allocations before Bind. Vertex/index offsets remain stable when raster buffers relocate; an active RaytracingRenderer caches addresses and blocks relocation. Released ranges may only rewind a contiguous unowned tail. See [part/editor integration](part-editor-and-robotics.md).
- **Watch the Harmony keystones** that fan out to many mods: `Universe.ExecuteNextVehicleSolvers`,
  `GameSettings.OnKeyAll` (HotkeyGuard), the three `*Module.UpdateRenderData` render prefixes,
  `PartModel.AddInstance`, `PartModelRenderer.UpdateRenderData`, and the `VehicleProvider` enumeration
  chain — one change here breaks several mods at once.

Status legend: **OK** unchanged 4750→5018 · **CHANGED** signature/shape changed · **BROKEN** non-functional
against 5018 (compile or silent runtime) · **ADDITIVE** new in 5018, not yet consumed.

---

## 3. Master table — by game type

> "Used by" lists every consuming mod (merged). Members reached through `ksa-abstractions.lib` helpers
> (`VehicleProvider`/`CelestialProvider`/`SimTimeProvider`/`PartHelpers`/`HotkeyGuard`)
> note the helper. Nested types are rows under their owner's subheader.

### KSA.AnimatedRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SetAnimation(IAnimation, float blend=0.2f)` | direct API | `KSA/AnimatedRenderable.cs` | kitten-animations | `kitten-animations.lib/KittenAnimationDriver.cs` | OK | forced-clip playback; no-op when the clip is already current (`BoneAnimRuntime.SetAnimation:92`) so safe per frame |
| `PlayAnimation(IAnimation, float blend=0.2f)` | direct API | `KSA/AnimatedRenderable.cs` | kitten-animations | `KittenAnimationDriver.cs` | OK | restart-from-frame-0 for the forced clip |
| `UpdateAnimation(double dt)` | **Harmony prefix** `(AnimatedRenderable __instance, ref double dt)` | `KSA/AnimatedRenderable.cs` | kitten-animations | `KittenAnimationPatches.cs` | OK | ⚠️ hot path — the only point in the frame where an animation override survives `KittenRenderable.UpdateRenderData`. Also scales `dt` for the playback-rate control |
| `FreezeAnimation : bool` | direct API | `KSA/AnimatedRenderable.cs` | kitten-animations | `KittenAnimationDriver.cs` | OK | freeze/pause the forced clip; released back to the game on override off |
| `AnimProcessors : List<IAnimProcessor>` | direct API | `KSA/AnimatedRenderable.cs` | kitten-animations | `KittenExpressionController.cs` | OK | mod **appends** its own `CatExpressionAnim` here (and removes it on unbind) |
| `MaterialIndices : protected int[]` | reflection-field | `KSA/AnimatedRenderable.cs` | doh, free-fallin | `doh.lib/Spawning/KittenSpawner.cs`; `free-fallin.lib/FreeFallinPatches.cs` | OK @5402 | in-place handle swap; free-fallin writes canopy material slot zero immediately before each chute draw and restores observed renderables on disable/unload |

### KSA.AssetBundle
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AssetBundle` (`[XmlRoot("Assets")]`) + `Assets : List<SerializedId>` (field) | direct API | `KSA/AssetBundle.cs` | parts-now | `parts-now.lib/Runtime/BundleParser.cs`; `BundleParserQueries.cs` | OK | deserialized **without** side effects for validation; classification helpers test most-derived type first |
| `OnDataLoad(Mod) : override void` | direct API | `KSA/AssetBundle.cs` | parts-now | `Runtime/RuntimeModLoaderStates.cs` | OK | the single call that registers a bundle's templates/materials/loaders into `ModLibrary` |

### KSA.AssetManager<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AssetMap : protected ConcurrentDictionary<AssetName,T>` | reflection-field (hierarchy) | `KSA/AssetManager.cs` | doh, humble-arteest | `doh.lib/Materials/MaterialSystemAccessor.cs`; `humble-arteest.lib/KittenColor.cs` | OK | walks base types |
| `GetOrLoad(AssetName) : T` | reflection-method | `KSA/AssetManager.cs` | doh | `MaterialSystemAccessor.cs` | OK | returns `GpuObjectAssetRef` |

### KSA.Asteroid / KSA.Comet
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.Astronomical
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Id : virtual string { get; protected set; }` (via `IObjectId`) | direct API | `KSA/Astronomical.cs` |VehicleProvider (→ most mods), eternal-flame, garrys-torch, i-feel-seen, kitchen-sink| `ksa-abstractions.lib/VehicleProvider.cs`; `eternal-flame.lib/EternalFlameLib.cs` | OK | `Vehicle.Id` resolves here (not declared on `Vehicle`) |

### KSA.AtmosphereReference / KSA.PhysicalAtmosphereReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `PhysicalAtmosphereReference.GetAtmosphericPressure(Camera) : static double` | direct API | `KSA/PhysicalAtmosphereReference.cs` | pyro | `pyro.lib/PlumePhysics.cs` | OK @5348 | returns **atm**; pyro converts to Pa for `PlumeData` |

### KSA.Battery
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Battery` (type, `Get<Battery>()`) | direct API | `KSA/Battery.cs` | its-so-shiny | `its-so-shiny.lib/ShinyGridBuilder.cs` | OK | battery anchors for power partitioning |
| `Refill(ref BatteryState state) : void` | direct API | `KSA/Battery.cs` | eternal-flame | `eternal-flame.lib/EternalFlameLib.cs` | OK | insulates mod from rev-4681 electrical refactor (body unchanged) |
| `MaximumCapacity : required Joules` | direct API (indirect) | `KSA/Battery.cs` | eternal-flame | via `Refill` | OK | read only inside `Refill`; mod never names `Joules` |

### KSA.Camera
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `_fovRadians : private float` | reflection-field (PRIVATE) | `KSA/Camera.cs` | glass | `glass.lib/GlassPatches.cs` | OK @5402 | **single highest-risk glass check — string private field; rename = silent FOV break.** Injection is now gated to `ViewportRegistry.IsMainCamera`, preserving independent secondary-camera FOV. |
| `ChangeFieldOfView(float change) : void` | Harmony pre + reflection-method (string) | `KSA/Camera.cs` | glass | `GlassPatches.cs` | OK @5402 | prefix returns false only for main-camera instances while override is active |
| `UpdateProjection() : void` | Harmony pre + reflection-method (string) | `KSA/Camera.cs` | glass | `GlassPatches.cs` | OK @5402 | injects `_fovRadians` for main Base/Map cameras only, then original rebuilds projection |
| `GetPositionEgo(IPosition) : double3` | direct API | `KSA/Camera.cs` | pyro, graffiti, hot-pursuit | `pyro.lib/PlumeEmitter.cs`; `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | OK @5402 | emitter/decal/picker positions are camera-ego |
| `NearbyCelestial : Celestial? { get; set; }` | direct API | `KSA/Camera.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | KSA's `OnFrameCelestials` only populates the main/frame camera; Hot Pursuit synchronizes this state for each mounted secondary camera so the local body is excluded from distant-sphere rendering. |
| `DistanceToNearbyCelestialKm` / `DistanceToNearbyCelestialSurfaceMeanKm` / `CurrentAltitudeKm` / `NearbyCelestialTerrainHeight` (public fields) | direct API (write) | `KSA/Camera.cs` | hot-pursuit | `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | mirrors the values assigned by KSA's main-camera `OnFrameCelestials`, including terrain-relative altitude, for secondary atmosphere/celestial consumers. |
| `GetFieldOfView() : float` (RADIANS) | direct API | `KSA/Camera.cs` | glass | `glass.lib/FovController.cs` | OK @5402 | getter returns radians; setter takes degrees (asymmetry) |
| `SetFieldOfView(float fovDegrees) : void` | direct API | `KSA/Camera.cs` | glass, hot-pursuit | `glass.lib/FovController.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs`, `HotPursuitPose.cs` | OK @5402 | setter takes degrees; Hot Pursuit clamps UI to KSA's 15–120 range |
| `GetPositionEgo(IPosition) : double3` | direct API | `KSA/Camera.cs` | i-feel-seen | `i-feel-seen.lib/IFeelSeenPatches.cs` | OK | |
| `Following : IFollowable? { get; }` · `SetFollow(IFollowable,bool,bool,bool)` | direct API | `KSA/Camera.cs` | camera-controller-override, hot-pursuit | `AnimationHelpers.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs`, `HotPursuitPose.cs` | OK @5402 | Hot Pursuit must pass `changeControl:false`; otherwise camera setup changes `Program.ControlledVehicle`. |
| `LookAtRotation(double3 fwdEcl, double3 upEcl) : doubleQuat` (static) | direct API | `KSA/Camera.cs` | camera-controller-override, hot-pursuit | `AnimationHelpers.cs`; `Animation/Animations/Spiral*Animation.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Hot Pursuit writes the result to `WorldRotation`. |
| `PositionEcl` / `WorldRotation` overrides · `EgoToEcl(double3)` | direct API (write/read) | `KSA/Camera.cs` | hot-pursuit | `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Same-frame mounted pose. `Camera.OnFrame` subsequently terrain-clamps to 0.5 m AGL and bakes view/frustum state. |
| `MVP.viewProjection` | direct (render) | `KSA/Camera.cs` (used `Program.cs`) | thug-life | `thug-life.lib/ThugLifeQuadRenderer.cs` | OK | per-frame quad MVP, from the **rendered** viewport's camera |
| `Unfollow(bool changeControl = true)` | direct API | `KSA/Camera.cs` | parts-now | `parts-now.lib/Runtime/PartThumbnailGenerator.cs` | OK | ⚠ **must** be called as `changeControl: false` — the defaulted overload nulls `Program.ControlledVehicle` and would drop the player's vessel mid-flight |
| `OnFrame(double dt)` · `LocalPosition`/`LocalRotation`/`LocalScale` (inherited `Transform3D`) | direct API (write) | `KSA/Camera.cs`; `KSA/Transform3D.cs` | parts-now | `PartThumbnailGenerator.cs` | OK | INVARIANT: the thumbnail camera is only ever **re-asserted** to origin/identity — the part is moved, never the camera (`ThumbnailCreator.MoveRootPart` assumes a camera parked at the origin) |
| `GetFieldOfView() : float` (RADIANS) · `NearPlane : float => 0.1f` | direct API (indirect) | `KSA/Camera.cs` (the row above cites `:702` from the 4750 baseline; `GetFieldOfView` is at **:765** in 5018) | glass (direct), parts-now (via `ThumbnailCreator.MoveRootPart`) | `KSA.Rendering/ThumbnailCreator.cs` | OK | `MoveRootPart(root, thumb, Camera)` forwards to the `(double fov, double nearPlane)` overload (`:194`) |

### KSA.CameraMode
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CameraMode.IVA` (enum) | enum | `KSA/CameraMode.cs` | IvaForceRender (kitchen-sink) | `kitchen-sink.lib/IvaForceRender.cs` | OK | compared vs `Viewport.Mode` |

### KSA.CatExpressionAnim
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CatExpressionAnim : CatPostAnim` (type) | direct API — **constructed by the mod** | `KSA/CatExpressionAnim.cs` | kitten-animations | `KittenExpressionController.cs` | OK | file byte-identical 4680↔5348. Mod builds its own instance and appends it to `AnimProcessors` |
| `CatPostAnim.CharacterAvatar` (`required` field) / `Priority : float` | direct API | `KSA/CatPostAnim.cs` | kitten-animations | `KittenExpressionController.cs` | OK | `required` — must be set in the object initialiser or it is a compile break |
| `ExpressionAnim : AnimationAssetRef?` | direct API | `KSA/CatExpressionAnim.cs` | kitten-animations | `KittenExpressionController.cs` | OK | |
| `ExpressionWeight : float` | direct API | `KSA/CatExpressionAnim.cs` | kitten-animations | `KittenExpressionController.cs`; `KittenAnimationDriver.cs` | OK | mod's own processor: eased per frame. Game's **reactive** processor: only *capped* — `KittenRenderable.UpdateRenderData` damps it from acceleration every frame |
| `_expressionPose : TransformTRS[]? (private)` | reflection-field (cached FieldInfo) | `KSA/CatExpressionAnim.cs` | kitten-animations | `KittenExpressionController.cs` | OK | set null to bust the sampled-pose cache when `ExpressionAnim` changes |

### KSA.CatFurRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialIndices : protected int[]` | reflection-field | `KSA/CatFurRenderable.cs` | doh | `KittenSpawner.cs` | OK | fur material handle swap |

### KSA.ChuteRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `Draw(float3[], float[]?, floatQuat[]?, ref readonly double4x4, float diameterM, double dt)` | **Harmony prefix** | `KSA/ChuteRenderable.cs` | free-fallin | `free-fallin.lib/FreeFallinPatches.cs` | OK (new @5402) | substitutes the nested animated renderable's material handle before its draw; single overload |
| `_renderable : private readonly AnimatedRenderable` | reflection-field (string) | `KSA/ChuteRenderable.cs` | free-fallin | `FreeFallinPatches.cs` | OK (new @5402) | load-bearing private field; exact-name reflection watchlist entry |
| ctor binds `ParachuteCanopyGlb` + material slot 0 `ParachuteCanopy_Material` and two-sided skinned techniques | behavior + asset invariant | `KSA/ChuteRenderable.cs` | free-fallin | `CanopyMaterialController.cs`; `FreeFallinPatches.cs` | OK (new @5402) | slot zero and two-sided main/prepass/shadow sharing are required for one material swap to cover the complete canopy |

### KSA.Celestial
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Celestial : Astronomical, IOrbiter,…` (type) | direct API | `KSA/Celestial.cs` |CelestialProvider (→ kiwis-marbles, )| `ksa-abstractions.lib/CelestialProvider.cs` | OK | `OfType<Celestial>()` |
| `SetOrbit(Orbit newOrbit)` | direct API | `KSA/Celestial.cs` | kiwis-marbles | `kiwis-marbles…/CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | bare `Orbit = newOrbit`; no `Children` re-parent (engine does it) |
| `Parent : IParentBody` (`=> Orbit.Parent`) | direct API | `KSA/Celestial.cs` | kiwis-marbles | `CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | old-parent lookup before swap |
| `IParentBody.UpdatePerFrameDataTree() : void` (default interface method) | direct API | `KSA/IParentBody.cs` | kiwis-marbles | `CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | subtree refresh after SetOrbit (replaced bare `UpdatePerFrameData()`) |
| `IParentBody.Children : List<IOrbiter>` | direct API | `KSA/IParentBody.cs` | kiwis-marbles | `CelestialWeldEngine.cs` (`Reparent`) | OK | Remove/Add across parents on cross-parent weld |
| `OrbitColor : byte4 { get; protected set; }` (via IOrbiter) | direct API | `KSA/Celestial.cs`; `KSA/IOrbiter.cs` | kiwis-marbles | `CelestialWeldEngine.cs` | OK | orbit line color |
| `Orbit : Orbit { get; set; }` | direct API | `KSA/Celestial.cs` | kiwis-marbles | `KiwisMarblesSubmod.cs` | OK | saved for restore |
| `MeanRadius : double` (override) | direct API | `KSA/Celestial.cs` | kiwis-marbles, graffiti | `KiwisMarblesSubmod.cs`; `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs` | OK | surface placement / terrain radius |
| `{GetCce2Ccf, GetCcf2Cce, GetCci2Cce} : doubleQuat` · `GetTerrainHeightFromDirCcf(double3, bool accurate) : double` · `GetDirCcfFromLatLon(double, double) : double3` · `static {GetLatitudeFromCcf, GetLongitudeFromCcf}(double3) : double` | direct API | `KSA/Celestial.cs` | graffiti | `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs` | OK @5348 | CPU terrain march + geodetic decal anchors. Height is metres above `MeanRadius` (0 with no heightmap); lat/lon statics return DEGREES. ⚠ **`accurate: true` is load-bearing**: since the 5319–5325 terrain precision rework, only accurate mode evaluates the procedural terrain modifiers (`Celestial.cs`) the rendered surface includes — an inaccurate radius parks the decal metres off the visible terrain. See `scope/decals.md` #10 |

### KSA.CelestialSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `All : LookupCollection<Astronomical>` | direct API | `KSA/CelestialSystem.cs` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs`; `CelestialProvider.cs` | OK | shared enumerator root |
| `Deregister(Vehicle)` | direct API | `KSA/CelestialSystem.cs` | doh | `KittenSpawner.cs` | OK | despawn |
| `JobSystems.VehicleSolver.Wait()` | direct API | `KSA/JobSystems.cs`; `Brutal.Concurrency.Jobs/JobScheduler.cs` | doh | `KittenSpawner.cs` | OK | waits for the background vehicle physics step before `new KittenEva` / `Vehicle.Dispose()`; avoids `ConstraintSim.UnlockShapes()` stepping-lock throw (5402) |
| `All.TryGet(string, out Astronomical)` (LookupCollection) | direct API | `KSA/CelestialSystem.cs` | doh | `KittenSpawner.cs` | OK | despawn lookup |
| `Get(string) : Astronomical?` | direct API | `KSA/CelestialSystem.cs` | graffiti | `graffiti.lib/GraffitiSubmod.cs` (`ResolveAnchor`) | OK @5348 | per-frame decal anchor re-resolution by vehicle/body id; null (dormant decal) on despawn |

### KSA.CharacterAvatar (+ nested CharacterCore)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Core : CharacterCore` (public **struct** field) | reflection-field | `KSA/CharacterAvatar.cs` | garrys-torch, doh, kitten-animations | `garrys-torch.lib/WeldEngine.cs`; `KittenSpawner.cs`; `KittenAnimationsSubmod.cs` | OK | garrys uses `GetField("Core")` + `SetValue` — correct only while `Core` is a value-type field |
| `CharacterCore.Scale : float = 0.01f` (field) | reflection-field | `KSA/CharacterAvatar.cs` | garrys-torch | `WeldEngine.cs` | OK | avatar uniform scale (`factor*0.01f`) |
| `Core.CharacterModel : AnimatedRenderable` | direct API | `KSA/CharacterAvatar.cs` | doh, kitten-animations | `KittenSpawner.cs`; `KittenAnimationsSubmod.cs` | OK | `.MaterialIndices` (doh); kitten matches the prefix against this instance |
| `Personality : CharacterPersonality` (field + enum) | direct API | `KSA/CharacterAvatar.cs` | kitten-animations | `Ui/PlaybackSection.cs`, `Ui/StrengthSection.cs` | OK | display only; decides whether a personality processor exists at all (Neutral = none) |
| `Core.Fur.CatFurRenderable` / `Core.Attachments.{Helmet,Mmu}` (field path) | reflection-field path | `KSA/CharacterAvatar.cs` | doh | `KittenSpawner.cs` | OK | helmet/visor/mmu mesh `MaterialIndices` |
| `Expressions.{Angry,Awe,Happy,Sad,Scared} : List<AnimationAssetRef>?` | direct API | `KSA/CharacterAvatar.cs` | kitten-animations | `KittenExpressionController.cs` | OK | per-variant selection or random pick |
| `Animations.MmuAnimations.{MmuIdleDefaultAnim, MmuIdleActionsAnim, MmuMove L/R/Fwd/Back/Up/Down LoopAnim, MmuArmRetractAnim}` | direct API | `KSA/CharacterAvatar.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | idle-actions list + arm-retract added 5348 pass |
| `Animations.{BlinkAnim, HelmetMaskAnim} : AnimationAssetRef?` | direct API | `KSA/CharacterAvatar.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | overlay pose sources |
| `Animations.WalkingAnimations.{RunningAnim, WalkingAnim}` | **superseded — no longer used** | `KSA/CharacterAvatar.cs` | — | — | n/a | ⚠️ `InitalizeFromCharacterRef` only assigns `WalkingAnim` and **never assigns `RunningAnim`**. Ground walk/run now come from `CharacterGroundAnimations` via `KittenRenderable` |

### KSA.CharacterReference / KSA.CharacterTexturesReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CharacterReference.CharacterTextures : CharacterTexturesReference` | direct API | `KSA/CharacterReference.cs` | doh | `doh.lib/Materials/MaterialFactory.cs` | OK | file byte-identical |
| `CharacterTexturesReference.{CharacterBodyMaterial, CharacterHeadMaterial, CharacterEyeMaterial} : PbrMaterialReference` | reflection-field | `KSA/CharacterTexturesReference.cs` | doh | `MaterialFactory.cs` | OK | file byte-identical |

### KSA.CharacterRenderSystem / KSA.CharacterRenderResources
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Program.CharacterRenderSystem` → `_resources : CharacterRenderResources` → `.FurTexture/.CatFurMaskTexture` (`.BindlessHandle`), `.FurSampler` (`.BindlessIndex`) | reflection-field | `KSA/CharacterRenderSystem.cs`; `KSA/CharacterRenderResources.cs` | doh | `MaterialFactory.cs` | OK | fur `ExtraData` handles; file diff is internal shader wiring only (rev 4745) |

### KSA.ColorRgbReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `R / G / B : float` | reflection-field (string) | `KSA/ColorRgbReference.cs` |zippo| `ksa-lights.lib/LightController.cs`| OK | Shared LightController reads ColorRgb and invokes OnDataLoad after editing channels. |
| `OnDataLoad(Mod) : void` | reflection-method (string) | `KSA/ColorRgbReference.cs` |zippo| `ksa-lights.lib/LightController.cs` | OK | 1-arg (`new object?[]{null}`); recomputes `Value` |

### KSA.Connection (nested in Part)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Connection.Connect(IConnector, IConnector)` | direct API | `KSA/Part.cs` |its-so-shiny| `its-so-shiny.lib/ShinyGridBuilder.cs` | OK | takes `IConnector` (Part implements), not `(Part,Part)` |
| `Connection.Disconnect()` | direct API | `KSA/Part.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | |

### KSA.Controller (+ OrbitController / FlyController)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OrbitController.OnFrame(Viewport, double inDeltaTime) : override void` | Harmony pre + reflection-method (string "OnFrame") | `KSA/OrbitController.cs` | camera-controller-override | `camera-controller-override.lib/CameraControllerOverridePatches.cs` | OK | bound to `KSA.OrbitController` via `using KSA;` (NOT RenderCore family) |
| `FlyController.OnFrame(Viewport, double inDeltaTime) : override void` | Harmony pre + reflection-method (string) | `KSA/FlyController.cs` | camera-controller-override | `CameraControllerOverridePatches.cs` | OK | |
| `Controller` (base, `__instance`) | direct API | `KSA/Controller.cs` | camera-controller-override | `CameraControllerOverridePatches.cs` | OK | |
| `Controller.Camera : Camera` (field) | direct API (read chain) | `KSA/Controller.cs` | camera-controller-override | `AnimationHelpers.cs` | OK | the real camera field (NOT `Transform`) |
| **field `Transform` (`___Transform` injector)** | Harmony field-injection (by name) | `KSA/Controller.cs` (NO such field) | camera-controller-override | `CameraControllerOverridePatches.cs` | **BROKEN** | no `Transform` field on KSA controllers in 4680 OR 4750 → `Apply` throws (swallowed) → animation prefix never attaches. Pre-existing, not a 4750 regression. Fix: inject `Camera ___Camera` |

### KSA.Cursor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetEgoRay(IViewport) : static Ray` (= `viewport.GetCamera().ScreenToEgoRay(GetPosition(viewport))`) · `GetPosition(IViewport) : float2` · `DesktopPosition : float2` | direct API | `KSA/Cursor.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | **CHANGED @5402** (fixed) | **replaced `InputRay`/`UpdateInputRay`/`ScreenPosition` @5402** — both pass `Program.MainViewport` and get the same-frame camera/cursor ray. |

### KSA.DeviceMeshInterleaved (+ nested static Shared)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Shared.{RunningVertexBufferSize, RunningIndexBufferSize} : public static uint` | direct API (**write**) | `KSA/DeviceMeshInterleaved.cs` | parts-now | `parts-now.lib/Runtime/MeshBudget.cs` | OK | On-demand growth after loader completion; released contiguous tails rewind the counters. Must remain public static settable uint. |
| `Shared.{VertexAllocation, IndexAllocation} : public static BufferEx` → `.BufferSize` | direct API | `KSA/DeviceMeshInterleaved.cs`; `Brutal.VulkanApi.Abstractions/BufferEx.cs` | parts-now | `MeshBudget.cs` | OK | authoritative allocated size vs the running bump cursor |
| `Shared.IsBuilt : public static bool` | direct API (tripwire) | `KSA/DeviceMeshInterleaved.cs` | parts-now | `MeshBudget.cs` | OK | must be false at reserve time, true on the first frame; a mismatch only WARNs |
| `Shared.Build() : static void` (one-shot) / `Shared.Rebuild()` | behavior dependency (no patch) | `KSA/DeviceMeshInterleaved.cs`; called from `DeviceMeshInterleaved.Bind() :195` ← `ModLibrary.Bind` (`KSA/ModLibrary.cs`) ← `KSA/Program.cs` | parts-now | — | OK | 🔶 **standing invariant U1/U2.** `Build()` sizes both buffers from the running counters, exactly once. `Rebuild()` copies `VertexAllocation.BufferSize` bytes out of the **old** buffer (`:82-83`) so it can never grow anything |
| `VerticesSize` / `IndicesSize : ByteSize` (fields) | direct API | `KSA/DeviceMeshInterleaved.cs` | parts-now | `MeshBudget.cs` (via `MeshReference.DeviceMeshesInterleaved`, `KSA/MeshReference.cs`) | OK | measured **before** `MeshReference.Dispose()` for the purge's leak accounting |

### KSA.DistanceReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `PartTemplate.Diameter : DistanceReference` (rev 4721) | direct API | `KSA/PartTemplate.cs` | not written) | — | ADDITIVE | mod never writes `<Diameter>`; mod-built parts miss size-filtered lists |

### KSA.EVADoor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CreateKittenEva(Vehicle)` (pattern mirrored, not called) | direct API (pattern) | `KSA/EVADoor.cs` | doh | `doh.lib/Spawning/KittenSpawner.cs` | OK | doh replicates this spawn shape |

### KSA.EditorTag
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.EngineController
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.FileReference (+ MeshAtlasFileReference / MeshFileReference / TextureReference)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `LocalPath : string` (field, XML attribute `Path`) · `IsReference() : override bool` · `Load() : void` · `ModPath` | direct API | `KSA/FileReference.cs` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs`; `BundleParserQueries.cs` | OK | ⚠ **`Load()` catches and logs its own exceptions instead of throwing** (`:66-147`), so a missing GLB/KTX2 produces a silent partial load. `VerifyLoadersProduced` re-derives every `DoLoad()` post-condition by hand (U9). `OnDataLoad` falls back to `Id = ModPath` when no `Id` is declared (`:43`) |
| `MeshAtlasFileReference.Meshes : List<MeshReference> { get; private set; }` | direct API | `KSA/MeshAtlasFileReference.cs` | parts-now | `RuntimeModLoaderDeltas.cs` | OK | non-empty is the atlas's success post-condition |
| `MeshAtlasFileReference.DoLoad()` mesh-naming rule (one `MeshReference` per `GltfJson.Meshes[i].Name`, skipping `_`-prefixed) | behavior dependency (**duplicated**, not called) | `KSA/MeshAtlasFileReference.cs` | parts-now | `Runtime/GlbMeshNames.cs`; `BundleValidatorContext.cs` | OK | ⚠ **U8.** Validation rule V6 must know the mesh ids before anything loads, so parts-now reads the GLB JSON chunk itself (no `Brutal.Gltf` reference). A change to the rule silently mis-validates |
| `MeshFileReference.Mesh : MeshReference?` (field) | direct API | `KSA/MeshFileReference.cs` | parts-now | `RuntimeModLoaderDeltas.cs` | OK | non-null is the mesh file's success post-condition |
| `TextureReference.{BindlessHandle : int, Texture : SimpleVkTexture, TextureAsset : TextureAsset, Dispose(Device)}` | direct API (GPU teardown) | `KSA/TextureReference.cs` | parts-now | `Runtime/RuntimeModPurgeSteps.cs` | OK | ⚠ `Dispose(Device)` calls `BindlessTextures.FreeTexture(BindlessHandle)` then `Texture.Dispose()`/`TextureAsset.Dispose()` with **no null checks**, and handle `0` is the shared *empty* texture → triple guard before calling. Type does **not** implement `IDisposable`; the `Device` arg is ignored |
| `MeshReference.{IsReference(), Dispose(), DeviceMeshesInterleaved}` | direct API | `KSA/MeshReference.cs` | parts-now | `RuntimeModLoaderDeltas.cs`; `RuntimeModPurgeSteps.cs` | OK | `MeshReference.Load` ends by clearing `_isReference` and calling `ModLibrary.RegisterBinder(this)` (`:107`) |

### KSA.FlightComputer (+ nested VehicleConfigInfo)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle.FlightComputer : FlightComputer { get; private set; }` | direct API | `KSA/Vehicle.cs` |average-twr| `average-twr.lib/TwrDataReader.cs`| OK | |
| `FlightComputer.AmbientPressure : float` (field) | direct API | `KSA/FlightComputer.cs` | average-twr | `TwrDataReader.cs` | **NEW @5117** | fed from `states.Environment.AtmosphericPressure`; 0 in vacuum |
| `Vehicle.ComputeActiveThrust(float ambientPressure) → float` | direct API | `KSA/Vehicle.cs` | average-twr | `TwrDataReader.cs` | **NEW @5117** | replaces `VehicleConfigInfo.TotalEngineVacuumThrust`; skips out-of-propellant engines; same call the navball TWR uses |
| ~~`FlightComputer.VehicleConfig : VehicleConfigInfo`~~ | direct API | — | *(none)* | — | **no longer used @5117** | still exists; average-twr no longer reads it |
| ~~`VehicleConfigInfo.TotalEngineVacuumThrust : float`~~ | direct API | — | — | — | 🔴 **REMOVED @5117 (rev 5114)** | with `TotalEngineVacuumMassFlowRate`, `TotalEngineExhaustVelocity`, `TotalEngineIsp`. Broke average-twr (CS1061) — **fixed** |

### KSA.FloatReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Value : float` | reflection-field (string) | `KSA/FloatReference.cs` | zippo | `ksa-lights.lib/LightController.cs` | OK | light `Intensity.Value` read/write — works |

### KSA.FlowRule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.GameSettings
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnKeyAll(GlfwKeyEvent) : static bool` | Harmony pre (HotkeyGuard) + reflection-method (`nameof`) | `KSA/GameSettings.cs` |**ALL top-level mods via HotkeyGuard** ( ships a local copy)| `ksa-abstractions.lib/HotkeyGuard.cs` | OK | suite-wide chokepoint; prefix `ref bool __result` swallows key while typing |
| `GameSettings.Current.Graphics.PartThumbnailSize : ushort` | direct API | `KSA/GameSettings.cs` | parts-now (indirect) | parts-now via `ThumbnailRenderer.SIZE` | OK | thumbnail size (rev 4696). parts-now reads it only through `ThumbnailRenderer.SIZE` and warns when it drifts from the boot-sized thumbnail viewport (U12); it never writes the setting |

### KSA.GaugeCanvas
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `_canvases : static List<GaugeCanvas>` (NonPublic+Static) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `con-man.lib/GaugeStateAccessor.cs` | OK | **required** (IsValid gate) |
| `_enabled : bool` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | **required** |
| `_customOffset : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | **required** |
| `_customScale : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | **required** |
| `_windowPosition : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | optional (degrades to Zero) |
| `_windowSize : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | optional (degrades to (100,100)) |
| `_windowTitle : string` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs` | con-man | `GaugeStateAccessor.cs` | OK | optional (skips reposition if null) |

### KSA.GenericGizmo
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GenericGizmo(MeshReference, IGizmoRenderData, int)` ctor; `.GetSegmentDataByViewport(IViewport) : PerSegmentData[]` (keyed by `ViewportId` @5402); `Static.GenericGizmoRenderData`; `PerSegmentData{Active,PositionEgo,Body2Cce,Scale,Color}` | render-pass | `KSA/GenericGizmo.cs` | dont-stifle-me | `dont-stifle-me.lib/PerAxisScaleDrag.cs` | OK @5402 (`Viewport`→`IViewport`) | per-axis scale-gizmo drag (reads `VehicleEditor.ScaleGizmo` segment data). Was flexo's editor gizmos until flexo was removed @5348 |

### KSA.GlobalShaderBindings
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `DescriptorSetLayout : static` · `DescriptorSet : static` · `DynamicOffset(int viewportIndex) : static` | direct API (render) | `KSA/GlobalShaderBindings.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | set 0 of the decal pipeline — the game-wide Camera/Lighting UBO block with a dynamic offset per viewport. Set order (0 global / 1 depth / 2 bindless) is baked into the GLSL |

### KSA.GltfPbrSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SuperMeshRenderSystem.GltfSystem`; `GltfPbrSystem.BlankMaterialTexture.BindlessHandle` | reflection-field | `KSA/GltfPbrSystem.cs` | doh | `MaterialFactory.cs` | OK | default-texture fallback |

### KSA.GpuObjectAssetRef
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `.Handle : int` | reflection-field | `KSA/GpuObjectAssetRef.cs` | doh | `MaterialSystemAccessor.cs` | OK | map name→buffer index |

### KSA.GpuObjectSystem<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `BigBuffer : BufferEx` (public get/protected set) | reflection-field | `KSA/GpuObjectSystem.cs` | doh, humble-arteest | `MaterialSystemAccessor.cs`; `KittenColor.cs` | OK | GPU material buffer |
| `DeviceCtx : IVulkanContext` (protected) | reflection-field (hierarchy) | `KSA/GpuObjectSystem.cs` | doh, humble-arteest | `MaterialSystemAccessor.cs`; `KittenColor.cs` | OK | |
| `CreateObject(AssetName, T) : bool` | reflection-method (doh) / direct API (free-fallin) | `KSA/GpuObjectSystem.cs` | doh, free-fallin | `MaterialSystemAccessor.cs`; `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | allocates immutable runtime materials; free-fallin creates one per Apply |

### KSA.GpuTextureSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetOrLoad`; `{SamplerRepeatHandle, DefaultWhiteTexture, DefaultBlackTexture}` | reflection-field/method | `KSA/GpuTextureSystem.cs` | doh | `MaterialSystemAccessor.cs`; `MaterialFactory.cs` | OK | texture bindless lookup; file byte-identical |
| `TryAddTexture(AssetName, TextureAsset, bool)` + `GetOrLoad` | direct API | `KSA/GpuTextureSystem.cs` | free-fallin | `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | uploads replacement/composited albedo and optional 1x1 PBR textures into KSA's bindless system |

### KSA.GrainGeometryLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `All() : static ReadOnlySpan<GrainGeometry>` · `TryGet(KeyHash) : static GrainGeometry?` | direct API (read-only) | `KSA/GrainGeometryLibrary.cs` | parts-now | `Runtime/BundleValidatorRulesReferences.cs` | OK | validation rule V10 — `<Grain Id>` must already exist; parts-now cannot extend this library at runtime (it is `Dictionary.Add`-populated once by `LoadAll`). Empty library → warning, not error |

### KSA.IFollowable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.IOrbiter
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `IOrbiter` (type) | direct API | `KSA/IOrbiter.cs` |CelestialProvider (→ kiwis-marbles, )| `CelestialProvider.cs` | OK | celestials + vehicles |
| `Parent : IParentBody { get; }` | direct API | `KSA/IOrbiter.cs` | kiwis-marbles | `CelestialWeldEngine.cs` | OK | null-checked |
| `Orbit : Orbit { get; }` | direct API | `KSA/IOrbiter.cs` | kiwis-marbles | `CelestialWeldEngine.cs` | OK | |
| `GetPositionCci() : double3` | direct API | `KSA/IOrbiter.cs` | kiwis-marbles | `CelestialWeldEngine.cs` | OK | (concrete `Vehicle.GetPositionCci` is a separate row) |
| `GetVelocityCci() : double3` | direct API | `KSA/IOrbiter.cs` | kiwis-marbles | `CelestialWeldEngine.cs` | OK | |

### KSA.IParentBody
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetCci2Cce() : doubleQuat` | direct API | `KSA/IParentBody.cs` | garrys-torch | `garrys-torch.lib/WeldEngine.cs` | OK | called on `Vehicle.Parent` |
| `Mass : double { get; }` | direct API (dead path) | `KSA/IParentBody.cs` | average-twr (dead) | `average-twr.lib/TwrDataReader.cs` | OK | `ComputeSurfaceGravity` not on sampling path |
| `MeanRadius : double` (via IRadius) | direct API | `KSA/IRadius.cs` |average-twr (dead)| `TwrDataReader.cs`;  `VehicleTelemetry.cs` | OK |Historical unused TWR helper; see telemetry scope.|
| `Children` (add/enumerate) | direct API | `KSA/IParentBody.cs` |doh| `KittenSpawner.cs`| OK | spawn parent / SOI tree walk |

### KSA.IPosition
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetPositionEcl() : double3` (base of IFollowable) | direct API | `KSA/IPosition.cs` | camera-controller-override | `AnimationHelpers.cs` | OK | target-tracking; reached only when `___Transform` fixed (dead) |

### KSA.JobSystems
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VehicleSolvers : static JobScheduler` → `JobScheduler.Wait()` | direct API | `KSA/JobSystems.cs` | garrys-torch | `garrys-torch.lib/GarrysTorchSubmod.cs` | OK | drains in-flight vehicle workers before teleport (race-avoidance) |

### KSA.KSAColor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Xkcd` (nested static class, reflected `GetProperties`) → `Color.Preset` | reflection-type | `KSA/KSAColor.cs` | XkcdColorHelper (→ zippo, doh palettes) | `ksa-abstractions.lib/XkcdColorHelper.cs` | OK | breaks only if `Xkcd` removed or `Color.Preset→float4` conversion dropped |
| `Xkcd.Scarlet`, `Xkcd.PaleGrey : Color.Preset` | direct API | `KSA/KSAColor.cs` |garrys-torch, skittles, con-man| `GarrysTorchSubmod.cs`;  `skittles…SkittlesSubmod.cs`;  `ConManSubmod.cs` | OK | button accents (cosmetic) |

### KSA.KeyframeAnimationModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.KinematicMeasurements
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AccelerationBody : double3` (field, backs `Vehicle.AccelerationBody`) | direct API | `KSA/KinematicMeasurements.cs` |geeforce| `geeforce.lib/GForceRecorder.cs`; `VehicleTelemetry.cs` | OK | body-frame proper accel |

### KSA.KittenEva
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `KittenEva` (type; `GetType().Name=="KittenEva"` / `is KittenEva`) | reflection-type (string, garrys) | `KSA/KittenEva.cs` | garrys-torch, doh, kitten-animations, thug-life, graffiti | `WeldEngine.cs`; `KittenSpawner.cs`; `KittenAvatarAccessor.cs`; `KittenGlassesPreset.cs`; `graffiti.lib/DecalPicker.cs` | OK | garrys string-compares the type name (silent break if renamed). thug-life and graffiti use a typed `is KittenEva` — compile-checked, not string-matched |
| Kitten sphere pick: `new BoundingSphere3D(double3, double)` · `Ray.Raycast(BoundingSphere3D, out double, out bool)` · `Double3Ex.GetAbsoluteLargestElement(double3)` · `Part.{PositionEgo(ref readonly double4x4), ScaleTotal}` · `PartTree.Root` | direct API | `KSA/BoundingSphere3D.cs`; `KSA/Ray.cs`; `KSA/Double3Ex.cs`; `KSA/Part.cs`; `KSA/PartTree.cs` | graffiti | `graffiti.lib/DecalPicker.cs` (`TryPickKitten`) | OK @5348 | a KittenEva has no raycastable part view mesh — this mirrors the game's own `KittenEva.UpdateHighlight` (`KittenEva.cs`) bounding-sphere hover pick, anchoring the decal to the root part |
| `_renderable : private KittenRenderable` | reflection-field (string) | `KSA/KittenEva.cs` | garrys-torch, doh | `WeldEngine.cs`; `KittenSpawner.cs` | OK | avatar root chain. **kitten-animations no longer uses it** — it reads the public `Renderable` property |
| `Renderable : KittenRenderable` (public property) | direct API | `KSA/KittenEva.cs` | kitten-animations | `KittenAvatarAccessor.cs` | OK | typed replacement for the `_renderable` reflection |
| `LocomotionState : LocomotionState` (public property) | direct API | `KSA/KittenEva.cs` | kitten-animations | `Ui/PlaybackSection.cs`, `Ui/TuningSection.cs` | OK | mode / ground speed / gravity readout |
| `ControlMode : KittenControlMode` (public property) | direct API | `KSA/KittenEva.cs` | kitten-animations | `Ui/PlaybackSection.cs` | OK | View vs Direct |
| `AnimPlaybackRate / AnimJumpChainStage / AnimJumpChainCountdown` (public properties) | direct API | `KSA/KittenEva.cs` | kitten-animations | `Ui/PlaybackSection.cs` | OK | forwarded from `KittenRenderable` |
| `new KittenEva(CelestialSystem, string, doubleQuat, double3, IParentBody, string, Part, Orbit)` | direct API (ctor) | `KSA/KittenEva.cs` | doh | `KittenSpawner.cs` | OK | 8-arg ctor |
| `Teleport(Orbit?, doubleQuat?, double3?)` (inherited Vehicle) | direct API | `KSA/Vehicle.cs` | doh | `KittenSpawner.cs` | OK | (shared with `Vehicle.Teleport`) |
| `IsControllable => true` (override) | enum/behavioral | `KSA/KittenEva.cs` | (informational) | — | ADDITIVE | new rev 4699; spawned/controlled kittens now controllable |

### KSA.KittenRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `_characterAvatar : private CharacterAvatar` | reflection-field (string) | `KSA/KittenRenderable.cs` | garrys-torch, doh, kitten-animations | `WeldEngine.cs`; `KittenSpawner.cs`; `KittenAvatarAccessor.cs` | OK | second link in avatar chain |
| `_groundIdleAnim, _groundWalkAnim, _groundRunAnim, _ladderAnim, _jumpIntroAnim, _flailAnim, _jumpLandAnim, _moonWalkAnim, _moonRunAnim, _swimAnim, _swimIdleAnim, _seatedIdleAnim : private AnimationAssetRef?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | ⚠️ **the only route to the ground locomotion set** — it is not exposed on `CharacterAvatar`. Misses are collected in `UnresolvedFields`, logged, and shown as a red UI warning |
| `_seatedIdleActionAnims : private List<AnimationAssetRef>?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | seated idle action clips |
| `_walkPairSampler, _runPairSampler, _swimPairSampler : private AnimationPairBlendSampler?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | playable + `.Weight` readout |
| `_blendSampler : private AnimationDirectionalBlendSampler` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimationCatalog.cs` | OK | MMU directional blend |
| `_catPersonalityExpressionAnim, _catExpressionAnim : private CatExpressionAnim` | reflection-field (string) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimProcessors.cs` | OK | resolved **by name** — `OfType<CatExpressionAnim>()` cannot tell the permanent mood face from the acceleration-reactive one |
| `_catEyeAnim : private CatEyeAnim` / `_catEarAnim : private CatEarAnim` | reflection-field (string) | `KSA/KittenRenderable.cs` | kitten-animations | `KittenAnimProcessors.cs` | OK | eye look/blink + ear mask weight |
| `UpdateRenderData(...)` per-frame `SetAnimation` + reactive-expression damping | behavioral | `KSA/KittenRenderable.cs` | kitten-animations | (motivates `KittenAnimationPatches`) | OK | ⚠️ **semantic dependency, invisible to the compiler**: the game re-picks the clip and rewrites the reactive expression weight every frame. If this stops happening the Harmony prefix becomes unnecessary but harmless; if it changes shape the override may need a different hook |

### KSA.CatEarAnim / KSA.CatEyeAnim
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CatEarAnim.ExpressionWeight : float` | direct API | `KSA/CatEarAnim.cs` | kitten-animations | `KittenAnimationDriver.cs` | OK | game writes it once at construction, so a mod value holds |
| `CatEyeAnim.MaxLookAtAngle : float` | direct API | `KSA/CatEyeAnim.cs` | kitten-animations | `KittenAnimationDriver.cs` | OK | game default 30 deg |
| `CatEyeAnim.LookPitchOffsetDeg : float` | direct API | `KSA/CatEyeAnim.cs` | kitten-animations | `KittenAnimationDriver.cs` | OK | ⚠️ game rewrites it every frame in `UpdateLocomotionAnimationState`, so it is re-applied from the pose prefix |

### KSA.KittenLocomotionTuning / KSA.KittenLocomotion
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `KittenLocomotionTuning.Current : static KittenLocomotionTuning` (field) + `.Default` | direct API — **mutated** | `KSA/KittenLocomotionTuning.cs` | kitten-animations | `Ui/TuningSection.cs` | OK | ⚠️ **global**: edits affect every kitten. The game ships the full editor at menu bar -> Debug -> Kitten Tuning (`KSA/Program.cs`) |
| `AnimBlendTime, IdleSpeedThreshold, PlaybackRateMin/Max, Walk/Run/Ladder/TumbleClipNominalSpeed, Moonwalk{Walk,Run}NominalSpeed, Moonwalk{Start,Full}Gravity, MoonwalkPlaybackScale, NominalSwimAnimSpeed, SwimBlendFullSpeed, SwimBlendHalfLife, SwimEyePitchFactor, JumpLandDuration, JumpLandBounceIgnoreTime, LadderEyePitchDeg : float` | direct API (`ref` to static struct fields) | `KSA/KittenLocomotionTuning.cs` | kitten-animations | `Ui/TuningSection.cs` | OK | animation-facing subset only; the scoped reset restores just these |
| `KittenLocomotion.ComputeMoonwalkWeight(float, in KittenLocomotionTuning)` | direct API | `KSA/KittenLocomotion.cs` | kitten-animations | `Ui/TuningSection.cs` | OK | derived readout |
| `KittenLocomotion.ResolveSwimBlend(float, in KittenLocomotionTuning)` | direct API | `KSA/KittenLocomotion.cs` | kitten-animations | `Ui/TuningSection.cs` | OK | derived readout |
| `LocomotionState.{Mode, GroundSpeed, GravityMagnitude}`; `LocomotionMode`, `JumpChainStage` (enums) | direct API | `KSA/LocomotionState.cs`; `LocomotionMode.cs`; `JumpChainStage.cs` | kitten-animations | `Ui/PlaybackSection.cs` | OK | status display |

### KSA.LightModule (+ nested TemplateData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `"KSA.LightModule+TemplateData"` (nested type by full name) | reflection-type (string) | `KSA/LightModule.cs` | zippo | `ksa-lights.lib/LightController.cs` | OK | hard-coded full name; rename → zero light parts |
| `LightModule` (type, `Get<LightModule>()`) | direct API | `KSA/LightModule.cs` |its-so-shiny (via ZippoLib)| `ActionScanner.cs` | OK | |
| `TemplateData.Intensity : FloatReference` (field) | reflection-field (string) | `KSA/LightModule.cs` | zippo | `ksa-lights.lib/LightController.cs` | OK | works |
| `TemplateData.ColorRgb : ColorRgbReference` (field) | reflection-field (zippo) | `KSA/LightModule.cs` |zippo| `ksa-lights.lib/LightController.cs`;   | OK | Shared LightController resolves ColorRgb. |

### KSA.Loading
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnFrame()` early-returns on `!Program.IsMainThread()`; `Task(string)` / `PushTask(LoadTask)` / `Current` | behavior dependency (no patch) | `KSA/Loading.cs`; `KSA/Program.cs` | parts-now | `Runtime/RuntimeModLoaderStates.cs` (design note + `Task.Run` worker) | OK | 🔶 **U7.** `FileReference.Load()` → `Loading.Task()` → `PushTask()` → `Current.OnFrame()` renders **and submits a whole ImGui frame**. parts-now runs `ILoader.Load()` on a worker precisely because that guard makes the chain a no-op there. Never null `Loading.Current` instead — `LoadTask`'s field initialiser throws, and the throw escapes `FileReference.Load`'s try block |

### KSA.LookupCollection<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UnsafeAsList() : List<T>` | direct API | `KSA/LookupCollection.cs` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs` | OK | then LINQ `OfType<Vehicle>/<Celestial>/<IOrbiter>` |

### KSA.MaterialData
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialData` (`[StructLayout(Sequential,Pack=1)]`; `AlbedoColor` @offset **16**) | direct API + GPU write | `KSA/MaterialData.cs` | doh, humble-arteest (KittenColor), free-fallin | `MaterialFactory.cs`; `KittenColor.cs`; `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | **byte-identical**; free-fallin supplies albedo/normal/PBR/emissive handles, tint and `RoughnessMetalScale`; Full Canopy additionally owns `ExtraData=(projection scale, cos rotation, sin rotation, 31415 marker)`; shader channel ABI is R=AO, G=roughness, B=metallic |

### KSA.MeshReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.MeshViewModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.Mod / KSA.ModManifest / KSA.ModEntry
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Mod.MakeUsing(string id, string manifestPath) : static Mod` | direct API | `KSA/Mod.cs` | parts-now | `Runtime/RuntimeModLoaderStates.cs` | OK | builds a `Mod` from a `mod.toml` path. Deliberately **not** registered into `ModLibrary.Lookup` — `MakeUsing` does not do it, and only the boot path does (`KSA/ModLibrary.cs`), which keeps `ModLibrary.Find` a reliable "loaded at boot?" test |
| `Mod.{DirectoryPath, Preload, Id}` | direct API | `KSA/Mod.cs` | parts-now | `RuntimeModLoaderStates.cs` | OK | `Preload` forced **false**: `FileReference.OnDataLoad` only calls `ModLibrary.RegisterLoader` while it is false, so a preloading mod would register templates whose files are never read |
| `ModManifest.{Mods : List<ModEntry>, Save()}` | direct API (**write to disk**) | `KSA/ModManifest.cs` | parts-now | `Io/ModFolderWriter.cs`; `Io/ModIdValidator.cs` | OK | so a runtime-installed mod also loads at the next launch (a saved vehicle would otherwise fail to resolve its parts). Null manifest ⇒ **fail closed** ("cannot prove the id is free") |
| `ModEntry.{Id, Enabled, New}` + `ModEntry(string, int)` ctor | direct API | `KSA/ModEntry.cs` | parts-now | `Io/ModFolderWriter.cs` | OK | parts-now writes `new ModEntry { Id, Enabled = true, New = false }` **on purpose** — the `(id, count)` ctor sets `Enabled=false, New=true`, which pops the game's "confirm mods" dialog at next boot |

### KSA.ModLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<T>(string id) : T where T:IKeyed` | direct API | `KSA/ModLibrary.cs` |its-so-shiny, thug-life, doh, humble-arteest, byo-music, graffiti| `its-so-shiny.lib/ShinyGridBuilder.cs`;  `ShinyGridBuilder.cs`;  `ThugLifeQuadRenderer.cs`;  `MaterialFactory.cs`;  `VehiclePaintShaders.cs`;  `MusicPlayer.cs`;  `graffiti.lib/DecalRenderer.cs` (`ShaderIncludeDirectory`) | OK | string-keyed; throws if id missing. Per-`T` asset ids in section 5 |
| `AllParts : internal static SerializedCollection<PartTemplate>` | reflection-field (string "AllParts") | `KSA/ModLibrary.cs` | doh, parts-now | `KittenSpawner.cs`; `parts-now.lib/Runtime/GameRegistry.cs` | OK | `.Find(KeyHash)` (doh, parts-now) / `.GetList` (parts-now) |
| `AllCharacters : internal static SerializedCollection<CharacterReference>` | reflection-field (string) | `KSA/ModLibrary.cs` | doh | `KittenSpawner.cs` | OK | character enumeration |
| `{AllMeshes, AllFiles, AllMaterials, AllPartGameDataReferences, AllEditorTagDefinitions}` : internal static `SerializedCollection<…>` | reflection-field (string ×5) | `KSA/ModLibrary.cs` | parts-now | `Runtime/GameRegistry.cs` | OK | the other five registries a runtime load writes into. All resolved once in `GameRegistry`'s static ctor; a miss is **fatal** (`IsHealthy=false` disables every Load button) |
| `Loaders : public static List<ILoader>` · `Binders : public static List<IBinder>` (+ `RegisterLoader`/`RegisterBinder`) | direct API (read + `RemoveAll`) | `KSA/ModLibrary.cs` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs`; `RuntimeModPurgeSteps.cs` | OK | mark/delta bookkeeping, then pruned on purge — KSA never clears either list |
| `Bind(Renderer) : static void` | behavior dependency (**re-implemented**, not called) | `KSA/ModLibrary.cs` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs` | OK | parts-now mirrors the per-binder body (`CreateStagingPool` + `binder.Bind`) minus the `Parallel.ForEachAsync`: the stock method binds **every** binder ever registered, which would reallocate every existing mesh's device primitives |
| `AttachGameData() : static void` | behavior dependency (**re-implemented**, not called) | `KSA/ModLibrary.cs` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs` | OK | `PartTemplate.ApplyGameData` is additive, so the stock method (which walks every registered entry) would **double** every part already attached at boot |
| `Find(string) : Mod?` → `Lookup : internal static SerializedCollection<Mod>` | direct API | `KSA/ModLibrary.cs` (registered only at `:430`) | parts-now | `Runtime/RuntimeModLoaderApi.cs`; `Io/ModFolderScanner.cs`; `Io/ModIdValidator.cs`; `RuntimeModLoaderStates.cs` | OK | "was this mod loaded at boot?" — parts-now refuses to load/reload such a mod. Fails **closed** |
| `{MOD_TOML, CONTENT_FOLDER, LocalModsFolderPath, LocalManifestPath, Manifest}` | direct API | `KSA/ModLibrary.cs` | parts-now | `Io/ModIdValidator.cs`; `Io/ModFolderWriter.cs`; `Runtime/PartsNowSettings.cs`; `Io/ModFolderScanner.cs` | OK | never hardcode a mods path in place of `LocalModsFolderPath`. `Manifest` is a public static field initialised to `null` |
| `Get<SoundBehavior>(string)` | direct API (validation only) | `KSA/ModLibrary.cs`; `KSA/SoundBehavior.cs` | parts-now | `Runtime/BundleValidatorRulesReferences.cs` | OK | V10 `<SoundEvent SoundId>` check. Only public path — `AllSoundBehaviours` is internal (`:108`) and `TryGet<T>` takes the strict `IsSubclassOf` branch (`:745`), so it never matches the base type. Throws `NullReferenceException` on a miss |

### KSA.Module
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Module.Parent : required Part` | direct API | `KSA/Module.cs` |its-so-shiny| `ShinyPatches.cs` | OK | `FullPart => PartParent ?? this` |

### KSA.ModuleBase (+ nested TemplateDataBase)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `TemplateDataBase.Id : [XmlAttribute] public string = ""` | direct API | `KSA/ModuleBase.cs` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs`; `LoadedModRecord.cs`; `RuntimeModPurgeSteps.cs` | OK | 🔶 **U5 — optional and non-unique.** The purge therefore matches model templates by **object identity**, never by id: an id match would miss every id-less template (leaving a stale `PartModel` that `PartModel.Get` hands to the reloaded part, complete with the purged mesh's old shared-buffer offsets) and would evict another mod's instances on a collision |

### KSA.ModuleList
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<T>() : Span<T>` | direct API | `KSA/ModuleList.cs` |its-so-shiny, doh, humble-arteest| `ActionScanner.cs`; `LcdGridBuilder.cs`; `ShinyGridBuilder.cs`; `KittenSpawner.cs`; `EngineEmissive.cs` | OK | generic module accessor |

### KSA.ModuleStateful (StateList + ModuleAndAllMutableStatesRef)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `StateList.NumModules : int` | direct API | `KSA/ModuleStateful.cs` | eternal-flame | `EternalFlameLib.cs` | OK | early-out when 0 |
| `StateList.Modules : Span<TModule>` | direct API | `KSA/ModuleStateful.cs` | eternal-flame | `EternalFlameLib.cs` | OK | iterates `Battery[]` |
| `StateList.GetModuleAndAllMutableStatesForInitialization(TModule) : ModuleAndAllMutableStatesRef` | direct API | `KSA/ModuleStateful.cs` | eternal-flame | `EternalFlameLib.cs` | OK | ref struct with `.Module`+`.State` |
| `ModuleAndAllMutableStatesRef.Module / .State` | direct API | `KSA/ModuleStateful.cs` | eternal-flame | `EternalFlameLib.cs` | OK | `.Module.Refill(ref .State)` |

### KSA.MusicPlayList
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MusicPlayList : SoundReference` (type) | direct API | `KSA/MusicPlayList.cs` | byo-music | `byo-music.lib/MusicPlayer.cs` | OK | |
| `PlayMusic(out ChannelWrapper?, ulong delaySamples=0)` | direct API | `KSA/MusicPlayList.cs` | byo-music | `MusicPlayer.cs` | OK | routes through `GameAudio.System` (FMOD) |

### KSA.NavBallData
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle.NavBallData : ref readonly NavBallData` | direct API | `KSA/Vehicle.cs` | average-twr | `average-twr.lib/TwrDataReader.cs` | OK | `ref readonly` accessor |
| `NavBallData.ThrustWeightRatio : double` (field) | direct API | `KSA/NavBallData.cs` | average-twr | `TwrDataReader.cs` | OK | 0 until flight computer populates |

### KSA.Orbit
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CreateFromStateCci(IParentBody, SimTime, double3, double3, byte4) : static Orbit` | direct API | `KSA/Orbit.cs` | garrys-torch, kiwis-marbles, doh | `WeldEngine.cs`; `CelestialWeldEngine.cs`; `KittenSpawner.cs` | OK | 5-arg state-vector factory; arg order/types must hold |
| `OrbitLineColor : byte4` (field) | direct API | `KSA/Orbit.cs` | garrys-torch, doh | `WeldEngine.cs`; `KittenSpawner.cs` | OK | |
| `StateVectors.{PositionCci, VelocityCci}` | direct API | `KSA/Orbit.cs` | doh | `KittenSpawner.cs` | OK | spawn positioning |

### KSA.Part (+ nested Connector)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `new Part(string inName, PartTemplate, PartInstance?=null, Part?=null)` (ctor) | direct API | `KSA/Part.cs` |its-so-shiny, doh| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs`; `KittenSpawner.cs` | OK | |
| `Id : string { get; init; }` | direct API | `KSA/Part.cs` |garrys-torch, zippo, , its-so-shiny, thug-life, kitchen-sink| `GarrysTorchSubmod.cs`;  `ZippoSubmod.cs`;  `ThugLifeSubmod.cs`| OK | combo labels / pixel-id parsing |
| `DisplayName : string { get; init; }` | direct API | `KSA/Part.cs` |zippo| `ZippoSubmod.cs`;  `ActionScanner.cs`| OK | |
| `Template : PartTemplate` (field) | direct API | `KSA/Part.cs` |garrys-torch, zippo, , its-so-shiny, thug-life, kitchen-sink, doh, parts-now| `GarrysTorchSubmod.cs`; `ksa-lights.lib/LightController.cs`; `ThugLifeSubmod.cs`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs` | OK | feeds reflection/labels; `Template.Id` (SerializedId). parts-now compares it against the record's part ids in the unload safety gate |
| `InstanceId : uint` | direct API | `KSA/Part.cs` |graffiti, hot-pursuit| `ActionScanner.cs`; `graffiti.lib/GraffitiSubmod.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs` | OK @5402 | stable sub-part addressing across per-frame target re-resolution |
| `RayCastEgo(ref readonly double4x4, Ray, out double ×2, out double3 ×4, out Part? closestSubPart, out Part?) : bool` | direct API | `KSA/Part.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | OK @5402 | KSA's watertight art-mesh raycast. Position/normal come back in the **returned hit sub-part's** local frame. |
| `Parachute.{ClothPositionsFront, AttachLocationPartAsmb, Parent, CanopyIndex}` · `ChuteClothSystem.Topology` · `ChuteClothTopology.{Rings,Spokes,ApexIndex,CanopyNodeCount,NodeIndex}` · `Ray.RaycastWatertight(v0,v1,v2,out t)` | direct API (cloth pick) | `KSA/Parachute.cs`; `KSA/ChuteClothSystem.cs`; `KSA/ChuteClothTopology.cs`; `KSA/Ray.cs` | graffiti | `graffiti.lib/DecalPicker.Parachute.cs`, `DecalAnchors.cs` | OK @5402 (added) | Deployed canopies are outside part view meshes. Graffiti raycasts an apex fan + ring quads over the published front cloth nodes, then retains node indices/barycentric weights; module `InstanceId` with parent-part id + canopy-index fallback re-resolves the canopy so the decal follows it. Live-check against the bone-skinned GLB surface. |
| `MatrixAsmb2Ego(in double4x4) : double4x4` | direct API | `KSA/Part.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | includes `Part.Scale` and the whole articulated sub-part parent chain |
| `Parts` (via `Vehicle.Parts.Parts`) / `Part.SubParts : ReadOnlySpan<Part>` | direct API | `KSA/Part.cs` |PartHelpers (→ zippo, its-so-shiny, humble-arteest, doh), garrys-torch, thug-life, parts-now| `PartHelpers.cs`;  `WeldEngine.cs`;  `ThugLifeSubmod.cs`;  `parts-now.lib/Runtime/RuntimeModUnloadGate.cs` | OK | recursion key. parts-now recurses it (plus `PartTree.Parts`, `VehicleEditingSpace.AllParts`, `VehicleEditor.UnattachedPartTrees`) to prove nothing alive still uses a mod's parts before purging |
| `FullPart : Part { get; }` | direct API | `KSA/Part.cs` |zippo, , its-so-shiny| `ZippoSubmod.cs`;  `ActionExecutor.cs`;  `ShinyPatches.cs` | OK | `=> PartParent ?? this` |
| `Modules : ModuleList` (field) | direct API | `KSA/Part.cs` |humble-arteest| `ActionScanner.cs`; `EngineEmissive.cs` | OK | `.Get<T>()` / `.Add(...)` |
| `SubtreeModules : ModuleList` (field) | direct API | `KSA/Part.cs` |doh| `ActionScanner.cs`; `LcdGridBuilder.cs`; `KittenSpawner.cs` | OK | anim/solar/tank discovery |
| `LightSwitch : PowerConsumer?` (field) | direct API | `KSA/Part.cs` |zippo, its-so-shiny| `ZippoSubmod.cs`; `ShinyPixelCell.cs` | OK | light on/off path |
| `Connection : (nested type)` → see KSA.Connection | — | `KSA/Part.cs` |its-so-shiny| — | OK | (Connect/Disconnect/OtherPart rows under KSA.Connection) |
| `Connections : List<Connection>` (field) | direct API | `KSA/Part.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | |
| `Scale : double3 { get; set; }` | direct API (write) | `KSA/Part.cs` |garrys-torch, its-so-shiny| `WeldEngine.cs`; `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | setter resets cached pos matrix |
| `PositionParentAsmb : double3 { get; set; }` | direct API (write) | `KSA/Part.cs` (kitchen-sink cites backing `:333`) |its-so-shiny| `its-so-shiny.lib/ShinyGridBuilder.cs`| OK | prefer `:449` (property) |
| `Asmb2ParentAsmb : doubleQuat { get; set; }` | direct API (write) | `KSA/Part.cs` (kitchen-sink cites backing `:337`) |its-so-shiny| `its-so-shiny.lib/ShinyGridBuilder.cs`| OK | part rotation write  |
| `PositionVehicleAsmb : double3` (computed) | direct API | `KSA/Part.cs` | garrys-torch | `WeldEngine.cs` | OK | part-anchor position |
| `Asmb2VehicleAsmb : doubleQuat` (computed) | direct API | `KSA/Part.cs` | garrys-torch | `WeldEngine.cs` | OK | part-anchor orientation |
| `PositionEgo(ref readonly double4x4) : double3` | direct (render) | `KSA/Part.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | per-frame model-ego |
| `Asmb2Ego(doubleQuat) : doubleQuat` | direct (render) | `KSA/Part.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | |
| `TreeParent : Part?` | direct API | `KSA/Part.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | manual tree wiring |
| `TreeChildren : List<Part>` (field) | direct API | `KSA/Part.cs` |its-so-shiny| `its-so-shiny.lib/ShinyGridBuilder.cs`| OK | sub-tree collection |
| `SetStage(int)` / `Stage` (get) | direct API | `KSA/Part.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | |
| ~~`_matrixAsmb` / `_matrixAsmb2Parent` : private double4x4~~ | reflection-field (string) | `KSA/Part.cs` | *(none)* | — | ⚠️ **sentinel changed @5117 (rev 5112)** | uncached sentinel went `double4x4.Identity` → all-NaN `UncachedMatrix` |
| `Tree : PartTree` → `.ReinitializeDerivedValues/.RefillConsumables` | direct API | `KSA/Part.cs` | doh | `KittenSpawner.cs` | OK | backpack/propellant init |

### KSA.PartModel (+ nested PerInstanceData, ViewportData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AddInstance(PerInstanceData, IViewport, int frameIndex) : void` | Harmony pre (humble vehicle-paint) + post (IvaForceRender) | `KSA/PartModel.cs` |humble-arteest (VehiclePaint), IvaForceRender (kitchen-sink)| `VehiclePaintPatches.cs` (`AddInstancePrefix`);  `IvaForceRender.cs` | OK | `PartModel.cs` byte-identical; 3-arg single overload. humble binds by param name `instanceData` and ORs paint into `StateBitFlag` |
| `..ctor(PartModelModule.Template) : protected` | Harmony post (ctor, `AccessTools.Constructor`) | `KSA/PartModel.cs` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs` | OK | explicit param-type array |
| `PerInstanceData` (struct: `ModelMatrix`@0 · `StateBitFlag`@64 · `EmissiveColor`@68 · `packing1`@72 · `Wetness`@76; 80 B) | direct API | `KSA/PartModel.cs` |IvaForceRender, humble-arteest (VehiclePaint)| `IvaForceRender.cs`;  `VehiclePaintPatches.cs` (`AddInstancePrefix`)| OK | humble now writes **only `StateBitFlag` bits 11..31** (no struct reinterpret, no game field clobbered)|
| `PerInstanceData.StateBitFlag` **bits 11..31** | free-bit reuse (per-instance mod payload) | writers `KSA/PartModelModule.cs`, `KSA/PartModelDynamicModule.cs`; readers `MeshIndirect.frag:308-353` | humble-arteest (VehiclePaint) | `VehiclePaint.cs` (`EncodeBits`, `PaintBitShift`) | OK | 🔶 **audit every game update.** Game uses bits 0..10 only; 21 free bits carry a 7:7:7 sRGB paint color. `RayTraceInstance.StateFlags` is `int`, so the bits survive the RT path |
| `ViewportData.Get(PartModel, Viewport) : ViewportData` → `.InstanceList.Add(...)` | direct API | `KSA/PartModel.cs` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs` | OK | re-add internal instance to per-viewport draw list (editor) |
| `Instances : static List<PartModel>` | direct API | `KSA/PartModel.cs` | IvaForceRender (kitchen-sink), parts-now | `IvaForceRender.cs`; `parts-now.lib/Runtime/RuntimeModPurgeSteps.cs` | OK | enumerated by `Enabled` setter. parts-now `RemoveAll`s its own templates' entries on purge — **KSA never prunes this list** |
| `InstancesRayTrace : static List<PartModel>` | direct API | `KSA/PartModel.cs` | parts-now | `RuntimeModPurgeSteps.cs` | OK | same purge pruning; `PartModelDynamic` has **no** such list (dynamic models are never ray traced) |
| `Get(PartModelModule.Template) : static PartModel` | direct API | `KSA/PartModel.cs` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs` | OK | model "warming" turns an unresolvable `<Mesh Id>` into a catchable load-time exception. Resolves by scanning `Instances` for a matching `Template.Id`, which is exactly why the purge must prune those lists |
| `WriteInstancesToGpu(Viewport, int)` dereferences `Template.Material.{DiffuseReference,NormalReference,PBRMap}.BindlessHandle` **unguarded** | behavior dependency (no patch) | `KSA/PartModel.cs`; `KSA/PartModelGlass.cs`; `KSA/PartModelDynamic.cs` | parts-now | `Runtime/BundleValidatorRulesSchema.cs` (rule V9) | OK | 🔶 **U3.** Only `EmissiveMap` is `?.`-guarded. V9 exists solely to stop a player-authored part crashing the game; **if KSA ever null-guards these, V9 becomes an unnecessary restriction worth relaxing** |
| `Template : PartModelModule.Template` (field) | direct API | `KSA/PartModel.cs` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs` | OK | |

### KSA.PartModelDynamic (+ nested PerInstanceData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AddInstance(PerInstanceData inInstanceData, Viewport, int) : void` | Harmony pre | `KSA/PartModelDynamic.cs` | humble-arteest (EngineEmissive) | `EngineEmissivePatches.cs` | OK | file byte-identical; param name `inInstanceData` matches |
| `PerInstanceData` (struct: `ModelMatrix`@0 · `StateBitFlag`@64 · `Temperature`@68 · `TfiThickness`@72 · `Wetness`@76; 80 B) | direct API (struct reinterpret for EngineEmissive) | `KSA/PartModelDynamic.cs` | humble-arteest (EngineEmissive, VehiclePaint) | `EngineEmissivePatches.cs`; `VehiclePaintPatches.cs` (`AddInstanceDynamicPrefix`) | OK | mirror struct matches exactly (`Temperature`@68, `TfiThickness`@72). VehiclePaint touches only `StateBitFlag` bits 11..31, so the two features compose |

### KSA.PartModelDynamicModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre (return false skips submit) | `KSA/PartModelDynamicModule.cs` |its-so-shiny, humble-arteest (VehiclePaint)| `ShinyPatches.cs`;  `VehiclePaintPatches.cs` (`PartModelDynamicModulePrefix`) | OK | humble reads `__instance.Parent` to know which `Part` is submitting; **only caller** of `PartModelDynamic.AddInstance` |
| `PartModelDynamicModule.PartModelDynamic : required` | direct API | `KSA/PartModelDynamicModule.cs` | humble-arteest (EngineEmissive) | `EngineEmissive.cs` | OK | file identical |
| `PartModelDynamic.{Instances : static List<PartModelDynamic>, Get(PartModelDynamicModule.Template)}`; `PartModelGlass.{Instances, InstancesRayTrace, Get(PartModelGlassModule.Template)}` | direct API | `KSA/PartModelDynamic.cs`; `KSA/PartModelGlass.cs` | parts-now | `Runtime/RuntimeModPurgeSteps.cs`; `RuntimeModLoaderGpuStates.cs` | OK | warm on load, prune on purge. The `PartModelDynamic`-has-no-`InstancesRayTrace` asymmetry is load-bearing |

### KSA.PartModelGlassModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre | `KSA/PartModelGlassModule.cs` |its-so-shiny| `ShinyPatches.cs` | OK | 4745 merged ModelGlass+ModelEye shaders; C# class unchanged |

### KSA.PartModelModule (+ nested Template, RaytracingMode)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre (return false skips submit) | `KSA/PartModelModule.cs` |its-so-shiny, humble-arteest (VehiclePaint)| `ShinyPatches.cs`;  `VehiclePaintPatches.cs` (`PartModelModulePrefix`) | OK | game uses `Parent.FullPart.LightSwitch` here; humble reads `Module<T>.Parent : Part` (`KSA/Module.cs`); **only caller** of `PartModel.AddInstance` |
| `Template.Internal : bool` (field) | direct API (write) | `KSA/PartModelModule.cs` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs` | OK | flipped false to force interior render |
| `Template.RayTracing : RaytracingMode` (field) | direct API | `KSA/PartModelModule.cs` | IvaForceRender | `IvaForceRender.cs` | OK | |
| `RaytracingMode.ShadowProxy` (enum) | enum | `KSA/PartModelModule.cs` | IvaForceRender | `IvaForceRender.cs` | OK | shadow-proxy skip in editor postfix |
| `PartModelModule.Template.RayTracers : static List<Template>` · `PartModelGlassModule.Template.RayTracers` | direct API (**prune**) | `KSA/PartModelModule.cs`; `KSA/PartModelGlassModule.cs` | parts-now | `Runtime/RuntimeModPurgeSteps.cs` | OK | two separate static registries KSA appends to (`:44`, `:34`) and never prunes. `PartModelDynamicModule.Template` has **no** `RayTracers` — do not add a third call |
| `PartModelModule.Template.{Mesh, Material}` · `PartModelGlassModule.Template.{Mesh, Material}` · `PartModelDynamicModule.Template.{Mesh, Material}` | direct API | `KSA/PartModelModule.cs`, `KSA/PartModelGlassModule.cs`, `KSA/PartModelDynamicModule.cs` | parts-now | `Runtime/BundleParserQueries.cs`; `RuntimeModLoaderGpuStates.cs` | OK | normalised into one `ModelComponent` shape for V9 and for blaming a failed GPU upload on the part that uses the asset |

### KSA.PartModelRenderer (+ nested ColorData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ColorData.BuildPipelineModel` / `BuildPipelineDynamic` (→ `ShaderReference.CompileVariantWithCustomOptions`) | behavior dependency (no patch) | `KSA/PartModelRenderer.cs` | humble-arteest (VehiclePaint) | — | OK | Part color pipelines recompile MeshIndirect **from disk per `ENABLE_*` variant** and destroy the module right after, which is why swapping `ShaderReference.Shader` cannot work and interception happens at `ShaderModuleUtils.FromFile` |

### KSA.PartTemplate (+ component template types)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<PartTemplate>(id)` / `Components : List<ModuleBase.TemplateDataBase>` (field) | reflection-field (string "Components") | `KSA/PartTemplate.cs` | zippo | `ksa-lights.lib/LightController.cs` | OK | walked to find light TemplateData |
| `PartTemplate.{ApplyGameData(PartGameDataReference), ResolveConsumerFeedPoints(), Dispose()}` | direct API | `KSA/PartTemplate.cs` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs`; `RuntimeModPurgeSteps.cs` | OK | `ApplyGameData` is **additive** (`AddRange` on connectors/masses/rockets/components) → parts-now attaches incrementally instead of calling `ModLibrary.AttachGameData()`. `ResolveConsumerFeedPoints()` starts with `ConsumerFeeds.Clear()`, so it **is** idempotent. `Dispose()` disposes only `Thumbnail` |
| `PartTemplate.{Thumbnail : ThumbnailReference?, IsSubPart : bool, Components, SubPartInstances, EditorTagsStrings : List<StringReference>}` | direct API | `KSA/PartTemplate.cs` | parts-now | `Runtime/PartThumbnailGenerator.cs`; `BundleValidatorRulesReferences.cs`; `Ui/ResultsPanel.cs` | OK | Before `OnDataLoad` runs, `Hash` is `KeyHash.Zero` and `EditorTags` is empty — validation therefore reads `Id` strings and `EditorTagsStrings` (`[XmlElement("EditorTag")]`, value in `StringReference.Value`, `KSA/StringReference.cs`) |
| `SubPartTemplate : PartTemplate` · `PartGameDataReference : PartTemplate` · `SubPartGameDataReference : PartGameDataReference` · `PartInstance.{InstanceOf, GetTemplate()}` | direct API (type hierarchy) | `KSA/SubPartTemplate.cs`; `KSA/PartGameDataReference.cs`; `KSA/SubPartGameDataReference.cs`; `KSA/PartInstance.cs` | parts-now | `Runtime/BundleParserQueries.cs`; `BundleValidatorRulesIdentity.cs` | OK | ⚠ a bare `is PartTemplate` matches **all four** part-shaped types — every parts-now classifier tests most-derived first. `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` throws `NullReferenceException` on a miss, which is what rule V5 pre-empts |
| `EditorTagDefinition : SerializedId` · `MeshViewModule.Template` | direct API | `KSA/EditorTagDefinition.cs`; `KSA/MeshViewModule.cs` | parts-now | `Runtime/GameRegistry.cs`; `BundleParserQueries.cs` | OK | tag-definition ids feed V7's known-tag set; `<MeshView>` presence is V12's warning |

### KSA.PartTree
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parts : ReadOnlySpan<Part>` | direct API | `KSA/PartTree.cs` |PartHelpers (→ many), garrys-torch, zippo, , its-so-shiny, thug-life, kitchen-sink| `PartHelpers.cs`; `ZippoSubmod.cs`; `ActionScanner.cs` | OK | top-level parts |
| `Root` | direct API | `KSA/PartTree.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | |
| `Batteries : ModuleStateful<…>.StateList` (field) | direct API | `KSA/PartTree.cs` | eternal-flame | `EternalFlameLib.cs` | OK | battery state list |
| `Modules.Get<Battery>()` (ModuleList) | direct API | `KSA/PartTree.cs` | its-so-shiny | `ShinyGridBuilder.cs` | OK | |
| `CreateFromNewPartTree(Part rootPart)` | direct API | `KSA/PartTree.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | core build path |
| `UpdateRenderData(ref readonly double4x4, bool isEditedVehicle, Viewport, int)` | direct API | `KSA/PartTree.cs` | i-feel-seen | `IFeelSeenPatches.cs` | OK | mod passes `in` → `ref readonly` |
| `States : ModuleStateList` (field) | direct API | `KSA/PartTree.cs` | kitchen-sink | `KitchenSinkLib.cs` | OK | passed as `oldStates` |
| `ReinitializeDerivedValues(ModuleStateList oldStates) : void` | direct API | `KSA/PartTree.cs` | kitchen-sink, doh | `KitchenSinkLib.cs`; `KittenSpawner.cs` | OK | also a 0-arg overload |
| `Controls : (control modules)` (rev 4699, backs `Vehicle.IsControllable`) | direct API | `KSA/PartTree.cs` | (informational) | — | ADDITIVE | new in 4750; not consumed |

### KSA.PbrMaterialReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `{DiffuseReference, NormalReference, PBRMap, EmissiveMap, Id}` + non-generic `.Get()` | reflection-field/method | `KSA/PbrMaterialReference.cs` | doh | `MaterialFactory.cs` | OK | `.BindlessHandle` off resolved `TextureReference`; file identical |
| `{DiffuseReference, NormalReference : TexturePowerReference?, PBRMap, EmissiveMap, ThinFilmMap}` (typed) + `_isReference = Diffuse==null && Normal==null && PBRMap==null` | direct API | `KSA/PbrMaterialReference.cs` | parts-now | `Runtime/BundleParserQueries.cs`; `BundleValidatorRulesSchema.cs`; `RuntimeModLoaderGpuStates.cs` | OK | V9 mirrors the `_isReference` test to tell a material **definition** from a **pointer** (an id-only `<PbrMaterial>` must be resolved against the submitted set, then the live registry, before its channels can be judged). V15 counts every channel with a `Path` as one bindless slot |

### KSA.PowerConsumer
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `LightIsActive : bool` (field) | direct API | `KSA/PowerConsumer.cs` |zippo, its-so-shiny| `ZippoSubmod.cs`; `ShinyPixelCell.cs` | OK | on/off toggle; rev-4681 electrical refactor didn't touch it |

### KSA.Program
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnDrawUiFrame(double)` | Harmony PREFIX (StarMap `[StarMapBeforeGui]`) | `KSA/Program.cs` | shell + all submods (every mod) | `unscience/Mod.cs` | OK | StarMap-owned string hook; drives per-frame `Update`/drain |
| `OnDrawUiViewports(double)` | Harmony POSTFIX (StarMap `[StarMapAfterGui]`) | `KSA/Program.cs` | shell + all submods | `unscience/Mod.cs` | OK | StarMap-owned string hook |
| `OnFrame(double,double)` | Harmony POSTFIX (StarMap `[StarMapAfterOnFrame]`) | `KSA/Program.cs` | (available; **not** used by supermod shell) | — | OK | StarMap dispatch only |
| `OnDrawUiConsole(double)` (`private void`) | Harmony PREFIX (**string** `"OnDrawUiConsole"`) | `KSA/Program.cs` @5348; called unconditionally `:2103` | unscience shell via `HiddenUiFrameHook` | `ksa-abstractions.lib/HiddenUiFrameHook.cs`; `unscience/Mod.cs`; `unscience/Patcher.cs` | OK @5348 | **Hidden-HUD fallback.** The two StarMap GUI targets above live inside `if (DrawUI)` in `OnFrame` (`:2093-2101`), so on F2 they are skipped and no StarMap GUI hook fires. This prefix replays `Mod.UpdateSubmods`/`UpdateWelds` at the same frame phase only while `Program.DrawUI` is false. Phase contract: every frame, after the UI block, before `ImGui.Render()`. Fallback anchor if renamed: `DrawFps()` (`:3008`) |
| `DrawUI : static bool` (prop) | direct API | `KSA/Program.cs` | HiddenUiFrameHook | `HiddenUiFrameHook.cs` | OK @5348 | gate for the fallback; flipped by `InputAction.ToggleUi` = F2 (`KSA/Input.cs`, handled `Program.cs`) |
| `DrawProgramMenusHook() : void` (empty modding hook) | Harmony post | `KSA/Program.cs` (cited `:3391` earlier) | unscience (MenuBarPatch) | `unscience/MenuBarPatch.cs` | OK | game ships as deliberate no-op; Unscience adds its workspace menu entry here |
| `ControlledVehicle : static Vehicle?` (field) | direct API | `KSA/Program.cs` |VehicleProvider (→ average-twr, geeforce, kitten-animations, )| `VehicleProvider.cs` | OK | |
| `ConsoleWindow : static ConsoleWindow` (field) | direct API | `KSA/Program.cs` | HotkeyGuard (→ all mods) | `HotkeyGuard.cs` | OK | `.IsOpen` guard (Brutal type — see section 3 Brutal) |
| `Editor : static VehicleEditor?` (field) | direct API | `KSA/Program.cs` | IvaForceRender, kitchen-sink, humble-arteest (VehiclePaint), parts-now | `IvaForceRender.cs`; `KitchenSinkLib.cs`; `PaintTargets.cs`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs`, `RuntimeModUnloader.cs` | OK | editor-only branch; humble uses it to pick flight vs editor paint targets. parts-now uses it for the unload safety gate and to clear the hover preview before a purge. Disposed+nulled in `Program.PrepareFrame` |
| `ThumbnailViewport : static IViewport` (a `PartThumbnailViewport` from `ViewportRegistry.CreatePartThumbnailViewport(_renderer, ViewportOptionFlags.RenderPartModels, sampler)`; throws until built) | direct (render) | `KSA/Program.cs` | parts-now | `Runtime/PartThumbnailGenerator.cs` | OK | dedicated offscreen thumbnail viewport — no camera save/restore, no resize, no `UpdateShaderData`. Shared with the part browser's hover preview (see `ThumbnailDynamic`) |
| `BindlessTextures : BindlessTextureLibrary` (public field) | direct API | `KSA/Program.cs` | parts-now, graffiti | `Runtime/BundleValidatorRulesIdentity.cs`; `Ui/StatusPanel.cs`; `graffiti.lib/DecalRenderer.cs`, `DecalTextures.cs` | OK | V15 texture-budget rule + the Status panel gauge; graffiti allocates/frees decal slots and binds the table as set 2. Constructed with `maxTextures = 1024` |
| `{EditorFlag : static bool, OffscreenTarget : static RenderTarget, RenderedViewport : static IViewport / MainViewport : static IGameViewport (`.ShaderSlot` feeds `GlobalShaderBindings.DynamicOffset`), SetViewport(CommandBuffer) : static, PointClampedSampler : static VkSampler, Instance.ResourceFrameIndex : int, Instance.ColorFormat : readonly VkFormat}` | direct API (render seam gates + pass state) | `KSA/Program.cs` | graffiti | `graffiti.lib/GraffitiPatches.cs`, `DecalRenderer.cs` | OK @5348 | the decal pass's editor/main-viewport identity checks + GridPass-style pass state (viewport, depth sampler, frame-ring slot, colour format). See `scope/decals.md` #2 |
| `IsMainThread() : static bool` | behavior dependency | `KSA/Program.cs` | parts-now | (via `Loading.OnFrame`, `KSA/Loading.cs`) | OK | 🔶 **U7** — see `KSA.Loading` |
| `RendererRebuildNeeded : static bool` (field) | direct API | `KSA/Program.cs` (consumed `PrepareFrame` :2096) | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | `VehiclePaintShaders.cs`; `CanopyProjectionShaders.cs` | OK | game's **deferred** full-renderer rebuild flag — the safe way for a mod to force shader/pipeline recompilation (same path a graphics-setting change takes) |
| `MainViewport : static IGameViewport { get; }` (= `ViewportRegistry.MainViewport`) | direct API | `KSA/Program.cs` | IvaForceRender, kitchen-sink, graffiti, hot-pursuit | `IvaForceRender.cs`; `DecalPicker.cs`; `DecalRenderer.cs`; `hot-pursuit.lib/HotPursuitPicker.cs`, `HotPursuitPose.cs` | OK @5402 | Hot Pursuit uses it only as the reference ego frame/picking viewport, never as the output target. |
| `FindNearbyCelestial(Camera) : static Celestial?` | direct API | `KSA/Program.cs` | hot-pursuit | `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | equivalent nearby-body lookup used after the mounted secondary camera writes `PositionEcl`; KSA's private `OnFrameCelestials` does not run for this camera. |
| `RenderViewport(CommandBuffer,IViewport,int) : private` | render-pass behavior | `KSA/Program.cs` | hot-pursuit | stock secondary viewport path | **LIMITATION @5402** | Secondary rendering runs stars, distant spheres, vehicle/part passes, and the stock translucent path, but omits `ParticleSystem`, `VolumetricExhaustRenderer`, the main planet/ocean/cloud pipeline, part-glass, and overall-bloom passes. Engine plumes and generic particles cannot be enabled by Hot Pursuit; those game-owned passes bind main-camera targets/resources. |
| `GetMainCamera() : static Camera` | direct API | `KSA/Program.cs` | glass | `glass.lib/FovController.cs` | OK @5402 | Glass is explicitly main-camera scoped so it does not overwrite Hot Pursuit's independent FOVs. |
| `GetRenderCamera() : Camera` (= `RenderedViewport.GetCamera()`) | direct (render) | `KSA/Program.cs`, `RenderedViewport` `:491` | thug-life | `ThugLifeQuadRenderer.cs` | OK | **replaced `GetMainCamera()` (`:584`)** — `RenderMainPass` runs per visible viewport (main + both crew-portrait viewports), and ego space is camera-relative, so the main camera mis-transformed the portrait passes |
| `GetRenderer() : Renderer` (→ `.Device`/`.Allocator`/`.Graphics`) | direct (render) | `KSA/Program.cs` (cited `:450` at the 4750 baseline) |thug-life, parts-now| `ThugLifeRenderManager.cs`;  `parts-now.lib/Runtime/RuntimeModLoaderGpuStates.cs`, `PartThumbnailGenerator.cs`, `RuntimeModUnloader.cs` | OK | Vulkan device. humble-arteest no longer needs it — the patched `FromFile` receives the device as an argument |
| `OffscreenTarget : RenderTarget` (→ `.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)`) | direct (render-pass) | `KSA/Program.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | replaced `OffScreenPass`/`RenderPassState` @5261 (dynamic rendering). ⚠ **null until `BuildRenderTargets()` (`Program.cs` @5402), which runs after `ModLibrary.LoadAll()` (`:942`) — i.e. after `[StarMapAllModsLoaded]`; the mod's pipeline build is lazy for exactly this reason** |
| `SetViewport(CommandBuffer)` | direct (render) | `KSA/Program.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | sizes to `RenderedViewport` |
| `GetPlayerDeltaTime() : static double` | direct API | `KSA/Program.cs` | garrys-torch | `WeldEngine.cs` | OK | fed into `GetJobSimStep` |
| `Instance : static (singleton)` | reflection (private) | `KSA/Program.cs` | doh, humble-arteest (KittenColor) | `MaterialSystemAccessor.cs`; `KittenColor.cs` | OK | render-systems root |
| `MaterialSystem : GpuMaterialSystem` (field) | reflection-field | `KSA/Program.cs` | doh, humble-arteest | `MaterialSystemAccessor.cs`; `KittenColor.cs` | OK | |
| `SuperMeshRenderSystem` (field) → `.TextureSystem : GpuTextureSystem` | reflection-field | `KSA/Program.cs`; `KSA/SuperMeshRenderSystem.cs` | doh | `MaterialSystemAccessor.cs` | OK | |
| `CharacterRenderSystem` (field) | reflection-field | `KSA/Program.cs` (`KSA/CharacterRenderSystem.cs`) | doh | `MaterialFactory.cs` | OK | |
| `LinearClampedSampler : static VkSampler` | direct (render) | `KSA/Program.cs` | parts-now | `parts-now.lib/Ui/ResultsPanel.cs` | OK | passed to `ThumbnailReference.GetOrCreateImGuiTexture` for the results-table thumbnails |
| `Instance : public static Program { get; private set; }` | direct API (typed) | `KSA/Program.cs` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs`; `Ui/StatusPanel.cs` | OK | same singleton doh/humble-arteest reach by reflection (row above, cited `:371` at the 4750 baseline); the **getter is public**, so parts-now reads it typed, purely to reach `BindlessTextures` |

### KSA.RocketCore
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.SerializedCollection<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetList() : List<T>` | reflection-method (string "GetList") | `KSA/SerializedCollection.cs` | doh | `KittenSpawner.cs` | OK | on `ModLibrary.AllParts`/`AllCharacters` |
| `Find(KeyHash) : T` | reflection-method | `KSA/SerializedCollection.cs` | doh | `KittenSpawner.cs` | OK | `"KittenBackPackPart"` |
| `GetList()` / `Find(KeyHash)` (typed, via `GameRegistry`) | direct API | `KSA/SerializedCollection.cs` | parts-now | `Runtime/GameRegistry.cs`; `RuntimeModLoaderDeltas.cs` | OK | `GetList()` hands back the **live** backing list, which is what makes `.Remove(item)` a real unregister |
| `_collection : private readonly ConcurrentDictionary<KeyHash,T>` | reflection-field (string "_collection", per closed generic) | `KSA/SerializedCollection.cs` | parts-now | `Runtime/GameRegistry.cs`, used `:154-165` | OK | 🔶 **U4.** `SerializedCollection<T>` exposes **no removal API** (`Register`/`Find`/`GetList` only), so unload and reload exist only through this field: removing from the list alone would leave `Find` resolving a purged item. **If KSA ever adds a real removal API, replace the reflection with it.** parts-now deliberately does not take the private `Lock` (`:12`) — game-thread-only access is what makes that safe |
| `Register(T) : bool` (returns **false** on duplicate `KeyHash`) | behavior dependency | `KSA/SerializedCollection.cs` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs` (V3/V4/V14) | OK | every caller reads `false` as "this is a reference to the existing entry", so a colliding Part is silently dropped and a colliding file's `Load()` never reads from disk. This is also why a reload **must** purge first (C5) |

### KSA.SerializedId
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Id : string { get; set; }` (base of PartTemplate, GaugeCanvas) | direct API | `KSA/SerializedId.cs` |  | `LayoutManager.cs`; `GarrysTorchSubmod.cs` | OK | layout key / template id |
| `Mod : Mod? { get; private set; }` | direct API | `KSA/SerializedId.cs` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs` | OK | names the **owning mod** in V3/V14 collision messages, and exempts ids owned by the mod currently being reloaded |

### KSA.SimStep
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Universe.GetJobSimStep(double) : SimStep` → `SimStep.NextTime : SimTime` | direct API | `KSA/Universe.cs` | garrys-torch | `WeldEngine.cs` | OK | tick-end time for new orbit state time |
| `SimStep` (param of `ExecuteNextVehicleSolvers`) | Harmony arg type | `KSA/Universe.cs` | eternal-flame, kitchen-sink, kiwis-marbles | (solver prefixes) | OK | prefixes ignore it (parameterless / by-name `dtPlayer` only) |

### KSA.SimTime
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SimTime` (`readonly struct`) | direct API | `KSA/SimTime.cs` |SimTimeProvider (→ geeforce, kiwis-marbles, garrys-torch, doh)| `SimTimeProvider.cs` | OK | |
| `Seconds() : double` (instance) | direct API | `KSA/SimTime.cs` |geeforce| `GeeForceSubmod.cs`; `MonitoringLoop.cs` | OK | timestamps sample |

### KSA.Situation / KSA.SituationEx
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.SolarPanel
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.StaticMeshRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialIndices : protected int[]` | reflection-field | `KSA/StaticMeshRenderable.cs` | doh | `KittenSpawner.cs` | OK | helmet/visor/mmu mesh handle swap |

### KSA.SubstanceLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `TryGetCombustionProcess(KeyHash)` + `KeyHash.Make` | direct API | `KSA/SubstanceLibrary.cs` | doh | `KittenSpawner.cs` | OK | `"MMH_NTO_1.6"` |
| `AllReactions() : static ReadOnlySpan<Reaction>` · `TryGetReaction(KeyHash) : static Reaction?` | direct API (read-only) | `KSA/SubstanceLibrary.cs` | doh, parts-now | `Runtime/BundleValidatorRulesReferences.cs` | OK | parts-now validation rule V10 — `<Reaction Id>` must already exist (the library is populated once at boot with `Dictionary.Add` and cannot take runtime entries). Empty library → warning, not error |
| `KeyHash.Make(ReadOnlySpan<char>) : static KeyHash` | direct API | `KSA/KeyHash.cs` | doh, parts-now | `Runtime/GameRegistry.cs`; `RuntimeModLoaderDeltas.cs`; `BundleValidatorRulesReferences.cs` | OK | **lowercases its input** → every parts-now id index is `OrdinalIgnoreCase` to match |

### KSA.SuperMeshRenderSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderMainPass(CommandBuffer) ` | Harmony post (render-pass) | `KSA/SuperMeshRenderSystem.cs` | thug-life | `thug-life.lib/ThugLifeRenderPatches.cs` | OK | records quad draws into offscreen MSAA pass; called 3× from Program |

### KSA.Tank
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ConfigureFor(IReactantMix)` | direct API | `KSA/Tank.cs` | doh | `KittenSpawner.cs` | OK | backpack propellant |

### KSA.Transform3D
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `PositionEcl : double3 { get; set; }` (virtual) | direct API (write) | `KSA/Transform3D.cs` | camera-controller-override | `KeyframeSequencePlayer.cs` | OK | mutated to move camera (dead until `___Transform` fixed); `Camera` overrides at `Camera.cs` |
| `LocalRotation : doubleQuat` (field) | direct API (write) | `KSA/Transform3D.cs` | camera-controller-override | `KeyframeSequencePlayer.cs` | OK | mutated to rotate camera (dead until fixed) |

### KSA.Universe
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` | Harmony pre (Priority.First) | `KSA/Universe.cs` | eternal-flame, kitchen-sink, kiwis-marbles | `unscience/Patcher.cs` (`EternalFlamePatches`, `KiwisMarblesPatches`); `unscience/Patcher.cs`; `kiwis-marbles.lib/KiwisMarblesPatches.cs` | OK | single overload → by-name `nameof`/`dtPlayer` resolution safe; kiwis-marbles depends on `PrepareFrame` ordering (Apply*Solvers before, ExecuteNextOrbitSolvers after) |
| `CurrentSystem : static CelestialSystem? { get; private set; }` | direct API | `KSA/Universe.cs` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs`; `CelestialProvider.cs` | OK | enumeration root |
| `GetElapsedSimTime() : static SimTime` | direct API | `KSA/Universe.cs` |SimTimeProvider (→ geeforce, kiwis-marbles, doh)| `SimTimeProvider.cs` | OK | |
| `GetJobSimStep(double) : SimStep` | direct API | `KSA/Universe.cs` | garrys-torch | `WeldEngine.cs` | OK | (see KSA.SimStep) |

### KSA.Vehicle
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle` (type) | direct API | `KSA/Vehicle.cs` |VehicleProvider (→ ~all)| `VehicleProvider.cs` | OK | `OfType<Vehicle>()` |
| `Parts : PartTree` (field) | direct API | `KSA/Vehicle.cs` |PartHelpers (→ many), eternal-flame, its-so-shiny, kitchen-sink| `PartHelpers.cs`;  `EternalFlameLib.cs`;  `its-so-shiny.lib/ShinyGridBuilder.cs` | OK | get+set |
| `Id` (inherited Astronomical.Id) | direct API | `KSA/Astronomical.cs` | (see KSA.Astronomical) | — | OK | |
| `RefillConsumables() : void` | direct API | `KSA/Vehicle.cs` | eternal-flame | `EternalFlameLib.cs` | OK | fuel/resource refill |
| `AddVolumetricExhaustInstances(Camera, Viewport, VolumetricExhaustRenderer, double frameDeltaTime) : void` | **Harmony postfix** `(Vehicle __instance, Camera camera, VolumetricExhaustRenderer renderer, double frameDeltaTime)` | `KSA/Vehicle.cs` | pyro | `pyro.lib/PyroPatches.cs` | OK @5348 | per-visible-vehicle exhaust submission (`Program.OnPreRender`); pyro adds its plumes to the same batch. Resolved via `nameof` (typed) |
| `PosAsmbToBody(double3) : double3` · `Body2Cce : doubleQuat` | direct API | `KSA/Vehicle.cs` | pyro | `pyro.lib/PlumeEmitter.cs` | OK @5348 | same chain as `RocketNozzleState.AddExhaustInstance` |
| `GetMatrixAsmb2Ego(Camera) : double4x4` · `BoundingSphereRadiusBody : double` · `static ComputeEnu2Cce(double3, doubleQuat) : doubleQuat?` | direct API | `KSA/Vehicle.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPicker.cs`, `HotPursuitPose.cs` | OK @5402 | raycast broad-phase + sub-part transform root; ENU helper is graffiti-only |
| `Teleport(Orbit?, doubleQuat?, double3?) : void` | direct API | `KSA/Vehicle.cs` | garrys-torch, doh (KittenEva) | `WeldEngine.cs`; `KittenSpawner.cs` | OK | core mutation; nullable params |
| `UpdatePerFrameData() : override void` | direct API | `KSA/Vehicle.cs` | garrys-torch, doh | `WeldEngine.cs`; `KittenSpawner.cs` | OK | refresh caches post-teleport |
| `UpdateVehicleConfiguration() : void` | direct API | `KSA/Vehicle.cs` |its-so-shiny| `LcdGridBuilder.cs`; `ShinyGridBuilder.cs` | OK | |
| `Parent : IParentBody => Orbit.Parent` | direct API | `KSA/Vehicle.cs` |garrys-torch, average-twr (dead), doh| `WeldEngine.cs`; `VehicleTelemetry.cs`; `KittenSpawner.cs` | OK | |
| `Orbit : Orbit => Patch.Orbit` | direct API | `KSA/Vehicle.cs` |garrys-torch| `WeldEngine.cs`; `VehicleTelemetry.cs` | OK | |
| `GetPositionCci() : double3` | direct API | `KSA/Vehicle.cs` | garrys-torch | `WeldEngine.cs` | OK | (concrete; cf. `IOrbiter.GetPositionCci`) |
| `GetVelocityCci() : double3` | direct API | `KSA/Vehicle.cs` | garrys-torch | `WeldEngine.cs` | OK | |
| `GetBody2Cci() : doubleQuat` | direct API | `KSA/Vehicle.cs` | garrys-torch | `WeldEngine.cs` | OK | |
| `GetAsmb2Cci() : doubleQuat` | direct API | `KSA/Vehicle.cs` | doh | `KittenSpawner.cs` | OK | spawn positioning |
| `CenterOfMassAsmb : double3` (field) | direct API | `KSA/Vehicle.cs` | garrys-torch | `WeldEngine.cs` | OK | part-anchor offset base |
| `BodyRates : double3` (field) | direct API | `KSA/Vehicle.cs` | garrys-torch, doh | `WeldEngine.cs`; `KittenSpawner.cs` | OK | NaN-guarded by mod |
| `Body2Cce : doubleQuat` (field) | direct API | `KSA/Vehicle.cs` | i-feel-seen, doh | `IFeelSeenPatches.cs`; `KittenSpawner.cs` | OK | |
| `Asmb2Ego : doubleQuat` | direct (render) | `KSA/Vehicle.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | |
| `GetMatrixAsmb2Ego(Camera) : double4x4` | direct API | `KSA/Vehicle.cs` | i-feel-seen, thug-life | `IFeelSeenPatches.cs`; `ThugLifeQuadRenderer.cs` | OK | |
| `GetWorldMatrix(Camera) : float4x4?` | Harmony pre + reflection-method (string) | `KSA/Vehicle.cs` | i-feel-seen | `IFeelSeenPatches.cs` | OK | string-resolved; non-virtual |
| `UpdateRenderData(Viewport, int) : virtual void` | Harmony pre + reflection-method (string) | `KSA/Vehicle.cs` | i-feel-seen | `IFeelSeenPatches.cs` | OK | string-resolved; `KittenEva` overrides (`KittenEva.cs`) |
| `IsEditedVehicle : bool` | direct API | `KSA/Vehicle.cs` | i-feel-seen | `IFeelSeenPatches.cs` | OK | |
| `NavBallData` (see KSA.NavBallData) | direct API | `KSA/Vehicle.cs` | average-twr | — | OK | |
| `FlightComputer` (see KSA.FlightComputer) | direct API | `KSA/Vehicle.cs` |average-twr| — | OK | |
| `TotalMass : float => _props.TotalMassPropsAsmb.Props.Mass` | direct API | `KSA/Vehicle.cs` |average-twr| `TwrDataReader.cs`; `VehicleTelemetry.cs` | OK | |
| `AccelerationBody : double3 => KinematicMeasurements.AccelerationBody` | direct API | `KSA/Vehicle.cs` |geeforce| `GForceRecorder.cs`; `VehicleTelemetry.cs` | OK | |
| `Dispose() : void` | direct API | `KSA/Vehicle.cs` | doh | `KittenSpawner.cs` | OK | despawn |
| `IsControllable : virtual bool` (rev 4699) | direct API | `KSA/Vehicle.cs` | (informational — not consumed) | — | ADDITIVE | new; gates control on a Control Module. RPC ignite path is NOT gated by it |

### KSA.VehicleEditingSpace
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parts : PartTree?` (field) | direct API | `KSA/VehicleEditingSpace.cs` (cited `:14` at the 4750 baseline) | kitchen-sink | `KitchenSinkLib.cs` | OK | null-guarded |
| `AllParts : ReadOnlySpan<Part> => Parts?.Parts ?? default` | direct API | `KSA/VehicleEditingSpace.cs` | parts-now | `Runtime/RuntimeModUnloadGate.cs` | OK | null-safe by construction — an empty editor yields an empty span, not an NRE |

### KSA.VehicleEditor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `EditingSpace : VehicleEditingSpace` (field) | direct API | `KSA/VehicleEditor.cs` (cited `:334` at the 4750 baseline) | kitchen-sink, parts-now | `KitchenSinkLib.cs`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs` | OK | |
| `RegisterTag` (tags registered from `CoreEditorTagsGameData.xml`, rev 4731/4741) | direct API | `KSA/PartTemplate.cs` | parts-now (V7) | — | CHANGED | tag categories drifted ("Interstage" removed; "Stages"→"Resource Groups"). `MarkEditorTagDefinitionsLoaded()` locks the list at boot, after which `RegisterTag` logs a warning and adds nothing — which is exactly what parts-now rule **V7** rejects up front |
| `ResetPartDiameterCache() : public static void` → clears `PartWindow._diameterCache` | direct API | `KSA/VehicleEditor.cs` | parts-now | `parts-now.lib/Runtime/EditorRefresh.cs` | OK | the **only** editor nudge a runtime load/purge needs: `PartWindow.OnDrawUi` re-reads `ModLibrary.AllParts.GetList()` every frame, but the diameter cache is built lazily and reused |
| `UnattachedPartTrees : List<PartTree>` (field) | direct API | `KSA/VehicleEditor.cs` | parts-now | `Runtime/RuntimeModUnloadGate.cs` | OK | loose part trees in the open editor also block an unload |
| `DynamicThumbnail : ThumbnailDynamic?` (field) | direct API | `KSA/VehicleEditor.cs` | parts-now | `Runtime/RuntimeModUnloader.cs` | OK | 🔶 **U6** — cleared with `SetSelectedPart(null)` as purge step 0; see `KSA.Rendering.Thumbnails` |
| `_editorTagLookup : private static Dictionary<uint,string>` | reflection-field (string) | `KSA/VehicleEditor.cs` | parts-now | `Runtime/GameRegistry.cs` | OK | **degraded, not fatal** — V7 falls back to the six built-in tags + `AllEditorTagDefinitions` ids |
| `ScaleBoundsFor(Part) : private static (double Min, double Max)` | Harmony postfix (by-name) | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorScalePatches.cs` | OK (**new @5348**) | rewrites `__result` to `(1e-6, +inf)` when clamp removal is on; the only place the 0.5x–2x clamp is expressed |
| `UpdateSelectedScale(ref readonly double4x4, Viewport) : private void` | Harmony prefix (by-name) | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorScalePatches.cs` | OK (**new @5348**) | skipped (returns false) when per-axis scaling is on; prefix binds `matrixVehicleAsmb2Ego` by name |
| `UpdateScaleGizmo(ref readonly double4x4, doubleQuat, Viewport, double) : public void` | Harmony postfix (by-name) | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorScalePatches.cs` | OK | per-frame drag-session reset on `!GizmoGrabbed` |
| `QuantizeScale(Part, double rawScale) : private static double` | Harmony prefix (by-name) + `AccessTools.MethodDelegate` | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorScalePatches.cs` | OK (**new @5348**) | prefix bypasses 0.25 m snapping when `Snap` is off (`rawScale` bound by name); delegate is what the per-axis drag calls |
| `ForEachPartWithSymmetry(Part, Action<Part>) : private static void` | reflection → `AccessTools.MethodDelegate` | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorScalePatches.cs` | OK (**new @5348**) | reused so per-axis drags propagate to symmetry siblings like stock |
| `Selected`, `HighlightedGizmoSegmentIndex`, `ScaleGizmo`, `CursorPositionScreen{,LastFrame}`, `GizmoGrabbed` (public fields) | direct API | `KSA/VehicleEditor.cs` | dont-stifle-me | `PerAxisScaleDrag.cs` | OK | 🔶 segment index→axis `0/1/2 = X/Y/Z` invariant of `ScaleGizmo`'s 3-segment ctor (`:1179`) |
| `DrawParachuteSection(Part, ReadOnlySpan<Parachute>) : private void` | Harmony prefix (by-name) | `KSA/VehicleEditor.cs` | dont-stifle-me | `EditorValueLimitPatches.cs` | OK (**new consumer @5402**) | expands the selected subtree's runtime chute diameter bounds to 2–1000 m immediately before the stock slider reads them; patch binds only `part`, not the byref-like span |

### KSA.Parachute / ChuteTuning
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parachute.SetDiameter(float) : void` | Harmony prefix (typed signature) | `KSA/Parachute.cs` | dont-stifle-me | `EditorValueLimitPatches.cs` | OK (**new consumer @5402**) | expands every chute on the part before stock `ChuteTuning.ClampDiameter`, keeping multi-canopy and editor-symmetry counterparts consistent |
| `Parachute.Tuning`; `ChuteTuning.{DiameterM, MinDiameterM, MaxDiameterM, ClampDiameter(float)}` | direct API | `KSA/Parachute.cs`; `KSA/ChuteTuning.cs` | dont-stifle-me | `EditorValueLimitPatches.cs` | OK (**new consumer @5402**) | original per-instance bounds are saved, changed to 2 / 1000 while enabled, and restored on toggle-off or unload; chosen diameter is preserved |

### KSA.VehicleEngine
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.IViewport / KSA.IGameViewport (replaced `KSA.Viewport` @5402)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `Mode : CameraMode { get; }` (property) | direct API | `KSA/IViewport.cs` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs` | OK (retyped) | vs `CameraMode.IVA`. Was a field on the old `Viewport` class |
| `GetCamera() : Camera` | direct API | `KSA/IViewport.cs` | i-feel-seen, parts-now, dont-stifle-me, hot-pursuit | `IFeelSeenPatches.cs`; `PartThumbnailGenerator.cs`; `PerAxisScaleDrag.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Hot Pursuit uses main as its reference camera. |
| `Size : int2 { get; }` · `ShaderSlot : int { get; }` (was `Viewport.Index`) | direct API | `KSA/IViewport.cs` | parts-now, graffiti | `PartThumbnailGenerator.cs`; `DecalRenderer.cs`; `ShaderSlot` consumed indirectly by `ThumbnailDynamic.UpdateGlobalCameraData`'s camera-UBO slice | OK (retyped) | slots come from `ViewportRegistry`'s pool (max 8); the per-viewport UBOs are now sized for 8 slots (rev 5401 stride fix, `GlobalShaderBindings.cs`) |
| `IViewport` (param of `UpdateRenderData`/`AddInstance`/`OnFrame`/`UpdateSelectedScale`/render prefixes) | Harmony arg type | `KSA/IViewport.cs` |its-so-shiny, i-feel-seen, humble-arteest, IvaForceRender, dont-stifle-me, camera-controller-override, pyro| (render/editor prefixes) | **CHANGED @5402** (fixed) | every game method that took `Viewport` now takes `IViewport`; all remain single overloads, so by-name `AccessTools.Method` still resolves. Prefixes that name the param declare `IViewport` |
| `ViewportOptionFlags.RenderPartModels` / `UseRaytracing` gates | new gating | `KSA/ViewportOptionFlags.cs`; `PartModel.cs` |IvaForceRender, humble-arteest,  (postfix/prefix on `AddInstance`)| `IvaForceRender.cs` | ADDITIVE | `AddInstance` early-returns for viewports without `RenderPartModels`; every game-created viewport has it (`ViewportPresets.cs`), so dormant. Recommended mirror in IvaForceRender's postfix (open) |

### KSA.ViewportRegistry / KSA.IGameViewport / KSA.IViewportOwner
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `MAX_VIEWPORTS = 8`; allocation sealed after boot; four `ViewportType.Secondary` instances | direct API + standing capacity invariant | `KSA/ViewportRegistry.cs`; `KSA/Program.cs` | hot-pursuit | `hot-pursuit.lib/HotPursuitSubmod.cs` | OK @5402 | Main + thumbnail + 4 secondary + 2 portraits fill all slots. New allocation is impossible after `SealAllocation`; leases are the supported surface. |
| `AvailableSecondaryCount`; `TryClaimSecondaryViewport(IViewportOwner,out IGameViewport)`; `TryGetOwned`; `ReleaseSecondaryViewport(IViewportOwner)` | direct API | `KSA/ViewportRegistry.cs` | hot-pursuit | `HotPursuitSubmod.cs` | OK @5402 | Shared with Add Camera and docking cameras. Claim/release resets viewport defaults; closing stock `DrawImGui` releases the lease. |
| `IGameViewport.{BaseCamera,SetName,SetCameraMode,DrawImGui}` + inherited visibility/resize APIs | direct API | `KSA/IGameViewport.cs`; `KSA/IViewport.cs`; `KSA/GameViewport.cs` | hot-pursuit | `HotPursuitSubmod.cs`, `.Ui.cs` | OK @5402 | Uses KSA-owned targets, texture, window and renderer; no custom GPU resources. |
| `IViewportOwner` empty marker | direct API | `KSA/IViewportOwner.cs` | hot-pursuit | `HotPursuitCamera.cs` | OK @5402 | One stable owner object per camera entry keys the registry ownership map by reference identity. |

### KSA.FixedController
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `OnFrame(IViewport,double)` | Harmony selective prefix | `KSA/FixedController.cs` | hot-pursuit | `hot-pursuit.lib/HotPursuitPatches.cs` | OK @5402 | Keystone same-frame seam: returns false only for a currently owned viewport after applying/retaining its mounted pose; caller `GameViewport.OnFrame` then immediately runs `Camera.OnFrame`. |

### KSA.VolumetricExhaustTemplate
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get(string id) : static VolumetricExhaustTemplate?` | direct API (read-only) | `KSA/VolumetricExhaustTemplate.cs` | parts-now, pyro | `Runtime/BundleValidatorRulesReferences.cs`; `pyro.lib/PlumeTemplates.cs` | OK | validation rule V10 — `<VolumetricExhaust Id>` must already resolve |
| `References : internal static SerializedCollection<VolumetricExhaustTemplate>` → `.GetList()` | **reflection-field (INTERNAL, string)** | `KSA/VolumetricExhaustTemplate.cs` | pyro | `pyro.lib/PlumeTemplates.cs` | OK @5348 | lists template ids for the combos; **falls back to the 7 stock ids** if missing |
| `Absorption` / `Emission` / `Noise` / `LengthWeights` / `Quality` (fields) + their `DoubleReference.Value`, `BoolReference.Value`, `ColorGradient.Color0..3 : ColorRgbReference`, `Flow.MachDiamonds.*`, `Quality.VolumetricVesselShadows` | direct API (read **and write**) | `KSA/VolumetricExhaustTemplate.cs`; `KSA/Absorption.cs`, `Emission.cs`, `Noise.cs`, `LengthWeights.cs`, `Quality.cs`, `MachDiamonds.cs`, `ColorGradient.cs` | pyro | `pyro.lib/PyroSubmod.TemplateUi.cs`; `PlumeEmitter.cs`; `PlumePhysics.cs` | OK @5348 | shared-template editor (same writes as the game's `VolumetricExhaustRenderer.OnDrawUi`); GPU `ExhaustTemplateData` buffer is rebuilt from these **every frame** in `Render()` (`VolumetricExhaustRenderer.cs`) |

### KSA.VolumetricExhaustRenderer
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VolumetricExhaustRenderer` (type; Harmony arg) | Harmony arg type | `KSA/VolumetricExhaustRenderer.cs` | pyro | `PyroPatches.cs` | OK @5348 | lib references `Brutal.Vulkan*` + `BepuUtilities` so the type resolves |
| `AddInstance(float3 emitterPosition, float3 axis, VolumetricExhaustInstance, float throttle, float3 airVelocity, float airDensity) : float` | direct API | `KSA/VolumetricExhaustRenderer.cs` | pyro | `pyro.lib/PlumeEmitter.cs` (+ `ComputeAirState` `:87-98`) | **CHANGED @5402** (fixed) | **gained `airVelocity`/`airDensity` @5402** for atmospheric plume bend/fold (`ExhaustPlumeDeformation`, `:809-811`); pyro mirrors `Vehicle.AddVolumetricExhaustInstances` (`Vehicle.cs`). ⚠ **refraction regression @5402:** nothing sets `_hasRefractionInstances` any more (OLD `:960`), so the refraction pass never runs — game-side, needs live confirmation. Previous note: the game's own nozzle submission entry; reads `instance.ShaderData` + `LastPlumeData`, derives all plume geometry. **5348 delta already handled:** reads `PlumeData.ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity` |
| `Disabled : bool` | direct API | `KSA/VolumetricExhaustRenderer.cs` | pyro | `pyro.lib/PyroSubmod.cs` | OK @5348 | `_maxInstanceCount == 0` (exhausts off in settings) |

### KSA.VolumetricExhaustInstance / KSA.VolumetricExhaustReference / KSA.ExhaustInstance
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VolumetricExhaustReference { Id }` + `Load() : void` + `Template` | direct API | `KSA/VolumetricExhaustReference.cs` | pyro | `pyro.lib/PlumeTemplates.cs` | OK @5348 | `Load()` resolves `_template` via `VolumetricExhaustTemplate.Get(Id)` — no reflection needed |
| `new VolumetricExhaustInstance(VolumetricExhaustReference)` · `Template` · `LastPlumeData` (public field) | direct API | `KSA/VolumetricExhaustInstance.cs` | pyro | `PlumeTemplates.cs`; `PlumeEmitter.cs` | OK @5348 | one per plume — owns the 4-slot startup/shutdown pulse tracker |
| `UpdateState(double simulationTime, bool isActive, double simulationDeltaTime, PlumeData) : bool` | direct API | `KSA/VolumetricExhaustInstance.cs` | pyro | `pyro.lib/PlumeEmitter.cs` | OK @5348 | false ⇒ fully shut down, skip submit. `isActive` = Enabled && Throttle>0 |
| `OnSettingsChanged() : void` | direct API | `KSA/VolumetricExhaustInstance.cs` | pyro | `pyro.lib/TemplateRefresher.cs` | OK @5348 | re-reads template into `_shaderData` after a Template Editor edit |
| `_shaderData : private ExhaustInstance` | **reflection-field (PRIVATE, string; `AccessTools.FieldRefAccess`)** | `KSA/VolumetricExhaustInstance.cs` | pyro | `pyro.lib/PlumeEmitter.cs` | OK @5348 | per-plume `absorptionDensity` / `refractionIntensity` overrides written before `AddInstance` copies the struct. **Gracefully disabled** (UI says so) if the field is gone |
| `ExhaustInstance.absorptionDensity` / `.refractionIntensity` (fields) | direct API (struct layout) | `KSA/ExhaustInstance.cs` | pyro | `PlumeEmitter.cs` | OK @5348 | ⚠ **layout drift** @5348: colours/noise/brightness moved OUT of this struct into `ExhaustTemplateData` (per-template buffer indexed by `templateIndex`) — that is why per-plume colour is not offered |
| `PlumeData` (struct, all `required` fields incl. **`ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity`, `InletTemperature` — new @5348**) | direct API (object initializer) | `KSA/PlumeData.cs` | pyro | `pyro.lib/PlumePhysics.cs` | OK @5348 | a renamed/added `required` member is a **compile** break here (good — loud) |

### KSA.GasProperties / KSA.GasConditions / KSA.RocketDesign (plume maths)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GasProperties { Gamma, SpecificGasConstant }` · `ComputeSpeedOfSound(float)` · `ComputeSupersonicExpansionPressureAngle(float,float)` · `ComputeSupersonicExpansionPressureMach(float,float)` · `ComputePrandtlMeyer(float)` | direct API | `KSA/GasProperties.cs` | pyro | `pyro.lib/PlumePhysics.cs` | OK @5348 | mirrors `RocketNozzle.UpdatePlumeData` (`KSA/RocketNozzle.cs`) |
| `GasConditions { Pressure, Temperature }` · `ComputeDensity(GasProperties)` | direct API | `KSA/GasConditions.cs` | pyro | `PlumePhysics.cs` | OK @5348 | pressures in **Pa** (game-internal unit) |
| `RocketDesign.SolveMachNumberFromAreaRatio(GasProperties, double) : static float` · `ComputeAreaRatioFromMachNumber(double, double) : static double` | direct API | `KSA/RocketDesign.cs` | pyro | `PlumePhysics.cs` | OK @5348 | exit Mach from (exit/throat)² ; Mach-disk area ratio |
| `Universe.GetElapsedSeconds()` · `Universe.GetSimulationSpeed()` | direct API | `KSA/Universe.cs` | pyro | `pyro.lib/PyroSubmod.cs` | OK @5348 | same time source as `RocketNozzleState.AddExhaustInstance` / `Vehicle.AddVolumetricExhaustInstances` |
| `PartTree.RocketNozzles.ModulesAndAllStates` (enumerator: `.FxState.VolumetricExhaust`, `.Module.RecomputeGasVisibilityDensity(in …)`) | direct API | `KSA/Vehicle.cs` (game's own use); `KSA/RocketNozzle.cs` | pyro | `pyro.lib/TemplateRefresher.cs` | OK @5348 | pushes Template Editor edits to real engine nozzles (mirrors the debug editor's `changed` path); wrapped in try/catch |
| `ColorRgbReference(float3)` + `OnDataLoad(new Mod())` · `Value.AsFloat3` | direct API | `KSA/ColorRgbReference.cs` | pyro | `pyro.lib/PyroSubmod.TemplateUi.cs` | OK @5348 | identical to the game's editor colour write (`VolumetricExhaustRenderer.cs`) |

### KSA.XmlHelper
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Serializers : public static Dictionary<Type, XmlSerializer>` → `[typeof(AssetBundle)]` | direct API | `KSA/XmlHelper.cs` | parts-now | `Runtime/BundleParser.cs` | OK | ⚠ **must** be the game's own serializer instance: it carries the `XmlAttributeOverrides` that map `<PartModel>`/`<Tank>`/`<Collider>`/`<Light>`… onto `PartTemplate.Components`. A hand-built `new XmlSerializer(typeof(AssetBundle))` silently drops every component. A missing entry is reported to the user, never thrown |

### KSA.Rendering (RenderTarget resolve seam — graffiti)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderTarget.ResolveAttachments(CommandBuffer inCmdBuffer) : void` | **Harmony postfix** `(RenderTarget __instance, CommandBuffer inCmdBuffer)` | `KSA.Rendering/RenderTarget.cs` | graffiti | `graffiti.lib/GraffitiPatches.cs` | OK @5348 | 🔶 graffiti's keystone seam: called unconditionally per viewport from `Program.RenderGame` (body MSAA-gated, postfix fires regardless) — the post-resolve window `GridPass` draws in. Resolved via `nameof`; param name `inCmdBuffer` is load-bearing for Harmony binding |
| `RenderTarget.{DepthImage, ColorImage : RenderImage?, Extent}` | direct API (render) | `KSA.Rendering/RenderTarget.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` (`RecordPass`) | OK @5348 | resolved single-sample scene depth (reverse-Z, sampled per fragment) + the colour attachment the pass draws into |
| `BarrierBatch` (span ctor, `Add`, `SubmitAndFlush`) · `ImageBarrierInfo.Presets.{DepthSampledReadF, ColorAttachmentReadWrite}` | direct API (render) | `KSA.Rendering/BarrierBatch.cs`; `KSA.Rendering/ImageBarrierInfo.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | depth is moved to sampled-read and LEFT there, exactly as `GridPass` leaves it — the engine's tracked-state barriers tolerate that |
| `RenderingPresets.{ReverseZDepthStencil.NoDepthTest, BlendState.BlendColorAlphaOver}` | direct API (render) | `KSA/RenderingPresets.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | no depth attachment at all — occlusion is per-fragment from the sampled depth; alpha-over composite |

### KSA.Rendering.Thumbnails (parts-now)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ThumbnailRenderer(Renderer)` ctor · `SIZE : static int` · `ColorFormat : static readonly VkFormat` · `{PerInstanceDataDescriptorSetLayout, PerDrawDataDescriptorSetLayout, Sampler}` · `RecordPartRender(CommandBuffer, ThumbnailReference, ThumbnailRenderResources, Viewport, string)` | render | `KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` | parts-now | `parts-now.lib/Runtime/PartThumbnailGenerator.cs` | OK | the three layouts + sampler are forwarded straight from `PartModelRenderer.ColorData` (`ThumbnailRenderer.cs`), so a Part-color-pipeline change reaches parts-now here |
| `ThumbnailRenderResources(Renderer, DescriptorSetLayoutEx, DescriptorSetLayoutEx, VkSampler, int)` · `.DrawCommandVector.ElementCount` · `.UpdateDescriptorSets()` · `.AddDraw(float4x4, PartModel*Module.Template)` | render | `KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs` | parts-now | `PartThumbnailGenerator.cs` | OK | 🔶 **U3** — `AddDraw` reads `inTemplate.Material.{DiffuseReference,NormalReference,PBRMap}.BindlessHandle` **unguarded** (`:138-140`). A zero draw count is diagnosed *before* an image is created, since `RecordPartRender` is what transitions the image out of `VK_IMAGE_LAYOUT_UNDEFINED` |
| `ThumbnailPart(Camera inParent, PartInstance? = null)` · `.Children : List<ThumbnailPart>?` · `.Dispose()` | render | `KSA.Rendering.Thumbnails/ThumbnailPart.cs` | parts-now | `PartThumbnailGenerator.cs` | OK | root part parented to the thumbnail viewport's camera |
| `ThumbnailReference.{ImageView : ImageViewEx, ModelTransform : TransformReference?, GetOrCreateImGuiTexture(VkSampler), Dispose(), CreateImageView(...)}` | render | `KSA.Rendering.Thumbnails/ThumbnailReference.cs`; `KSA/TransformReference.cs` | parts-now | `PartThumbnailGenerator.cs`; `RuntimeModPurgeSteps.cs`; `Ui/ResultsPanel.cs` | OK | ⚠ **`ImageView.IsNull()` is a load-bearing guard.** A `<Thumbnail>` from XML has a `ModelTransform` but **never had `CreateImageView` called**, so `Dispose()` NREs on a null captured `Device`. parts-now also preserves a declared `ModelTransform` across regeneration, which the game's own `CreateThumbnailImage` (`ThumbnailCreator.cs`) drops |
| `ThumbnailCreator.{ResetRootPart, AddPart, MoveRootPart, CollectDraws, CreateThumbnailReference}` | render | `KSA.Rendering/ThumbnailCreator.cs` | parts-now | `PartThumbnailGenerator.cs` | OK | same framing as the game's own `PreparePartThumbnails` (`:54`). `AddPart` only walks `SubPartInstances`, so a SubPart collects no draws |
| `ThumbnailDynamic.{UpdateGlobalCameraData(Viewport, Camera) : static, SetSelectedPart(PartTemplate?), Render(double)}` | render | `KSA.Rendering.Thumbnails/ThumbnailDynamic.cs` | parts-now | `PartThumbnailGenerator.cs`; `RuntimeModUnloader.cs` | OK | 🔶 **U6.** `Render`'s `ResetRootPart`/`AddPart`/`MoveRootPart` block (`:184-186`) sits **outside** its try/catch (`:197`), and `AddPart` → `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` throws on a purged template — straight out of `Editor.OnPreRender` (`KSA/VehicleEditor.cs` ← `KSA/Program.cs`). Hence purge step 0 clears `SetSelectedPart(null)` first. parts-now and `ThumbnailDynamic` share the thumbnail viewport safely **only** because parts-now submits in `Program.OnDrawUiFrame` and `Render` runs later in the same frame |

### RenderCore.* (game-side render layer)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | direct (render) | `RenderCore/RenderTechnique.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | |
| `ShaderModuleUtils.FromFile(Device, string filePath, out VkShaderStageFlags shaderStage, CompileOptions? options)` | **Harmony pre** (humble-arteest, free-fallin) | `RenderCore/ShaderModuleUtils.cs` |humble-arteest (VehiclePaint), free-fallin (Full Canopy)| `VehiclePaintPatches.cs` / `CanopyProjectionShaders.cs` (`FromFilePrefix`)| OK | Shared in-memory shader seam. Humble targets two part fragments; free-fallin targets `Model.vert`, `Model_Skinned.vert`, and `ModelPbr.frag`; both pass through all other paths and fall back to stock on error. Param names `device`/`filePath`/`shaderStage`/`options` are load-bearing for Harmony binding. |
| `KSA.Rendering.Utils.SetShaderFromMod(SimpleShaderStages, Device, string modId, bool useCustomOptions)` | **Harmony prefix** | `KSA.Rendering/Utils.cs` | free-fallin (Full Canopy) | `CanopyProjectionShaders.cs` (`SetShaderFromModPrefix`) | OK @5402 | Ordinary model rebuilds reuse cached `ShaderReference` modules and otherwise bypass `FromFile`. For the three projection shader ids only, the prefix sets `useCustomOptions=true`, routing compilation through `CompileVariantWithCustomOptions` → the `FromFile` transform. |
| `ShaderModuleUtils.FromString(Device, ReadOnlySpan<byte> shaderCode, VkShaderStageFlags, CompileOptions?, ReadOnlySpan<byte> debugName)` | direct (render) | `RenderCore/ShaderModuleUtils.cs` (was :77) | humble-arteest (VehiclePaint), free-fallin (Full Canopy), graffiti | `VehiclePaintPatches.cs` / `CanopyProjectionShaders.cs` (`FromFilePrefix`); `graffiti.lib/DecalRenderer.cs` (`Compile`) | OK | `debugName` becomes shaderc's input-file name → relative `#include` resolution. Shader patchers pass a NUL-terminated real path; graffiti passes a fake filename next to shipped `GridFrag` so `Common/*.glsl` resolves. |
| `BindlessTextureLibrary.{DescriptorSetLayout, DescriptorSet, AddTexture(VkImageView) : int, FreeTexture(int)}` | direct API (render) | `RenderCore.Systems/BindlessTextureLibrary.cs` | graffiti | `graffiti.lib/DecalRenderer.cs`, `DecalTextures.cs` | OK @5348 | decal-texture slots + set 2 of the decal pipeline. UpdateAfterBind\|PartiallyBound layout makes live slot writes legal; `FreeTexture` rewrites the slot to the empty texture, so only the image needs deferred destroy. Shares the same 1024-slot pool parts-now budgets (V15) |
| `TextureLoader.LoadFromMemory(bytes, FormatType.Png, LoadOptions)` · `TextureAsset(.LoadOptions(R8G8B8A8UNorm, KtxTranscodeFmt.Rgba32))` · `new SimpleVkTexture(Allocator, StagingPool, TextureAsset, CreateOptions)` · `Stb/Ktx/GliTexture.Destroy()` | direct API (texture upload) | `Brutal.TextureApi/TextureLoader.cs`; `RenderCore/TextureAsset.cs`; `RenderCore/SimpleVkTexture.cs` | graffiti | `graffiti.lib/DecalTextures.cs` (`Upload`) | OK @5348 | the exact decode/upload pair `TextureReference.DoLoad` uses. `ITexture` is not IDisposable — `Destroy()` must be called or the native decode buffer leaks. Max edge 2048, downsampled, full mip chain |
| `ShaderModuleUtils.ShaderStageFromFileExtension(string) : VkShaderStageFlags` | direct (render) | `RenderCore/ShaderModuleUtils.cs` | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | `VehiclePaintPatches.cs`; `CanopyProjectionShaders.cs` (`FromFilePrefix`) | OK | fills the skipped original's `out` param |
| `Brutal.ShaderCApi.CompileOptions` (readonly struct) | Harmony arg type (cross-asm) | `Brutal.ShaderCApi/CompileOptions.cs` (Brutal.ShaderC.dll) | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | both `.lib.csproj` references | OK | needed to declare the `FromFile` prefix signature; options pass through untouched |
| `Presets.{InputAssembly.TriangleList, Rasterization.Fill.CullNone, BlendState.BlendColorAlpha}` | direct (render) | `RenderCore.Pipelines/SimplePipelineCreator.cs` | thug-life | `ThugLifeQuadRenderer.cs` | OK | pipeline presets |
| `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | direct (render) | `RenderCore` (e.g. `OceanRenderer.cs`) | thug-life | `ThugLifeQuadRenderer.cs` | OK | reverse-Z; 4730/4733 depth-prepass didn't alter it |
| `Renderer.{Device, Allocator, Graphics, DynamicStateInfo, ViewportState}` | direct (render) | `KSA`/`RenderCore` (via `Program.GetRenderer`) | thug-life | `ThugLifeQuadRenderer.cs` | OK | compile-verified |
| `Renderer : KSADeviceContextEx` → `.Device : DeviceEx`, `.Allocator : KsaVmaAllocator`, `.Graphics : Queue` | direct (render) | `Core/Renderer.cs`; `Core/KSADeviceContextEx.cs`; `KSA/KsaVmaAllocator.cs` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs`; `PartThumbnailGenerator.cs`; `RuntimeModUnloader.cs`; `ThumbnailReadback.cs` | OK | ⚠ **`Allocator`'s declared type drags in `Brutal.Vulkan.Vma.dll`** (`KsaVmaAllocator : IVmaAllocator`, `Brutal.VulkanApi.Vma/IVmaAllocator.cs`) — a **new** game-DLL reference for this repo (`parts-now.lib.csproj`) |
| `BindlessTextureLibrary.{TextureCount : int, MaxTextures : readonly int}` | direct API | `RenderCore.Systems/BindlessTextureLibrary.cs` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs`; `Ui/StatusPanel.cs` | OK | ⚠ ships in **`Planet.Render.Core.dll`** — the second **new** game-DLL reference. The pool is `new FreeListIndexPool(maxTextures, allowResize: false)` with 1024 slots (`KSA/Program.cs`), so exhausting it is **fatal, not slow**; rule V15 holds 16 slots in reserve and refuses an over-budget load |
| `IBufferAllocator.CreateStagingPool(Queue, int, VkCommandBufferLevel = Primary)` · `Queue.Family` · `Queue.Submit(Span<VkSemaphore>, Span<VkPipelineStageFlags>, Span<CommandBuffer>, Span<VkSemaphore>, VkFence)` · `Device.{CreateCommandPool, AllocateCommandBuffer, CreateFence, WaitForFence, DestroyFence, FreeCommandBuffers, DestroyCommandPool, WaitIdle}` | GPU (Brutal.VulkanApi) | `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs`; `Brutal.VulkanApi/Queue.cs`; `Brutal.VulkanApi.Abstractions/QueueExtensions.cs`; `Brutal.VulkanApi.Abstractions/DeviceExtensions.cs`; `Brutal.VulkanApi/VkDevice.cs` | parts-now | `RuntimeModLoaderGpuStates.cs`; `PartThumbnailGenerator.cs`; `RuntimeModUnloader.cs` | OK | parts-now owns a **private transient** `VkCommandPool` and one fence per thumbnail; the whole render is submit-and-wait on the game thread (only safe from `Program.OnDrawUiFrame`). `WaitIdle` gates purge step 1. Same Brutal-bump churn surface as thug-life/doh, but all compile-checked |

### KSA.ShaderReference (asset-reference type)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ShaderReference : FileReference, IKeyed` (type) | direct API | `KSA/ShaderReference.cs` |thug-life, humble-arteest| `ThugLifeQuadRenderer.cs`; `VehiclePaintShaders.cs` (`TryResolveShaderPath`) | OK | via `ModLibrary.Get<ShaderReference>` |
| `ModPath` (on `FileReference` base, public property) | direct API | `KSA/FileReference.cs` |humble-arteest| `VehiclePaintShaders.cs` (`TryResolveShaderPath`)| OK | resolve on-disk shader path (humble: pre-flight anchor check only) |

### Brutal.* (game-shipped; risk-bearing only)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ConsoleWindow.IsOpen : bool => _show` | direct API | `Brutal.ImGuiApi.Abstractions/ConsoleWindow.cs` | HotkeyGuard (→ all mods) | `ksa-abstractions.lib/HotkeyGuard.cs` | OK | guard bypassed while dev console open |
| `ImGui.GetIO().WantTextInput` | direct API | `Brutal.ImGuiApi/*` | HotkeyGuard (→ all mods) | `HotkeyGuard.cs` | OK | detects ImGui text-input focus; watch on Brutal bumps |
| `ImGuiStyle.Colors : float4_60` / `ImGuiStylePtr` (60-color array + 72 style members) | direct API | `Brutal.ImGuiApi/ImGuiStyle.cs`; `ImGuiStylePtr.cs` | skittles, con-man | `skittles.lib/ThemeDefinition.cs`; `ConManSubmod.cs` | OK | hard-codes 60 colors + fixed style-var list; a Brutal slot/member add is silently dropped — watch every Brutal bump |
| `ImGuiCol` (enum, 60 slots `Text`…`ModalWindowDimBg` + `COUNT`) | enum | `Brutal.ImGuiApi/ImGuiCol.cs` | skittles | `skittles.lib/ThemeSerializer.cs` | OK | hard-coded `60` count must match |
| `VkUtils.StageAndUploadToBuffer` / `BufferEx.VkBuffer` / `IVulkanContext.Device.CreateStagingPool` / `ByteSize.Of<T>()` | GPU write (Brutal.VulkanApi) | `Brutal.VulkanApi(.Abstractions)` | doh, humble-arteest (KittenColor) | `MaterialSystemAccessor.cs`; `KittenColor.cs` | OK | GPU material-buffer write; rev-4729 Brutal bump is the churn surface (build passes). The `Span<float4>`→bytes conversion now uses the BCL `MemoryMarshal.AsBytes`; the `CommunityToolkit.HighPerformance` game-DLL reference it used to need is **retired** — that DLL is not in `ksa-game-assemblies/current/dll/` (`copy-ksa.ts` does not copy it), so the reference broke any build pointed at that tree |
| `SimpleVkTexture` / `VkUtils.UploadBufferToImage` + pipeline/descriptor primitives (`DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer`) | GPU (Brutal.VulkanApi/RenderCore) | `Brutal.VulkanApi`, `RenderCore`, `Core` | thug-life | `ThugLifeTextureFactory.cs`; `ThugLifeQuadRenderer.cs` | OK | custom Vulkan pipeline; highest churn surface (rev 4729); 4750 build passes |

### KSA planetary rings data + renderer (rocky-mcrock-face)
> Full detail in [`rings.md`](rings.md). No Harmony patches — a public-data swap + the game's own renderer rebuild. Rows marked *invariant* are relied upon, not called.

| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5348 | Notes |
|---|---|---|---|---|---|---|
| `AstronomicalTemplate.RingsReference : PlanetaryRingsReference?` (public field; via `Celestial.BodyTemplate`) | direct API | `KSA/AstronomicalTemplate.cs`; `KSA/Celestial.cs` | rocky-mcrock-face | `RingSwapController.cs` (`RefreshBodies`) | OK | how a ringed body is found |
| `PlanetaryRingsReference.{Texture, ControlTexture, RingObjects}` · `RingObjectsReference.{Lods, MaterialReference, Size, Thickness, RenderDistance, Density, NumLods}` · `RingLodReference.{MinScreenSizePixels, MeshFileReference}` · `PbrMaterialReference.{DiffuseReference, NormalReference, PBRMap}` (all public fields) | direct API | `KSA/PlanetaryRingsReference.cs`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs`; `KSA/PbrMaterialReference.cs` | rocky-mcrock-face | `RingSwapController.cs` (`Apply`/`Restore`/`TakeSnapshot`) | OK | the whole swap surface; ControlTexture is snapshotted but deliberately never swapped (CPU-sampled as RGBA8 — see `rings.md` #5) |
| `MeshFileReference.{Get(), Mesh : MeshReference?}` — **the mesh swap slot** | direct API | `KSA/MeshFileReference.cs` | rocky-mcrock-face | `RingSwapController.cs` | OK | renderer reads `Lods[i].MeshFileReference.Get().Mesh` at data build |
| `MeshReference` public surface: `Id/Simple/Interleaved/PrimitiveCount/BoundingSphereRadius` fields, `HostPrimitives/DevicePrimitives` get-only props, `DeviceMesh => DevicePrimitives[0]`, `Bind(Renderer, StagingPool)`, `Dispose()` | direct API | `KSA/MeshReference.cs` | rocky-mcrock-face | `RingMeshFactory.cs`, `RingAssetCatalog.cs` | OK | clone-and-convert path for interleaved subpart meshes; multi-primitive shape is new @5348 |
| `TextureReference.{Id, BindlessHandle}` · `TexturePowerReference` (type filter for normal maps) | direct API | `KSA/TextureReference.cs`; `KSA/TexturePowerReference.cs` | rocky-mcrock-face | `RingAssetCatalog.cs` | OK | `BindlessHandle == 0` ⇒ excluded from the pickers |
| `Program.{Instance, GetRenderer(), RebuildRenderer(bool = false)}` · `GameSettings.{ShowRings(), ShowRingMeshes()}` · `Universe.CurrentSystem.All.OfType<Celestial>()` | direct API | `KSA/Program.cs`; `KSA/GameSettings.cs`; `KSA/Universe.cs` | rocky-mcrock-face | `RingSwapController.cs`, `RockyMcRockFaceSubmod.Ui.cs` | OK | `RebuildRenderer` is the apply mechanism — the same path the game's graphics settings use |
| `PlanetaryRingsRenderData` ctor bakes the reference tree (`LodProperties[i].Y = DeviceMesh.IndexCount`, `MeshCullingRadius`, bindless material ids) · `PlanetaryRingsRenderer.{PopulatePlanets, RenderMeshes}` draw `MeshLods[i].DeviceMesh` only (primitive 0) | **invariant** | `KSA.Rendering.Rings.Rendering/PlanetaryRingsRenderData.cs`; `PlanetaryRingsRenderer.cs` | rocky-mcrock-face | design keystone (`rings.md` #1-#3) | OK | if ring data stops being rebuilt from the reference tree, Apply silently stops working |

### KSA planetary rings — runtime definition (bloomin-onion)
> Full detail in [`rings.md`](rings.md) (bloomin-onion section, rows B1-B10). No Harmony patches — constructs a `PlanetaryRingsReference` tree, assigns it to the body template, refreshes the transparencies body list and runs the game's own renderer rebuild. Reuses rocky-mcrock-face's catalog/mesh rows above.

| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5348 | Notes |
|---|---|---|---|---|---|---|
| `PlanetaryRingsReference` / `PlanetaryRingsVolumeReference` / `RingRaymarchingStepReference` / `RingObjectsReference` / `RingLodReference` / `MeshFileReference.Mesh` / `PbrMaterialReference` — **all public fields, constructed from scratch** (`IsValid()` deliberately not used: `DistanceReference.IsValid` demands > 100 km) | direct API | `KSA/PlanetaryRingsReference.cs`; `KSA/PlanetaryRingsVolumeReference.cs`; `KSA/RingRaymarchingStepReference.cs`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs`; `KSA/MeshFileReference.cs`; `KSA/PbrMaterialReference.cs` | bloomin-onion | `RingReferenceBuilder.cs` (`Build`), `RingDefinitionSerializer.cs` (`FromReference`) | OK | a new required field on any of these classes silently defaults — see `rings.md` B-narrative #1 |
| `DistanceReference(double, DistanceUnit)` · `RadianReference(double)` + `ToDegrees()` · `DoubleReference.FromValue` · `BoolReference(bool)` · `MathEx.{ToDeviationAngle, ToCompassAngle}(double)` · `OrbitDefinitionFrame` | direct API | `KSA/DistanceReference.cs`; `KSA/RadianReference.cs`; `KSA/DoubleReference.cs`; `KSA/BoolReference.cs`; `KSA/MathEx.cs` | bloomin-onion | `RingReferenceBuilder.cs` | OK | value wrappers the XML loader would create; angle normalization mirrors `PlanetaryRingsReference.OnDataLoad` |
| `AstronomicalTemplate.RingsReference` (public field, **written**) · `Celestial.{BodyTemplate, MeanRadius, Parent}` | direct API | `KSA/AstronomicalTemplate.cs`; `KSA/Celestial.cs` | bloomin-onion | `RingDefinitionController.cs` | OK | original reference snapshotted per template for Remove |
| `PlanetTransparenciesRenderer.PopulatePlanets() : bool` (public) | direct API | `KSA/PlanetTransparenciesRenderer.cs` | bloomin-onion | `RingRendererRebuilder.cs` | OK | re-derives which bodies have rings; its result must be written to `_anyRings` (watchlist) |
| `TextureReference` (subclassed): `Category, Width, Height, BindlessHandle, Bind(Renderer, StagingPool)` (virtual), `Dispose(Device)`, `SetHash()` · `RenderCore.TextureAsset(ITexture, string)` · `GenericTexture.Defaults.RGBA8UNorm(int2)` + `.Data` · `TextureFormat.Descriptor().{IsBlockCompressed, BlockSizeInBytes}` | direct API | `KSA/TextureReference.cs`; `RenderCore/TextureAsset.cs`; `Brutal.TextureApi.Abstractions/GenericTexture.cs` | bloomin-onion | `PaintedTextureReference.cs`, `RingReferenceBuilder.cs` (`IsCpuSampleable`) | OK | painted band/control strips are real `TextureReference`s bound through the game's own path |
| `PlanetTransparenciesRenderer.RebuildFrameResources` gating (`!_ringRendererCreated && _anyRings` → `CreateRingsRenderer`) · `PlanetaryRingsRenderer.PopulatePlanets` ctor-only · `PlanetRenderer` per-frame `RingsReference` read (ring shadow) · `AtmosphereRenderer.AssignPlanetSlots` keyed on `AtmosphericBody` only | **invariant** | `KSA/PlanetTransparenciesRenderer.cs`; `PlanetaryRingsRenderer.cs`; `KSA/PlanetRenderer.cs`; `KSA/AtmosphereRenderer.cs` | bloomin-onion | design keystone (`rings.md` B-narrative #1, #6, #10) | OK | a ring-only body joining the transparencies list must stay harmless to the atmosphere renderer |

---

## 4. String-based reflection watchlist (highest silent-break risk)

NOT compile-checked — a game rename breaks these at runtime with no build error. Re-verify each name
on every game update FIRST.

| Type.Member (string) | Mod(s) | Why string-based | 5348 |
|---|---|---|---|
| `Camera.OnFrame` (`OrbitController`/`FlyController.OnFrame`) | camera-controller-override | `AccessTools.Method(…, "OnFrame")` | OK |
| ~~`Controller.___Transform`~~ (field injector) | ~~camera-controller-override~~ | ~~Harmony field-injection by name~~ | **RETIRED @5261** — the prefix now reads the public `__instance.Camera` (`CameraControllerOverridePatches.cs`), so the injector is gone and this can no longer fail at `Apply` time. ((no `Transform` member exists on `KSA.Controller` in either tree), but `Camera` is the field that actually carries the view.) |
| `Camera._fovRadians` | glass | `AccessTools.Field` private field by name | OK (single most-important glass check) |
| `Camera.ChangeFieldOfView` / `Camera.UpdateProjection` | glass | `AccessTools.Method` by name | OK |
| `Vehicle.GetWorldMatrix` / `Vehicle.UpdateRenderData` | i-feel-seen | `AccessTools.Method(typeof(Vehicle), "…")` | OK |
| `VolumetricExhaustTemplate.References` (internal static field) | pyro | `AccessTools.Field(…, "References")` → `SerializedCollection<T>.GetList()` (`PlumeTemplates.cs`) | OK @5348 — soft: falls back to the stock 7 ids via public `Get(id)` |
| `VolumetricExhaustInstance._shaderData` (private struct field) | pyro | `AccessTools.FieldRefAccess<…, ExhaustInstance>("_shaderData")` (`PlumeEmitter.cs`) | OK @5348 — soft: per-plume look overrides disable with a UI notice |
| `KittenEva` (type name) / `KittenEva._renderable` → `KittenRenderable._characterAvatar` → `CharacterAvatar.Core` → `CharacterCore.Scale` | garrys-torch, doh, kitten-animations | `GetType().Name` compare + private field chain | OK |
| `ChuteRenderable._renderable` → `AnimatedRenderable.MaterialIndices` | free-fallin | private/protected field chain used immediately before `ChuteRenderable.Draw`; writes material slot zero and weakly tracks the renderable for restore | OK @5402 — new game surface and new consumer; both exact names are load-bearing |
| `CharacterAvatar.Core.{CharacterModel,Fur,Attachments}…MaterialIndices` (AnimatedRenderable/CatFurRenderable/StaticMeshRenderable) | doh | private field-path + `protected int[]` | OK |
| `CatExpressionAnim._expressionPose` | kitten-animations | private field by name (cache bust) | OK |
| `KittenRenderable._ground{Idle,Walk,Run}Anim`, `_ladderAnim`, `_jumpIntroAnim`, `_flailAnim`, `_jumpLandAnim`, `_moon{Walk,Run}Anim`, `_swimAnim`, `_swimIdleAnim`, `_seatedIdleAnim`, `_seatedIdleActionAnims`, `_walk/_run/_swimPairSampler`, `_blendSampler` | kitten-animations | 17 private fields by name — the only route to the ground animation set | OK — degrades per field into a UI warning, never a crash |
| `KittenRenderable._catPersonalityExpressionAnim / _catExpressionAnim / _catEyeAnim / _catEarAnim` | kitten-animations | private fields by name; distinguishes the two same-typed expression processors | OK |
| `AnimatedRenderable.UpdateAnimation` | kitten-animations | `AccessTools.Method` by name (Harmony prefix) | OK — loud `MissingMethodException` at `Apply` if renamed |
| `LightModule.TemplateData` (`"KSA.LightModule+TemplateData"`) + `PartTemplate.Components` + `TemplateData.Intensity`/`FloatReference.Value` + `ColorRgbReference.{R,G,B,OnDataLoad}` | zippo, its-so-shiny (via ksa-lights.lib) | hard-coded type/field/method names | OK |
| `GaugeCanvas._canvases/_enabled/_customOffset/_customScale/_windowPosition/_windowSize/_windowTitle` | con-man | 7 private fields by name (IsValid canary) | OK — all 7 still declared **on `GaugeCanvas` itself** (not lifted to `GaugeBase`, which would break `GetField`). `_windowTitle` went `private`→`protected`; still `NonPublic\|Instance`, so it resolves. **Behavioral risk instead:** revs 4919/4940/4959/5003 rebuilt the gauge/HUD system around con-man, rev 5201 added context-visibility gating, and **rev 5293 added a global Hud Scale applied after `_customScale`** (see historical evidence) |
| `Program.Instance`/`MaterialSystem`/`SuperMeshRenderSystem`/`CharacterRenderSystem` + `GpuObjectSystem.{BigBuffer,DeviceCtx,CreateObject}` + `AssetManager.{AssetMap,GetOrLoad}` + `GpuObjectAssetRef.Handle` + `GpuTextureSystem.*` + `Pbr/Character*Reference.*` | doh, humble-arteest (KittenColor) | deep render-system reflection bridge | OK |
| `ModLibrary.AllParts`/`AllCharacters` + `SerializedCollection.{GetList,Find}` | doh | internal static fields/methods by name | OK |
| `ModLibrary.AllParts` | parts-now | `GetField("AllParts", Static\|NonPublic\|Public)` in `parts-now.lib/Runtime/GameRegistry.cs` — the **only** file in parts-now allowed to reflect | OK |
| `ModLibrary.AllMeshes` | parts-now, rocky-mcrock-face | `GetField("AllMeshes")` — `GameRegistry.cs`; `ksa-rings.lib/RingAssetCatalog.cs` (`Collection<T>`) | OK |
| `ModLibrary.AllFiles` | parts-now, rocky-mcrock-face | `GetField("AllFiles")` — `GameRegistry.cs`; `ksa-rings.lib/RingAssetCatalog.cs` (`Collection<T>`) | OK |
| `ModLibrary.AllGltfs` | rocky-mcrock-face | `GetField("AllGltfs")` — `RingAssetCatalog.cs` (`Collection<T>`); source of character/MMU/helmet meshes for the ring picker. Degrades to those entries missing from the list | OK |
| `MeshReference.<HostPrimitives>k__BackingField` (auto-prop backing field) | rocky-mcrock-face (bloomin-onion via `RingMeshFactory`) | `GetField` by name in `RingMeshFactory.cs` — shares CPU geometry into a converted clone. Null-checked: a miss fails Apply with a UI error, never crashes | OK |
| `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated}` | rocky-mcrock-face, bloomin-onion | private-field access in `RingSwapController` — **load-bearing for Apply**: the existing rings renderer is disposed (public `Dispose()` after `Device.WaitIdle`) and `_ringRendererCreated` cleared so `RebuildFrameResources` takes its `CreateRingsRenderer` branch and re-reads the ring data (`PopulatePlanets` is ctor-only). A rename degrades to a frame-resources-only rebuild: Apply hitches but changes nothing (immediately user-visible, not a crash) | OK |
| `PlanetTransparenciesRenderer._anyRings` (private bool) | bloomin-onion | `ReflectionHelpers.SetFieldValue` in `RingRendererRebuilder.Rebuild` after the public `PopulatePlanets()` — **load-bearing for adding rings to a system that has none**: `RebuildFrameResources` only creates the rings renderer when `_anyRings`. A rename is a silent no-op: Apply reports success but nothing renders in ringless systems (Saturn systems unaffected). Immediately user-visible, never a crash | OK |
| `TextureReference.<TextureAsset>k__BackingField` (private-set auto-prop) | bloomin-onion | `GetField` by name in `PaintedTextureReference` — seeds the in-memory asset the game's own `Bind` reads. Null-checked: a miss disables Painted band mode (`IsSupported == false`, UI falls back to Texture mode with a message) | OK |
| `StaticCelestial._distantRenderer` → `DistantSphereRenderer._data` (+ struct fields `UseRingShadows, RingInnerRadius, RingOuterRadius, RingTextureId, SamplerClampId`) | bloomin-onion | base-type private field walk + `GetField` by name in `RingRendererRebuilder.SyncDistantSphereShadow` — **cosmetic only** (far-away sphere ring shadow); every step null-tolerant inside try/catch | OK |
| `ModLibrary.AllMaterials` | parts-now | `GetField("AllMaterials")` — `GameRegistry.cs` | OK |
| `ModLibrary.AllPartGameDataReferences` | parts-now | `GetField("AllPartGameDataReferences")` — `GameRegistry.cs`. **Note the plural `References` suffix**, unlike its five siblings | OK |
| `ModLibrary.AllEditorTagDefinitions` | parts-now | `GetField("AllEditorTagDefinitions")` — `GameRegistry.cs`; feeds validation rule V7 | OK |
| `SerializedCollection<T>._collection` (private `ConcurrentDictionary<KeyHash,T>`) | parts-now | `GetField("_collection", Instance\|NonPublic)` per closed generic — `GameRegistry.cs`, used by `Unregister` `:154-165`. **`SerializedCollection<T>` has no removal API, so unload/reload exist only because of this** (see U4) | OK |
| `VehicleEditor._editorTagLookup` (private static `Dictionary<uint,string>`) | parts-now | `GetField("_editorTagLookup", Static\|NonPublic)` — `GameRegistry.cs`. **Degraded, not fatal**: V7 falls back to the six built-in tags + `AllEditorTagDefinitions` ids | OK |
| `VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale` / `UpdateScaleGizmo` / `QuantizeScale` / `ForEachPartWithSymmetry` | dont-stifle-me | `AccessTools.Method(typeof(VehicleEditor), "…")` — `EditorScalePatches.cs`; the first three are Harmony targets, the last two become delegates. Any miss throws at `Apply()` (logged, mod shows a red notice; stock behavior remains). Four of the five **first appeared in 5348** | OK |
| `VehicleEditor.DrawParachuteSection` | dont-stifle-me | `AccessTools.Method(typeof(VehicleEditor), "DrawParachuteSection")` — `EditorValueLimitPatches.cs`; a miss throws at `Apply()` and disables only the configurable editor-limit patch group in unscience | OK @5402 |
| ~~`Part._matrixAsmb` / `Part._matrixAsmb2Parent`~~ |  | ~~private fields by name (cache safety)~~ | **RETIRED @5117** — replaced by the public `Part.ResetCachedPosMatrixValues()`. Rev 5112 changed the uncached sentinel from identity to NaN, which turned the old identity-write from a no-op into a transform-corrupting write. **Removing a watchlist entry is the best outcome available here** — this row can no longer break silently |
| `PartTree.RecomputeStaticMass` | kitchen-sink | HarmonyLib `Traverse.Method("RecomputeStaticMass")` | OK |
| `GameSettings.OnKeyAll` | all mods (HotkeyGuard) | `AccessTools.Method(…, nameof(OnKeyAll))` | OK |
| `Program.OnDrawUiConsole` (private) | unscience (HiddenUiFrameHook) | `AccessTools.Method(typeof(Program), "OnDrawUiConsole")` — `HiddenUiFrameHook.cs`. Miss throws at `Patch()` → logged/skipped; symptom is mods freezing on F2 again. Must remain an every-frame call *after* the `if (DrawUI)` block and *before* `ImGui.Render()` (`Program.cs` @5348) | OK |
| `Universe.ExecuteNextVehicleSolvers` | eternal-flame, kitchen-sink, kiwis-marbles | `AccessTools.Method` by name (no param array) | OK (single overload) |

---

## 5. Shaders & game assets subtable

| Asset / shader | Kind | Referenced as | Content path (NEW) | Consumer | 5348 |
|---|---|---|---|---|---|
| `UnlitMesh.vert` / `UnlitMesh.frag` | shader | `ModLibrary.Get<ShaderReference>("UnlitMeshVert"/"UnlitMeshFrag")` | `Core/DefaultAssets.xml:66,67` → `Core/Shaders/Mesh/UnlitMesh.*` | thug-life | OK (**byte-identical 4750→5018**; also untouched by 4693/4745) |
| `GridFrag` (path anchor only) + `Common/Camera.glsl` / `Common/TextureSet.glsl` (headers, `#include`d) | shader include root + headers | `ModLibrary.Get<ShaderReference>("GridFrag").ModPath` → its **directory** is the `#include` root for graffiti's two runtime-compiled decal shaders | `Content/Core/DefaultAssets.xml:373` → `Core/Shaders/Grid.frag`; `Core/Shaders/Common/*.glsl` | graffiti | OK @5348 — a `global.camera`/`global.lighting` struct or `SAMPLE_TEXTURE`/`SET_TEXTURE` macro change fails at shaderc compile (loud console line; decals self-disable) |
| `MeshIndirect.frag` + `MeshIndirectRaytraced.frag` (paint injection) | shader text-edit (in memory, via the `FromFile` prefix) | matched by **file name**; anchor = first `vec3 sampledColor …;` line; requires `inStateFlags` varying and `gammaToLinear` (`Common/Shared.glsl:203`) | `Content/Core/Shaders/Mesh/MeshIndirect.frag:114`; `MeshIndirectRaytraced.frag:156` | humble-arteest (VehiclePaint) | OK (rebuilt for 5018) — if the anchor moves, `Enable` fails with a UI message and rendering stays stock |
| `MeshIndirect.frag` (Temperature LUT, `#ifdef ENABLE_TEMPERATURE`) | shader (read-only, no edit) | — | `Content/Core/Shaders/Mesh/MeshIndirect.frag:214-219` | humble-arteest (EngineEmissive) | OK (MOVED from `DynamicMeshIndirect.frag` rev 4693; feature still works) |
| `Model.vert` + `Model_Skinned.vert` + `ModelPbr.frag` → `TextureSet.glsl` / `MaterialSet.glsl` | shader text-edit (in memory, via the `FromFile` prefix) | exact declaration/assignment/call anchors; added location-3 `vec2`; Full Canopy marker in `Material.extraData.w` | `Content/Core/Shaders/Mesh/Model{,_Skinned}.vert`; `Mesh/ModelPbr.frag`; `Common/{TextureSet,MaterialSet}.glsl` | free-fallin; existing read-only albedo effect also used by doh and humble-arteest (KittenColor) | OK @5402 — transformed shaders compile to valid SPIR-V; static vertex supplies pass-through varying, skinned vertex derives bind-pose X/Z projection, fragment substitutes only marked albedo sampling |
| `ParachuteCanopyGlb` + `ParachuteCanopy_Material` (`Diffuse`, `Normal`, `AoRoughMetal`) | skinned GLTF + PBR material/texture assets | exact ids from `ChuteRenderable` / `ModLibrary.Get<PbrMaterialReference>` | `Content/Core/ParachuteAssets.xml:4,23-27`; `Core/Textures/ParachuteCanopy_{Diffuse,Normal,PBR}.ktx2` | free-fallin | OK @5402 — runtime albedo is BC7; center-decal mode reopens `TextureReference.ModPath` and explicitly transcodes the source KTX2 to RGBA8 |
| `DynamicMeshIndirect.vert/.frag`, `ModelEye.frag`, `ModelGlass.frag` | shader (removed) | (design assumption only) | — | humble-arteest (narrative), its-so-shiny GlassModule (C# only) | n/a (removed 4693/4745; `ModelTranslucent.frag` new 4747 — not referenced by id) |
| Exhaust templates `EngineALarge`, `EngineAMed`, `EngineACompact`, `EngineAVernier`, `EngineATurbine`, `RCS`, `MmuRcsVac` | `VolumetricExhaustTemplate` ids | `VolumetricExhaustTemplate.Get(id)` — **fallback list only** (`PlumeTemplates.cs`); normally enumerated live from `References` | `Core/ExhaustAssets.xml:3,307,650,993,1331,1670,2009` | pyro | OK @5348 (`EngineALarge` is the create-form default) |
| `LightPart` template (`<PowerConsumer LightSwitch="true">`) | part template | `ModLibrary.Get<PartTemplate>("LightPart")` | `Core/PartAssets.xml:19`; `Core/CoreElectricalAGameData.xml:221` | its-so-shiny | OK |
| `"KittenBackPackPart"` | part template | `ModLibrary.AllParts.Find(KeyHash)` | `Core/*` | doh | OK |
| Characters (e.g. `"Calico"`) | character | `ModLibrary.AllCharacters.GetList()` | `Core/*` | doh | OK (no hard-coded id) |
| Reaction `"MMH_NTO"` (was combustion process `"MMH_NTO_1.6"`) | substance | `SubstanceLibrary.TryGetReaction(KeyHash)` → `MixtureReaction.AtMixtureRatio(DefaultMixtureRatio).ReactantMix` | `Core/Reactions.xml` (`<MixtureReaction Id="MMH_NTO">`, `DefaultMixtureRatio` 1.65) | doh | **CHANGED** (5018 — mixture ratio is no longer part of the id; old id resolves to nothing) |
| Fur texture `"FurNoise"` | texture (indirect) | `CharacterRenderResources.FurTexture.BindlessHandle` | `Core/*` | doh | OK |
| `MusicPlayList "SabotageMusic"` | sound | `ModLibrary.Get<MusicPlayList>("SabotageMusic")` | not stock (`Core/Sounds.xml` has `EarthSOIMusic`,…) | byo-music | n/a (placeholder; null-guarded; never stock in 4680/4750) |

> **parts-now references no game asset by id and ships none.** It *consumes* the game's own
> `<Assets>` bundle schema through the game's own serializer (`XmlHelper.Serializers`), and *writes*
> a mod folder (`mod.toml` + `<modId>-{assets,part,gamedata}.xml`) under
> `ModLibrary.LocalModsFolderPath` plus a `ModEntry` in `<user>/manifest.toml`. The only XML names it
> hard-codes are the ones its validation rules match by string: `<Substance>`, `<MixtureReaction>`,
> `<FixedReaction>`, `<ThermalReaction>`, `<GrainGeometry>`, `<Situation>`, `<EditorTagDef>` (V8,
> rejected as out of scope) and `<Reaction Id>`, `<Grain Id>`, `<VolumetricExhaust Id>`,
> `<SoundEvent SoundId>`, `<Mesh Id>`, `<EditorTag Value>`, `Path=` (V6/V7/V10/V11 reference checks).

---

## Historical evidence

See [dated integration and upgrade reference](history/game-integration-surface.md) for prior build comparisons and retired integrations. That archive does not define current ownership or verification status.

## Runtime ownership additions

- `IWorkspaceFeature.ConfigureRuntime(FeatureRuntime)`, `ReleaseLiveState()` and `UpdateAfterGui(double)` are the shared lifecycle interface. Feature groups own separate Harmony IDs; the host retains HotkeyGuard/menu/hidden-HUD only. EternalFlamePatches moved into eternal-flame.lib; IvaForceRender moved into kitchen-sink.lib.
- `OwnedGpuAssets`: protected `AssetManager<T>.AssetMap`, exact `ConcurrentDictionary<AssetName,T>` removal, `LoadedAssetRef.Dispose`, `GpuObjectAssetRef.Handle`, `GpuTextureAssetRef`, `Device.WaitIdle`. Used by Free Fallin and DOH for allocations they create.
- `FeatureUi`: `ImGui.Internal.ErrorRecoveryStoreState`, `ErrorRecoveryTryToRecoverState`, native `ImGuiErrorRecoveryState`, and temporary `ImGuiIO.ConfigErrorRecoveryEnableAssert` suppression during recovery.
- `LightStateLease`: `LightModule.TemplateData.ColorRgb.R/G/B/IndexedColor`, `ColorRgbReference.OnDataLoad`, `Intensity.Value`, `PowerConsumer.LightIsActive`. Ownership coordination is in ksa-lights; its generic reference-counted restoration algorithm is in contracts.
- Garry captures/restores `Part.Scale` and reflected `CharacterAvatar.Core.Scale`; Glass captures `Camera.GetFieldOfView` before writing/restoring through `SetFieldOfView`; kitten animation captures persistent ear/eye/personality fields and detaches its `AnimProcessors` entry; Con Man captures live gauge enabled/offset/scale fields before applying.
- Parts Now `SharedMeshBuffers`: `DeviceMeshInterleaved.Shared.VertexAllocation/IndexAllocation`, `BufferEx`, `IBufferAllocator`, `RaytraceAllocator`, `CreateStagingPool`, `CommandBuffer.CopyBuffer`, submission/fence wait and `Program.RaytracingRenderer`. The latter blocks relocation because its BLAS and SubPartRefs cache addresses beyond renderer Rebuild.

These integrations compile against 5402; no native UI/gameplay/GPU acceptance is inferred from compilation.
