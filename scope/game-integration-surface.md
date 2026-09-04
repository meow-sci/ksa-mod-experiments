# Game Integration Surface — master index (unscience KSA mod suite)

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
  no C# change (mesh-deform is broken this way; humble-arteest Vehicle Paint was rebuilt for 5018 and
  now fails loudly instead of silently if its anchor moves).
- **Re-verify the 🔶 standing invariants.** These are facts about the game that no symbol lookup and
  no compile can check, and each one fails silently:
  - `PerInstanceData.StateBitFlag` bits **11..31** are still unused by KSA (humble-arteest Vehicle
    Paint) — see `KSA.PartModel` below and [`character-and-materials.md`](character-and-materials.md).
  - **`[StarMapAllModsLoaded]` still fires before `ModLibrary.Bind()`** (parts-now). StarMap
    implements it as a Harmony postfix on `ModLibrary.LoadAll()` (`KSA/Program.cs:956`), and
    `ModLibrary.Bind(_renderer)` at `KSA/Program.cs:942` (was `:985` at 5261) is where the shared interleaved mesh buffers
    are allocated **once**. parts-now reserves headroom in between. If that order ever changes, every
    runtime-loaded mesh writes past the end of the shared vertex buffer, with no error anywhere. See
    `KSA.DeviceMeshInterleaved` below and [`part-editor-and-robotics.md`](part-editor-and-robotics.md)
    → parts-now **U1**. Six further parts-now invariants (**U2**–**U7**) live in that same section.
- **Watch the Harmony keystones** that fan out to many mods: `Universe.ExecuteNextVehicleSolvers`,
  `GameSettings.OnKeyAll` (HotkeyGuard), the three `*Module.UpdateRenderData` render prefixes,
  `PartModel.AddInstance`, `PartModelRenderer.UpdateRenderData`, and the `VehicleProvider` enumeration
  chain — one change here breaks several mods at once.

Status legend: **OK** unchanged 4750→5018 · **CHANGED** signature/shape changed · **BROKEN** non-functional
against 5018 (compile or silent runtime) · **ADDITIVE** new in 5018, not yet consumed.

---

## 3. Master table — by game type

> "Used by" lists every consuming mod (merged). Members reached through `ksa-abstractions.lib` helpers
> (`VehicleProvider`/`CelestialProvider`/`SimTimeProvider`/`PartHelpers`/`HotkeyGuard`/`IvaForceRender`)
> note the helper. Nested types are rows under their owner's subheader.

### KSA.AnimatedRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SetAnimation(IAnimation, float blend=0.2f)` | direct API | `KSA/AnimatedRenderable.cs:113` | kitten-animations | `kitten-animations.lib/KittenAnimationDriver.cs` | OK | forced-clip playback; no-op when the clip is already current (`BoneAnimRuntime.SetAnimation:92`) so safe per frame |
| `PlayAnimation(IAnimation, float blend=0.2f)` | direct API | `KSA/AnimatedRenderable.cs:118` | kitten-animations | `KittenAnimationDriver.cs` | OK | restart-from-frame-0 for the forced clip |
| `UpdateAnimation(double dt)` | **Harmony prefix** `(AnimatedRenderable __instance, ref double dt)` | `KSA/AnimatedRenderable.cs:123` | kitten-animations | `KittenAnimationPatches.cs` | OK | ⚠️ hot path — the only point in the frame where an animation override survives `KittenRenderable.UpdateRenderData`. Also scales `dt` for the playback-rate control |
| `FreezeAnimation : bool` | direct API | `KSA/AnimatedRenderable.cs:53` | kitten-animations | `KittenAnimationDriver.cs` | OK | freeze/pause the forced clip; released back to the game on override off |
| `AnimProcessors : List<IAnimProcessor>` | direct API | `KSA/AnimatedRenderable.cs:47` | kitten-animations | `KittenExpressionController.cs` | OK | mod **appends** its own `CatExpressionAnim` here (and removes it on unbind) |
| `MaterialIndices : protected int[]` | reflection-field | `KSA/AnimatedRenderable.cs:34` | doh, free-fallin | `doh.lib/Spawning/KittenSpawner.cs:388-408`; `free-fallin.lib/FreeFallinPatches.cs` | OK @5402 | in-place handle swap; free-fallin writes canopy material slot zero immediately before each chute draw and restores observed renderables on disable/unload |

### KSA.AssetBundle
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AssetBundle` (`[XmlRoot("Assets")]`) + `Assets : List<SerializedId>` (field) | direct API | `KSA/AssetBundle.cs:8,67` | parts-now | `parts-now.lib/Runtime/BundleParser.cs:102`; `BundleParserQueries.cs:38` | OK | deserialized **without** side effects for validation; classification helpers test most-derived type first |
| `OnDataLoad(Mod) : override void` | direct API | `KSA/AssetBundle.cs:74` | parts-now | `Runtime/RuntimeModLoaderStates.cs:200` | OK | the single call that registers a bundle's templates/materials/loaders into `ModLibrary` |

### KSA.AssetManager<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AssetMap : protected ConcurrentDictionary<AssetName,T>` | reflection-field (hierarchy) | `KSA/AssetManager.cs:11` | doh, humble-arteest | `doh.lib/Materials/MaterialSystemAccessor.cs:67`; `humble-arteest.lib/KittenColor.cs:55-73` | OK | walks base types |
| `GetOrLoad(AssetName) : T` | reflection-method | `KSA/AssetManager.cs:49` | doh | `MaterialSystemAccessor.cs:81,151` | OK | returns `GpuObjectAssetRef` |

### KSA.Asteroid / KSA.Comet
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Asteroid : MinorBody` / `Comet : MinorBody` (type, `is not Asteroid and not Comet`) | direct API (typecheck) | `KSA/Asteroid.cs:3`, `KSA/Comet.cs:3` | marque | `marque.lib/MarqueLib.cs:56,114,157-159` | OK | "Planetoids" filter |

### KSA.Astronomical
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Id : virtual string { get; protected set; }` (via `IObjectId`) | direct API | `KSA/Astronomical.cs:85` | VehicleProvider (→ most mods), eternal-flame, garrys-torch, i-feel-seen, kitchen-sink, marque, steely-eyed, unladen-swallow | `ksa-abstractions.lib/VehicleProvider.cs:22`; `eternal-flame.lib/EternalFlameLib.cs:74` | OK | `Vehicle.Id` resolves here (not declared on `Vehicle`) |

### KSA.AtmosphereReference / KSA.PhysicalAtmosphereReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `IParentBody.GetAtmosphereReference() : AtmosphereReference?` | direct API | `KSA/Vehicle.cs:2221` (call site) | steely-eyed | `…lib/Telemetry/VehicleTelemetry.cs:50` | OK | null on airless bodies |
| `PhysicalAtmosphereReference.Physical.Height : DistanceReference` | direct API | `KSA/PhysicalAtmosphereReference.cs:23` | steely-eyed | `VehicleTelemetry.cs:52` | OK | |
| `GetAtmosphericPressureAtAltitude(double) : double` | direct API | `KSA/PhysicalAtmosphereReference.cs:80` | steely-eyed | `VehicleTelemetry.cs:101` | OK | in try/catch |
| `GetAtmosphericDensityAtAltitude(double) : double` | direct API | `KSA/PhysicalAtmosphereReference.cs:85` | steely-eyed | `VehicleTelemetry.cs:102` | OK | |
| `PhysicalAtmosphereReference.GetAtmosphericPressure(Camera) : static double` | direct API | `KSA/PhysicalAtmosphereReference.cs:50` | pyro | `pyro.lib/PlumePhysics.cs:113` | OK @5348 | returns **atm**; pyro converts to Pa for `PlumeData` |

### KSA.Battery
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Battery` (type, `Get<Battery>()`) | direct API | `KSA/Battery.cs:7` | its-so-shiny | `its-so-shiny.lib/ShinyGridBuilder.cs:205,211` | OK | battery anchors for power partitioning |
| `Refill(ref BatteryState state) : void` | direct API | `KSA/Battery.cs:59` | eternal-flame | `eternal-flame.lib/EternalFlameLib.cs:137` | OK | insulates mod from rev-4681 electrical refactor (body unchanged) |
| `MaximumCapacity : required Joules` | direct API (indirect) | `KSA/Battery.cs:21` | eternal-flame | via `Refill` | OK | read only inside `Refill`; mod never names `Joules` |

### KSA.Camera
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `_fovRadians : private float` | reflection-field (PRIVATE) | `KSA/Camera.cs:53` | glass | `glass.lib/GlassPatches.cs` | OK @5402 | **single highest-risk glass check — string private field; rename = silent FOV break.** Injection is now gated to `ViewportRegistry.IsMainCamera`, preserving independent secondary-camera FOV. |
| `ChangeFieldOfView(float change) : void` | Harmony pre + reflection-method (string) | `KSA/Camera.cs:450` | glass | `GlassPatches.cs:25,50` | OK @5402 | prefix returns false only for main-camera instances while override is active |
| `UpdateProjection() : void` | Harmony pre + reflection-method (string) | `KSA/Camera.cs:466` | glass | `GlassPatches.cs` | OK @5402 | injects `_fovRadians` for main Base/Map cameras only, then original rebuilds projection |
| `GetPositionEgo(IPosition) : double3` | direct API | `KSA/Camera.cs:164` | pyro, graffiti, hot-pursuit | `pyro.lib/PlumeEmitter.cs`; `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | OK @5402 | emitter/decal/picker positions are camera-ego |
| `NearbyCelestial : Celestial? { get; set; }` | direct API | `KSA/Camera.cs:71` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | KSA's `OnFrameCelestials` only populates the main/frame camera; Hot Pursuit synchronizes this state for each mounted secondary camera so the local body is excluded from distant-sphere rendering. |
| `DistanceToNearbyCelestialKm` / `DistanceToNearbyCelestialSurfaceMeanKm` / `CurrentAltitudeKm` / `NearbyCelestialTerrainHeight` (public fields) | direct API (write) | `KSA/Camera.cs:31-37` | hot-pursuit | `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | mirrors the values assigned by KSA's main-camera `OnFrameCelestials`, including terrain-relative altitude, for secondary atmosphere/celestial consumers. |
| `GetFieldOfView() : float` (RADIANS) | direct API | `KSA/Camera.cs:785` | glass | `glass.lib/FovController.cs:42` | OK @5402 | getter returns radians; setter takes degrees (asymmetry) |
| `SetFieldOfView(float fovDegrees) : void` | direct API | `KSA/Camera.cs:412` | glass, hot-pursuit | `glass.lib/FovController.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs`, `HotPursuitPose.cs` | OK @5402 | setter takes degrees; Hot Pursuit clamps UI to KSA's 15–120 range |
| `GetPositionEgo(IPosition) : double3` | direct API | `KSA/Camera.cs:213` | i-feel-seen | `i-feel-seen.lib/IFeelSeenPatches.cs:57` | OK | |
| `Following : IFollowable? { get; }` · `SetFollow(IFollowable,bool,bool,bool)` | direct API | `KSA/Camera.cs:82,597` | camera-controller-override, hot-pursuit | `AnimationHelpers.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs`, `HotPursuitPose.cs` | OK @5402 | Hot Pursuit must pass `changeControl:false`; otherwise camera setup changes `Program.ControlledVehicle`. |
| `LookAtRotation(double3 fwdEcl, double3 upEcl) : doubleQuat` (static) | direct API | `KSA/Camera.cs:198` | camera-controller-override, hot-pursuit | `AnimationHelpers.cs`; `Animation/Animations/Spiral*Animation.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Hot Pursuit writes the result to `WorldRotation`. |
| `PositionEcl` / `WorldRotation` overrides · `EgoToEcl(double3)` | direct API (write/read) | `KSA/Camera.cs:110,134,145` | hot-pursuit | `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Same-frame mounted pose. `Camera.OnFrame` subsequently terrain-clamps to 0.5 m AGL and bakes view/frustum state. |
| `MVP.viewProjection` | direct (render) | `KSA/Camera.cs` (used `Program.cs:2394`) | thug-life | `thug-life.lib/ThugLifeQuadRenderer.cs:256` | OK | per-frame quad MVP, from the **rendered** viewport's camera |
| `Unfollow(bool changeControl = true)` | direct API | `KSA/Camera.cs:607` | parts-now | `parts-now.lib/Runtime/PartThumbnailGenerator.cs:188` | OK | ⚠ **must** be called as `changeControl: false` — the defaulted overload nulls `Program.ControlledVehicle` and would drop the player's vessel mid-flight |
| `OnFrame(double dt)` · `LocalPosition`/`LocalRotation`/`LocalScale` (inherited `Transform3D`) | direct API (write) | `KSA/Camera.cs:482`; `KSA/Transform3D.cs:9,13,11` | parts-now | `PartThumbnailGenerator.cs:189-191` | OK | INVARIANT: the thumbnail camera is only ever **re-asserted** to origin/identity — the part is moved, never the camera (`ThumbnailCreator.MoveRootPart` assumes a camera parked at the origin) |
| `GetFieldOfView() : float` (RADIANS) · `NearPlane : float => 0.1f` | direct API (indirect) | `KSA/Camera.cs:765,65` (the row above cites `:702` from the 4750 baseline; `GetFieldOfView` is at **:765** in 5018) | glass (direct), parts-now (via `ThumbnailCreator.MoveRootPart`) | `KSA.Rendering/ThumbnailCreator.cs:191` | OK | `MoveRootPart(root, thumb, Camera)` forwards to the `(double fov, double nearPlane)` overload (`:194`) |

### KSA.CameraMode
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CameraMode.IVA` (enum) | enum | `KSA/CameraMode.cs:14` | IvaForceRender (kitchen-sink) | `ksa-abstractions.lib/IvaForceRender.cs:102` | OK | compared vs `Viewport.Mode` |

### KSA.CatExpressionAnim
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CatExpressionAnim : CatPostAnim` (type) | direct API — **constructed by the mod** | `KSA/CatExpressionAnim.cs:8` | kitten-animations | `KittenExpressionController.cs` | OK | file byte-identical 4680↔5348. Mod builds its own instance and appends it to `AnimProcessors` |
| `CatPostAnim.CharacterAvatar` (`required` field) / `Priority : float` | direct API | `KSA/CatPostAnim.cs:10,12` | kitten-animations | `KittenExpressionController.cs` | OK | `required` — must be set in the object initialiser or it is a compile break |
| `ExpressionAnim : AnimationAssetRef?` | direct API | `KSA/CatExpressionAnim.cs:14` | kitten-animations | `KittenExpressionController.cs` | OK | |
| `ExpressionWeight : float` | direct API | `KSA/CatExpressionAnim.cs:12` | kitten-animations | `KittenExpressionController.cs`; `KittenAnimationDriver.cs` | OK | mod's own processor: eased per frame. Game's **reactive** processor: only *capped* — `KittenRenderable.UpdateRenderData` damps it from acceleration every frame |
| `_expressionPose : TransformTRS[]? (private)` | reflection-field (cached FieldInfo) | `KSA/CatExpressionAnim.cs:16` | kitten-animations | `KittenExpressionController.cs` | OK | set null to bust the sampled-pose cache when `ExpressionAnim` changes |

### KSA.CatFurRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialIndices : protected int[]` | reflection-field | `KSA/CatFurRenderable.cs:22` | doh | `KittenSpawner.cs:388-408,523-537` | OK | fur material handle swap |

### KSA.ChuteRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `Draw(float3[], float[]?, floatQuat[]?, ref readonly double4x4, float diameterM, double dt)` | **Harmony prefix** | `KSA/ChuteRenderable.cs:32` | free-fallin | `free-fallin.lib/FreeFallinPatches.cs` | OK (new @5402) | substitutes the nested animated renderable's material handle before its draw; single overload |
| `_renderable : private readonly AnimatedRenderable` | reflection-field (string) | `KSA/ChuteRenderable.cs:13` | free-fallin | `FreeFallinPatches.cs` | OK (new @5402) | load-bearing private field; exact-name reflection watchlist entry |
| ctor binds `ParachuteCanopyGlb` + material slot 0 `ParachuteCanopy_Material` and two-sided skinned techniques | behavior + asset invariant | `KSA/ChuteRenderable.cs:17-29` | free-fallin | `CanopyMaterialController.cs`; `FreeFallinPatches.cs` | OK (new @5402) | slot zero and two-sided main/prepass/shadow sharing are required for one material swap to cover the complete canopy |

### KSA.Celestial
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Celestial : Astronomical, IOrbiter,…` (type) | direct API | `KSA/Celestial.cs:19` | CelestialProvider (→ kiwis-marbles, marque) | `ksa-abstractions.lib/CelestialProvider.cs:12` | OK | `OfType<Celestial>()` |
| `SetOrbit(Orbit newOrbit)` | direct API | `KSA/Celestial.cs:153` | kiwis-marbles | `kiwis-marbles…/CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | bare `Orbit = newOrbit`; no `Children` re-parent (engine does it) |
| `Parent : IParentBody` (`=> Orbit.Parent`) | direct API | `KSA/Celestial.cs:73` | kiwis-marbles | `CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | old-parent lookup before swap |
| `IParentBody.UpdatePerFrameDataTree() : void` (default interface method) | direct API | `KSA/IParentBody.cs:110` | kiwis-marbles | `CelestialWeldEngine.cs` (`ApplyOrbit`) | OK | subtree refresh after SetOrbit (replaced bare `UpdatePerFrameData()`) |
| `IParentBody.Children : List<IOrbiter>` | direct API | `KSA/IParentBody.cs:27` | kiwis-marbles | `CelestialWeldEngine.cs` (`Reparent`) | OK | Remove/Add across parents on cross-parent weld |
| `OrbitColor : byte4 { get; protected set; }` (via IOrbiter) | direct API | `KSA/Celestial.cs:63`; `KSA/IOrbiter.cs:24` | kiwis-marbles | `CelestialWeldEngine.cs:36` | OK | orbit line color |
| `Orbit : Orbit { get; set; }` | direct API | `KSA/Celestial.cs:57` | kiwis-marbles | `KiwisMarblesSubmod.cs:422` | OK | saved for restore |
| `MeanRadius : double` (override) | direct API | `KSA/Celestial.cs:77` | kiwis-marbles, graffiti | `KiwisMarblesSubmod.cs:146,321,334`; `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs` | OK | surface placement / terrain radius |
| `{GetCce2Ccf, GetCcf2Cce, GetCci2Cce} : doubleQuat` · `GetTerrainHeightFromDirCcf(double3, bool accurate) : double` · `GetDirCcfFromLatLon(double, double) : double3` · `static {GetLatitudeFromCcf, GetLongitudeFromCcf}(double3) : double` | direct API | `KSA/Celestial.cs:540,534,522,792,670,708,743` | graffiti | `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs` | OK @5348 | CPU terrain march + geodetic decal anchors. Height is metres above `MeanRadius` (0 with no heightmap); lat/lon statics return DEGREES. ⚠ **`accurate: true` is load-bearing**: since the 5319–5325 terrain precision rework, only accurate mode evaluates the procedural terrain modifiers (`Celestial.cs:877-880`) the rendered surface includes — an inaccurate radius parks the decal metres off the visible terrain. See `scope/decals.md` #10 |

### KSA.CelestialSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `All : LookupCollection<Astronomical>` | direct API | `KSA/CelestialSystem.cs:57` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs:15`; `CelestialProvider.cs:11-12` | OK | shared enumerator root |
| `GetWorldSun() : StellarBody` | direct API | `KSA/CelestialSystem.cs` (call `Universe.cs:173`) | marque | `marque.lib/MarqueLib.cs:93` | OK | root of celestial tree |
| `Deregister(Vehicle)` | direct API | `KSA/CelestialSystem.cs` | doh | `KittenSpawner.cs:62,67,68` | OK | despawn |
| `JobSystems.VehicleSolver.Wait()` | direct API | `KSA/JobSystems.cs:16`; `Brutal.Concurrency.Jobs/JobScheduler.cs:51` | doh | `KittenSpawner.cs:69,161,226-229` | OK | waits for the background vehicle physics step before `new KittenEva` / `Vehicle.Dispose()`; avoids `ConstraintSim.UnlockShapes()` stepping-lock throw (5402) |
| `All.TryGet(string, out Astronomical)` (LookupCollection) | direct API | `KSA/CelestialSystem.cs` | doh | `KittenSpawner.cs:62` | OK | despawn lookup |
| `Get(string) : Astronomical?` | direct API | `KSA/CelestialSystem.cs` | graffiti | `graffiti.lib/GraffitiSubmod.cs` (`ResolveAnchor`) | OK @5348 | per-frame decal anchor re-resolution by vehicle/body id; null (dormant decal) on despawn |

### KSA.CharacterAvatar (+ nested CharacterCore)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Core : CharacterCore` (public **struct** field) | reflection-field | `KSA/CharacterAvatar.cs:209` | garrys-torch, doh, kitten-animations | `garrys-torch.lib/WeldEngine.cs:172-173,182`; `KittenSpawner.cs:388-408`; `KittenAnimationsSubmod.cs` | OK | garrys uses `GetField("Core")` + `SetValue` — correct only while `Core` is a value-type field |
| `CharacterCore.Scale : float = 0.01f` (field) | reflection-field | `KSA/CharacterAvatar.cs:33` | garrys-torch | `WeldEngine.cs:176-188` | OK | avatar uniform scale (`factor*0.01f`) |
| `Core.CharacterModel : AnimatedRenderable` | direct API | `KSA/CharacterAvatar.cs:32` | doh, kitten-animations | `KittenSpawner.cs:388-408`; `KittenAnimationsSubmod.cs` | OK | `.MaterialIndices` (doh); kitten matches the prefix against this instance |
| `Personality : CharacterPersonality` (field + enum) | direct API | `KSA/CharacterAvatar.cs:21-28,219` | kitten-animations | `Ui/PlaybackSection.cs`, `Ui/StrengthSection.cs` | OK | display only; decides whether a personality processor exists at all (Neutral = none) |
| `Core.Fur.CatFurRenderable` / `Core.Attachments.{Helmet,Mmu}` (field path) | reflection-field path | `KSA/CharacterAvatar.cs` | doh | `KittenSpawner.cs:388-408,523-537` | OK | helmet/visor/mmu mesh `MaterialIndices` |
| `Expressions.{Angry,Awe,Happy,Sad,Scared} : List<AnimationAssetRef>?` | direct API | `KSA/CharacterAvatar.cs:192-200` | kitten-animations | `KittenExpressionController.cs` | OK | per-variant selection or random pick |
| `Animations.MmuAnimations.{MmuIdleDefaultAnim, MmuIdleActionsAnim, MmuMove L/R/Fwd/Back/Up/Down LoopAnim, MmuArmRetractAnim}` | direct API | `KSA/CharacterAvatar.cs:158-177` | kitten-animations | `KittenAnimationCatalog.cs` | OK | idle-actions list + arm-retract added 5348 pass |
| `Animations.{BlinkAnim, HelmetMaskAnim} : AnimationAssetRef?` | direct API | `KSA/CharacterAvatar.cs:149,151` | kitten-animations | `KittenAnimationCatalog.cs` | OK | overlay pose sources |
| `Animations.WalkingAnimations.{RunningAnim, WalkingAnim}` | **superseded — no longer used** | `KSA/CharacterAvatar.cs:179-184` | — | — | n/a | ⚠️ `InitalizeFromCharacterRef` only assigns `WalkingAnim` and **never assigns `RunningAnim`**. Ground walk/run now come from `CharacterGroundAnimations` via `KittenRenderable` |

### KSA.CharacterReference / KSA.CharacterTexturesReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CharacterReference.CharacterTextures : CharacterTexturesReference` | direct API | `KSA/CharacterReference.cs:32` | doh | `doh.lib/Materials/MaterialFactory.cs:382,390` | OK | file byte-identical |
| `CharacterTexturesReference.{CharacterBodyMaterial, CharacterHeadMaterial, CharacterEyeMaterial} : PbrMaterialReference` | reflection-field | `KSA/CharacterTexturesReference.cs:9,12,15` | doh | `MaterialFactory.cs:406-408` | OK | file byte-identical |

### KSA.CharacterRenderSystem / KSA.CharacterRenderResources
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Program.CharacterRenderSystem` → `_resources : CharacterRenderResources` → `.FurTexture/.CatFurMaskTexture` (`.BindlessHandle`), `.FurSampler` (`.BindlessIndex`) | reflection-field | `KSA/CharacterRenderSystem.cs:7`; `KSA/CharacterRenderResources.cs:24-30` | doh | `MaterialFactory.cs:504-525` | OK | fur `ExtraData` handles; file diff is internal shader wiring only (rev 4745) |

### KSA.ColorRgbReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `R / G / B : float` | reflection-field (string) | `KSA/ColorRgbReference.cs:10,13,16` | zippo (unreachable, see Color bug), red-alert | `LightController.cs:61-63,82-86`; `red-alert.lib/LightActions.cs:83-86` | OK | red-alert path works; zippo path dead due to `Color` bug |
| `OnDataLoad(Mod) : void` | reflection-method (string) | `KSA/ColorRgbReference.cs:35` | zippo, red-alert | `LightController.cs:82-86`; `LightActions.cs:83-86` | OK | 1-arg (`new object?[]{null}`); recomputes `Value` |

### KSA.Connection (nested in Part)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Connection.Connect(IConnector, IConnector)` | direct API | `KSA/Part.cs:285` | blinky, its-so-shiny | `blinky.lib/LcdGridBuilder.cs:352`; `its-so-shiny.lib/ShinyGridBuilder.cs:221` | OK | takes `IConnector` (Part implements), not `(Part,Part)` |
| `Connection.Disconnect()` | direct API | `KSA/Part.cs:301` | blinky, its-so-shiny | `LcdGridBuilder.cs:216`; `ShinyGridBuilder.cs:131` | OK | |
| `Connection.OtherPart(Part)` | direct API | `KSA/Part.cs:264` | blinky (debug) | `BlinkySubmod.cs:645` | OK | |

### KSA.Controller (+ OrbitController / FlyController)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OrbitController.OnFrame(Viewport, double inDeltaTime) : override void` | Harmony pre + reflection-method (string "OnFrame") | `KSA/OrbitController.cs:375` | camera-controller-override | `camera-controller-override.lib/CameraControllerOverridePatches.cs:25,29` | OK | bound to `KSA.OrbitController` via `using KSA;` (NOT RenderCore family) |
| `FlyController.OnFrame(Viewport, double inDeltaTime) : override void` | Harmony pre + reflection-method (string) | `KSA/FlyController.cs:417` | camera-controller-override | `CameraControllerOverridePatches.cs:26,31` | OK | |
| `Controller` (base, `__instance`) | direct API | `KSA/Controller.cs:8` | camera-controller-override | `CameraControllerOverridePatches.cs:42` | OK | |
| `Controller.Camera : Camera` (field) | direct API (read chain) | `KSA/Controller.cs:12` | camera-controller-override | `AnimationHelpers.cs:33` | OK | the real camera field (NOT `Transform`) |
| **field `Transform` (`___Transform` injector)** | Harmony field-injection (by name) | `KSA/Controller.cs` (NO such field) | camera-controller-override | `CameraControllerOverridePatches.cs:42,48` | **BROKEN** | no `Transform` field on KSA controllers in 4680 OR 4750 → `Apply` throws (swallowed) → animation prefix never attaches. Pre-existing, not a 4750 regression. Fix: inject `Camera ___Camera` |

### KSA.Cursor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetEgoRay(IViewport) : static Ray` (= `viewport.GetCamera().ScreenToEgoRay(GetPosition(viewport))`) · `GetPosition(IViewport) : float2` · `DesktopPosition : float2` | direct API | `KSA/Cursor.cs:27,22,11` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | **CHANGED @5402** (fixed) | **replaced `InputRay`/`UpdateInputRay`/`ScreenPosition` @5402** — both pass `Program.MainViewport` and get the same-frame camera/cursor ray. |

### KSA.DeviceMeshInterleaved (+ nested static Shared)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Shared.{RunningVertexBufferSize, RunningIndexBufferSize} : public static uint` | direct API (**write**) | `KSA/DeviceMeshInterleaved.cs:25,27` | parts-now | `parts-now.lib/Runtime/MeshBudget.cs:86-89,134-138,180-181,232-233` | OK | 🔶 the headroom trick: inflated at `[StarMapAllModsLoaded]`, rewound on the first UI frame. Must stay **public static settable `uint`** |
| `Shared.{VertexAllocation, IndexAllocation} : public static BufferEx` → `.BufferSize` | direct API | `KSA/DeviceMeshInterleaved.cs:19,21`; `Brutal.VulkanApi.Abstractions/BufferEx.cs:90` | parts-now | `MeshBudget.cs:80,83` | OK | authoritative allocated size vs the running bump cursor |
| `Shared.IsBuilt : public static bool` | direct API (tripwire) | `KSA/DeviceMeshInterleaved.cs:31` | parts-now | `MeshBudget.cs:124,173` | OK | must be false at reserve time, true on the first frame; a mismatch only WARNs |
| `Shared.Build() : static void` (one-shot) / `Shared.Rebuild()` | behavior dependency (no patch) | `KSA/DeviceMeshInterleaved.cs:33,69`; called from `DeviceMeshInterleaved.Bind() :195` ← `ModLibrary.Bind` (`KSA/ModLibrary.cs:1732`) ← `KSA/Program.cs:985` | parts-now | — | OK | 🔶 **standing invariant U1/U2.** `Build()` sizes both buffers from the running counters, exactly once. `Rebuild()` copies `VertexAllocation.BufferSize` bytes out of the **old** buffer (`:82-83`) so it can never grow anything |
| `VerticesSize` / `IndicesSize : ByteSize` (fields) | direct API | `KSA/DeviceMeshInterleaved.cs:115,125` | parts-now | `MeshBudget.cs:276-277` (via `MeshReference.DeviceMeshesInterleaved`, `KSA/MeshReference.cs:32`) | OK | measured **before** `MeshReference.Dispose()` for the purge's leak accounting |

### KSA.DistanceReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `InMeters() : double` | direct API | `KSA/DistanceReference.cs:148` | steely-eyed | `VehicleTelemetry.cs:52` | OK | atmosphere height |
| `PartTemplate.Diameter : DistanceReference` (rev 4721) | direct API | `KSA/PartTemplate.cs:76-77` | not written) | — | ADDITIVE | mod never writes `<Diameter>`; mod-built parts miss size-filtered lists |

### KSA.EVADoor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CreateKittenEva(Vehicle)` (pattern mirrored, not called) | direct API (pattern) | `KSA/EVADoor.cs:84` | doh | `doh.lib/Spawning/KittenSpawner.cs:13-21` | OK | doh replicates this spawn shape |

### KSA.EditorTag
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.EngineController
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SetIsActive(Vehicle?, bool)` | direct API | `KSA/EngineController.cs:46` | blinky | `blinky.lib/BlinkyGridManager.cs:224,252,266` | OK | pixel on/off (called with `null` vehicle) |
| `IsActive : bool { get; }` | direct API | `KSA/EngineController.cs:24` | blinky | `blinky.lib/NonLcdEngineCache.cs:36` | OK | |
| `MinimumThrottle : float (settable)` | direct API | `KSA/EngineController.cs` | blinky | `LcdGridBuilder.cs:472` | OK | |
| `Cores : RocketCore[]` | direct API (debug) | `KSA/EngineController.cs:18` | blinky | `BlinkySubmod.cs:612-618` | OK | diagnose button only |

### KSA.FileReference (+ MeshAtlasFileReference / MeshFileReference / TextureReference)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `LocalPath : string` (field, XML attribute `Path`) · `IsReference() : override bool` · `Load() : void` · `ModPath` | direct API | `KSA/FileReference.cs:12,56,66,23` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs:194,196,203`; `BundleParserQueries.cs:252-272` | OK | ⚠ **`Load()` catches and logs its own exceptions instead of throwing** (`:66-147`), so a missing GLB/KTX2 produces a silent partial load. `VerifyLoadersProduced` re-derives every `DoLoad()` post-condition by hand (U9). `OnDataLoad` falls back to `Id = ModPath` when no `Id` is declared (`:43`) |
| `MeshAtlasFileReference.Meshes : List<MeshReference> { get; private set; }` | direct API | `KSA/MeshAtlasFileReference.cs:10` | parts-now | `RuntimeModLoaderDeltas.cs:217` | OK | non-empty is the atlas's success post-condition |
| `MeshAtlasFileReference.DoLoad()` mesh-naming rule (one `MeshReference` per `GltfJson.Meshes[i].Name`, skipping `_`-prefixed) | behavior dependency (**duplicated**, not called) | `KSA/MeshAtlasFileReference.cs:25-38` | parts-now | `Runtime/GlbMeshNames.cs:48-79`; `BundleValidatorContext.cs:157` | OK | ⚠ **U8.** Validation rule V6 must know the mesh ids before anything loads, so parts-now reads the GLB JSON chunk itself (no `Brutal.Gltf` reference). A change to the rule silently mis-validates |
| `MeshFileReference.Mesh : MeshReference?` (field) | direct API | `KSA/MeshFileReference.cs:14` | parts-now | `RuntimeModLoaderDeltas.cs:220` | OK | non-null is the mesh file's success post-condition |
| `TextureReference.{BindlessHandle : int, Texture : SimpleVkTexture, TextureAsset : TextureAsset, Dispose(Device)}` | direct API (GPU teardown) | `KSA/TextureReference.cs:67,61,58,74` | parts-now | `Runtime/RuntimeModPurgeSteps.cs:146-154` | OK | ⚠ `Dispose(Device)` calls `BindlessTextures.FreeTexture(BindlessHandle)` then `Texture.Dispose()`/`TextureAsset.Dispose()` with **no null checks**, and handle `0` is the shared *empty* texture → triple guard before calling. Type does **not** implement `IDisposable`; the `Device` arg is ignored |
| `MeshReference.{IsReference(), Dispose(), DeviceMeshesInterleaved}` | direct API | `KSA/MeshReference.cs:65,145,32` | parts-now | `RuntimeModLoaderDeltas.cs:232`; `RuntimeModPurgeSteps.cs:206,217` | OK | `MeshReference.Load` ends by clearing `_isReference` and calling `ModLibrary.RegisterBinder(this)` (`:107`) |

### KSA.FlightComputer (+ nested VehicleConfigInfo)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle.FlightComputer : FlightComputer { get; private set; }` | direct API | `KSA/Vehicle.cs:415` | average-twr, blinky (debug) | `average-twr.lib/TwrDataReader.cs:17`; `BlinkySubmod.cs:612-618` | OK | |
| `FlightComputer.AmbientPressure : float` (field) | direct API | `KSA/FlightComputer.cs:57` | average-twr | `TwrDataReader.cs:26` | **NEW @5117** | fed from `states.Environment.AtmosphericPressure`; 0 in vacuum |
| `Vehicle.ComputeActiveThrust(float ambientPressure) → float` | direct API | `KSA/Vehicle.cs:6069` | average-twr | `TwrDataReader.cs:26` | **NEW @5117** | replaces `VehicleConfigInfo.TotalEngineVacuumThrust`; skips out-of-propellant engines; same call the navball TWR uses |
| ~~`FlightComputer.VehicleConfig : VehicleConfigInfo`~~ | direct API | — | *(none)* | — | **no longer used @5117** | still exists; average-twr no longer reads it |
| ~~`VehicleConfigInfo.TotalEngineVacuumThrust : float`~~ | direct API | — | — | — | 🔴 **REMOVED @5117 (rev 5114)** | with `TotalEngineVacuumMassFlowRate`, `TotalEngineExhaustVelocity`, `TotalEngineIsp`. Broke average-twr (CS1061) — **fixed** |

### KSA.FloatReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Value : float` | reflection-field (string) | `KSA/FloatReference.cs:9` | zippo | `zippo.lib/LightController.cs:50,71` | OK | light `Intensity.Value` read/write — works |

### KSA.FlowRule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `FlowRule` (enum) + `ResourceManager.FlowRule` | direct API (debug) | `KSA/FlowRule.cs` | blinky | `BlinkySubmod.cs:612-618` | OK | diagnose button only |

### KSA.GameSettings
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnKeyAll(GlfwKeyEvent) : static bool` | Harmony pre (HotkeyGuard) + reflection-method (`nameof`) | `KSA/GameSettings.cs:2379` | **ALL top-level mods via HotkeyGuard** (marque ships a local copy) | `ksa-abstractions.lib/HotkeyGuard.cs:21,23` | OK | suite-wide chokepoint; prefix `ref bool __result` swallows key while typing |
| `GameSettings.Current.Graphics.PartThumbnailSize : ushort` | direct API | `KSA/GameSettings.cs` | parts-now (indirect) | parts-now via `ThumbnailRenderer.SIZE` | OK | thumbnail size (rev 4696). parts-now reads it only through `ThumbnailRenderer.SIZE` and warns when it drifts from the boot-sized thumbnail viewport (U12); it never writes the setting |

### KSA.GaugeCanvas
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnDrawMenuBar() : static void` | Harmony pre | `KSA/GaugeCanvas.cs:809` | marque | `marque/Patcher.cs:39-44` | OK | renders View-menu "Marque" submenu |
| `_canvases : static List<GaugeCanvas>` (NonPublic+Static) | reflection-field (string) | `KSA/GaugeCanvas.cs:20` | con-man | `con-man.lib/GaugeStateAccessor.cs:28` | OK | **required** (IsValid gate) |
| `_enabled : bool` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:63` | con-man | `GaugeStateAccessor.cs:29` | OK | **required** |
| `_customOffset : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:54` | con-man | `GaugeStateAccessor.cs:30` | OK | **required** |
| `_customScale : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:56` | con-man | `GaugeStateAccessor.cs:31` | OK | **required** |
| `_windowPosition : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:50` | con-man | `GaugeStateAccessor.cs:32` | OK | optional (degrades to Zero) |
| `_windowSize : float2` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:52` | con-man | `GaugeStateAccessor.cs:33` | OK | optional (degrades to (100,100)) |
| `_windowTitle : string` (NonPublic) | reflection-field (string) | `KSA/GaugeCanvas.cs:37` | con-man | `GaugeStateAccessor.cs:34` | OK | optional (skips reposition if null) |

### KSA.GenericGizmo
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GenericGizmo(MeshReference, IGizmoRenderData, int)` ctor; `.GetSegmentDataByViewport(IViewport) : PerSegmentData[]` (keyed by `ViewportId` @5402); `Static.GenericGizmoRenderData`; `PerSegmentData{Active,PositionEgo,Body2Cce,Scale,Color}` | render-pass | `KSA/GenericGizmo.cs:208,277,15,170` | dont-stifle-me | `dont-stifle-me.lib/PerAxisScaleDrag.cs:43` | OK @5402 (`Viewport`→`IViewport`) | per-axis scale-gizmo drag (reads `VehicleEditor.ScaleGizmo` segment data). Was flexo's editor gizmos until flexo was removed @5348 |

### KSA.GlobalShaderBindings
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `DescriptorSetLayout : static` · `DescriptorSet : static` · `DynamicOffset(int viewportIndex) : static` | direct API (render) | `KSA/GlobalShaderBindings.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | set 0 of the decal pipeline — the game-wide Camera/Lighting UBO block with a dynamic offset per viewport. Set order (0 global / 1 depth / 2 bindless) is baked into the GLSL |

### KSA.GltfPbrSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SuperMeshRenderSystem.GltfSystem`; `GltfPbrSystem.BlankMaterialTexture.BindlessHandle` | reflection-field | `KSA/GltfPbrSystem.cs:31` | doh | `MaterialFactory.cs:541-577,592-593` | OK | default-texture fallback |

### KSA.GpuObjectAssetRef
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `.Handle : int` | reflection-field | `KSA/GpuObjectAssetRef.cs` | doh | `MaterialSystemAccessor.cs:154,183,249` | OK | map name→buffer index |

### KSA.GpuObjectSystem<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `BigBuffer : BufferEx` (public get/protected set) | reflection-field | `KSA/GpuObjectSystem.cs:18` | doh, humble-arteest | `MaterialSystemAccessor.cs:71`; `KittenColor.cs:191-215` | OK | GPU material buffer |
| `DeviceCtx : IVulkanContext` (protected) | reflection-field (hierarchy) | `KSA/GpuObjectSystem.cs:16` | doh, humble-arteest | `MaterialSystemAccessor.cs:75`; `KittenColor.cs:55-73` | OK | |
| `CreateObject(AssetName, T) : bool` | reflection-method (doh) / direct API (free-fallin) | `KSA/GpuObjectSystem.cs:45` | doh, free-fallin | `MaterialSystemAccessor.cs:78,123`; `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | allocates immutable runtime materials; free-fallin creates one per Apply |

### KSA.GpuTextureSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetOrLoad`; `{SamplerRepeatHandle, DefaultWhiteTexture, DefaultBlackTexture}` | reflection-field/method | `KSA/GpuTextureSystem.cs:26,32,34` | doh | `MaterialSystemAccessor.cs:84-90`; `MaterialFactory.cs:541-577` | OK | texture bindless lookup; file byte-identical |
| `TryAddTexture(AssetName, TextureAsset, bool)` + `GetOrLoad` | direct API | `KSA/GpuTextureSystem.cs:85-100` | free-fallin | `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | uploads replacement/composited albedo and optional 1x1 PBR textures into KSA's bindless system |

### KSA.GrainGeometryLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `All() : static ReadOnlySpan<GrainGeometry>` · `TryGet(KeyHash) : static GrainGeometry?` | direct API (read-only) | `KSA/GrainGeometryLibrary.cs:25,41` | parts-now | `Runtime/BundleValidatorRulesReferences.cs:195,209` | OK | validation rule V10 — `<Grain Id>` must already exist; parts-now cannot extend this library at runtime (it is `Dictionary.Add`-populated once by `LoadAll`). Empty library → warning, not error |

### KSA.IFollowable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.IOrbiter
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `IOrbiter` (type) | direct API | `KSA/IOrbiter.cs:10` | CelestialProvider (→ kiwis-marbles, marque) | `CelestialProvider.cs:16` | OK | celestials + vehicles |
| `Parent : IParentBody { get; }` | direct API | `KSA/IOrbiter.cs:18` | kiwis-marbles | `CelestialWeldEngine.cs:21,26` | OK | null-checked |
| `Orbit : Orbit { get; }` | direct API | `KSA/IOrbiter.cs:16` | kiwis-marbles | `CelestialWeldEngine.cs:21` | OK | |
| `GetPositionCci() : double3` | direct API | `KSA/IOrbiter.cs:52` | kiwis-marbles | `CelestialWeldEngine.cs:24` | OK | (concrete `Vehicle.GetPositionCci` is a separate row) |
| `GetVelocityCci() : double3` | direct API | `KSA/IOrbiter.cs:66` | kiwis-marbles | `CelestialWeldEngine.cs:25` | OK | |
| `ShowOrbit : ref bool { get; }` (set) | direct API | `KSA/IOrbiter.cs:20` | marque | `marque.lib/MarqueLib.cs:49,84` | OK | `ref bool` property assignment |

### KSA.IParentBody
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetCci2Cce() : doubleQuat` | direct API | `KSA/IParentBody.cs:47` | garrys-torch | `garrys-torch.lib/WeldEngine.cs:75` | OK | called on `Vehicle.Parent` |
| `Mass : double { get; }` | direct API (dead path) | `KSA/IParentBody.cs:11` | average-twr (dead) | `average-twr.lib/TwrDataReader.cs:12` | OK | `ComputeSurfaceGravity` not on sampling path |
| `MeanRadius : double` (via IRadius) | direct API | `KSA/IRadius.cs:5` (steely cite `Vehicle.cs:482`) | average-twr (dead), steely-eyed | `TwrDataReader.cs:11`; `VehicleTelemetry.cs:47` | OK | Ap/Pe altitude (steely); dead path (average-twr) |
| `Children` (add/enumerate) | direct API | `KSA/IParentBody.cs` | doh, marque | `KittenSpawner.cs:174`; `MarqueLib.cs:99,126-135` | OK | spawn parent / SOI tree walk |
| `Id` (via IObjectId) | direct API | `KSA/IParentBody.cs`, `KSA/Astronomical.cs` | steely-eyed | `VehicleTelemetry.cs:46` | OK | SOI-change key |

### KSA.IPosition
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetPositionEcl() : double3` (base of IFollowable) | direct API | `KSA/IPosition.cs:7` | camera-controller-override | `AnimationHelpers.cs:33` | OK | target-tracking; reached only when `___Transform` fixed (dead) |

### KSA.JobSystems
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VehicleSolvers : static JobScheduler` → `JobScheduler.Wait()` | direct API | `KSA/JobSystems.cs:11` | garrys-torch | `garrys-torch.lib/GarrysTorchSubmod.cs:93` | OK | drains in-flight vehicle workers before teleport (race-avoidance) |

### KSA.KSAColor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Xkcd` (nested static class, reflected `GetProperties`) → `Color.Preset` | reflection-type | `KSA/KSAColor.cs:23` | XkcdColorHelper (→ zippo, doh palettes) | `ksa-abstractions.lib/XkcdColorHelper.cs:22,29` | OK | breaks only if `Xkcd` removed or `Color.Preset→float4` conversion dropped |
| `Xkcd.Scarlet`, `Xkcd.PaleGrey : Color.Preset` | direct API | `KSA/KSAColor.cs:1561,837` | garrys-torch, red-alert, skittles, con-man | `GarrysTorchSubmod.cs:333-334`; `red-alert…RedAlertSubmod.cs:128,129`; `skittles…SkittlesSubmod.cs:108-109`; `ConManSubmod.cs:125-126` | OK | button accents (cosmetic) |

### KSA.KeyframeAnimationModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ShowDeployRetract : bool` (field) | direct API | `KSA/KeyframeAnimationModule.cs:82` | red-alert | `red-alert.lib/ActionScanner.cs:51` | OK | deploy/retract vs continuous actuate |
| `TimeGoal : float` (field) | direct API | `KSA/KeyframeAnimationModule.cs:76` | red-alert | `ActionExecutor.cs:92,101` | OK | actuation driver |
| `Shared : KeyframeAnimationData` (field) → `.Duration` | direct API | `KSA/KeyframeAnimationModule.cs:74` | red-alert | `ActionExecutor.cs:92,101` | OK | |

### KSA.KinematicMeasurements
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AccelerationBody : double3` (field, backs `Vehicle.AccelerationBody`) | direct API | `KSA/KinematicMeasurements.cs:9` | geeforce, steely-eyed | `geeforce.lib/GForceRecorder.cs:96`; `VehicleTelemetry.cs:81` | OK | body-frame proper accel |

### KSA.KittenEva
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `KittenEva` (type; `GetType().Name=="KittenEva"` / `is KittenEva`) | reflection-type (string, garrys) | `KSA/KittenEva.cs:13` | garrys-torch, doh, kitten-animations, thug-life, graffiti | `WeldEngine.cs:161`; `KittenSpawner.cs`; `KittenAvatarAccessor.cs`; `KittenGlassesPreset.cs:38`; `graffiti.lib/DecalPicker.cs` | OK | garrys string-compares the type name (silent break if renamed). thug-life and graffiti use a typed `is KittenEva` — compile-checked, not string-matched |
| Kitten sphere pick: `new BoundingSphere3D(double3, double)` · `Ray.Raycast(BoundingSphere3D, out double, out bool)` · `Double3Ex.GetAbsoluteLargestElement(double3)` · `Part.{PositionEgo(ref readonly double4x4), ScaleTotal}` · `PartTree.Root` | direct API | `KSA/BoundingSphere3D.cs`; `KSA/Ray.cs:38`; `KSA/Double3Ex.cs:165`; `KSA/Part.cs:264,794`; `KSA/PartTree.cs:97` | graffiti | `graffiti.lib/DecalPicker.cs` (`TryPickKitten`) | OK @5348 | a KittenEva has no raycastable part view mesh — this mirrors the game's own `KittenEva.UpdateHighlight` (`KittenEva.cs:1097-1124`) bounding-sphere hover pick, anchoring the decal to the root part |
| `_renderable : private KittenRenderable` | reflection-field (string) | `KSA/KittenEva.cs:15` | garrys-torch, doh | `WeldEngine.cs:165`; `KittenSpawner.cs:506` | OK | avatar root chain. **kitten-animations no longer uses it** — it reads the public `Renderable` property |
| `Renderable : KittenRenderable` (public property) | direct API | `KSA/KittenEva.cs:59` | kitten-animations | `KittenAvatarAccessor.cs` | OK | typed replacement for the `_renderable` reflection |
| `LocomotionState : LocomotionState` (public property) | direct API | `KSA/KittenEva.cs:51` | kitten-animations | `Ui/PlaybackSection.cs`, `Ui/TuningSection.cs` | OK | mode / ground speed / gravity readout |
| `ControlMode : KittenControlMode` (public property) | direct API | `KSA/KittenEva.cs:67` | kitten-animations | `Ui/PlaybackSection.cs` | OK | View vs Direct |
| `AnimPlaybackRate / AnimJumpChainStage / AnimJumpChainCountdown` (public properties) | direct API | `KSA/KittenEva.cs:53,55,57` | kitten-animations | `Ui/PlaybackSection.cs` | OK | forwarded from `KittenRenderable` |
| `new KittenEva(CelestialSystem, string, doubleQuat, double3, IParentBody, string, Part, Orbit)` | direct API (ctor) | `KSA/KittenEva.cs:27` | doh | `KittenSpawner.cs:156` | OK | 8-arg ctor |
| `Teleport(Orbit?, doubleQuat?, double3?)` (inherited Vehicle) | direct API | `KSA/Vehicle.cs:1594` | doh | `KittenSpawner.cs:171` | OK | (shared with `Vehicle.Teleport`) |
| `IsControllable => true` (override) | enum/behavioral | `KSA/KittenEva.cs:15` | (informational) | — | ADDITIVE | new rev 4699; spawned/controlled kittens now controllable |

### KSA.KittenRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `_characterAvatar : private CharacterAvatar` | reflection-field (string) | `KSA/KittenRenderable.cs:12` | garrys-torch, doh, kitten-animations | `WeldEngine.cs:168`; `KittenSpawner.cs:513`; `KittenAvatarAccessor.cs` | OK | second link in avatar chain |
| `_groundIdleAnim, _groundWalkAnim, _groundRunAnim, _ladderAnim, _jumpIntroAnim, _flailAnim, _jumpLandAnim, _moonWalkAnim, _moonRunAnim, _swimAnim, _swimIdleAnim, _seatedIdleAnim : private AnimationAssetRef?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs:42-66` | kitten-animations | `KittenAnimationCatalog.cs` | OK | ⚠️ **the only route to the ground locomotion set** — it is not exposed on `CharacterAvatar`. Misses are collected in `UnresolvedFields`, logged, and shown as a red UI warning |
| `_seatedIdleActionAnims : private List<AnimationAssetRef>?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs:58` | kitten-animations | `KittenAnimationCatalog.cs` | OK | seated idle action clips |
| `_walkPairSampler, _runPairSampler, _swimPairSampler : private AnimationPairBlendSampler?` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs:68-72` | kitten-animations | `KittenAnimationCatalog.cs` | OK | playable + `.Weight` readout |
| `_blendSampler : private AnimationDirectionalBlendSampler` | reflection-field (string, cached FieldInfo) | `KSA/KittenRenderable.cs:38` | kitten-animations | `KittenAnimationCatalog.cs` | OK | MMU directional blend |
| `_catPersonalityExpressionAnim, _catExpressionAnim : private CatExpressionAnim` | reflection-field (string) | `KSA/KittenRenderable.cs:32,34` | kitten-animations | `KittenAnimProcessors.cs` | OK | resolved **by name** — `OfType<CatExpressionAnim>()` cannot tell the permanent mood face from the acceleration-reactive one |
| `_catEyeAnim : private CatEyeAnim` / `_catEarAnim : private CatEarAnim` | reflection-field (string) | `KSA/KittenRenderable.cs:36,30` | kitten-animations | `KittenAnimProcessors.cs` | OK | eye look/blink + ear mask weight |
| `UpdateRenderData(...)` per-frame `SetAnimation` + reactive-expression damping | behavioral | `KSA/KittenRenderable.cs:281-345,419-514` | kitten-animations | (motivates `KittenAnimationPatches`) | OK | ⚠️ **semantic dependency, invisible to the compiler**: the game re-picks the clip and rewrites the reactive expression weight every frame. If this stops happening the Harmony prefix becomes unnecessary but harmless; if it changes shape the override may need a different hook |

### KSA.CatEarAnim / KSA.CatEyeAnim
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CatEarAnim.ExpressionWeight : float` | direct API | `KSA/CatEarAnim.cs:13` | kitten-animations | `KittenAnimationDriver.cs` | OK | game writes it once at construction, so a mod value holds |
| `CatEyeAnim.MaxLookAtAngle : float` | direct API | `KSA/CatEyeAnim.cs:22` | kitten-animations | `KittenAnimationDriver.cs` | OK | game default 30 deg |
| `CatEyeAnim.LookPitchOffsetDeg : float` | direct API | `KSA/CatEyeAnim.cs:24` | kitten-animations | `KittenAnimationDriver.cs` | OK | ⚠️ game rewrites it every frame in `UpdateLocomotionAnimationState`, so it is re-applied from the pose prefix |

### KSA.KittenLocomotionTuning / KSA.KittenLocomotion
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `KittenLocomotionTuning.Current : static KittenLocomotionTuning` (field) + `.Default` | direct API — **mutated** | `KSA/KittenLocomotionTuning.cs:147,149` | kitten-animations | `Ui/TuningSection.cs` | OK | ⚠️ **global**: edits affect every kitten. The game ships the full editor at menu bar -> Debug -> Kitten Tuning (`KSA/Program.cs:3589`) |
| `AnimBlendTime, IdleSpeedThreshold, PlaybackRateMin/Max, Walk/Run/Ladder/TumbleClipNominalSpeed, Moonwalk{Walk,Run}NominalSpeed, Moonwalk{Start,Full}Gravity, MoonwalkPlaybackScale, NominalSwimAnimSpeed, SwimBlendFullSpeed, SwimBlendHalfLife, SwimEyePitchFactor, JumpLandDuration, JumpLandBounceIgnoreTime, LadderEyePitchDeg : float` | direct API (`ref` to static struct fields) | `KSA/KittenLocomotionTuning.cs:7-145` | kitten-animations | `Ui/TuningSection.cs` | OK | animation-facing subset only; the scoped reset restores just these |
| `KittenLocomotion.ComputeMoonwalkWeight(float, in KittenLocomotionTuning)` | direct API | `KSA/KittenLocomotion.cs:24` | kitten-animations | `Ui/TuningSection.cs` | OK | derived readout |
| `KittenLocomotion.ResolveSwimBlend(float, in KittenLocomotionTuning)` | direct API | `KSA/KittenLocomotion.cs:476` | kitten-animations | `Ui/TuningSection.cs` | OK | derived readout |
| `LocomotionState.{Mode, GroundSpeed, GravityMagnitude}`; `LocomotionMode`, `JumpChainStage` (enums) | direct API | `KSA/LocomotionState.cs:7,13,35`; `LocomotionMode.cs`; `JumpChainStage.cs` | kitten-animations | `Ui/PlaybackSection.cs` | OK | status display |

### KSA.LightModule (+ nested TemplateData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `"KSA.LightModule+TemplateData"` (nested type by full name) | reflection-type (string) | `KSA/LightModule.cs:12` | zippo | `zippo.lib/LightController.cs:39` | OK | hard-coded full name; rename → zero light parts |
| `LightModule` (type, `Get<LightModule>()`) | direct API | `KSA/LightModule.cs` | red-alert, its-so-shiny (via ZippoLib) | `ActionScanner.cs:68`; `LightActions.cs:51` | OK | |
| `LightModule.Template : TemplateData` (public field, assigned) | direct API | `KSA/LightModule.cs:59` | red-alert | `LightActions.cs:64,72,73` | OK | per-instance unshare swaps cloned TemplateData; must stay writable field |
| `TemplateData.Intensity : FloatReference` (field) | reflection-field (string) | `KSA/LightModule.cs:30` | zippo | `LightController.cs:50,71` | OK | works |
| `TemplateData.ColorRgb : ColorRgbReference` (field) | reflection-field (zippo) / direct (red-alert) | `KSA/LightModule.cs:33` | zippo (intended), red-alert | `LightController.cs` (intended); `LightActions.cs:58,71` | OK | red-alert uses it correctly |
| `TemplateData` field **`"Color"`** (no such field) | reflection-field (string) | `KSA/LightModule.cs:33` (actual field is `ColorRgb`) | zippo | `LightController.cs:59,80` | **BROKEN** | `GetField("Color")`→null ⇒ color get/set silent no-op. Pre-existing in 4680 AND 4750 (not a regression). Fix: `"Color"`→`"ColorRgb"` |

### KSA.Loading
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnFrame()` early-returns on `!Program.IsMainThread()`; `Task(string)` / `PushTask(LoadTask)` / `Current` | behavior dependency (no patch) | `KSA/Loading.cs:90-94,50,36,23`; `KSA/Program.cs:520` | parts-now | `Runtime/RuntimeModLoaderStates.cs:232-256` (design note + `Task.Run` worker) | OK | 🔶 **U7.** `FileReference.Load()` → `Loading.Task()` → `PushTask()` → `Current.OnFrame()` renders **and submits a whole ImGui frame**. parts-now runs `ILoader.Load()` on a worker precisely because that guard makes the chain a no-op there. Never null `Loading.Current` instead — `LoadTask`'s field initialiser throws, and the throw escapes `FileReference.Load`'s try block |

### KSA.LookupCollection<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UnsafeAsList() : List<T>` | direct API | `KSA/LookupCollection.cs:210` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs:15` | OK | then LINQ `OfType<Vehicle>/<Celestial>/<IOrbiter>` |

### KSA.MaterialData
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialData` (`[StructLayout(Sequential,Pack=1)]`; `AlbedoColor` @offset **16**) | direct API + GPU write | `KSA/MaterialData.cs:6-23` | doh, humble-arteest (KittenColor), free-fallin | `MaterialFactory.cs:247-257`; `KittenColor.cs:191-215`; `free-fallin.lib/CanopyMaterialController.cs` | OK @5402 | **byte-identical**; free-fallin supplies albedo/normal/PBR/emissive handles, tint and `RoughnessMetalScale`; Full Canopy additionally owns `ExtraData=(projection scale, cos rotation, sin rotation, 31415 marker)`; shader channel ABI is R=AO, G=roughness, B=metallic |

### KSA.MeshReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.MeshViewModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|

### KSA.Mod / KSA.ModManifest / KSA.ModEntry
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Mod.MakeUsing(string id, string manifestPath) : static Mod` | direct API | `KSA/Mod.cs:102` | parts-now | `Runtime/RuntimeModLoaderStates.cs:153` | OK | builds a `Mod` from a `mod.toml` path. Deliberately **not** registered into `ModLibrary.Lookup` — `MakeUsing` does not do it, and only the boot path does (`KSA/ModLibrary.cs:430`), which keeps `ModLibrary.Find` a reliable "loaded at boot?" test |
| `Mod.{DirectoryPath, Preload, Id}` | direct API | `KSA/Mod.cs:90,77,81` | parts-now | `RuntimeModLoaderStates.cs:161-176` | OK | `Preload` forced **false**: `FileReference.OnDataLoad` only calls `ModLibrary.RegisterLoader` while it is false, so a preloading mod would register templates whose files are never read |
| `ModManifest.{Mods : List<ModEntry>, Save()}` | direct API (**write to disk**) | `KSA/ModManifest.cs:12,27` | parts-now | `Io/ModFolderWriter.cs:146,155,172-173`; `Io/ModIdValidator.cs:175,181` | OK | so a runtime-installed mod also loads at the next launch (a saved vehicle would otherwise fail to resolve its parts). Null manifest ⇒ **fail closed** ("cannot prove the id is free") |
| `ModEntry.{Id, Enabled, New}` + `ModEntry(string, int)` ctor | direct API | `KSA/ModEntry.cs:24,9,21,40` | parts-now | `Io/ModFolderWriter.cs:172` | OK | parts-now writes `new ModEntry { Id, Enabled = true, New = false }` **on purpose** — the `(id, count)` ctor sets `Enabled=false, New=true`, which pops the game's "confirm mods" dialog at next boot |

### KSA.ModLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<T>(string id) : T where T:IKeyed` | direct API | `KSA/ModLibrary.cs:968` | blinky, its-so-shiny, thug-life, doh, humble-arteest, mesh-deform, byo-music, graffiti | `LcdGridBuilder.cs:51`; `ShinyGridBuilder.cs:27`; `ThugLifeQuadRenderer.cs:114,115`; `MaterialFactory.cs:219`; `VehiclePaintShaders.cs`; `MeshDeformShaders.cs:73`; `MusicPlayer.cs:8`; `graffiti.lib/DecalRenderer.cs` (`ShaderIncludeDirectory`) | OK | string-keyed; throws if id missing. Per-`T` asset ids in section 5 |
| `AllParts : internal static SerializedCollection<PartTemplate>` | reflection-field (string "AllParts") | `KSA/ModLibrary.cs:86` | doh, parts-now | `KittenSpawner.cs:322`; `parts-now.lib/Runtime/GameRegistry.cs:72` | OK | `.Find(KeyHash)` (doh, parts-now) / `.GetList` (parts-now) |
| `AllCharacters : internal static SerializedCollection<CharacterReference>` | reflection-field (string) | `KSA/ModLibrary.cs:90` | doh | `KittenSpawner.cs:347,354,357` | OK | character enumeration |
| `{AllMeshes, AllFiles, AllMaterials, AllPartGameDataReferences, AllEditorTagDefinitions}` : internal static `SerializedCollection<…>` | reflection-field (string ×5) | `KSA/ModLibrary.cs:80,68,70,78,134` | parts-now | `Runtime/GameRegistry.cs:73-77,292` | OK | the other five registries a runtime load writes into. All resolved once in `GameRegistry`'s static ctor; a miss is **fatal** (`IsHealthy=false` disables every Load button) |
| `Loaders : public static List<ILoader>` · `Binders : public static List<IBinder>` (+ `RegisterLoader`/`RegisterBinder`) | direct API (read + `RemoveAll`) | `KSA/ModLibrary.cs:144,146,180,209` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs:33,36,80,93`; `RuntimeModPurgeSteps.cs:285-286` | OK | mark/delta bookkeeping, then pruned on purge — KSA never clears either list |
| `Bind(Renderer) : static void` | behavior dependency (**re-implemented**, not called) | `KSA/ModLibrary.cs:1732` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs:93-94` | OK | parts-now mirrors the per-binder body (`CreateStagingPool` + `binder.Bind`) minus the `Parallel.ForEachAsync`: the stock method binds **every** binder ever registered, which would reallocate every existing mesh's device primitives |
| `AttachGameData() : static void` | behavior dependency (**re-implemented**, not called) | `KSA/ModLibrary.cs:1746` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs:215-271` | OK | `PartTemplate.ApplyGameData` is additive, so the stock method (which walks every registered entry) would **double** every part already attached at boot |
| `Find(string) : Mod?` → `Lookup : internal static SerializedCollection<Mod>` | direct API | `KSA/ModLibrary.cs:175,172,66` (registered only at `:430`) | parts-now | `Runtime/RuntimeModLoaderApi.cs:280`; `Io/ModFolderScanner.cs:249`; `Io/ModIdValidator.cs:166`; `RuntimeModLoaderStates.cs:153` | OK | "was this mod loaded at boot?" — parts-now refuses to load/reload such a mod. Fails **closed** |
| `{MOD_TOML, CONTENT_FOLDER, LocalModsFolderPath, LocalManifestPath, Manifest}` | direct API | `KSA/ModLibrary.cs:136,138,166,168,148` | parts-now | `Io/ModIdValidator.cs:158,175,214`; `Io/ModFolderWriter.cs:110,146,174`; `Runtime/PartsNowSettings.cs:65`; `Io/ModFolderScanner.cs:135` | OK | never hardcode a mods path in place of `LocalModsFolderPath`. `Manifest` is a public static field initialised to `null` |
| `Get<SoundBehavior>(string)` | direct API (validation only) | `KSA/ModLibrary.cs:975`; `KSA/SoundBehavior.cs:6` | parts-now | `Runtime/BundleValidatorRulesReferences.cs:295` | OK | V10 `<SoundEvent SoundId>` check. Only public path — `AllSoundBehaviours` is internal (`:108`) and `TryGet<T>` takes the strict `IsSubclassOf` branch (`:745`), so it never matches the base type. Throws `NullReferenceException` on a miss |

### KSA.Module
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Module.Parent : required Part` | direct API | `KSA/Module.cs:268` | blinky, its-so-shiny | `BlinkyPatches.cs:63`; `ShinyPatches.cs:57-63` | OK | `FullPart => PartParent ?? this` |

### KSA.ModuleBase (+ nested TemplateDataBase)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `TemplateDataBase.Id : [XmlAttribute] public string = ""` | direct API | `KSA/ModuleBase.cs:8-11` | parts-now | `Runtime/RuntimeModLoaderDeltas.cs:130-147`; `LoadedModRecord.cs:91-105`; `RuntimeModPurgeSteps.cs:109-120` | OK | 🔶 **U5 — optional and non-unique.** The purge therefore matches model templates by **object identity**, never by id: an id match would miss every id-less template (leaving a stale `PartModel` that `PartModel.Get` hands to the reloaded part, complete with the purged mesh's old shared-buffer offsets) and would evict another mod's instances on a collision |

### KSA.ModuleList
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<T>() : Span<T>` | direct API | `KSA/ModuleList.cs:112` | red-alert, blinky, its-so-shiny, doh, humble-arteest | `ActionScanner.cs:68`; `LcdGridBuilder.cs:327`; `ShinyGridBuilder.cs:205`; `KittenSpawner.cs:278-289`; `EngineEmissive.cs:123` | OK | generic module accessor |

### KSA.ModuleStateful (StateList + ModuleAndAllMutableStatesRef)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `StateList.NumModules : int` | direct API | `KSA/ModuleStateful.cs:251` | eternal-flame | `EternalFlameLib.cs:129` | OK | early-out when 0 |
| `StateList.Modules : Span<TModule>` | direct API | `KSA/ModuleStateful.cs:243` | eternal-flame | `EternalFlameLib.cs:132` | OK | iterates `Battery[]` |
| `StateList.GetModuleAndAllMutableStatesForInitialization(TModule) : ModuleAndAllMutableStatesRef` | direct API | `KSA/ModuleStateful.cs:508` | eternal-flame | `EternalFlameLib.cs:136` | OK | ref struct with `.Module`+`.State` |
| `ModuleAndAllMutableStatesRef.Module / .State` | direct API | `KSA/ModuleStateful.cs:516` | eternal-flame | `EternalFlameLib.cs:137` | OK | `.Module.Refill(ref .State)` |

### KSA.MusicPlayList
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MusicPlayList : SoundReference` (type) | direct API | `KSA/MusicPlayList.cs:6` | byo-music | `byo-music.lib/MusicPlayer.cs:8` | OK | |
| `PlayMusic(out ChannelWrapper?, ulong delaySamples=0)` | direct API | `KSA/MusicPlayList.cs:21` | byo-music | `MusicPlayer.cs:10` | OK | routes through `GameAudio.System` (FMOD) |

### KSA.NavBallData
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle.NavBallData : ref readonly NavBallData` | direct API | `KSA/Vehicle.cs:528` | average-twr | `average-twr.lib/TwrDataReader.cs:6` | OK | `ref readonly` accessor |
| `NavBallData.ThrustWeightRatio : double` (field) | direct API | `KSA/NavBallData.cs:21` | average-twr | `TwrDataReader.cs:6` | OK | 0 until flight computer populates |

### KSA.Orbit
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `CreateFromStateCci(IParentBody, SimTime, double3, double3, byte4) : static Orbit` | direct API | `KSA/Orbit.cs:1396` | garrys-torch, kiwis-marbles, doh | `WeldEngine.cs:121`; `CelestialWeldEngine.cs:31`; `KittenSpawner.cs:169,258` | OK | 5-arg state-vector factory; arg order/types must hold |
| `OrbitLineColor : byte4` (field) | direct API | `KSA/Orbit.cs:1062` | garrys-torch, doh | `WeldEngine.cs:126`; `KittenSpawner.cs` | OK | |
| `{Apoapsis, Periapsis, Eccentricity, Inclination, Period, SemiMajorAxis} : double` | direct API | `KSA/Orbit.cs` (exercised `IOrbiter.cs:97-106`) | steely-eyed | `VehicleTelemetry.cs:64-73` | OK | all `double.IsFinite`-guarded |
| `StateVectors.{PositionCci, VelocityCci}` | direct API | `KSA/Orbit.cs` | doh | `KittenSpawner.cs:231,239-242` | OK | spawn positioning |

### KSA.Part (+ nested Connector)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Part` (type) | direct API | `KSA/Part.cs` | PartHelpers (→ many), unladen-swallow, blinky | `PartHelpers.cs:11`; `BlinkyGridScanEndpoint.cs:58` | OK | |
| `new Part(string inName, PartTemplate, PartInstance?=null, Part?=null)` (ctor) | direct API | `KSA/Part.cs:765` | blinky, its-so-shiny, doh | `LcdGridBuilder.cs:268`; `ShinyGridBuilder.cs:157`; `KittenSpawner.cs:278` | OK | |
| `Id : string { get; init; }` | direct API | `KSA/Part.cs:411` | garrys-torch, zippo, red-alert, blinky, its-so-shiny, thug-life, kitchen-sink, mesh-deform | `GarrysTorchSubmod.cs:188`; `ZippoSubmod.cs`; `ThugLifeSubmod.cs:128`; `MeshDeformSubmod.cs:317-319` | OK | combo labels / pixel-id parsing |
| `DisplayName : string { get; init; }` | direct API | `KSA/Part.cs:413` | zippo, red-alert, mesh-deform | `ZippoSubmod.cs`; `ActionScanner.cs:27`; `MeshDeformSubmod.cs:282` | OK | |
| `Template : PartTemplate` (field) | direct API | `KSA/Part.cs:323` | garrys-torch, zippo, red-alert, blinky, its-so-shiny, thug-life, kitchen-sink, doh, parts-now | `GarrysTorchSubmod.cs:188`; `LightController.cs:92`; `ThugLifeSubmod.cs:122`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs:78,148` | OK | feeds reflection/labels; `Template.Id` (SerializedId). parts-now compares it against the record's part ids in the unload safety gate |
| `InstanceId : uint` | direct API | `KSA/Part.cs:321` | red-alert, graffiti, hot-pursuit | `ActionScanner.cs`; `graffiti.lib/GraffitiSubmod.cs`; `hot-pursuit.lib/HotPursuitSubmod.cs` | OK @5402 | stable sub-part addressing across per-frame target re-resolution |
| `RayCastEgo(ref readonly double4x4, Ray, out double ×2, out double3 ×4, out Part? closestSubPart, out Part?) : bool` | direct API | `KSA/Part.cs:2398` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`; `hot-pursuit.lib/HotPursuitPicker.cs` | OK @5402 | KSA's watertight art-mesh raycast. Position/normal come back in the **returned hit sub-part's** local frame. |
| `MatrixAsmb2Ego(in double4x4) : double4x4` | direct API | `KSA/Part.cs:1165` | graffiti, hot-pursuit | `graffiti.lib/DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | includes `Part.Scale` and the whole articulated sub-part parent chain |
| `Parts` (via `Vehicle.Parts.Parts`) / `Part.SubParts : ReadOnlySpan<Part>` | direct API | `KSA/Part.cs:655` | PartHelpers (→ zippo, its-so-shiny, humble-arteest, doh), garrys-torch, thug-life, kitchen-sink, mesh-deform, parts-now | `PartHelpers.cs:32`; `WeldEngine.cs:157`; `FlexoPartTest.cs:302`; `ThugLifeSubmod.cs:308`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs:154` | OK | recursion key. parts-now recurses it (plus `PartTree.Parts`, `VehicleEditingSpace.AllParts`, `VehicleEditor.UnattachedPartTrees`) to prove nothing alive still uses a mod's parts before purging |
| `FullPart : Part { get; }` | direct API | `KSA/Part.cs:659` | zippo, red-alert, blinky, its-so-shiny | `ZippoSubmod.cs:152`; `ActionExecutor.cs:80`; `BlinkyPatches.cs:63`; `ShinyPatches.cs:57-63` | OK | `=> PartParent ?? this` |
| `IsSubPart : bool` | direct API | `KSA/Part.cs:657` | blinky | `LcdGridBuilder.cs:326` | OK | |
| `Modules : ModuleList` (field) | direct API | `KSA/Part.cs:401` | red-alert, humble-arteest | `ActionScanner.cs:68`; `EngineEmissive.cs:123` | OK | `.Get<T>()` / `.Add(...)` |
| `SubtreeModules : ModuleList` (field) | direct API | `KSA/Part.cs:409` | red-alert, blinky, doh | `ActionScanner.cs:50`; `LcdGridBuilder.cs:327`; `KittenSpawner.cs:278-289` | OK | anim/solar/tank discovery |
| `LightSwitch : PowerConsumer?` (field) | direct API | `KSA/Part.cs:407` | zippo, red-alert, its-so-shiny | `ZippoSubmod.cs:152`; `LightActions.cs:41`; `ShinyPixelCell.cs:24` | OK | light on/off path |
| `Connection : (nested type)` → see KSA.Connection | — | `KSA/Part.cs` | blinky, its-so-shiny | — | OK | (Connect/Disconnect/OtherPart rows under KSA.Connection) |
| `Connections : List<Connection>` (field) | direct API | `KSA/Part.cs:391` | blinky, its-so-shiny | `LcdGridBuilder.cs:214`; `ShinyGridBuilder.cs:133` | OK | |
| `Scale : double3 { get; set; }` | direct API (write) | `KSA/Part.cs:499` | garrys-torch, blinky, its-so-shiny | `WeldEngine.cs:200`; `LcdGridBuilder.cs:305`; `ShinyGridBuilder.cs:186` | OK | setter resets cached pos matrix |
| `PositionParentAsmb : double3 { get; set; }` | direct API (write) | `KSA/Part.cs:449` (kitchen-sink cites backing `:333`) | blinky, its-so-shiny, kitchen-sink | `LcdGridBuilder.cs:299`; `FlexoPartTest.cs:216` | OK | prefer `:449` (property); kitchen-sink touches the backing field |
| `Asmb2ParentAsmb : doubleQuat { get; set; }` | direct API (write) | `KSA/Part.cs:463` (kitchen-sink cites backing `:337`) | blinky, its-so-shiny, kitchen-sink | `LcdGridBuilder.cs:302`; `FlexoPartTest.cs:217` | OK | part rotation write (kitchen-sink's part-move experiment) |
| `PositionVehicleAsmb : double3` (computed) | direct API | `KSA/Part.cs:415` | garrys-torch | `WeldEngine.cs:58` | OK | part-anchor position |
| `Asmb2VehicleAsmb : doubleQuat` (computed) | direct API | `KSA/Part.cs:431` | garrys-torch | `WeldEngine.cs:61` | OK | part-anchor orientation |
| `PositionEgo(ref readonly double4x4) : double3` | direct (render) | `KSA/Part.cs:677` | thug-life | `ThugLifeQuadRenderer.cs:282` | OK | per-frame model-ego |
| `Asmb2Ego(doubleQuat) : doubleQuat` | direct (render) | `KSA/Part.cs:682` | thug-life | `ThugLifeQuadRenderer.cs:283` | OK | |
| `BoundingBoxVehicleAsmb : (double3,double3) { get; set; }` + `ComputeBoundingBoxVehicleAsmb()` | direct API | `KSA/Part.cs:515,853` | kitchen-sink | `FlexoPartTest.cs:253` | OK | keep cached bounds coherent after move |
| `TreeParent : Part?` | direct API | `KSA/Part.cs:385` | blinky, its-so-shiny | `LcdGridBuilder.cs:103-104`; `ShinyGridBuilder.cs:76-77` | OK | manual tree wiring |
| `TreeChildren : List<Part>` (field) | direct API | `KSA/Part.cs:387` | blinky, its-so-shiny, kitchen-sink | `LcdGridBuilder.cs:228-230`; `FlexoPartTest.cs:227` | OK | sub-tree collection |
| `SetStage(int)` / `Stage` (get) | direct API | `KSA/Part.cs:731,517` | blinky, its-so-shiny | `LcdGridBuilder.cs:124,127`; `ShinyGridBuilder.cs:87` | OK | |
| ~~`_matrixAsmb` / `_matrixAsmb2Parent` : private double4x4~~ | reflection-field (string) | `KSA/Part.cs:536,552` | *(none)* | — | ⚠️ **sentinel changed @5117 (rev 5112)** | uncached sentinel went `double4x4.Identity` → all-NaN `UncachedMatrix` |
| `Tree : PartTree` → `.ReinitializeDerivedValues/.RefillConsumables` | direct API | `KSA/Part.cs` | doh | `KittenSpawner.cs:278-289` | OK | backpack/propellant init |

### KSA.PartModel (+ nested PerInstanceData, ViewportData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AddInstance(PerInstanceData, Viewport, int frameIndex) : void` | Harmony pre (humble vehicle-paint, mesh-deform) + post (IvaForceRender) | `KSA/PartModel.cs:375` | humble-arteest (VehiclePaint), mesh-deform, IvaForceRender (kitchen-sink) | `VehiclePaintPatches.cs` (`AddInstancePrefix`); `MeshDeformPatches.cs:51-60`; `IvaForceRender.cs:46` | OK | `PartModel.cs` byte-identical; 3-arg single overload. humble binds by param name `instanceData` and ORs paint into `StateBitFlag` |
| `..ctor(PartModelModule.Template) : protected` | Harmony post (ctor, `AccessTools.Constructor`) | `KSA/PartModel.cs:351` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs:42` | OK | explicit param-type array |
| `PerInstanceData` (struct: `ModelMatrix`@0 · `StateBitFlag`@64 · `EmissiveColor`@68 · `packing1`@72 · `Wetness`@76; 80 B) | direct API | `KSA/PartModel.cs:299-310` | IvaForceRender, humble-arteest (VehiclePaint), mesh-deform | `IvaForceRender.cs:98`; `VehiclePaintPatches.cs` (`AddInstancePrefix`); `MeshDeformManager.cs:127-134` | OK | humble now writes **only `StateBitFlag` bits 11..31** (no struct reinterpret, no game field clobbered); ⚠ mesh-deform still reuses `packing1`@72 + `Wetness`@76 |
| `PerInstanceData.StateBitFlag` **bits 11..31** | free-bit reuse (per-instance mod payload) | writers `KSA/PartModelModule.cs:82-133`, `KSA/PartModelDynamicModule.cs:81-107`; readers `MeshIndirect.frag:308-353` | humble-arteest (VehiclePaint) | `VehiclePaint.cs` (`EncodeBits`, `PaintBitShift`) | OK | 🔶 **audit every game update.** Game uses bits 0..10 only; 21 free bits carry a 7:7:7 sRGB paint color. `RayTraceInstance.StateFlags` is `int`, so the bits survive the RT path |
| `ViewportData.Get(PartModel, Viewport) : ViewportData` → `.InstanceList.Add(...)` | direct API | `KSA/PartModel.cs:281,277` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs:105` | OK | re-add internal instance to per-viewport draw list (editor) |
| `Instances : static List<PartModel>` | direct API | `KSA/PartModel.cs:325` | IvaForceRender (kitchen-sink), parts-now | `IvaForceRender.cs:111`; `parts-now.lib/Runtime/RuntimeModPurgeSteps.cs:109` | OK | enumerated by `Enabled` setter. parts-now `RemoveAll`s its own templates' entries on purge — **KSA never prunes this list** |
| `InstancesRayTrace : static List<PartModel>` | direct API | `KSA/PartModel.cs:327` | parts-now | `RuntimeModPurgeSteps.cs:110` | OK | same purge pruning; `PartModelDynamic` has **no** such list (dynamic models are never ray traced) |
| `Get(PartModelModule.Template) : static PartModel` | direct API | `KSA/PartModel.cs:333` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs:297` | OK | model "warming" turns an unresolvable `<Mesh Id>` into a catchable load-time exception. Resolves by scanning `Instances` for a matching `Template.Id`, which is exactly why the purge must prune those lists |
| `WriteInstancesToGpu(Viewport, int)` dereferences `Template.Material.{DiffuseReference,NormalReference,PBRMap}.BindlessHandle` **unguarded** | behavior dependency (no patch) | `KSA/PartModel.cs:393`; `KSA/PartModelGlass.cs:539`; `KSA/PartModelDynamic.cs:385` | parts-now | `Runtime/BundleValidatorRulesSchema.cs:87-108` (rule V9) | OK | 🔶 **U3.** Only `EmissiveMap` is `?.`-guarded. V9 exists solely to stop a player-authored part crashing the game; **if KSA ever null-guards these, V9 becomes an unnecessary restriction worth relaxing** |
| `Template : PartModelModule.Template` (field) | direct API | `KSA/PartModel.cs:329` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs:87,89,113` | OK | |

### KSA.PartModelDynamic (+ nested PerInstanceData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `AddInstance(PerInstanceData inInstanceData, Viewport, int) : void` | Harmony pre | `KSA/PartModelDynamic.cs:379` | humble-arteest (EngineEmissive) | `EngineEmissivePatches.cs:40,51` | OK | file byte-identical; param name `inInstanceData` matches |
| `PerInstanceData` (struct: `ModelMatrix`@0 · `StateBitFlag`@64 · `Temperature`@68 · `TfiThickness`@72 · `Wetness`@76; 80 B) | direct API (struct reinterpret for EngineEmissive) | `KSA/PartModelDynamic.cs:309-320` | humble-arteest (EngineEmissive, VehiclePaint) | `EngineEmissivePatches.cs:29-36`; `VehiclePaintPatches.cs` (`AddInstanceDynamicPrefix`) | OK | mirror struct matches exactly (`Temperature`@68, `TfiThickness`@72). VehiclePaint touches only `StateBitFlag` bits 11..31, so the two features compose |

### KSA.PartModelDynamicModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre (return false skips submit) | `KSA/PartModelDynamicModule.cs:55` | blinky, its-so-shiny, humble-arteest (VehiclePaint) | `BlinkyPatches.cs:27,31`; `ShinyPatches.cs:26,30`; `VehiclePaintPatches.cs` (`PartModelDynamicModulePrefix`) | OK | humble reads `__instance.Parent` to know which `Part` is submitting; **only caller** of `PartModelDynamic.AddInstance` |
| `PartModelDynamicModule.PartModelDynamic : required` | direct API | `KSA/PartModelDynamicModule.cs:32` | humble-arteest (EngineEmissive) | `EngineEmissive.cs:123,129,159` | OK | file identical |
| `PartModelDynamic.{Instances : static List<PartModelDynamic>, Get(PartModelDynamicModule.Template)}`; `PartModelGlass.{Instances, InstancesRayTrace, Get(PartModelGlassModule.Template)}` | direct API | `KSA/PartModelDynamic.cs:335,341`; `KSA/PartModelGlass.cs:474,476,482` | parts-now | `Runtime/RuntimeModPurgeSteps.cs:112-116`; `RuntimeModLoaderGpuStates.cs:301,305` | OK | warm on load, prune on purge. The `PartModelDynamic`-has-no-`InstancesRayTrace` asymmetry is load-bearing |

### KSA.PartModelGlassModule
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre | `KSA/PartModelGlassModule.cs:69` | blinky, its-so-shiny | `BlinkyPatches.cs:28,32`; `ShinyPatches.cs:27,31` | OK | 4745 merged ModelGlass+ModelEye shaders; C# class unchanged |

### KSA.PartModelModule (+ nested Template, RaytracingMode)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `UpdateRenderData(in double4x4, bool, Viewport, int)` | Harmony pre (return false skips submit) | `KSA/PartModelModule.cs:79` | blinky, its-so-shiny, mesh-deform, humble-arteest (VehiclePaint) | `BlinkyPatches.cs:26,30`; `ShinyPatches.cs:25,29`; `MeshDeformPatches.cs:34-43`; `VehiclePaintPatches.cs` (`PartModelModulePrefix`) | OK | game uses `Parent.FullPart.LightSwitch` here; humble reads `Module<T>.Parent : Part` (`KSA/Module.cs:419`); **only caller** of `PartModel.AddInstance` |
| `Parent : Part` | direct API | `KSA/PartModelModule.cs:71` | mesh-deform | `MeshDeformPatches.cs:101` | OK | `__instance.Parent` |
| `Template.Internal : bool` (field) | direct API (write) | `KSA/PartModelModule.cs:36` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs:87,89,113,125` | OK | flipped false to force interior render |
| `Template.RayTracing : RaytracingMode` (field) | direct API | `KSA/PartModelModule.cs:30` | IvaForceRender | `IvaForceRender.cs:103` | OK | |
| `RaytracingMode.ShadowProxy` (enum) | enum | `KSA/PartModelModule.cs:14` | IvaForceRender | `IvaForceRender.cs:103` | OK | shadow-proxy skip in editor postfix |
| `PartModelModule.Template.RayTracers : static List<Template>` · `PartModelGlassModule.Template.RayTracers` | direct API (**prune**) | `KSA/PartModelModule.cs:21`; `KSA/PartModelGlassModule.cs:14` | parts-now | `Runtime/RuntimeModPurgeSteps.cs:119-120` | OK | two separate static registries KSA appends to (`:44`, `:34`) and never prunes. `PartModelDynamicModule.Template` has **no** `RayTracers` — do not add a third call |
| `PartModelModule.Template.{Mesh, Material}` · `PartModelGlassModule.Template.{Mesh, Material}` · `PartModelDynamicModule.Template.{Mesh, Material}` | direct API | `KSA/PartModelModule.cs`, `KSA/PartModelGlassModule.cs`, `KSA/PartModelDynamicModule.cs` | parts-now | `Runtime/BundleParserQueries.cs:220-229`; `RuntimeModLoaderGpuStates.cs:166-168` | OK | normalised into one `ModelComponent` shape for V9 and for blaming a failed GPU upload on the part that uses the asset |

### KSA.PartModelRenderer (+ nested ColorData)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ColorData.Rebuild() : static void` | direct API | `KSA/PartModelRenderer.cs:282` | mesh-deform | `MeshDeformShaders.cs:43,77` | OK | pipeline rebuild after shader swap. ⚠ destroys/recreates pipelines immediately — unsafe mid-frame. humble-arteest no longer calls it; it sets `Program.RendererRebuildNeeded` instead |
| `ColorData.BuildPipelineModel` / `BuildPipelineDynamic` (→ `ShaderReference.CompileVariantWithCustomOptions`) | behavior dependency (no patch) | `KSA/PartModelRenderer.cs:104,193` | humble-arteest (VehiclePaint) | — | OK | Part color pipelines recompile MeshIndirect **from disk per `ENABLE_*` variant** and destroy the module right after, which is why swapping `ShaderReference.Shader` cannot work and interception happens at `ShaderModuleUtils.FromFile` |

### KSA.PartTemplate (+ component template types)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get<PartTemplate>(id)` / `Components : List<ModuleBase.TemplateDataBase>` (field) | reflection-field (string "Components") | `KSA/PartTemplate.cs:91` | zippo | `zippo.lib/LightController.cs:33` | OK | walked to find light TemplateData |
| `PartTemplate.{ApplyGameData(PartGameDataReference), ResolveConsumerFeedPoints(), Dispose()}` | direct API | `KSA/PartTemplate.cs:231,379,226` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs:237,258`; `RuntimeModPurgeSteps.cs:48` | OK | `ApplyGameData` is **additive** (`AddRange` on connectors/masses/rockets/components) → parts-now attaches incrementally instead of calling `ModLibrary.AttachGameData()`. `ResolveConsumerFeedPoints()` starts with `ConsumerFeeds.Clear()`, so it **is** idempotent. `Dispose()` disposes only `Thumbnail` |
| `PartTemplate.{Thumbnail : ThumbnailReference?, IsSubPart : bool, Components, SubPartInstances, EditorTagsStrings : List<StringReference>}` | direct API | `KSA/PartTemplate.cs:103,111,105,21,30` | parts-now | `Runtime/PartThumbnailGenerator.cs:262,279,311-320`; `BundleValidatorRulesReferences.cs:41,155`; `Ui/ResultsPanel.cs:125` | OK | Before `OnDataLoad` runs, `Hash` is `KeyHash.Zero` and `EditorTags` is empty — validation therefore reads `Id` strings and `EditorTagsStrings` (`[XmlElement("EditorTag")]`, value in `StringReference.Value`, `KSA/StringReference.cs:9`) |
| `SubPartTemplate : PartTemplate` · `PartGameDataReference : PartTemplate` · `SubPartGameDataReference : PartGameDataReference` · `PartInstance.{InstanceOf, GetTemplate()}` | direct API (type hierarchy) | `KSA/SubPartTemplate.cs:3`; `KSA/PartGameDataReference.cs:5`; `KSA/SubPartGameDataReference.cs:3`; `KSA/PartInstance.cs:16,94` | parts-now | `Runtime/BundleParserQueries.cs:34-74`; `BundleValidatorRulesIdentity.cs:298-310` | OK | ⚠ a bare `is PartTemplate` matches **all four** part-shaped types — every parts-now classifier tests most-derived first. `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` throws `NullReferenceException` on a miss, which is what rule V5 pre-empts |
| `EditorTagDefinition : SerializedId` · `MeshViewModule.Template` | direct API | `KSA/EditorTagDefinition.cs:5`; `KSA/MeshViewModule.cs:9` | parts-now | `Runtime/GameRegistry.cs:244-249`; `BundleParserQueries.cs:242` | OK | tag-definition ids feed V7's known-tag set; `<MeshView>` presence is V12's warning |

### KSA.PartTree
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parts : ReadOnlySpan<Part>` | direct API | `KSA/PartTree.cs:67` | PartHelpers (→ many), garrys-torch, zippo, red-alert, blinky, its-so-shiny, thug-life, kitchen-sink, mesh-deform | `PartHelpers.cs:13`; `ZippoSubmod.cs:406`; `ActionScanner.cs:17` | OK | top-level parts |
| `Root` | direct API | `KSA/PartTree.cs` | blinky, its-so-shiny | `LcdGridBuilder.cs:135`; `ShinyGridBuilder.cs:146` | OK | |
| `Batteries : ModuleStateful<…>.StateList` (field) | direct API | `KSA/PartTree.cs:37` | eternal-flame | `EternalFlameLib.cs:128` | OK | battery state list |
| `Modules.Get<Battery>()` (ModuleList) | direct API | `KSA/PartTree.cs` | its-so-shiny | `ShinyGridBuilder.cs:205` | OK | |
| `CreateFromNewPartTree(Part rootPart)` | direct API | `KSA/PartTree.cs:117` | blinky, its-so-shiny | `LcdGridBuilder.cs:135`; `ShinyGridBuilder.cs:94` | OK | core build path |
| `UpdateRenderData(ref readonly double4x4, bool isEditedVehicle, Viewport, int)` | direct API | `KSA/PartTree.cs:435` | i-feel-seen | `IFeelSeenPatches.cs:70` | OK | mod passes `in` → `ref readonly` |
| `States : ModuleStateList` (field) | direct API | `KSA/PartTree.cs:25` | kitchen-sink | `KitchenSinkLib.cs:59` | OK | passed as `oldStates` |
| `ReinitializeDerivedValues(ModuleStateList oldStates) : void` | direct API | `KSA/PartTree.cs:189` | kitchen-sink, doh | `KitchenSinkLib.cs:60`; `KittenSpawner.cs:278-289` | OK | also a 0-arg overload |
| `RecomputeStaticMass() : private void` | reflection-method (Traverse, string) | `KSA/PartTree.cs:306` | kitchen-sink | `FlexoPartTest.cs:319` | OK | string-named; caught/logged if renamed |
| `Controls : (control modules)` (rev 4699, backs `Vehicle.IsControllable`) | direct API | `KSA/PartTree.cs:49` | (informational) | — | ADDITIVE | new in 4750; not consumed |

### KSA.PbrMaterialReference
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `{DiffuseReference, NormalReference, PBRMap, EmissiveMap, Id}` + non-generic `.Get()` | reflection-field/method | `KSA/PbrMaterialReference.cs:9-18` | doh | `MaterialFactory.cs:413-418,242-245` | OK | `.BindlessHandle` off resolved `TextureReference`; file identical |
| `{DiffuseReference, NormalReference : TexturePowerReference?, PBRMap, EmissiveMap, ThinFilmMap}` (typed) + `_isReference = Diffuse==null && Normal==null && PBRMap==null` | direct API | `KSA/PbrMaterialReference.cs:9,12,15,18,21,64` | parts-now | `Runtime/BundleParserQueries.cs:178-201`; `BundleValidatorRulesSchema.cs:273-308`; `RuntimeModLoaderGpuStates.cs:182-188` | OK | V9 mirrors the `_isReference` test to tell a material **definition** from a **pointer** (an id-only `<PbrMaterial>` must be resolved against the submitted set, then the live registry, before its channels can be judged). V15 counts every channel with a `Path` as one bindless slot |

### KSA.PowerConsumer
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `LightIsActive : bool` (field) | direct API | `KSA/PowerConsumer.cs:28` | zippo, red-alert, its-so-shiny | `ZippoSubmod.cs:161`; `LightActions.cs:42`; `ShinyPixelCell.cs:24,27` | OK | on/off toggle; rev-4681 electrical refactor didn't touch it |

### KSA.Program
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `OnDrawUiFrame(double)` | Harmony PREFIX (StarMap `[StarMapBeforeGui]`) | `KSA/Program.cs:2639` | shell + all submods (every mod) | `unscience/Mod.cs:122` | OK | StarMap-owned string hook; drives per-frame `Update`/drain |
| `OnDrawUiViewports(double)` | Harmony POSTFIX (StarMap `[StarMapAfterGui]`) | `KSA/Program.cs:2666` | shell + all submods | `unscience/Mod.cs:135` | OK | StarMap-owned string hook |
| `OnFrame(double,double)` | Harmony POSTFIX (StarMap `[StarMapAfterOnFrame]`) | `KSA/Program.cs:1986` | (available; **not** used by supermod shell) | — | OK | StarMap dispatch only |
| `OnDrawUiConsole(double)` (`private void`) | Harmony PREFIX (**string** `"OnDrawUiConsole"`) | `KSA/Program.cs:2880` @5348; called unconditionally `:2103` | unscience shell via `HiddenUiFrameHook` | `ksa-abstractions.lib/HiddenUiFrameHook.cs:28,44,47`; `unscience/Mod.cs:116-117`; `unscience/Patcher.cs:49,99` | OK @5348 | **Hidden-HUD fallback.** The two StarMap GUI targets above live inside `if (DrawUI)` in `OnFrame` (`:2093-2101`), so on F2 they are skipped and no StarMap GUI hook fires. This prefix replays `Mod.UpdateSubmods`/`UpdateWelds` at the same frame phase only while `Program.DrawUI` is false. Phase contract: every frame, after the UI block, before `ImGui.Render()`. Fallback anchor if renamed: `DrawFps()` (`:3008`) |
| `DrawUI : static bool` (prop) | direct API | `KSA/Program.cs:504` | HiddenUiFrameHook | `HiddenUiFrameHook.cs:40,64` | OK @5348 | gate for the fallback; flipped by `InputAction.ToggleUi` = F2 (`KSA/Input.cs:297`, handled `Program.cs:1694`) |
| `DrawProgramMenusHook() : void` (empty modding hook) | Harmony post | `KSA/Program.cs:3736` (cited `:3391` earlier) | unscience (MenuBarPatch), dont-stifle-me standalone (MenuBarPatch) | `unscience/MenuBarPatch.cs:8`; `dont-stifle-me/MenuBarPatch.cs:15` | OK | game ships as deliberate no-op; dont-stifle-me draws a `BeginMenu("Don't Stifle Me")` here |
| `ControlledVehicle : static Vehicle?` (field) | direct API | `KSA/Program.cs:254` | VehicleProvider (→ average-twr, geeforce, kitten-animations, unladen-swallow) | `VehicleProvider.cs:11` | OK | |
| `ConsoleWindow : static ConsoleWindow` (field) | direct API | `KSA/Program.cs:246` | HotkeyGuard (→ all mods) | `HotkeyGuard.cs:38` | OK | `.IsOpen` guard (Brutal type — see section 3 Brutal) |
| `Editor : static VehicleEditor?` (field) | direct API | `KSA/Program.cs:202` | IvaForceRender, kitchen-sink, humble-arteest (VehiclePaint), parts-now | `IvaForceRender.cs:100`; `KitchenSinkLib.cs:56`; `PaintTargets.cs`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs:98`, `RuntimeModUnloader.cs:110` | OK | editor-only branch; humble uses it to pick flight vs editor paint targets. parts-now uses it for the unload safety gate and to clear the hover preview before a purge. Disposed+nulled in `Program.PrepareFrame` |
| `ThumbnailViewport : static IViewport` (a `PartThumbnailViewport` from `ViewportRegistry.CreatePartThumbnailViewport(_renderer, ViewportOptionFlags.RenderPartModels, sampler)`; throws until built) | direct (render) | `KSA/Program.cs:497,949` | parts-now | `Runtime/PartThumbnailGenerator.cs:141` | OK | dedicated offscreen thumbnail viewport — no camera save/restore, no resize, no `UpdateShaderData`. Shared with the part browser's hover preview (see `ThumbnailDynamic`) |
| `BindlessTextures : BindlessTextureLibrary` (public field) | direct API | `KSA/Program.cs:88,850` | parts-now, graffiti | `Runtime/BundleValidatorRulesIdentity.cs:222`; `Ui/StatusPanel.cs:202-210`; `graffiti.lib/DecalRenderer.cs`, `DecalTextures.cs` | OK | V15 texture-budget rule + the Status panel gauge; graffiti allocates/frees decal slots and binds the table as set 2. Constructed with `maxTextures = 1024` |
| `{EditorFlag : static bool, OffscreenTarget : static RenderTarget, RenderedViewport : static IViewport / MainViewport : static IGameViewport (`.ShaderSlot` feeds `GlobalShaderBindings.DynamicOffset`), SetViewport(CommandBuffer) : static, PointClampedSampler : static VkSampler, Instance.ResourceFrameIndex : int, Instance.ColorFormat : readonly VkFormat}` | direct API (render seam gates + pass state) | `KSA/Program.cs:224,457,491,485,4293,469,218,222` | graffiti | `graffiti.lib/GraffitiPatches.cs`, `DecalRenderer.cs` | OK @5348 | the decal pass's editor/main-viewport identity checks + GridPass-style pass state (viewport, depth sampler, frame-ring slot, colour format). See `scope/decals.md` #2 |
| `IsMainThread() : static bool` | behavior dependency | `KSA/Program.cs:520` | parts-now | (via `Loading.OnFrame`, `KSA/Loading.cs:92`) | OK | 🔶 **U7** — see `KSA.Loading` |
| `RendererRebuildNeeded : static bool` (field) | direct API | `KSA/Program.cs:431` (consumed `PrepareFrame` :2096) | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | `VehiclePaintShaders.cs`; `CanopyProjectionShaders.cs` | OK | game's **deferred** full-renderer rebuild flag — the safe way for a mod to force shader/pipeline recompilation (same path a graphics-setting change takes) |
| `MainViewport : static IGameViewport { get; }` (= `ViewportRegistry.MainViewport`) | direct API | `KSA/Program.cs:485` | IvaForceRender, kitchen-sink, graffiti, hot-pursuit | `IvaForceRender.cs`; `DecalPicker.cs`; `DecalRenderer.cs`; `hot-pursuit.lib/HotPursuitPicker.cs`, `HotPursuitPose.cs` | OK @5402 | Hot Pursuit uses it only as the reference ego frame/picking viewport, never as the output target. |
| `FindNearbyCelestial(Camera) : static Celestial?` | direct API | `KSA/Program.cs:5037` | hot-pursuit | `hot-pursuit.lib/HotPursuitCelestialState.cs` | OK @5402 | equivalent nearby-body lookup used after the mounted secondary camera writes `PositionEcl`; KSA's private `OnFrameCelestials` does not run for this camera. |
| `RenderViewport(CommandBuffer,IViewport,int) : private` | render-pass behavior | `KSA/Program.cs:4313` | hot-pursuit | stock secondary viewport path | **LIMITATION @5402** | Secondary rendering runs stars, distant spheres, vehicle/part passes, and the stock translucent path, but omits `ParticleSystem`, `VolumetricExhaustRenderer`, the main planet/ocean/cloud pipeline, part-glass, and overall-bloom passes. Engine plumes and generic particles cannot be enabled by Hot Pursuit; those game-owned passes bind main-camera targets/resources. |
| `GetMainCamera() : static Camera` | direct API | `KSA/Program.cs:632` | glass | `glass.lib/FovController.cs` | OK @5402 | Glass is explicitly main-camera scoped so it does not overwrite Hot Pursuit's independent FOVs. |
| `GetRenderCamera() : Camera` (= `RenderedViewport.GetCamera()`) | direct (render) | `KSA/Program.cs:642`, `RenderedViewport` `:491` | thug-life | `ThugLifeQuadRenderer.cs:252` | OK | **replaced `GetMainCamera()` (`:584`)** — `RenderMainPass` runs per visible viewport (main + both crew-portrait viewports), and ego space is camera-relative, so the main camera mis-transformed the portrait passes |
| `GetRenderer() : Renderer` (→ `.Device`/`.Allocator`/`.Graphics`) | direct (render) | `KSA/Program.cs:486` (cited `:450` at the 4750 baseline) | thug-life, mesh-deform, parts-now | `ThugLifeRenderManager.cs:81`; `MeshDeformShaders.cs:39`; `parts-now.lib/Runtime/RuntimeModLoaderGpuStates.cs:85`, `PartThumbnailGenerator.cs:129`, `RuntimeModUnloader.cs:123` | OK | Vulkan device. humble-arteest no longer needs it — the patched `FromFile` receives the device as an argument |
| `OffscreenTarget : RenderTarget` (→ `.SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)`) | direct (render-pass) | `KSA/Program.cs:457` | thug-life | `ThugLifeQuadRenderer.cs:152` | OK | replaced `OffScreenPass`/`RenderPassState` @5261 (dynamic rendering). ⚠ **null until `BuildRenderTargets()` (`Program.cs:970` @5402), which runs after `ModLibrary.LoadAll()` (`:942`) — i.e. after `[StarMapAllModsLoaded]`; the mod's pipeline build is lazy for exactly this reason** |
| `SetViewport(CommandBuffer)` | direct (render) | `KSA/Program.cs:4293` | thug-life | `ThugLifeQuadRenderer.cs:264` | OK | sizes to `RenderedViewport` |
| `GetPlayerDeltaTime() : static double` | direct API | `KSA/Program.cs:4467` | garrys-torch | `WeldEngine.cs:119` | OK | fed into `GetJobSimStep` |
| `Instance : static (singleton)` | reflection (private) | `KSA/Program.cs:371` | doh, humble-arteest (KittenColor) | `MaterialSystemAccessor.cs:53,56`; `KittenColor.cs:55-73` | OK | render-systems root |
| `MaterialSystem : GpuMaterialSystem` (field) | reflection-field | `KSA/Program.cs:94` | doh, humble-arteest | `MaterialSystemAccessor.cs:63`; `KittenColor.cs:55-73` | OK | |
| `SuperMeshRenderSystem` (field) → `.TextureSystem : GpuTextureSystem` | reflection-field | `KSA/Program.cs:96`; `KSA/SuperMeshRenderSystem.cs:39` | doh | `MaterialSystemAccessor.cs:84,87,90` | OK | |
| `CharacterRenderSystem` (field) | reflection-field | `KSA/Program.cs` (`KSA/CharacterRenderSystem.cs:7`) | doh | `MaterialFactory.cs:504-525` | OK | |
| `LinearClampedSampler : static VkSampler` | direct (render) | `KSA/Program.cs:427` | parts-now | `parts-now.lib/Ui/ResultsPanel.cs:133` | OK | passed to `ThumbnailReference.GetOrCreateImGuiTexture` for the results-table thumbnails |
| `Instance : public static Program { get; private set; }` | direct API (typed) | `KSA/Program.cs:405` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs:221`; `Ui/StatusPanel.cs:201` | OK | same singleton doh/humble-arteest reach by reflection (row above, cited `:371` at the 4750 baseline); the **getter is public**, so parts-now reads it typed, purely to reach `BindlessTextures` |

### KSA.RocketCore
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RocketCore.ResourceManager` | direct API (debug) | `KSA/RocketCore.cs:14` | blinky | `BlinkySubmod.cs:612-618` | OK | diagnose button only |

### KSA.SerializedCollection<T>
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GetList() : List<T>` | reflection-method (string "GetList") | `KSA/SerializedCollection.cs:42` | doh | `KittenSpawner.cs:347` | OK | on `ModLibrary.AllParts`/`AllCharacters` |
| `Find(KeyHash) : T` | reflection-method | `KSA/SerializedCollection.cs:37` | doh | `KittenSpawner.cs:329,333` | OK | `"KittenBackPackPart"` |
| `GetList()` / `Find(KeyHash)` (typed, via `GameRegistry`) | direct API | `KSA/SerializedCollection.cs:42,37` | parts-now | `Runtime/GameRegistry.cs:152,170-188`; `RuntimeModLoaderDeltas.cs:30-35` | OK | `GetList()` hands back the **live** backing list, which is what makes `.Remove(item)` a real unregister |
| `_collection : private readonly ConcurrentDictionary<KeyHash,T>` | reflection-field (string "_collection", per closed generic) | `KSA/SerializedCollection.cs:14` | parts-now | `Runtime/GameRegistry.cs:356-357`, used `:154-165` | OK | 🔶 **U4.** `SerializedCollection<T>` exposes **no removal API** (`Register`/`Find`/`GetList` only), so unload and reload exist only through this field: removing from the list alone would leave `Find` resolving a purged item. **If KSA ever adds a real removal API, replace the reflection with it.** parts-now deliberately does not take the private `Lock` (`:12`) — game-thread-only access is what makes that safe |
| `Register(T) : bool` (returns **false** on duplicate `KeyHash`) | behavior dependency | `KSA/SerializedCollection.cs:20,28` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs:121-197` (V3/V4/V14) | OK | every caller reads `false` as "this is a reference to the existing entry", so a colliding Part is silently dropped and a colliding file's `Load()` never reads from disk. This is also why a reload **must** purge first (C5) |

### KSA.SerializedId
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Id : string { get; set; }` (base of PartTemplate, GaugeCanvas) | direct API | `KSA/SerializedId.cs:13` |  | `LayoutManager.cs:119`; `GarrysTorchSubmod.cs:188` | OK | layout key / template id |
| `Mod : Mod? { get; private set; }` | direct API | `KSA/SerializedId.cs:16` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs:186,252,263-274` | OK | names the **owning mod** in V3/V14 collision messages, and exempts ids owned by the mod currently being reloaded |

### KSA.SimStep
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Universe.GetJobSimStep(double) : SimStep` → `SimStep.NextTime : SimTime` | direct API | `KSA/Universe.cs:2188` | garrys-torch | `WeldEngine.cs:119` | OK | tick-end time for new orbit state time |
| `SimStep` (param of `ExecuteNextVehicleSolvers`) | Harmony arg type | `KSA/Universe.cs:1775` | eternal-flame, kitchen-sink, kiwis-marbles | (solver prefixes) | OK | prefixes ignore it (parameterless / by-name `dtPlayer` only) |

### KSA.SimTime
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SimTime` (`readonly struct`) | direct API | `KSA/SimTime.cs:6` | SimTimeProvider (→ geeforce, steely-eyed, kiwis-marbles, garrys-torch, doh) | `SimTimeProvider.cs:9` | OK | |
| `Seconds() : double` (instance) | direct API | `KSA/SimTime.cs:67` | geeforce, steely-eyed | `GeeForceSubmod.cs:34`; `MonitoringLoop.cs:45` | OK | timestamps sample |

### KSA.Situation / KSA.SituationEx
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle.Situation : Situation => _props.Situation` | direct API | `KSA/Vehicle.cs:494` | steely-eyed | `VehicleTelemetry.cs:85` | OK | |
| `Situation` (`enum : byte`, 8 states) via `.ToString()` string-compare | enum (string-compare) | `KSA/Situation.cs:3` | steely-eyed | `VehicleTelemetry.cs:87-89`; `EventDetector.cs:62` | OK | byte-identical; rename breaks silently (rev 4704 aerostat = value reuse, no new names) |
| `SituationEx.HasAnyContact(this Situation) : bool` | direct API (ext) | `KSA/SituationEx.cs:18` | steely-eyed | `VehicleTelemetry.cs:86` | OK | liftoff/landing logic |

### KSA.SolarPanel
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `SolarPanel` (type, `Get<SolarPanel>()` presence test) | direct API | `KSA/SolarPanel.cs:8` | red-alert | `ActionScanner.cs:51` | OK | rev-4681 added power internals; red-alert reads none |

### KSA.StaticMeshRenderable
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `MaterialIndices : protected int[]` | reflection-field | `KSA/StaticMeshRenderable.cs:31` | doh | `KittenSpawner.cs:388-408,523-537` | OK | helmet/visor/mmu mesh handle swap |

### KSA.SubstanceLibrary
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `TryGetCombustionProcess(KeyHash)` + `KeyHash.Make` | direct API | `KSA/SubstanceLibrary.cs:122` | doh | `KittenSpawner.cs:281` | OK | `"MMH_NTO_1.6"` |
| `AllReactions() : static ReadOnlySpan<Reaction>` · `TryGetReaction(KeyHash) : static Reaction?` | direct API (read-only) | `KSA/SubstanceLibrary.cs:62,218` | doh, parts-now | `Runtime/BundleValidatorRulesReferences.cs:194,205` | OK | parts-now validation rule V10 — `<Reaction Id>` must already exist (the library is populated once at boot with `Dictionary.Add` and cannot take runtime entries). Empty library → warning, not error |
| `KeyHash.Make(ReadOnlySpan<char>) : static KeyHash` | direct API | `KSA/KeyHash.cs:15` | doh, parts-now | `Runtime/GameRegistry.cs:170-188`; `RuntimeModLoaderDeltas.cs:262`; `BundleValidatorRulesReferences.cs:205,209` | OK | **lowercases its input** → every parts-now id index is `OrdinalIgnoreCase` to match |

### KSA.SuperMeshRenderSystem
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderMainPass(CommandBuffer) ` | Harmony post (render-pass) | `KSA/SuperMeshRenderSystem.cs:329` | thug-life | `thug-life.lib/ThugLifeRenderPatches.cs:19-21,44` | OK | records quad draws into offscreen MSAA pass; called 3× from Program |

### KSA.Tank
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Tank` (type, `Get<Tank>()`) | direct API | `KSA/Tank.cs` | blinky | `LcdGridBuilder.cs:469` | OK | |
| `ConfigureFor(IReactantMix)` | direct API | `KSA/Tank.cs:382` | doh | `KittenSpawner.cs:278-289` | OK | backpack propellant |

### KSA.Transform3D
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `PositionEcl : double3 { get; set; }` (virtual) | direct API (write) | `KSA/Transform3D.cs:15` | camera-controller-override | `KeyframeSequencePlayer.cs:450,473` | OK | mutated to move camera (dead until `___Transform` fixed); `Camera` overrides at `Camera.cs:94` |
| `LocalRotation : doubleQuat` (field) | direct API (write) | `KSA/Transform3D.cs:13` | camera-controller-override | `KeyframeSequencePlayer.cs:451,476` | OK | mutated to rotate camera (dead until fixed) |

### KSA.Universe
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ExecuteNextVehicleSolvers(double dtPlayer, SimStep simStep) : static void` | Harmony pre (Priority.First) | `KSA/Universe.cs:1775` | eternal-flame, kitchen-sink, kiwis-marbles | `unscience/Patcher.cs` (`EternalFlamePatches`, `KiwisMarblesPatches`); `kitchen-sink/Patcher.cs:56`; `kiwis-marbles.lib/KiwisMarblesPatches.cs` | OK | single overload → by-name `nameof`/`dtPlayer` resolution safe; kiwis-marbles depends on `PrepareFrame` ordering (Apply*Solvers before, ExecuteNextOrbitSolvers after) |
| `CurrentSystem : static CelestialSystem? { get; private set; }` | direct API | `KSA/Universe.cs:92` | VehicleProvider/CelestialProvider (→ ~all feature mods) | `VehicleProvider.cs:15`; `CelestialProvider.cs:11` | OK | enumeration root |
| `GetElapsedSimTime() : static SimTime` | direct API | `KSA/Universe.cs:1991` | SimTimeProvider (→ geeforce, steely-eyed, kiwis-marbles, doh) | `SimTimeProvider.cs:9` | OK | |
| `GetJobSimStep(double) : SimStep` | direct API | `KSA/Universe.cs:2188` | garrys-torch | `WeldEngine.cs:119` | OK | (see KSA.SimStep) |

### KSA.Vehicle
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Vehicle` (type) | direct API | `KSA/Vehicle.cs:28` | VehicleProvider (→ ~all), unladen-swallow | `VehicleProvider.cs:11` | OK | `OfType<Vehicle>()` |
| `Parts : PartTree` (field) | direct API | `KSA/Vehicle.cs:264` | PartHelpers (→ many), eternal-flame, blinky, its-so-shiny, kitchen-sink, mesh-deform | `PartHelpers.cs:13`; `EternalFlameLib.cs:128`; `LcdGridBuilder.cs:37` | OK | get+set (blinky swaps tree) |
| `Id` (inherited Astronomical.Id) | direct API | `KSA/Astronomical.cs:85` | (see KSA.Astronomical) | — | OK | |
| `RefillConsumables() : void` | direct API | `KSA/Vehicle.cs:2300` | eternal-flame | `EternalFlameLib.cs:80` | OK | fuel/resource refill |
| `AddVolumetricExhaustInstances(Camera, Viewport, VolumetricExhaustRenderer, double frameDeltaTime) : void` | **Harmony postfix** `(Vehicle __instance, Camera camera, VolumetricExhaustRenderer renderer, double frameDeltaTime)` | `KSA/Vehicle.cs:5303` | pyro | `pyro.lib/PyroPatches.cs:16,35` | OK @5348 | per-visible-vehicle exhaust submission (`Program.OnPreRender`); pyro adds its plumes to the same batch. Resolved via `nameof` (typed) |
| `PosAsmbToBody(double3) : double3` · `Body2Cce : doubleQuat` | direct API | `KSA/Vehicle.cs:1218,374` | pyro | `pyro.lib/PlumeEmitter.cs:73-74` | OK @5348 | same chain as `RocketNozzleState.AddExhaustInstance` |
| `GetMatrixAsmb2Ego(Camera) : double4x4` · `BoundingSphereRadiusBody : double` · `static ComputeEnu2Cce(double3, doubleQuat) : doubleQuat?` | direct API | `KSA/Vehicle.cs` | graffiti, hot-pursuit | `graffiti.lib/DecalPicker.cs`, `DecalAnchors.cs`; `hot-pursuit.lib/HotPursuitPicker.cs`, `HotPursuitPose.cs` | OK @5402 | raycast broad-phase + sub-part transform root; ENU helper is graffiti-only |
| `Teleport(Orbit?, doubleQuat?, double3?) : void` | direct API | `KSA/Vehicle.cs:1594` | garrys-torch, doh (KittenEva) | `WeldEngine.cs:129`; `KittenSpawner.cs:171` | OK | core mutation; nullable params |
| `UpdatePerFrameData() : override void` | direct API | `KSA/Vehicle.cs:1972` | garrys-torch, doh | `WeldEngine.cs:130`; `KittenSpawner.cs:175` | OK | refresh caches post-teleport |
| `UpdateVehicleConfiguration() : void` | direct API | `KSA/Vehicle.cs:1263` | blinky, its-so-shiny | `LcdGridBuilder.cs:149`; `ShinyGridBuilder.cs:98` | OK | |
| `UpdateAfterPartTreeModification() : void` | direct API | `KSA/Vehicle.cs:1277` | kitchen-sink | `FlexoPartTest.cs:320` | OK | recompute mass/aero/CoM |
| `Parent : IParentBody => Orbit.Parent` | direct API | `KSA/Vehicle.cs:332` | garrys-torch, average-twr (dead), steely-eyed, doh | `WeldEngine.cs:19`; `VehicleTelemetry.cs:44`; `KittenSpawner.cs:230` | OK | |
| `Orbit : Orbit => Patch.Orbit` | direct API | `KSA/Vehicle.cs:330` | garrys-torch, steely-eyed | `WeldEngine.cs:126`; `VehicleTelemetry.cs:64` | OK | |
| `GetPositionCci() : double3` | direct API | `KSA/Vehicle.cs:1949` | garrys-torch | `WeldEngine.cs:28` | OK | (concrete; cf. `IOrbiter.GetPositionCci`) |
| `GetVelocityCci() : double3` | direct API | `KSA/Vehicle.cs:1897` | garrys-torch | `WeldEngine.cs:29` | OK | |
| `GetBody2Cci() : doubleQuat` | direct API | `KSA/Vehicle.cs:2242` | garrys-torch | `WeldEngine.cs:30,90` | OK | |
| `GetAsmb2Cci() : doubleQuat` | direct API | `KSA/Vehicle.cs:2247` | doh | `KittenSpawner.cs:231` | OK | spawn positioning |
| `CenterOfMassAsmb : double3` (field) | direct API | `KSA/Vehicle.cs:510` | garrys-torch | `WeldEngine.cs:58` | OK | part-anchor offset base |
| `BodyRates : double3` (field) | direct API | `KSA/Vehicle.cs:458` | garrys-torch, doh | `WeldEngine.cs:85`; `KittenSpawner.cs:239-242` | OK | NaN-guarded by mod |
| `Body2Cce : doubleQuat` (field) | direct API | `KSA/Vehicle.cs:423` | i-feel-seen, doh | `IFeelSeenPatches.cs:59`; `KittenSpawner.cs:239-242` | OK | |
| `Asmb2Ego : doubleQuat` | direct (render) | `KSA/Vehicle.cs` | thug-life | `ThugLifeQuadRenderer.cs:283` | OK | |
| `GetMatrixAsmb2Ego(Camera) : double4x4` | direct API | `KSA/Vehicle.cs:833` | i-feel-seen, thug-life | `IFeelSeenPatches.cs:69`; `ThugLifeQuadRenderer.cs:281` | OK | |
| `GetWorldMatrix(Camera) : float4x4?` | Harmony pre + reflection-method (string) | `KSA/Vehicle.cs:2772` | i-feel-seen | `IFeelSeenPatches.cs:27,30` | OK | string-resolved; non-virtual |
| `UpdateRenderData(Viewport, int) : virtual void` | Harmony pre + reflection-method (string) | `KSA/Vehicle.cs:2785` | i-feel-seen | `IFeelSeenPatches.cs:28,31` | OK | string-resolved; `KittenEva` overrides (`KittenEva.cs:62`) |
| `IsEditedVehicle : bool` | direct API | `KSA/Vehicle.cs:356` | i-feel-seen | `IFeelSeenPatches.cs:70` | OK | |
| `NavBallData` (see KSA.NavBallData) | direct API | `KSA/Vehicle.cs:528` | average-twr | — | OK | |
| `FlightComputer` (see KSA.FlightComputer) | direct API | `KSA/Vehicle.cs:415` | average-twr, blinky (debug) | — | OK | |
| `TotalMass : float => _props.TotalMassPropsAsmb.Props.Mass` | direct API | `KSA/Vehicle.cs:512` | average-twr, steely-eyed | `TwrDataReader.cs:18`; `VehicleTelemetry.cs:76` | OK | |
| `InertMass : float` | direct API | `KSA/Vehicle.cs:514` | steely-eyed | `VehicleTelemetry.cs:77` | OK | |
| `PropellantMass : float` | direct API | `KSA/Vehicle.cs:516` | steely-eyed | `VehicleTelemetry.cs:78` | OK | |
| `AccelerationBody : double3 => KinematicMeasurements.AccelerationBody` | direct API | `KSA/Vehicle.cs:518` | geeforce, steely-eyed | `GForceRecorder.cs:96`; `VehicleTelemetry.cs:81` | OK | |
| `OrbitalSpeed : double => GetVelocityCci().Length()` | direct API | `KSA/Vehicle.cs:532` | steely-eyed | `VehicleTelemetry.cs:59` | OK | |
| `GetSurfaceSpeed() : double` | direct API | `KSA/Vehicle.cs:2169` | steely-eyed | `VehicleTelemetry.cs:60` | OK | |
| `GetInertialSpeed() : double` | direct API | `KSA/Vehicle.cs:2164` | steely-eyed | `VehicleTelemetry.cs:61` | OK | |
| `GetBarometricAltitude() : double` | direct API | `KSA/Vehicle.cs:2175` | steely-eyed | `VehicleTelemetry.cs:55` | OK | |
| `GetRadarAltitude() : double` | direct API | `KSA/Vehicle.cs:2180` | steely-eyed | `VehicleTelemetry.cs:56` | OK | |
| `GetPositionEcl() : double3` | direct API | `KSA/Vehicle.cs:613` | steely-eyed | `VehicleTelemetry.cs:111` | OK | (cf. `IPosition.GetPositionEcl`) |
| `GetManualThrottle()` | direct API (debug) | `KSA/Vehicle.cs:822` | blinky | `BlinkySubmod.cs:586-587` | OK | diagnose button only |
| `SetEnum(Enum?) : void` | direct API | `KSA/Vehicle.cs:4838` | unladen-swallow, blinky | `ActionIgnite.cs:34`; `BlinkyGridManager.cs:258` | OK | `VehicleEngine` branch → private `SetAction` (`Vehicle.cs:4912`, ungated by `IsControllable`) |
| `Dispose() : void` | direct API | `KSA/Vehicle.cs` | doh | `KittenSpawner.cs:68` | OK | despawn |
| `ShowOrbit` (as IOrbiter) | direct API | `KSA/Vehicle.cs` | marque | `MarqueLib.cs:84` | OK | (see KSA.IOrbiter.ShowOrbit) |
| `IsControllable : virtual bool` (rev 4699) | direct API | `KSA/Vehicle.cs:526` | (informational — not consumed) | — | ADDITIVE | new; gates control on a Control Module. RPC ignite path is NOT gated by it |

### KSA.VehicleEditingSpace
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parts : PartTree?` (field) | direct API | `KSA/VehicleEditingSpace.cs:16` (cited `:14` at the 4750 baseline) | kitchen-sink | `KitchenSinkLib.cs:57,59` | OK | null-guarded |
| `AllParts : ReadOnlySpan<Part> => Parts?.Parts ?? default` | direct API | `KSA/VehicleEditingSpace.cs:32` | parts-now | `Runtime/RuntimeModUnloadGate.cs:110` | OK | null-safe by construction — an empty editor yields an empty span, not an NRE |

### KSA.VehicleEditor
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `EditingSpace : VehicleEditingSpace` (field) | direct API | `KSA/VehicleEditor.cs:407` (cited `:334` at the 4750 baseline) | kitchen-sink, parts-now | `KitchenSinkLib.cs:57`; `parts-now.lib/Runtime/RuntimeModUnloadGate.cs:105` | OK | |
| `RegisterTag` (tags registered from `CoreEditorTagsGameData.xml`, rev 4731/4741) | direct API | `KSA/PartTemplate.cs:127-129` | parts-now (V7) | — | CHANGED | tag categories drifted ("Interstage" removed; "Stages"→"Resource Groups"). `MarkEditorTagDefinitionsLoaded()` locks the list at boot, after which `RegisterTag` logs a warning and adds nothing — which is exactly what parts-now rule **V7** rejects up front |
| `ResetPartDiameterCache() : public static void` → clears `PartWindow._diameterCache` | direct API | `KSA/VehicleEditor.cs:6187,55` | parts-now | `parts-now.lib/Runtime/EditorRefresh.cs:41` | OK | the **only** editor nudge a runtime load/purge needs: `PartWindow.OnDrawUi` re-reads `ModLibrary.AllParts.GetList()` every frame, but the diameter cache is built lazily and reused |
| `UnattachedPartTrees : List<PartTree>` (field) | direct API | `KSA/VehicleEditor.cs:529` | parts-now | `Runtime/RuntimeModUnloadGate.cs:119-124` | OK | loose part trees in the open editor also block an unload |
| `DynamicThumbnail : ThumbnailDynamic?` (field) | direct API | `KSA/VehicleEditor.cs:547` | parts-now | `Runtime/RuntimeModUnloader.cs:110-116` | OK | 🔶 **U6** — cleared with `SetSelectedPart(null)` as purge step 0; see `KSA.Rendering.Thumbnails` |
| `_editorTagLookup : private static Dictionary<uint,string>` | reflection-field (string) | `KSA/VehicleEditor.cs:399` | parts-now | `Runtime/GameRegistry.cs:320` | OK | **degraded, not fatal** — V7 falls back to the six built-in tags + `AllEditorTagDefinitions` ids |
| `ScaleBoundsFor(Part) : private static (double Min, double Max)` | Harmony postfix (by-name) | `KSA/VehicleEditor.cs:3877` | dont-stifle-me | `EditorScalePatches.cs:56,89` | OK (**new @5348**) | rewrites `__result` to `(1e-6, +inf)` when clamp removal is on; the only place the 0.5x–2x clamp is expressed |
| `UpdateSelectedScale(ref readonly double4x4, Viewport) : private void` | Harmony prefix (by-name) | `KSA/VehicleEditor.cs:3841` | dont-stifle-me | `EditorScalePatches.cs:58,124` | OK (**new @5348**) | skipped (returns false) when per-axis scaling is on; prefix binds `matrixVehicleAsmb2Ego` by name |
| `UpdateScaleGizmo(ref readonly double4x4, doubleQuat, Viewport, double) : public void` | Harmony postfix (by-name) | `KSA/VehicleEditor.cs:3614` | dont-stifle-me | `EditorScalePatches.cs:60,113` | OK | per-frame drag-session reset on `!GizmoGrabbed` |
| `QuantizeScale(Part, double rawScale) : private static double` | Harmony prefix (by-name) + `AccessTools.MethodDelegate` | `KSA/VehicleEditor.cs:3907` | dont-stifle-me | `EditorScalePatches.cs:48,52,62,103` | OK (**new @5348**) | prefix bypasses 0.25 m snapping when `Snap` is off (`rawScale` bound by name); delegate is what the per-axis drag calls |
| `ForEachPartWithSymmetry(Part, Action<Part>) : private static void` | reflection → `AccessTools.MethodDelegate` | `KSA/VehicleEditor.cs:3881` | dont-stifle-me | `EditorScalePatches.cs:50,54` | OK (**new @5348**) | reused so per-axis drags propagate to symmetry siblings like stock |
| `Selected`, `HighlightedGizmoSegmentIndex`, `ScaleGizmo`, `CursorPositionScreen{,LastFrame}`, `GizmoGrabbed` (public fields) | direct API | `KSA/VehicleEditor.cs:551,579,573,681,683,581` | dont-stifle-me | `PerAxisScaleDrag.cs:32-43` | OK | 🔶 segment index→axis `0/1/2 = X/Y/Z` invariant of `ScaleGizmo`'s 3-segment ctor (`:1179`) |
| `DrawParachuteSection(Part, ReadOnlySpan<Parachute>) : private void` | Harmony prefix (by-name) | `KSA/VehicleEditor.cs:1932` | dont-stifle-me | `EditorValueLimitPatches.cs:29-35,73-83` | OK (**new consumer @5402**) | expands the selected subtree's runtime chute diameter bounds to 2–1000 m immediately before the stock slider reads them; patch binds only `part`, not the byref-like span |

### KSA.Parachute / ChuteTuning
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Parachute.SetDiameter(float) : void` | Harmony prefix (typed signature) | `KSA/Parachute.cs:369` | dont-stifle-me | `EditorValueLimitPatches.cs:31-37,85-92` | OK (**new consumer @5402**) | expands every chute on the part before stock `ChuteTuning.ClampDiameter`, keeping multi-canopy and editor-symmetry counterparts consistent |
| `Parachute.Tuning`; `ChuteTuning.{DiameterM, MinDiameterM, MaxDiameterM, ClampDiameter(float)}` | direct API | `KSA/Parachute.cs:140`; `KSA/ChuteTuning.cs:5,33-35,61` | dont-stifle-me | `EditorValueLimitPatches.cs:63-70,94-106` | OK (**new consumer @5402**) | original per-instance bounds are saved, changed to 2 / 1000 while enabled, and restored on toggle-off or unload; chosen diameter is preserved |

### KSA.VehicleEngine
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VehicleEngine : enum byte { MainIgnite, MainShutdown }` | enum | `KSA/VehicleEngine.cs:3-6` | unladen-swallow, blinky | `ActionIgnite.cs:34`; `BlinkyGridManager.cs:258` | OK | both members present |

### KSA.IViewport / KSA.IGameViewport (replaced `KSA.Viewport` @5402)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `Mode : CameraMode { get; }` (property) | direct API | `KSA/IViewport.cs:29` | IvaForceRender (kitchen-sink) | `IvaForceRender.cs:102` | OK (retyped) | vs `CameraMode.IVA`. Was a field on the old `Viewport` class |
| `GetCamera() : Camera` | direct API | `KSA/IViewport.cs:51` | i-feel-seen, parts-now, dont-stifle-me, hot-pursuit | `IFeelSeenPatches.cs`; `PartThumbnailGenerator.cs`; `PerAxisScaleDrag.cs`; `hot-pursuit.lib/HotPursuitPose.cs` | OK @5402 | Hot Pursuit uses main as its reference camera. |
| `Size : int2 { get; }` · `ShaderSlot : int { get; }` (was `Viewport.Index`) | direct API | `KSA/IViewport.cs:41,13` | parts-now, graffiti | `PartThumbnailGenerator.cs:515`; `DecalRenderer.cs:402`; `ShaderSlot` consumed indirectly by `ThumbnailDynamic.UpdateGlobalCameraData`'s camera-UBO slice | OK (retyped) | slots come from `ViewportRegistry`'s pool (max 8); the per-viewport UBOs are now sized for 8 slots (rev 5401 stride fix, `GlobalShaderBindings.cs:94,217`) |
| `IViewport` (param of `UpdateRenderData`/`AddInstance`/`OnFrame`/`UpdateSelectedScale`/render prefixes) | Harmony arg type | `KSA/IViewport.cs:9` | blinky, its-so-shiny, mesh-deform, i-feel-seen, humble-arteest, IvaForceRender, dont-stifle-me, camera-controller-override, pyro | (render/editor prefixes) | **CHANGED @5402** (fixed) | every game method that took `Viewport` now takes `IViewport`; all remain single overloads, so by-name `AccessTools.Method` still resolves. Prefixes that name the param declare `IViewport` |
| `ViewportOptionFlags.RenderPartModels` / `UseRaytracing` gates | new gating | `KSA/ViewportOptionFlags.cs`; `PartModel.cs:410-415` | IvaForceRender, humble-arteest, mesh-deform (postfix/prefix on `AddInstance`) | `IvaForceRender.cs:98-106` | ADDITIVE | `AddInstance` early-returns for viewports without `RenderPartModels`; every game-created viewport has it (`ViewportPresets.cs`), so dormant. Recommended mirror in IvaForceRender's postfix (open) |

### KSA.ViewportRegistry / KSA.IGameViewport / KSA.IViewportOwner
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `MAX_VIEWPORTS = 8`; allocation sealed after boot; four `ViewportType.Secondary` instances | direct API + standing capacity invariant | `KSA/ViewportRegistry.cs:18,72-94`; `KSA/Program.cs:948-962` | hot-pursuit | `hot-pursuit.lib/HotPursuitSubmod.cs` | OK @5402 | Main + thumbnail + 4 secondary + 2 portraits fill all slots. New allocation is impossible after `SealAllocation`; leases are the supported surface. |
| `AvailableSecondaryCount`; `TryClaimSecondaryViewport(IViewportOwner,out IGameViewport)`; `TryGetOwned`; `ReleaseSecondaryViewport(IViewportOwner)` | direct API | `KSA/ViewportRegistry.cs:54,181,213,246` | hot-pursuit | `HotPursuitSubmod.cs` | OK @5402 | Shared with Add Camera and docking cameras. Claim/release resets viewport defaults; closing stock `DrawImGui` releases the lease. |
| `IGameViewport.{BaseCamera,SetName,SetCameraMode,DrawImGui}` + inherited visibility/resize APIs | direct API | `KSA/IGameViewport.cs`; `KSA/IViewport.cs`; `KSA/GameViewport.cs:193` | hot-pursuit | `HotPursuitSubmod.cs`, `.Ui.cs` | OK @5402 | Uses KSA-owned targets, texture, window and renderer; no custom GPU resources. |
| `IViewportOwner` empty marker | direct API | `KSA/IViewportOwner.cs` | hot-pursuit | `HotPursuitCamera.cs` | OK @5402 | One stable owner object per camera entry keys the registry ownership map by reference identity. |

### KSA.FixedController
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5402 | Notes |
|---|---|---|---|---|---|---|
| `OnFrame(IViewport,double)` | Harmony selective prefix | `KSA/FixedController.cs:22` | hot-pursuit | `hot-pursuit.lib/HotPursuitPatches.cs` | OK @5402 | Keystone same-frame seam: returns false only for a currently owned viewport after applying/retaining its mounted pose; caller `GameViewport.OnFrame` then immediately runs `Camera.OnFrame`. |

### KSA.VolumetricExhaustTemplate
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Get(string id) : static VolumetricExhaustTemplate?` | direct API (read-only) | `KSA/VolumetricExhaustTemplate.cs:50` | parts-now, pyro | `Runtime/BundleValidatorRulesReferences.cs:213`; `pyro.lib/PlumeTemplates.cs:38,51` | OK | validation rule V10 — `<VolumetricExhaust Id>` must already resolve |
| `References : internal static SerializedCollection<VolumetricExhaustTemplate>` → `.GetList()` | **reflection-field (INTERNAL, string)** | `KSA/VolumetricExhaustTemplate.cs:38` | pyro | `pyro.lib/PlumeTemplates.cs:46` | OK @5348 | lists template ids for the combos; **falls back to the 7 stock ids** if missing |
| `Absorption` / `Emission` / `Noise` / `LengthWeights` / `Quality` (fields) + their `DoubleReference.Value`, `BoolReference.Value`, `ColorGradient.Color0..3 : ColorRgbReference`, `Flow.MachDiamonds.*`, `Quality.VolumetricVesselShadows` | direct API (read **and write**) | `KSA/VolumetricExhaustTemplate.cs:12-27`; `KSA/Absorption.cs`, `Emission.cs`, `Noise.cs`, `LengthWeights.cs`, `Quality.cs`, `MachDiamonds.cs`, `ColorGradient.cs` | pyro | `pyro.lib/PyroSubmod.TemplateUi.cs`; `PlumeEmitter.cs:85`; `PlumePhysics.cs:102-105` | OK @5348 | shared-template editor (same writes as the game's `VolumetricExhaustRenderer.OnDrawUi`); GPU `ExhaustTemplateData` buffer is rebuilt from these **every frame** in `Render()` (`VolumetricExhaustRenderer.cs:1236-1243`) |

### KSA.VolumetricExhaustRenderer
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VolumetricExhaustRenderer` (type; Harmony arg) | Harmony arg type | `KSA/VolumetricExhaustRenderer.cs:20` | pyro | `PyroPatches.cs:36` | OK @5348 | lib references `Brutal.Vulkan*` + `BepuUtilities` so the type resolves |
| `AddInstance(float3 emitterPosition, float3 axis, VolumetricExhaustInstance, float throttle, float3 airVelocity, float airDensity) : float` | direct API | `KSA/VolumetricExhaustRenderer.cs:710` | pyro | `pyro.lib/PlumeEmitter.cs:76-78` (+ `ComputeAirState` `:87-98`) | **CHANGED @5402** (fixed) | **gained `airVelocity`/`airDensity` @5402** for atmospheric plume bend/fold (`ExhaustPlumeDeformation`, `:809-811`); pyro mirrors `Vehicle.AddVolumetricExhaustInstances` (`Vehicle.cs:5518-5525`). ⚠ **refraction regression @5402:** nothing sets `_hasRefractionInstances` any more (OLD `:960`), so the refraction pass never runs — game-side, needs live confirmation. Previous note: the game's own nozzle submission entry; reads `instance.ShaderData` + `LastPlumeData`, derives all plume geometry. **5348 delta already handled:** reads `PlumeData.ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity` |
| `Disabled : bool` | direct API | `KSA/VolumetricExhaustRenderer.cs:352` | pyro | `pyro.lib/PyroSubmod.cs:75` | OK @5348 | `_maxInstanceCount == 0` (exhausts off in settings) |

### KSA.VolumetricExhaustInstance / KSA.VolumetricExhaustReference / KSA.ExhaustInstance
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `VolumetricExhaustReference { Id }` + `Load() : void` + `Template` | direct API | `KSA/VolumetricExhaustReference.cs` | pyro | `pyro.lib/PlumeTemplates.cs:55-59` | OK @5348 | `Load()` resolves `_template` via `VolumetricExhaustTemplate.Get(Id)` — no reflection needed |
| `new VolumetricExhaustInstance(VolumetricExhaustReference)` · `Template` · `LastPlumeData` (public field) | direct API | `KSA/VolumetricExhaustInstance.cs:75` | pyro | `PlumeTemplates.cs:59`; `PlumeEmitter.cs:43` | OK @5348 | one per plume — owns the 4-slot startup/shutdown pulse tracker |
| `UpdateState(double simulationTime, bool isActive, double simulationDeltaTime, PlumeData) : bool` | direct API | `KSA/VolumetricExhaustInstance.cs:91` | pyro | `pyro.lib/PlumeEmitter.cs:56` | OK @5348 | false ⇒ fully shut down, skip submit. `isActive` = Enabled && Throttle>0 |
| `OnSettingsChanged() : void` | direct API | `KSA/VolumetricExhaustInstance.cs` | pyro | `pyro.lib/TemplateRefresher.cs:20,42` | OK @5348 | re-reads template into `_shaderData` after a Template Editor edit |
| `_shaderData : private ExhaustInstance` | **reflection-field (PRIVATE, string; `AccessTools.FieldRefAccess`)** | `KSA/VolumetricExhaustInstance.cs:48` | pyro | `pyro.lib/PlumeEmitter.cs:25,84-87` | OK @5348 | per-plume `absorptionDensity` / `refractionIntensity` overrides written before `AddInstance` copies the struct. **Gracefully disabled** (UI says so) if the field is gone |
| `ExhaustInstance.absorptionDensity` / `.refractionIntensity` (fields) | direct API (struct layout) | `KSA/ExhaustInstance.cs` | pyro | `PlumeEmitter.cs:86-87` | OK @5348 | ⚠ **layout drift** @5348: colours/noise/brightness moved OUT of this struct into `ExhaustTemplateData` (per-template buffer indexed by `templateIndex`) — that is why per-plume colour is not offered |
| `PlumeData` (struct, all `required` fields incl. **`ApparentExhaustVelocity`, `ThroatRadius`, `ThroatDensity`, `InletTemperature` — new @5348**) | direct API (object initializer) | `KSA/PlumeData.cs` | pyro | `pyro.lib/PlumePhysics.cs:70-92` | OK @5348 | a renamed/added `required` member is a **compile** break here (good — loud) |

### KSA.GasProperties / KSA.GasConditions / KSA.RocketDesign (plume maths)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `GasProperties { Gamma, SpecificGasConstant }` · `ComputeSpeedOfSound(float)` · `ComputeSupersonicExpansionPressureAngle(float,float)` · `ComputeSupersonicExpansionPressureMach(float,float)` · `ComputePrandtlMeyer(float)` | direct API | `KSA/GasProperties.cs` | pyro | `pyro.lib/PlumePhysics.cs:30-83` | OK @5348 | mirrors `RocketNozzle.UpdatePlumeData` (`KSA/RocketNozzle.cs`) |
| `GasConditions { Pressure, Temperature }` · `ComputeDensity(GasProperties)` | direct API | `KSA/GasConditions.cs` | pyro | `PlumePhysics.cs:37-42,89` | OK @5348 | pressures in **Pa** (game-internal unit) |
| `RocketDesign.SolveMachNumberFromAreaRatio(GasProperties, double) : static float` · `ComputeAreaRatioFromMachNumber(double, double) : static double` | direct API | `KSA/RocketDesign.cs:168,187` | pyro | `PlumePhysics.cs:33,61` | OK @5348 | exit Mach from (exit/throat)² ; Mach-disk area ratio |
| `Universe.GetElapsedSeconds()` · `Universe.GetSimulationSpeed()` | direct API | `KSA/Universe.cs:2054,1334` | pyro | `pyro.lib/PyroSubmod.cs:77-78` | OK @5348 | same time source as `RocketNozzleState.AddExhaustInstance` / `Vehicle.AddVolumetricExhaustInstances` |
| `PartTree.RocketNozzles.ModulesAndAllStates` (enumerator: `.FxState.VolumetricExhaust`, `.Module.RecomputeGasVisibilityDensity(in …)`) | direct API | `KSA/Vehicle.cs:5310` (game's own use); `KSA/RocketNozzle.cs:182` | pyro | `pyro.lib/TemplateRefresher.cs:36-43` | OK @5348 | pushes Template Editor edits to real engine nozzles (mirrors the debug editor's `changed` path); wrapped in try/catch |
| `ColorRgbReference(float3)` + `OnDataLoad(new Mod())` · `Value.AsFloat3` | direct API | `KSA/ColorRgbReference.cs:22,28,35` | pyro | `pyro.lib/PyroSubmod.TemplateUi.cs:123-127` | OK @5348 | identical to the game's editor colour write (`VolumetricExhaustRenderer.cs:2306-2311`) |

### KSA.XmlHelper
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `Serializers : public static Dictionary<Type, XmlSerializer>` → `[typeof(AssetBundle)]` | direct API | `KSA/XmlHelper.cs:13,46` | parts-now | `Runtime/BundleParser.cs:89-90,102` | OK | ⚠ **must** be the game's own serializer instance: it carries the `XmlAttributeOverrides` that map `<PartModel>`/`<Tank>`/`<Collider>`/`<Light>`… onto `PartTemplate.Components`. A hand-built `new XmlSerializer(typeof(AssetBundle))` silently drops every component. A missing entry is reported to the user, never thrown |

### KSA.Rendering (RenderTarget resolve seam — graffiti)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderTarget.ResolveAttachments(CommandBuffer inCmdBuffer) : void` | **Harmony postfix** `(RenderTarget __instance, CommandBuffer inCmdBuffer)` | `KSA.Rendering/RenderTarget.cs:315` | graffiti | `graffiti.lib/GraffitiPatches.cs` | OK @5348 | 🔶 graffiti's keystone seam: called unconditionally per viewport from `Program.RenderGame` (body MSAA-gated, postfix fires regardless) — the post-resolve window `GridPass` draws in. Resolved via `nameof`; param name `inCmdBuffer` is load-bearing for Harmony binding |
| `RenderTarget.{DepthImage, ColorImage : RenderImage?, Extent}` | direct API (render) | `KSA.Rendering/RenderTarget.cs:36,38,48` | graffiti | `graffiti.lib/DecalRenderer.cs` (`RecordPass`) | OK @5348 | resolved single-sample scene depth (reverse-Z, sampled per fragment) + the colour attachment the pass draws into |
| `BarrierBatch` (span ctor, `Add`, `SubmitAndFlush`) · `ImageBarrierInfo.Presets.{DepthSampledReadF, ColorAttachmentReadWrite}` | direct API (render) | `KSA.Rendering/BarrierBatch.cs`; `KSA.Rendering/ImageBarrierInfo.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | depth is moved to sampled-read and LEFT there, exactly as `GridPass` leaves it — the engine's tracked-state barriers tolerate that |
| `RenderingPresets.{ReverseZDepthStencil.NoDepthTest, BlendState.BlendColorAlphaOver}` | direct API (render) | `KSA/RenderingPresets.cs` | graffiti | `graffiti.lib/DecalRenderer.cs` | OK @5348 | no depth attachment at all — occlusion is per-fragment from the sampled depth; alpha-over composite |

### KSA.Rendering.Thumbnails (parts-now)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ThumbnailRenderer(Renderer)` ctor · `SIZE : static int` · `ColorFormat : static readonly VkFormat` · `{PerInstanceDataDescriptorSetLayout, PerDrawDataDescriptorSetLayout, Sampler}` · `RecordPartRender(CommandBuffer, ThumbnailReference, ThumbnailRenderResources, Viewport, string)` | render | `KSA.Rendering.Thumbnails/ThumbnailRenderer.cs:33,31,13,25,27,29,111` | parts-now | `parts-now.lib/Runtime/PartThumbnailGenerator.cs:131,281-286,339,350` | OK | the three layouts + sampler are forwarded straight from `PartModelRenderer.ColorData` (`ThumbnailRenderer.cs:37-39`), so a Part-color-pipeline change reaches parts-now here |
| `ThumbnailRenderResources(Renderer, DescriptorSetLayoutEx, DescriptorSetLayoutEx, VkSampler, int)` · `.DrawCommandVector.ElementCount` · `.UpdateDescriptorSets()` · `.AddDraw(float4x4, PartModel*Module.Template)` | render | `KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs:33,17,89,125,156` | parts-now | `PartThumbnailGenerator.cs:281-294,322` | OK | 🔶 **U3** — `AddDraw` reads `inTemplate.Material.{DiffuseReference,NormalReference,PBRMap}.BindlessHandle` **unguarded** (`:138-140`). A zero draw count is diagnosed *before* an image is created, since `RecordPartRender` is what transitions the image out of `VK_IMAGE_LAYOUT_UNDEFINED` |
| `ThumbnailPart(Camera inParent, PartInstance? = null)` · `.Children : List<ThumbnailPart>?` · `.Dispose()` | render | `KSA.Rendering.Thumbnails/ThumbnailPart.cs:72,22,78` | parts-now | `PartThumbnailGenerator.cs:143,270,456` | OK | root part parented to the thumbnail viewport's camera |
| `ThumbnailReference.{ImageView : ImageViewEx, ModelTransform : TransformReference?, GetOrCreateImGuiTexture(VkSampler), Dispose(), CreateImageView(...)}` | render | `KSA.Rendering.Thumbnails/ThumbnailReference.cs:16,13,36,54,31`; `KSA/TransformReference.cs:6` | parts-now | `PartThumbnailGenerator.cs:312,319`; `RuntimeModPurgeSteps.cs:43-46`; `Ui/ResultsPanel.cs:125,133` | OK | ⚠ **`ImageView.IsNull()` is a load-bearing guard.** A `<Thumbnail>` from XML has a `ModelTransform` but **never had `CreateImageView` called**, so `Dispose()` NREs on a null captured `Device`. parts-now also preserves a declared `ModelTransform` across regeneration, which the game's own `CreateThumbnailImage` (`ThumbnailCreator.cs:143`) drops |
| `ThumbnailCreator.{ResetRootPart, AddPart, MoveRootPart, CollectDraws, CreateThumbnailReference}` | render | `KSA.Rendering/ThumbnailCreator.cs:213,176,189,123,150` | parts-now | `PartThumbnailGenerator.cs:268-290,318` | OK | same framing as the game's own `PreparePartThumbnails` (`:54`). `AddPart` only walks `SubPartInstances`, so a SubPart collects no draws |
| `ThumbnailDynamic.{UpdateGlobalCameraData(Viewport, Camera) : static, SetSelectedPart(PartTemplate?), Render(double)}` | render | `KSA.Rendering.Thumbnails/ThumbnailDynamic.cs:272,89,167` | parts-now | `PartThumbnailGenerator.cs:195`; `RuntimeModUnloader.cs:116` | OK | 🔶 **U6.** `Render`'s `ResetRootPart`/`AddPart`/`MoveRootPart` block (`:184-186`) sits **outside** its try/catch (`:197`), and `AddPart` → `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` throws on a purged template — straight out of `Editor.OnPreRender` (`KSA/VehicleEditor.cs:4265` ← `KSA/Program.cs:2288`). Hence purge step 0 clears `SetSelectedPart(null)` first. parts-now and `ThumbnailDynamic` share the thumbnail viewport safely **only** because parts-now submits in `Program.OnDrawUiFrame` and `Render` runs later in the same frame |

### RenderCore.* (game-side render layer)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `RenderTechnique.CreateShaderStages(Device, Span<ShaderReference>, Span<VkSpecializationInfo>=default)` | direct (render) | `RenderCore/RenderTechnique.cs:37` | thug-life | `ThugLifeQuadRenderer.cs:117` | OK | |
| `ShaderModuleUtils.FromFile(Device, string filePath, out VkShaderStageFlags shaderStage, CompileOptions? options)` | **Harmony pre** (humble-arteest, free-fallin) + reflection-method (mesh-deform) | `RenderCore/ShaderModuleUtils.cs:115` | humble-arteest (VehiclePaint), free-fallin (Full Canopy), mesh-deform | `VehiclePaintPatches.cs` / `CanopyProjectionShaders.cs` (`FromFilePrefix`); `MeshDeformShaders.cs:373-397` | OK | Shared in-memory shader seam. Humble targets two part fragments; free-fallin targets `Model.vert`, `Model_Skinned.vert`, and `ModelPbr.frag`; both pass through all other paths and fall back to stock on error. Param names `device`/`filePath`/`shaderStage`/`options` are load-bearing for Harmony binding. |
| `KSA.Rendering.Utils.SetShaderFromMod(SimpleShaderStages, Device, string modId, bool useCustomOptions)` | **Harmony prefix** | `KSA.Rendering/Utils.cs:589` | free-fallin (Full Canopy) | `CanopyProjectionShaders.cs` (`SetShaderFromModPrefix`) | OK @5402 | Ordinary model rebuilds reuse cached `ShaderReference` modules and otherwise bypass `FromFile`. For the three projection shader ids only, the prefix sets `useCustomOptions=true`, routing compilation through `CompileVariantWithCustomOptions` → the `FromFile` transform. |
| `ShaderModuleUtils.FromString(Device, ReadOnlySpan<byte> shaderCode, VkShaderStageFlags, CompileOptions?, ReadOnlySpan<byte> debugName)` | direct (render) | `RenderCore/ShaderModuleUtils.cs:79` (was :77) | humble-arteest (VehiclePaint), free-fallin (Full Canopy), graffiti | `VehiclePaintPatches.cs` / `CanopyProjectionShaders.cs` (`FromFilePrefix`); `graffiti.lib/DecalRenderer.cs` (`Compile`) | OK | `debugName` becomes shaderc's input-file name → relative `#include` resolution. Shader patchers pass a NUL-terminated real path; graffiti passes a fake filename next to shipped `GridFrag` so `Common/*.glsl` resolves. |
| `BindlessTextureLibrary.{DescriptorSetLayout, DescriptorSet, AddTexture(VkImageView) : int, FreeTexture(int)}` | direct API (render) | `RenderCore.Systems/BindlessTextureLibrary.cs:38,155,198` | graffiti | `graffiti.lib/DecalRenderer.cs`, `DecalTextures.cs` | OK @5348 | decal-texture slots + set 2 of the decal pipeline. UpdateAfterBind\|PartiallyBound layout makes live slot writes legal; `FreeTexture` rewrites the slot to the empty texture, so only the image needs deferred destroy. Shares the same 1024-slot pool parts-now budgets (V15) |
| `TextureLoader.LoadFromMemory(bytes, FormatType.Png, LoadOptions)` · `TextureAsset(.LoadOptions(R8G8B8A8UNorm, KtxTranscodeFmt.Rgba32))` · `new SimpleVkTexture(Allocator, StagingPool, TextureAsset, CreateOptions)` · `Stb/Ktx/GliTexture.Destroy()` | direct API (texture upload) | `Brutal.TextureApi/TextureLoader.cs:130`; `RenderCore/TextureAsset.cs:35`; `RenderCore/SimpleVkTexture.cs:245` | graffiti | `graffiti.lib/DecalTextures.cs` (`Upload`) | OK @5348 | the exact decode/upload pair `TextureReference.DoLoad` uses. `ITexture` is not IDisposable — `Destroy()` must be called or the native decode buffer leaks. Max edge 2048, downsampled, full mip chain |
| `ShaderModuleUtils.ShaderStageFromFileExtension(string) : VkShaderStageFlags` | direct (render) | `RenderCore/ShaderModuleUtils.cs:198` | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | `VehiclePaintPatches.cs`; `CanopyProjectionShaders.cs` (`FromFilePrefix`) | OK | fills the skipped original's `out` param |
| `Brutal.ShaderCApi.CompileOptions` (readonly struct) | Harmony arg type (cross-asm) | `Brutal.ShaderCApi/CompileOptions.cs:10` (Brutal.ShaderC.dll) | humble-arteest (VehiclePaint), free-fallin (Full Canopy) | both `.lib.csproj` references | OK | needed to declare the `FromFile` prefix signature; options pass through untouched |
| `Presets.{InputAssembly.TriangleList, Rasterization.Fill.CullNone, BlendState.BlendColorAlpha}` | direct (render) | `RenderCore.Pipelines/SimplePipelineCreator.cs:15` | thug-life | `ThugLifeQuadRenderer.cs:140,141,143` | OK | pipeline presets |
| `RenderingPresets.ReverseZDepthStencil.DepthTestWrite` | direct (render) | `RenderCore` (e.g. `OceanRenderer.cs:292`) | thug-life | `ThugLifeQuadRenderer.cs:142` | OK | reverse-Z; 4730/4733 depth-prepass didn't alter it |
| `Renderer.{Device, Allocator, Graphics, DynamicStateInfo, ViewportState}` | direct (render) | `KSA`/`RenderCore` (via `Program.GetRenderer`) | thug-life | `ThugLifeQuadRenderer.cs:137,138` | OK | compile-verified |
| `Renderer : KSADeviceContextEx` → `.Device : DeviceEx`, `.Allocator : KsaVmaAllocator`, `.Graphics : Queue` | direct (render) | `Core/Renderer.cs:14`; `Core/KSADeviceContextEx.cs:55,57,59`; `KSA/KsaVmaAllocator.cs:12` | parts-now | `Runtime/RuntimeModLoaderGpuStates.cs:85,93`; `PartThumbnailGenerator.cs:129-138,341-390`; `RuntimeModUnloader.cs:123-124`; `ThumbnailReadback.cs:156` | OK | ⚠ **`Allocator`'s declared type drags in `Brutal.Vulkan.Vma.dll`** (`KsaVmaAllocator : IVmaAllocator`, `Brutal.VulkanApi.Vma/IVmaAllocator.cs:3`) — a **new** game-DLL reference for this repo (`parts-now.lib.csproj`) |
| `BindlessTextureLibrary.{TextureCount : int, MaxTextures : readonly int}` | direct API | `RenderCore.Systems/BindlessTextureLibrary.cs:41,19` | parts-now | `Runtime/BundleValidatorRulesIdentity.cs:231-232`; `Ui/StatusPanel.cs:209-210` | OK | ⚠ ships in **`Planet.Render.Core.dll`** — the second **new** game-DLL reference. The pool is `new FreeListIndexPool(maxTextures, allowResize: false)` with 1024 slots (`KSA/Program.cs:850`), so exhausting it is **fatal, not slow**; rule V15 holds 16 slots in reserve and refuses an over-budget load |
| `IBufferAllocator.CreateStagingPool(Queue, int, VkCommandBufferLevel = Primary)` · `Queue.Family` · `Queue.Submit(Span<VkSemaphore>, Span<VkPipelineStageFlags>, Span<CommandBuffer>, Span<VkSemaphore>, VkFence)` · `Device.{CreateCommandPool, AllocateCommandBuffer, CreateFence, WaitForFence, DestroyFence, FreeCommandBuffers, DestroyCommandPool, WaitIdle}` | GPU (Brutal.VulkanApi) | `Brutal.VulkanApi.Abstractions/StagingPoolExtensions.cs:5`; `Brutal.VulkanApi/Queue.cs:10`; `Brutal.VulkanApi.Abstractions/QueueExtensions.cs:7`; `Brutal.VulkanApi.Abstractions/DeviceExtensions.cs:193,281,291,297`; `Brutal.VulkanApi/VkDevice.cs` | parts-now | `RuntimeModLoaderGpuStates.cs:93`; `PartThumbnailGenerator.cs:135-138,341,356,361,368,372,390,493`; `RuntimeModUnloader.cs:124` | OK | parts-now owns a **private transient** `VkCommandPool` and one fence per thumbnail; the whole render is submit-and-wait on the game thread (only safe from `Program.OnDrawUiFrame`). `WaitIdle` gates purge step 1. Same Brutal-bump churn surface as thug-life/doh, but all compile-checked |

### KSA.ShaderReference (asset-reference type)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ShaderReference : FileReference, IKeyed` (type) | direct API | `KSA/ShaderReference.cs:20` | thug-life, humble-arteest, mesh-deform | `ThugLifeQuadRenderer.cs:114`; `VehiclePaintShaders.cs` (`TryResolveShaderPath`) | OK | via `ModLibrary.Get<ShaderReference>` |
| `Shader : VkShaderModule? { get; private set; }` (+ `<Shader>k__BackingField`) | reflection-field (string) | `KSA/ShaderReference.cs:33` | mesh-deform | `MeshDeformShaders.cs:232-252` | OK | swap via private setter. **Inert for part color shaders since rev 4693** — the color pipelines never read it; humble-arteest no longer uses this |
| `DoLoad() : internal void` | reflection-method (string) | `KSA/ShaderReference.cs:167` | mesh-deform | `MeshDeformShaders.cs:65-73` | OK | restore original shader; humble-arteest no longer uses this |
| `ModPath` (on `FileReference` base, public property) | direct API | `KSA/FileReference.cs:23` | humble-arteest, mesh-deform | `VehiclePaintShaders.cs` (`TryResolveShaderPath`); `MeshDeformShaders.cs:336,341` | OK | resolve on-disk shader path (humble: pre-flight anchor check only) |

### Brutal.* (game-shipped; risk-bearing only)
| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 4750 | Notes |
|---|---|---|---|---|---|---|
| `ConsoleWindow.IsOpen : bool => _show` | direct API | `Brutal.ImGuiApi.Abstractions/ConsoleWindow.cs:292` | HotkeyGuard (→ all mods) | `ksa-abstractions.lib/HotkeyGuard.cs:38` | OK | guard bypassed while dev console open |
| `ImGui.GetIO().WantTextInput` | direct API | `Brutal.ImGuiApi/*` | HotkeyGuard (→ all mods) | `HotkeyGuard.cs:38` | OK | detects ImGui text-input focus; watch on Brutal bumps |
| `ImGuiStyle.Colors : float4_60` / `ImGuiStylePtr` (60-color array + 72 style members) | direct API | `Brutal.ImGuiApi/ImGuiStyle.cs:188`; `ImGuiStylePtr.cs` | skittles, con-man | `skittles.lib/ThemeDefinition.cs:89-90`; `ConManSubmod.cs:68-73` | OK | hard-codes 60 colors + fixed style-var list; a Brutal slot/member add is silently dropped — watch every Brutal bump |
| `ImGuiCol` (enum, 60 slots `Text`…`ModalWindowDimBg` + `COUNT`) | enum | `Brutal.ImGuiApi/ImGuiCol.cs:5-65` | skittles | `skittles.lib/ThemeSerializer.cs:12-31` | OK | hard-coded `60` count must match |
| `VkUtils.StageAndUploadToBuffer` / `BufferEx.VkBuffer` / `IVulkanContext.Device.CreateStagingPool` / `ByteSize.Of<T>()` | GPU write (Brutal.VulkanApi) | `Brutal.VulkanApi(.Abstractions)` | doh, humble-arteest (KittenColor) | `MaterialSystemAccessor.cs:282-295`; `KittenColor.cs:191-215` | OK | GPU material-buffer write; rev-4729 Brutal bump is the churn surface (build passes). The `Span<float4>`→bytes conversion now uses the BCL `MemoryMarshal.AsBytes`; the `CommunityToolkit.HighPerformance` game-DLL reference it used to need is **retired** — that DLL is not in `ksa-game-assemblies/current/dll/` (`copy-ksa.ts` does not copy it), so the reference broke any build pointed at that tree |
| `SimpleVkTexture` / `VkUtils.UploadBufferToImage` + pipeline/descriptor primitives (`DescriptorSetLayoutEx`, `DescriptorPoolEx`, `VertexInput`, `ShaderStages`, `CommandBuffer`) | GPU (Brutal.VulkanApi/RenderCore) | `Brutal.VulkanApi`, `RenderCore`, `Core` | thug-life | `ThugLifeTextureFactory.cs:33,64`; `ThugLifeQuadRenderer.cs` | OK | custom Vulkan pipeline; highest churn surface (rev 4729); 4750 build passes |

### KSA planetary rings data + renderer (rocky-mcrock-face)
> Full detail in [`rings.md`](rings.md). No Harmony patches — a public-data swap + the game's own renderer rebuild. Rows marked *invariant* are relied upon, not called.

| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5348 | Notes |
|---|---|---|---|---|---|---|
| `AstronomicalTemplate.RingsReference : PlanetaryRingsReference?` (public field; via `Celestial.BodyTemplate`) | direct API | `KSA/AstronomicalTemplate.cs:66`; `KSA/Celestial.cs:83` | rocky-mcrock-face | `RingSwapController.cs` (`RefreshBodies`) | OK | how a ringed body is found |
| `PlanetaryRingsReference.{Texture, ControlTexture, RingObjects}` · `RingObjectsReference.{Lods, MaterialReference, Size, Thickness, RenderDistance, Density, NumLods}` · `RingLodReference.{MinScreenSizePixels, MeshFileReference}` · `PbrMaterialReference.{DiffuseReference, NormalReference, PBRMap}` (all public fields) | direct API | `KSA/PlanetaryRingsReference.cs:23-35`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs:8,11`; `KSA/PbrMaterialReference.cs:10-17` | rocky-mcrock-face | `RingSwapController.cs` (`Apply`/`Restore`/`TakeSnapshot`) | OK | the whole swap surface; ControlTexture is snapshotted but deliberately never swapped (CPU-sampled as RGBA8 — see `rings.md` #5) |
| `MeshFileReference.{Get(), Mesh : MeshReference?}` — **the mesh swap slot** | direct API | `KSA/MeshFileReference.cs:15,28` | rocky-mcrock-face | `RingSwapController.cs` | OK | renderer reads `Lods[i].MeshFileReference.Get().Mesh` at data build |
| `MeshReference` public surface: `Id/Simple/Interleaved/PrimitiveCount/BoundingSphereRadius` fields, `HostPrimitives/DevicePrimitives` get-only props, `DeviceMesh => DevicePrimitives[0]`, `Bind(Renderer, StagingPool)`, `Dispose()` | direct API | `KSA/MeshReference.cs:17-58,120,145` | rocky-mcrock-face | `RingMeshFactory.cs`, `RingAssetCatalog.cs` | OK | clone-and-convert path for interleaved subpart meshes; multi-primitive shape is new @5348 |
| `TextureReference.{Id, BindlessHandle}` · `TexturePowerReference` (type filter for normal maps) | direct API | `KSA/TextureReference.cs:70`; `KSA/TexturePowerReference.cs` | rocky-mcrock-face | `RingAssetCatalog.cs` | OK | `BindlessHandle == 0` ⇒ excluded from the pickers |
| `Program.{Instance, GetRenderer(), RebuildRenderer(bool = false)}` · `GameSettings.{ShowRings(), ShowRingMeshes()}` · `Universe.CurrentSystem.All.OfType<Celestial>()` | direct API | `KSA/Program.cs:434,535,4742`; `KSA/GameSettings.cs:3122,3133`; `KSA/Universe.cs:94` | rocky-mcrock-face | `RingSwapController.cs`, `RockyMcRockFaceSubmod.Ui.cs` | OK | `RebuildRenderer` is the apply mechanism — the same path the game's graphics settings use |
| `PlanetaryRingsRenderData` ctor bakes the reference tree (`LodProperties[i].Y = DeviceMesh.IndexCount`, `MeshCullingRadius`, bindless material ids) · `PlanetaryRingsRenderer.{PopulatePlanets, RenderMeshes}` draw `MeshLods[i].DeviceMesh` only (primitive 0) | **invariant** | `KSA.Rendering.Rings.Rendering/PlanetaryRingsRenderData.cs:180-326`; `PlanetaryRingsRenderer.cs:324,571-603` | rocky-mcrock-face | design keystone (`rings.md` #1-#3) | OK | if ring data stops being rebuilt from the reference tree, Apply silently stops working |

### KSA planetary rings — runtime definition (bloomin-onion)
> Full detail in [`rings.md`](rings.md) (bloomin-onion section, rows B1-B10). No Harmony patches — constructs a `PlanetaryRingsReference` tree, assigns it to the body template, refreshes the transparencies body list and runs the game's own renderer rebuild. Reuses rocky-mcrock-face's catalog/mesh rows above.

| Member (signature) | Kind | Decomp path | Used by | Mod code ref(s) | 5348 | Notes |
|---|---|---|---|---|---|---|
| `PlanetaryRingsReference` / `PlanetaryRingsVolumeReference` / `RingRaymarchingStepReference` / `RingObjectsReference` / `RingLodReference` / `MeshFileReference.Mesh` / `PbrMaterialReference` — **all public fields, constructed from scratch** (`IsValid()` deliberately not used: `DistanceReference.IsValid` demands > 100 km) | direct API | `KSA/PlanetaryRingsReference.cs`; `KSA/PlanetaryRingsVolumeReference.cs`; `KSA/RingRaymarchingStepReference.cs`; `KSA/RingObjectsReference.cs`; `KSA/RingLodReference.cs`; `KSA/MeshFileReference.cs:15`; `KSA/PbrMaterialReference.cs:10-17` | bloomin-onion | `RingReferenceBuilder.cs` (`Build`), `RingDefinitionSerializer.cs` (`FromReference`) | OK | a new required field on any of these classes silently defaults — see `rings.md` B-narrative #1 |
| `DistanceReference(double, DistanceUnit)` · `RadianReference(double)` + `ToDegrees()` · `DoubleReference.FromValue` · `BoolReference(bool)` · `MathEx.{ToDeviationAngle, ToCompassAngle}(double)` · `OrbitDefinitionFrame` | direct API | `KSA/DistanceReference.cs:105-140`; `KSA/RadianReference.cs:23,66`; `KSA/DoubleReference.cs:44`; `KSA/BoolReference.cs:14`; `KSA/MathEx.cs:178,189` | bloomin-onion | `RingReferenceBuilder.cs` | OK | value wrappers the XML loader would create; angle normalization mirrors `PlanetaryRingsReference.OnDataLoad` |
| `AstronomicalTemplate.RingsReference` (public field, **written**) · `Celestial.{BodyTemplate, MeanRadius, Parent}` | direct API | `KSA/AstronomicalTemplate.cs:66`; `KSA/Celestial.cs:73,83,91` | bloomin-onion | `RingDefinitionController.cs` | OK | original reference snapshotted per template for Remove |
| `PlanetTransparenciesRenderer.PopulatePlanets() : bool` (public) | direct API | `KSA/PlanetTransparenciesRenderer.cs:169` | bloomin-onion | `RingRendererRebuilder.cs` | OK | re-derives which bodies have rings; its result must be written to `_anyRings` (watchlist) |
| `TextureReference` (subclassed): `Category, Width, Height, BindlessHandle, Bind(Renderer, StagingPool)` (virtual), `Dispose(Device)`, `SetHash()` · `RenderCore.TextureAsset(ITexture, string)` · `GenericTexture.Defaults.RGBA8UNorm(int2)` + `.Data` · `TextureFormat.Descriptor().{IsBlockCompressed, BlockSizeInBytes}` | direct API | `KSA/TextureReference.cs:36-77,133-166`; `RenderCore/TextureAsset.cs:21`; `Brutal.TextureApi.Abstractions/GenericTexture.cs:80,122` | bloomin-onion | `PaintedTextureReference.cs`, `RingReferenceBuilder.cs` (`IsCpuSampleable`) | OK | painted band/control strips are real `TextureReference`s bound through the game's own path |
| `PlanetTransparenciesRenderer.RebuildFrameResources` gating (`!_ringRendererCreated && _anyRings` → `CreateRingsRenderer`) · `PlanetaryRingsRenderer.PopulatePlanets` ctor-only · `PlanetRenderer` per-frame `RingsReference` read (ring shadow) · `AtmosphereRenderer.AssignPlanetSlots` keyed on `AtmosphericBody` only | **invariant** | `KSA/PlanetTransparenciesRenderer.cs:325-343`; `PlanetaryRingsRenderer.cs:324-346`; `KSA/PlanetRenderer.cs:1985-1993`; `KSA/AtmosphereRenderer.cs:305-317` | bloomin-onion | design keystone (`rings.md` B-narrative #1, #6, #10) | OK | a ring-only body joining the transparencies list must stay harmless to the atmosphere renderer |

---

## 4. String-based reflection watchlist (highest silent-break risk)

NOT compile-checked — a game rename breaks these at runtime with no build error. Re-verify each name
on every game update FIRST.

| Type.Member (string) | Mod(s) | Why string-based | 5348 |
|---|---|---|---|
| `Camera.OnFrame` (`OrbitController`/`FlyController.OnFrame`) | camera-controller-override | `AccessTools.Method(…, "OnFrame")` | OK |
| ~~`Controller.___Transform`~~ (field injector) | ~~camera-controller-override~~ | ~~Harmony field-injection by name~~ | **RETIRED @5261** — the prefix now reads the public `__instance.Camera` (`CameraControllerOverridePatches.cs:42-54`), so the injector is gone and this can no longer fail at `Apply` time. ((no `Transform` member exists on `KSA.Controller` in either tree), but `Camera` is the field that actually carries the view.) |
| `Camera._fovRadians` | glass | `AccessTools.Field` private field by name | OK (single most-important glass check) |
| `Camera.ChangeFieldOfView` / `Camera.UpdateProjection` | glass | `AccessTools.Method` by name | OK |
| `Vehicle.GetWorldMatrix` / `Vehicle.UpdateRenderData` | i-feel-seen | `AccessTools.Method(typeof(Vehicle), "…")` | OK |
| `VolumetricExhaustTemplate.References` (internal static field) | pyro | `AccessTools.Field(…, "References")` → `SerializedCollection<T>.GetList()` (`PlumeTemplates.cs:46`) | OK @5348 — soft: falls back to the stock 7 ids via public `Get(id)` |
| `VolumetricExhaustInstance._shaderData` (private struct field) | pyro | `AccessTools.FieldRefAccess<…, ExhaustInstance>("_shaderData")` (`PlumeEmitter.cs:25`) | OK @5348 — soft: per-plume look overrides disable with a UI notice |
| `KittenEva` (type name) / `KittenEva._renderable` → `KittenRenderable._characterAvatar` → `CharacterAvatar.Core` → `CharacterCore.Scale` | garrys-torch, doh, kitten-animations | `GetType().Name` compare + private field chain | OK |
| `ChuteRenderable._renderable` → `AnimatedRenderable.MaterialIndices` | free-fallin | private/protected field chain used immediately before `ChuteRenderable.Draw`; writes material slot zero and weakly tracks the renderable for restore | OK @5402 — new game surface and new consumer; both exact names are load-bearing |
| `CharacterAvatar.Core.{CharacterModel,Fur,Attachments}…MaterialIndices` (AnimatedRenderable/CatFurRenderable/StaticMeshRenderable) | doh | private field-path + `protected int[]` | OK |
| `CatExpressionAnim._expressionPose` | kitten-animations | private field by name (cache bust) | OK |
| `KittenRenderable._ground{Idle,Walk,Run}Anim`, `_ladderAnim`, `_jumpIntroAnim`, `_flailAnim`, `_jumpLandAnim`, `_moon{Walk,Run}Anim`, `_swimAnim`, `_swimIdleAnim`, `_seatedIdleAnim`, `_seatedIdleActionAnims`, `_walk/_run/_swimPairSampler`, `_blendSampler` | kitten-animations | 17 private fields by name — the only route to the ground animation set | OK — degrades per field into a UI warning, never a crash |
| `KittenRenderable._catPersonalityExpressionAnim / _catExpressionAnim / _catEyeAnim / _catEarAnim` | kitten-animations | private fields by name; distinguishes the two same-typed expression processors | OK |
| `AnimatedRenderable.UpdateAnimation` | kitten-animations | `AccessTools.Method` by name (Harmony prefix) | OK — loud `MissingMethodException` at `Apply` if renamed |
| `LightModule.TemplateData` (`"KSA.LightModule+TemplateData"`) + `PartTemplate.Components` + `TemplateData.Intensity`/`FloatReference.Value` + `ColorRgbReference.{R,G,B,OnDataLoad}` | zippo, red-alert, its-so-shiny (via ZippoLib) | hard-coded type/field/method names | OK |
| ~~`LightModule.TemplateData."Color"`~~ | ~~zippo~~ | ~~`GetField("Color")` — wrong name~~ | **RETIRED @5348** — the bug is gone: the code reads `"ColorRgb"` (`zippo.lib/LightController.cs:59,80`), which is the real field. Fixed by commit `07787ea`; earlier scope text calling this BROKEN was **stale**. There is no `GetField("Color")` anywhere in the repo. |
| `GaugeCanvas._canvases/_enabled/_customOffset/_customScale/_windowPosition/_windowSize/_windowTitle` | con-man | 7 private fields by name (IsValid canary) | OK — all 7 still declared **on `GaugeCanvas` itself** (not lifted to `GaugeBase`, which would break `GetField`). `_windowTitle` went `private`→`protected`; still `NonPublic\|Instance`, so it resolves. **Behavioral risk instead:** revs 4919/4940/4959/5003 rebuilt the gauge/HUD system around con-man, rev 5201 added context-visibility gating, and **rev 5293 added a global Hud Scale applied after `_customScale`** (see §6) |
| `Program.Instance`/`MaterialSystem`/`SuperMeshRenderSystem`/`CharacterRenderSystem` + `GpuObjectSystem.{BigBuffer,DeviceCtx,CreateObject}` + `AssetManager.{AssetMap,GetOrLoad}` + `GpuObjectAssetRef.Handle` + `GpuTextureSystem.*` + `Pbr/Character*Reference.*` | doh, humble-arteest (KittenColor) | deep render-system reflection bridge | OK |
| `ShaderReference.{Shader (+k__BackingField), DoLoad, ModPath, LocalPath}` + `RenderCore.ShaderModuleUtils.FromFile` | mesh-deform | private/internal member names, cross-asm | OK (but `Shader` is inert for part color pipelines since 4693) |
| `ModLibrary.AllParts`/`AllCharacters` + `SerializedCollection.{GetList,Find}` | doh | internal static fields/methods by name | OK |
| `ModLibrary.AllParts` | parts-now | `GetField("AllParts", Static\|NonPublic\|Public)` in `parts-now.lib/Runtime/GameRegistry.cs:72,292` — the **only** file in parts-now allowed to reflect | OK |
| `ModLibrary.AllMeshes` | parts-now, rocky-mcrock-face | `GetField("AllMeshes")` — `GameRegistry.cs:73`; `rocky-mcrock-face.lib/RingAssetCatalog.cs` (`Collection<T>`) | OK |
| `ModLibrary.AllFiles` | parts-now, rocky-mcrock-face | `GetField("AllFiles")` — `GameRegistry.cs:74`; `rocky-mcrock-face.lib/RingAssetCatalog.cs` (`Collection<T>`) | OK |
| `ModLibrary.AllGltfs` | rocky-mcrock-face | `GetField("AllGltfs")` — `RingAssetCatalog.cs` (`Collection<T>`); source of character/MMU/helmet meshes for the ring picker. Degrades to those entries missing from the list | OK |
| `MeshReference.<HostPrimitives>k__BackingField` (auto-prop backing field) | rocky-mcrock-face (bloomin-onion via `RingMeshFactory`) | `GetField` by name in `RingMeshFactory.cs` — shares CPU geometry into a converted clone. Null-checked: a miss fails Apply with a UI error, never crashes | OK |
| `Program._planetTransparenciesRenderer` → `PlanetTransparenciesRenderer.{_ringsRenderer, _ringRendererCreated}` | rocky-mcrock-face, bloomin-onion | private-field access in `RingSwapController` — **load-bearing for Apply**: the existing rings renderer is disposed (public `Dispose()` after `Device.WaitIdle`) and `_ringRendererCreated` cleared so `RebuildFrameResources` takes its `CreateRingsRenderer` branch and re-reads the ring data (`PopulatePlanets` is ctor-only). A rename degrades to a frame-resources-only rebuild: Apply hitches but changes nothing (immediately user-visible, not a crash) | OK |
| `PlanetTransparenciesRenderer._anyRings` (private bool) | bloomin-onion | `ReflectionHelpers.SetFieldValue` in `RingRendererRebuilder.Rebuild` after the public `PopulatePlanets()` — **load-bearing for adding rings to a system that has none**: `RebuildFrameResources` only creates the rings renderer when `_anyRings`. A rename is a silent no-op: Apply reports success but nothing renders in ringless systems (Saturn systems unaffected). Immediately user-visible, never a crash | OK |
| `TextureReference.<TextureAsset>k__BackingField` (private-set auto-prop) | bloomin-onion | `GetField` by name in `PaintedTextureReference` — seeds the in-memory asset the game's own `Bind` reads. Null-checked: a miss disables Painted band mode (`IsSupported == false`, UI falls back to Texture mode with a message) | OK |
| `StaticCelestial._distantRenderer` → `DistantSphereRenderer._data` (+ struct fields `UseRingShadows, RingInnerRadius, RingOuterRadius, RingTextureId, SamplerClampId`) | bloomin-onion | base-type private field walk + `GetField` by name in `RingRendererRebuilder.SyncDistantSphereShadow` — **cosmetic only** (far-away sphere ring shadow); every step null-tolerant inside try/catch | OK |
| `ModLibrary.AllMaterials` | parts-now | `GetField("AllMaterials")` — `GameRegistry.cs:75` | OK |
| `ModLibrary.AllPartGameDataReferences` | parts-now | `GetField("AllPartGameDataReferences")` — `GameRegistry.cs:76`. **Note the plural `References` suffix**, unlike its five siblings | OK |
| `ModLibrary.AllEditorTagDefinitions` | parts-now | `GetField("AllEditorTagDefinitions")` — `GameRegistry.cs:77`; feeds validation rule V7 | OK |
| `SerializedCollection<T>._collection` (private `ConcurrentDictionary<KeyHash,T>`) | parts-now | `GetField("_collection", Instance\|NonPublic)` per closed generic — `GameRegistry.cs:356-357`, used by `Unregister` `:154-165`. **`SerializedCollection<T>` has no removal API, so unload/reload exist only because of this** (see U4) | OK |
| `VehicleEditor._editorTagLookup` (private static `Dictionary<uint,string>`) | parts-now | `GetField("_editorTagLookup", Static\|NonPublic)` — `GameRegistry.cs:320`. **Degraded, not fatal**: V7 falls back to the six built-in tags + `AllEditorTagDefinitions` ids | OK |
| `VehicleEditor.ScaleBoundsFor` / `UpdateSelectedScale` / `UpdateScaleGizmo` / `QuantizeScale` / `ForEachPartWithSymmetry` | dont-stifle-me | `AccessTools.Method(typeof(VehicleEditor), "…")` — `EditorScalePatches.cs:16-20,38-49`; the first three are Harmony targets, the last two become delegates. Any miss throws at `Apply()` (logged, mod shows a red notice; stock behavior remains). Four of the five **first appeared in 5348** | OK |
| `VehicleEditor.DrawParachuteSection` | dont-stifle-me | `AccessTools.Method(typeof(VehicleEditor), "DrawParachuteSection")` — `EditorValueLimitPatches.cs:15,29`; a miss throws at `Apply()` and disables only the configurable editor-limit patch group in unscience | OK @5402 |
| ~~`Part._matrixAsmb` / `Part._matrixAsmb2Parent`~~ |  | ~~private fields by name (cache safety)~~ | **RETIRED @5117** — replaced by the public `Part.ResetCachedPosMatrixValues()`. Rev 5112 changed the uncached sentinel from identity to NaN, which turned the old identity-write from a no-op into a transform-corrupting write. **Removing a watchlist entry is the best outcome available here** — this row can no longer break silently |
| `PartTree.RecomputeStaticMass` | kitchen-sink | HarmonyLib `Traverse.Method("RecomputeStaticMass")` | OK |
| `ResourceManagerBase.NearestToFurtherestNode(SameStage)` | blinky (diagnose-only) | base-type private field by name | OK (field names intact) — but the **owner moved**: `ResourceManager` is no longer on `RocketCore`, it is on the `Combustor` subclass (`SolidMotor` cores have none). Reached via a `core is Combustor` test since 5018 |
| `GameSettings.OnKeyAll` | all mods (HotkeyGuard) | `AccessTools.Method(…, nameof(OnKeyAll))` | OK |
| `Program.OnDrawUiConsole` (private) | unscience (HiddenUiFrameHook) | `AccessTools.Method(typeof(Program), "OnDrawUiConsole")` — `HiddenUiFrameHook.cs:44`. Miss throws at `Patch()` → logged/skipped; symptom is mods freezing on F2 again. Must remain an every-frame call *after* the `if (DrawUI)` block and *before* `ImGui.Render()` (`Program.cs:2103` @5348) | OK |
| `Universe.ExecuteNextVehicleSolvers` | eternal-flame, kitchen-sink, kiwis-marbles | `AccessTools.Method` by name (no param array) | OK (single overload) |
| `Situation` enum names (`"Landed"/"Floating"/…`) via `.ToString()` | steely-eyed | enum-name string compare | OK |

---

## 5. Shaders & game assets subtable

| Asset / shader | Kind | Referenced as | Content path (NEW) | Consumer | 5348 |
|---|---|---|---|---|---|
| `UnlitMesh.vert` / `UnlitMesh.frag` | shader | `ModLibrary.Get<ShaderReference>("UnlitMeshVert"/"UnlitMeshFrag")` | `Core/DefaultAssets.xml:66,67` → `Core/Shaders/Mesh/UnlitMesh.*` | thug-life | OK (**byte-identical 4750→5018**; also untouched by 4693/4745) |
| `GridFrag` (path anchor only) + `Common/Camera.glsl` / `Common/TextureSet.glsl` (headers, `#include`d) | shader include root + headers | `ModLibrary.Get<ShaderReference>("GridFrag").ModPath` → its **directory** is the `#include` root for graffiti's two runtime-compiled decal shaders | `Content/Core/DefaultAssets.xml:373` → `Core/Shaders/Grid.frag`; `Core/Shaders/Common/*.glsl` | graffiti | OK @5348 — a `global.camera`/`global.lighting` struct or `SAMPLE_TEXTURE`/`SET_TEXTURE` macro change fails at shaderc compile (loud console line; decals self-disable) |
| `MeshIndirect.vert` (struct + varying anchors) | shader text-edit | `Get<ShaderReference>("MeshIndirectVert")`, GLSL anchor strings | `Content/Core/Shaders/Mesh/MeshIndirect.vert` | mesh-deform | **BROKEN** (anchors diverged; see §6). humble-arteest **no longer touches this file** |
| `MeshIndirect.frag` + `MeshIndirectRaytraced.frag` (paint injection) | shader text-edit (in memory, via the `FromFile` prefix) | matched by **file name**; anchor = first `vec3 sampledColor …;` line; requires `inStateFlags` varying and `gammaToLinear` (`Common/Shared.glsl:203`) | `Content/Core/Shaders/Mesh/MeshIndirect.frag:114`; `MeshIndirectRaytraced.frag:156` | humble-arteest (VehiclePaint) | OK (rebuilt for 5018) — if the anchor moves, `Enable` fails with a UI message and rendering stays stock |
| `MeshIndirect.frag` (Temperature LUT, `#ifdef ENABLE_TEMPERATURE`) | shader (read-only, no edit) | — | `Content/Core/Shaders/Mesh/MeshIndirect.frag:214-219` | humble-arteest (EngineEmissive) | OK (MOVED from `DynamicMeshIndirect.frag` rev 4693; feature still works) |
| `Model.vert` + `Model_Skinned.vert` + `ModelPbr.frag` → `TextureSet.glsl` / `MaterialSet.glsl` | shader text-edit (in memory, via the `FromFile` prefix) | exact declaration/assignment/call anchors; added location-3 `vec2`; Full Canopy marker in `Material.extraData.w` | `Content/Core/Shaders/Mesh/Model{,_Skinned}.vert`; `Mesh/ModelPbr.frag`; `Common/{TextureSet,MaterialSet}.glsl` | free-fallin; existing read-only albedo effect also used by doh and humble-arteest (KittenColor) | OK @5402 — transformed shaders compile to valid SPIR-V; static vertex supplies pass-through varying, skinned vertex derives bind-pose X/Z projection, fragment substitutes only marked albedo sampling |
| `ParachuteCanopyGlb` + `ParachuteCanopy_Material` (`Diffuse`, `Normal`, `AoRoughMetal`) | skinned GLTF + PBR material/texture assets | exact ids from `ChuteRenderable` / `ModLibrary.Get<PbrMaterialReference>` | `Content/Core/ParachuteAssets.xml:4,23-27`; `Core/Textures/ParachuteCanopy_{Diffuse,Normal,PBR}.ktx2` | free-fallin | OK @5402 — runtime albedo is BC7; center-decal mode reopens `TextureReference.ModPath` and explicitly transcodes the source KTX2 to RGBA8 |
| `DynamicMeshIndirect.vert/.frag`, `ModelEye.frag`, `ModelGlass.frag` | shader (removed) | (design assumption only) | — | humble-arteest (narrative), blinky/its-so-shiny GlassModule (C# only) | n/a (removed 4693/4745; `ModelTranslucent.frag` new 4747 — not referenced by id) |
| Exhaust templates `EngineALarge`, `EngineAMed`, `EngineACompact`, `EngineAVernier`, `EngineATurbine`, `RCS`, `MmuRcsVac` | `VolumetricExhaustTemplate` ids | `VolumetricExhaustTemplate.Get(id)` — **fallback list only** (`PlumeTemplates.cs:13`); normally enumerated live from `References` | `Core/ExhaustAssets.xml:3,307,650,993,1331,1670,2009` | pyro | OK @5348 (`EngineALarge` is the create-form default) |
| Engine part templates `CorePropulsionA_Prefab_EngineA2..A6` | part template | `ModLibrary.Get<PartTemplate>(id)` (default A3 everywhere) | `Core/CorePropulsionAAssets.xml`; `Core/CorePropulsionAGameData.xml:118,182,246,291,373` | blinky | OK — **`EngineA1` is gone from Content entirely** and has been removed from blinky's presets and config default (2026-08-23) |
| Engine feed connector `_connector3` (`<Capabilities>BulkFluid</Capabilities>`) + `<ConsumerFeedWiring>/<FeedsFrom>` on A2–A6 | part-template wiring | reached via `RocketCore.FeedConnectors`, not by id | `Core/CorePropulsionAGameData.xml:189-193` (A3; A2/A4/A5/A6 alike) | blinky | OK — **load-bearing**: the pixel engines only receive propellant because blinky connects *this* connector to a tank part. If the game drops `BulkFluid` or the `FeedsFrom` wiring, every grid goes dark again |
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

## 6. Confirmed-broken / changed summary (vs 5402)

### 5348 → 5402 (current span — 54 revisions, 5349–5402; only rev 5401 logged)

Reconstructed from the source diff: a **viewport registry rework** (`Viewport` class → `IViewport`/
`IGameViewport`/`ViewportRegistry`, `Index` → `ShaderSlot`, per-viewport GPU arrays fixed at 8 slots —
rev 5401's "thumbnail stride" fix), **parachutes** with a Bepu cloth solver (`JobSystems.ClothSolvers`,
runs before the vehicle solvers), **part structural failure / debris** (`PartFailure`,
`Part.CrashTolerancePascals`, `Vehicle.IsDebris`), an **exhaust plume deformation** rework
(`ExhaustPlumeDeformation`, `PlumeBend.glsl`, `ExhaustInstance` 224 → 272 B), and a **light-switch
consolidation** (`Part.IsLightSwitchedOff()`). Full review:
[`../plans/KSA_5402_UPGRADE.md`](../plans/KSA_5402_UPGRADE.md).

**CHANGED (compile breaks against 5402 — all fixed this pass)**
- `KSA.Viewport` **removed** → `IViewport`/`IGameViewport`; `Viewport.Index` → `IViewport.ShaderSlot`.
  Six one-line retypes: `ksa-abstractions.lib/IvaForceRender.cs:98`, `dont-stifle-me.lib/EditorScalePatches.cs:124`,
  `dont-stifle-me.lib/PerAxisScaleDrag.cs:28`, `i-feel-seen.lib/IFeelSeenPatches.cs:64`,
  `parts-now.lib/Runtime/PartThumbnailGenerator.cs:61,176,338,512`, `graffiti.lib/DecalRenderer.cs:402`.
- `Cursor.InputRay` **removed** → `Cursor.GetEgoRay(IViewport)` (`KSA/Cursor.cs:27`); ray is now
  same-frame. `graffiti.lib/DecalPicker.cs:56`.
- `VolumetricExhaustRenderer.AddInstance` **re-signatured** (+`float3 airVelocity, float airDensity`,
  returns `float`) (`:710`). `pyro.lib/PlumeEmitter.cs:76-98` mirrors `Vehicle.AddVolumetricExhaustInstances`.

**BEHAVIORAL (compile-clean, no symbol moved — needs a live pass)**
- ⚠️ **pyro / game — exhaust refraction dead.** `_hasRefractionInstances` is never set true in 5402
  (OLD `VolumetricExhaustRenderer.cs:960`); pyro's Refraction slider is inert. Game-side. `scope/exhaust-plumes.md`
- ⚠️ **garrys-torch vs part failure.** `PartFailure.Detect` (`PhysicsBubble.cs:1459`) can now shed
  debris / destroy overlapping welded vehicles; `WeldEngine.UpdateWeld:19` has no disposed guard.
  `Vehicle.Teleport` byte-identical. Recommended: guard + live weld test. `scope/vehicle-physics.md`
- ⚠️ **graffiti terrain decals.** `Celestial.GetTerrainHeightFromDirCcf` accurate path now uses
  `MeanRadius` (`Celestial.cs:825-857`). Live placement check. `scope/decals.md`
- ✅ **IvaForceRender vs the `RenderPartModels` gate** — the postfix now mirrors both of the
  original's viewport gates (`ksa-abstractions.lib/IvaForceRender.cs:107-108`); it was dormant either
  way. `scope/00-architecture-and-abstractions.md`
- ⚠️ **thug-life** — `RenderMainPass` (`:347`) now also runs per secondary viewport; still unverified live.
- Debris fragments (`Vehicle.IsDebris`) are now filtered out of `VehicleProvider.GetAllVehicles()`
  by default (`bool includeDebris = false`); `FindVehicle`, parts-now's fail-closed unload gate and
  graffiti's decal pick opt back in. `kiwis-marbles`: cloth solvers snapshot before the weld prefix (one-frame
  lag for a chute near a welded body). `glass`: `UpdateProjection` prefix now also hits thumbnail cameras.
  `zippo`/`red-alert`/`its-so-shiny`: light state now resolved via `Part.IsLightSwitchedOff()`, which
  still reads `LightIsActive` first — no change. `Part.DisplayName` now prefers the template name (labels).

**VERIFIED CLEAN this span**
- **Entire string-reflection watchlist (§4) resolves**, same kind and type (con-man's seven fields
  byte-identical at the same lines; `CharacterAvatar.Core`/`CharacterCore.Scale` still fields).
- **Every Harmony patch-target signature unchanged** apart from `Viewport → IViewport`, all single
  overloads; `GameSettings.cs` byte-identical; `ExecuteNextVehicleSolvers` body identical.
- **GPU byte layouts identical** — `PerInstanceData` (both), `MaterialData`; `StateBitFlag` bits 11..31 unused.
- **Shaders** — `MeshIndirect.*`, `UnlitMesh.*`, `MaterialSet.glsl`, ring shaders byte-identical; anchors intact.
- **Frames** — `IParentBody`, `CelestialFrameMath`, `Camera` FOV members, `KinematicMeasurements`,
  `NavBallData`, `Situation` unchanged. No `Brutal*` decomp changes.

**Known-broken reconciliation:** `___Transform` and zippo `"Color"` are closed (stale rows corrected);
humble-arteest Vehicle Paint / mesh-deform still dead by design (4693); the "supermod never wires
`IvaForceRender.Patch`" note was stale (wired at `unscience/Patcher.cs:74`).

### 5261 → 5348 (previous span — 87 revisions, 5262–5348)

Dominated by **ground clutter** (collisions, exclusion masks, destruction, distribution fixes), a
**terrain precision rework** (`PrecisionFuncs.glsl`, `AnchoredNoise.glsl`, split-double anchors), a
**clustered-lighting rewrite** (rev 5301: `ViewportLightModes`, `PartModelShadowCull`, VRAM 25.9 MB →
1.08 MB), a **static-object pipeline** (launchpads, decals, `StaticObject.vert/.frag`), a
**physics-bubble merge/split rewrite** (revs 5331/5339), a **vehicle-power rework onto electrical
circuits** (rev 5326), **Vulkan 1.3 → 1.4** (rev 5315), and the one that reached us: a **part/module
sequencing refactor** (rev 5329). Full review:
[`../plans/KSA_5348_UPGRADE.md`](../plans/KSA_5348_UPGRADE.md).

**CHANGED (compile break against 5348 — resolved by removing the mod)**
- `PartTemplate.Decoupler` **removed** and `KSA.DecouplerTemplate` **deleted**, rev 5329
  (*"Sequencing has moved from parts down to modules"*). Decouplers are now a module:
  `KSA.Decoupler.TemplateData` (`[XmlType(TypeName = "Decoupler")]`) inside `PartTemplate.Components`,
  with `ConnectorId`/`Force` as **fields**. Three × CS1061 in `space-tape.lib/PartImporter.cs:124,128,129`.
  → **space-tape was defunct and has been deleted** (mod + `.lib`, solution entries, supermod wiring).
  The solution went 57 → **55 projects** and the supermod now bundles **22** submods (was 23). The
  on-disk XML `<Decoupler ConnectorId=… Force=… />` is
  **unchanged** — only the typed reader broke. `scope/part-editor-and-robotics.md`

**REMOVED (not a game break — mod deleted by choice, after this pass)**
- ❌ **flexo (robotics) deleted.** `flexo.lib` compiled **clean** against 5348 and every one of its
  patch targets verified OK below; it was removed because the hinge approach never worked in-game and
  will not be reattempted this way. Mod + `.lib`, both solution entries, the `unscience.csproj`
  `ProjectReference` and the supermod wiring in `unscience/Mod.cs` + `unscience/Patcher.cs` are all
  unwired. Solution **55 projects**, supermod **22 submods**. Two game surfaces became **unowned** with
  it — `PartModelRenderer.UpdateRenderData(Viewport, int)` (the keystone render hook, inherited from
  space-tape) and `OrbitLinePass.AddLineVertex`/`AddLineEnd` — and their table rows are gone;
  `GenericGizmo` survives under dont-stifle-me. `PartTree.RecomputeStaticMass` stays on the
  string-reflection watchlist for kitchen-sink alone. `scope/part-editor-and-robotics.md`

**BEHAVIORAL (compile-clean, no symbol moved — needs a live pass)**
- ⚠️ **con-man vs the new global Hud Scale** (rev 5293). `GaugeCanvas` now divides by
  `GameSettings.GetGaugeScale()` in `PixelsToUv` (`:536`), multiplies its size constraint by it
  (`:817`) and wraps draws in `ConsoleStyle.BeginGaugeHostScope(gaugeScale * clamp(ContentScale,
  0.6, 3))` (`:859-866`). All seven reflected fields still resolve **byte-identically at the same line
  numbers**, but con-man's saved `_windowPosition`/`_windowSize`/`_customScale` are now in a scaled
  space it does not model — **layouts saved at one Hud Scale restore wrong at another.** Stacks on the
  still-open rev-5201 context gate. Also: `RecalculateAll()` gained an optional `bool forceReattach`
  (source-compatible; con-man does not call it), `Detached = false` is reset on reattach (`:954`), and
  the crew-portraits canvas gained a `GameSettings.ShowCrewPortraitCameras()` gate (rev 5276).
  Rev 5277 removed `GameSettings.Interface.FontSize` and redefined `GetInterfaceScale()` from
  `FontSize/20f` to a 50–200 % `Interface.Scale`. **Open.** `scope/ui-customization.md`
- ⚠️ **kitten-animations vs the per-frame pose guard** (rev 5278). `AnimatedRenderable` gained
  `private ulong _lastPoseUpdateFrameNumber` and the pose path is now gated on
  `Program.FrameNumber != _lastPoseUpdateFrameNumber` (was `!FreezeAnimation`). `CatExpressionAnim` is
  **byte-identical** and `_expressionPose` still resolves, but a forced second pose evaluation **in the
  same frame is now dropped** — the first concrete mechanism found for the standing *"always the same
  expression"* entry in `ISSUES.md`. **Open.** `scope/character-and-materials.md`
- ⚠️ **thug-life's render environment moved under it.** Everything it binds to is intact
  (`RenderMainPass` signature, `Program.OffscreenTarget`, `UnlitMesh.*` byte-identical), but rev 5315
  raised the game to **Vulkan 1.4** and rev 5283 added **UI coverage culling**
  (`Content/Core/Shaders/UiCoverage/*`, seven new ids in `DefaultAssets.xml`,
  `GaugeCanvas.RegisterOpaqueCoverage`). Neither is statically clearable.
  `scope/pixel-grids-and-render.md`
- ✅ **blinky / its-so-shiny — favourable.** Rev 5326 moved `PowerManager.PopulateGraph` out of the
  constructor (`KSA/PowerManager.cs:14` @5261) into `OnDrawUi` behind `ShowFlow`
  (`:130-138` @5348); power now runs on the new `PartTree.ElectricalCircuits`. **The O(N³) DFS both
  grid builders are architected around no longer runs during play** (`LcdGridBuilder.cs:62,114,319`;
  `ShinyGridBuilder.cs:42`). No change needed; their splitting optimisations are now dead weight.
  Counterweight: `Part.Modules` is now `new ModuleList(keepModuleIdIndex: true)` for every part.
- ⚠️ **parts-now vs load-time part validation** (rev 5340). `Program.cs:1212-1215` now runs
  `PartArchetypes.WarnOnMalformedParts()` in a `Loading.Task("Part Validation")`, constructing a real
  `Part` from **every** non-subpart template in `ModLibrary.AllParts`. Rev 5329 added
  `PartTemplate.WarnOnDuplicateModuleIds()`. Ordering invariant holds — `ModLibrary.Bind(_renderer)` is
  at `:942`, before validation at `:1214` — so parts-now's generated parts **will** be instantiated and
  may surface new load-time warnings. `scope/part-editor-and-robotics.md`
- Editor scaling changed **triaxial → uniform, clamped 0.5×–2×**, and modules gained `IRescale`
  (rev 5329) — dont-stifle-me (which exists to undo it), parts-now. Engine deactivate-mid-burn fixed (5333) and sequence-0 no longer
  zeroes delta-v/TWR (5318) — blinky, average-twr. TVC gains retuned (5317) — average-twr, geeforce,
  steely-eyed. Lights now register for **all** viewports, not just `Program.MainViewport`
  (rev 5301, `LightModule.cs:125,141`) — zippo, red-alert, its-so-shiny.

**VERIFIED CLEAN this span**
- **Entire string-reflection watchlist (§4) resolves**, plus a **field-vs-property audit**: rev 5329
  moved `Parent` from a `Module<T>` field to a `ModuleBase.Parent` auto-property (and `ModuleBase` now
  implements `IPartParent`), which would silently break `GetField("Parent")` —
  `ksa-abstractions.lib/ReflectionHelpers` has no property fallback. **No mod reflects on `Parent`.**
  `CharacterAvatar.Core` and `CharacterCore.Scale` are both still plain fields.
- **Every Harmony patch-target signature unchanged** (line shifts only), including
  `Universe.ExecuteNextVehicleSolvers(double, SimStep)` despite a substantially rewritten body
  (bubble ownership moved into `VehicleUpdateTask`, revs 5331/5339), and
  `PartModelRenderer.UpdateRenderData(Viewport, int)` — at the time of this pass flexo's explicit
  `[Viewport, int]` array still resolved uniquely (the new 3-arg `(Viewport, int, ref readonly double4x4)`
  overload is on other types); that hook is now **unowned** after flexo's removal.
  `JobSystems.VehicleSolver` (garrys-torch's drain) is intact.
- **jplrepo's IL transpiler anchor holds.** Rev 5332 rewrote `Program.DrawMenuBar` (Save/Load hidden
  while the editor is open) but the version-string block is untouched: still exactly one
  `ImGui.SetCursorPosY`, at `Program.cs:509→512`, right after `DrawProgramMenusHook()`.
- **GPU byte layouts identical** — `PartModel.PerInstanceData`, `PartModelDynamic.PerInstanceData`,
  `MaterialData`. humble-arteest's padding-byte hijack and doh's `handle*80+16` writes are safe.
- **Shaders** — `UnlitMesh.vert/.frag` and `MeshIndirect.vert` **byte-identical**. `MeshIndirect.frag`
  changed by one line (`SamplePortraitLight` → `SampleMeshForwardLights`, rev 5301), leaving
  humble-arteest's `vec3 sampledColor` anchor at `:114` and the `ENABLE_TEMPERATURE` LUT intact.
  `ModelPbr.frag` gained a `RAYTRACED_REFLECTIONS` variant; the `albedo` path and
  `Common/MaterialSet.glsl` are unchanged.
- **Coordinate frames unchanged** — rev 5280's `CelestialFrameMath.{ComputeCcf2Cci, ComposeCcf2Cce}` is
  a pure extraction; `Celestial.GetCcf2Cci`/`GetCci2Ccf`/`GetCci2Cce`/`GetCce2Cci` keep their
  signatures and semantics. `Camera.cs` and `KinematicMeasurements.cs` are **byte-identical**
  (glass, geeforce clean). `Battery.cs` byte-identical (eternal-flame clean).
- **Provider chokepoint intact** — `Universe.CurrentSystem` → `CelestialSystem.All` →
  `LookupCollection<T>.UnsafeAsList()` all unchanged.
- **doh's MMU walk survives a retype.** Rev 5269 changed `CharacterAvatar.Attachments.Mmu.MmuMesh`
  from `StaticMeshRenderable` to `AnimatedRenderable`; doh walks by field name and finds
  `MaterialIndices` anywhere in the hierarchy (`KittenSpawner.cs:542-556`), and `AnimatedRenderable`
  declares it (`:35`).
- **`Part` API churned but missed the suite.** Rev 5329 **removed** `Part.Sequence`, `SetSequence(int)`,
  `ActivateInStage(Vehicle?)`, `DeactivateInStage(Vehicle?)` and `ScaleTotal`; added
  `ActivateSubtreeInStage(Vehicle?, int)`, `Set`/`Shift SubtreeModulesSequence`,
  `CountEnabledSubtreeSequencedModules`, `HasSubtreeSequencedModule`, `GetSubtreeSequencedModules`,
  `RefreshScale`, `RefreshScaleAndReposition`, `RefreshTankContents`, `FindSurfaceMountPointPartAsmb`.
  **No unscience mod referenced any removed member.** `PartTree` gained public `RefreshStaticMass()`;
  kitchen-sink still `Traverse`s the private `RecomputeStaticMass`, which remains (flexo did too, until
  it was removed).
- **Assets** — every referenced id present and unchanged. `DefaultAssets.xml` removed the `LaunchPad`
  GltfFile (rev 5328) and added `StaticObject*`, seven `UiMask*` and `LightEvalStats`; none are
  referenced by unscience.
- **StarMap seams present** — `Program.OnDrawUiFrame` / `OnFrame` / `DrawProgramMenusHook`.

**Known-broken reconciliation this span**
- **zippo `GetField("Color")`** — ✅ **CLOSED; the previous scope text was stale.** The code reads
  `"ColorRgb"` (`zippo.lib/LightController.cs:59,80`), the real field name. Fixed by commit `07787ea`.
- **camera-controller-override `___Transform`** — ✅ still closed (retired @5261).
- **humble-arteest Vehicle Paint** — ❌ still dead by design (rev 4693); self-disables; anchors resolve.
- **mesh-deform** — ❌ still broken and **unchanged**: `MeshIndirect.vert` is byte-identical to 5261 and
  its struct anchor (`"    uint EmissiveColor;"` immediately followed by `};`) still doesn't match —
  the file has `uint EmissiveColor;` then `#endif` (`:16-17`). Its world-position anchor (`:63`) does
  match.
- **blinky broken** — ✅ **closed 2026-08-23.** Two separate bugs: (a) `LcdGridConfig.EnginePartId`
  defaulted to `"CorePropulsionA_Prefab_EngineA1"`, absent from Content — now `A3`, and `EngineA1` is
  out of `BlinkySubmod.EnginePresets`; (b) the real one — the pixel engines could not reach propellant
  because blinky connected `Part`↔`Part` instead of via the engine's **declared feed connector**,
  which the 5018 feed rewrite made mandatory. See `scope/pixel-grids-and-render.md` → *Root cause of
  "blinky broken"*.
- **unscience never wires `IvaForceRender.Patch`** — ❌ still open.
- **space-tape's API-drift cluster** — ✅ closed by removal.
- **flexo's `R-flexo-1`…`R-flexo-4` risk cluster** (by-name solver patch, the private
  cache-invalidation contract, `RecomputeStaticMass`, solver-phase tree mutation) — ✅ closed by removal.

---

### 5117 → 5261 (previous span — 420 decomp files, ~16.7k inserted lines)

Dominated by a **vehicle-threading rewrite** (`DynamicWorkerPool`/`ParallelBatch`/`PhysicsBubble`
islands, `BepuWorkerDispatcher`), the **`SimTime` → `UniverseTime`** 128-bit-nanosecond migration, an
**EVA/kitten overhaul** (control modes, ladders, jump/tumble anims, kitten cameras + portrait UI), a
**gauge/HUD context-visibility system**, and continued **render-pass modernization**.
Full review: [`../plans/KSA_5261_UPGRADE.md`](../plans/KSA_5261_UPGRADE.md).

**CHANGED (compile breaks against 5261 — all now FIXED)**
- `SimTime` **renamed** to `UniverseTime` (now `Int128` nanoseconds, was double seconds) and
  `Universe.GetElapsedSimTime()` → `Universe.GetElapsedTime()`, rev 5211. `.Seconds()` survives on the
  new type, and every consumer either calls it or passes the value into `Orbit.CreateFromStateCci`, so
  **no arithmetic changed**. → `ksa-abstractions.lib/SimTimeProvider.cs:9`,
  `doh.lib/Spawning/KittenSpawner.cs:167,257`, `garrys-torch.lib/WeldEngine.cs:119`.
  `scope/00-architecture-and-abstractions.md`, `scope/vehicle-physics.md`
- `JobSystems.VehicleSolvers` **split** into `VehicleSolver` (single-runner `JobScheduler`
  orchestrator) + `VehicleWorkerPool` (`DynamicWorkerPool` of parallel islands), revs 5208–5216. →
  `garrys-torch.lib/GarrysTorchSubmod.cs:93` now waits on `VehicleSolver`. This is the **complete**
  drain: the pool exposes no `Wait()` and is only driven through scoped `ParallelBatch()` fork/join,
  so pool work joins before the queued `_vehicleUpdateTask` finishes — the game drains identically in
  `Universe.DeserializeSave`. `scope/vehicle-physics.md`
- `ImageBarrierInfo.Presets.SampledReadFragment` → **`SampledReadF`** (abbreviation sweep:
  `…Vertex`→`…V`, `…Fragment`→`…F`, `…Compute`→`…C`). Same layout/access/stage. **Originated in the
  unvalidated 5118–5168 window**, not this build — the preset list is byte-identical OLD↔NEW. →
  `parts-now.lib/Runtime/ThumbnailReadback.cs:56,84`. `scope/part-editor-and-robotics.md`
- `Program.OffScreenPass` **removed**. Not a rename — the game **migrated the main scene pass from
  classic Vulkan render passes to dynamic rendering.** The offscreen target is now
  `Program.OffscreenTarget` (`RenderTarget : IRenderPassInfo`), which is `PassContext.MainOpaquePass`
  and has **no `.Pass` / `.SampleCount`**; pass compatibility comes from
  `SetupGraphicsPipeline(ref VkGraphicsPipelineCreateInfo)`, which chains a
  `VkPipelineRenderingCreateInfo` onto `pNext`, sets `RenderPass = VkRenderPass.NullHandle` and fills
  `MultisampleState` from the target's `Samples`. **Originated in the unvalidated 5118–5168 window.**
  → `thug-life.lib/ThugLifeQuadRenderer.cs:110` now mirrors the game's own main-pass pipelines
  (`GenericMeshRenderer.cs:305`, `PartModelRenderer.cs:184,269`, `PartModelGlass.cs:269`).
  `scope/pixel-grids-and-render.md`

**BEHAVIORAL (compile-clean, no symbol moved — needs a live pass)**
- ⚠️ **con-man vs the new gauge context system** (rev 5201). `GaugeCanvas` gained
  `VisibleInContext: List<GaugeVisibilityFlag>`, `IsContextVisible()` and `ApplyContextOverrides()`,
  and the draw gate is now
  `!_enabled || !IsContextVisible() || (this == Program.CrewPortraitsCanvas && !CrewPortraitPanel.HasOccupants)`.
  con-man's `_enabled` reflection still resolves, but **setting it true no longer guarantees the gauge
  draws** — a canvas with context flags (`Burn`, `Engines`, `EVA`, `Vehicle`, `Sequence`, `Target`,
  `IVA`, `Thrusters`, `Atmosphere`) stays hidden unless every flag matches the controlled vehicle.
  `GameSettings.Current.GaugeContextOverrides` (persisted to `settings.toml`) is a second source of
  truth con-man does not know about. **Open — not fixed.** `scope/ui-customization.md`
- ✅ **blinky's dark grids — root-caused and fixed 2026-08-23.** The missing `EngineA1` part id was
  real but secondary; the reason *every* grid stayed dark is that the **5018 fuel/resource rewrite made
  a declared feed connector mandatory**. `ResourceManager.CanFlowAcross` refuses the first hop out of
  a consumer part unless the connection sits on a connector named by the template's
  `ConsumerFeedWiring`/`FeedsFrom`, and the base check additionally demands `BulkFluid`, which a bare
  `Part`↔`Part` connection never carries (`Part.EndpointCapabilities` is `null`, and
  `Intersect(null, null)` gives `Electricity | ServiceFluid`). blinky now connects
  `RocketCore.FeedConnectors[i]` → fuel `Part`. Both defaults moved to `EngineA3`.
  `scope/pixel-grids-and-render.md`
- Vehicle-control lockout tightened (revs 5252/5253: `ControlsLockout`, engine shutdown blocked without
  a control module) — unladen-swallow RPC, blinky, its-so-shiny, eternal-flame.
- **All batteries ×10 maximum capacity** (rev 5227, Content-only) — eternal-flame, its-so-shiny,
  space-tape, red-alert.
- New kitten anim states (ladders/jump/tumble, revs 5203/5233/5244/5249) — kitten-animations, doh,
  garrys-torch. Map grid moved out of screen space + per-body grid plane (revs 5256/5257) — space-tape,
  flexo. Cursor/orbit-line arbitration rework (rev 5221) — marque, kiwis-marbles, space-tape, flexo.

**VERIFIED CLEAN this span**
- Entire string-reflection watchlist (§4) resolves; every Harmony patch-target signature unchanged
  (line shifts only), including `Universe.ExecuteNextVehicleSolvers(double, SimStep)` — still one overload.
- `PerInstanceData` and `MaterialData` byte layouts **identical**. `UnlitMesh.vert/.frag` and
  `MeshIndirect.vert` **identical**; `MeshIndirect.frag` changed by exactly one line (rev 5196 portrait
  lights) with humble-arteest's `vec3 sampledColor` anchor, `inStateFlags` guard and the
  `ENABLE_TEMPERATURE` LUT all intact.
- StarMap's seams (`Program.OnDrawUiFrame`/`OnDrawUiViewports`/`OnFrame`/`DrawProgramMenusHook`) present.
- `KittenEva` changes are purely **additive**. Rev 5242's row-major quaternion/mat3 fix changed **no
  `Brutal.Numerics` file** — frames used by garrys-torch/kiwis-marbles/thug-life are untouched.

---

### 5018 → 5117 (previous span — 223 decomp files, ~11.7k inserted lines; 103 Content files)

Dominated by a **crew/roster system** (KittenRoster, IVASeat, EVADoor↔seat linking, crew assignment
UI), a **burn/orbit UX rework** (right-click burn context menu, danger arcs), **launch pads**,
**vehicle structural destruction**, and **cloud shadows on vessels**.

**CHANGED (compile breaks against 5117 — both now FIXED)**
- `Double3Ex.{Up,Down,Left,Right,Forward,Backward}` **removed** (rev 5067: *"they were misleading and
  often misused"*). Game migrated its own uses to new `Camera.{ForwardView,RightView,UpView}` and
  renamed `Camera.GetForward/GetRight/GetUp` → `Get*Ecl` (**no mod calls the renamed accessors**).
  15 CS0117 in space-tape. → new `ksa-abstractions.lib/Directions.cs` (identical values).
  `scope/part-editor-and-robotics.md`, `scope/00-architecture-and-abstractions.md`
- `FlightComputer.VehicleConfigInfo.TotalEngineVacuumThrust` **removed** with the rest of the
  vacuum-referenced family (`TotalEngineVacuumMassFlowRate`, `TotalEngineExhaustVelocity`,
  `TotalEngineIsp`), rev 5114. 1 CS1061 in average-twr. → now
  `Vehicle.ComputeActiveThrust(FlightComputer.AmbientPressure)`, the game's own navball path.
  `scope/telemetry.md`

**CHANGED (silent — compile-clean, was actively corrupting; now FIXED)**
- `Part`'s **uncached-matrix sentinel** changed from `double4x4.Identity` to an all-NaN
  `UncachedMatrix` (rev 5112, `MatrixAsmb2VehicleAsmb` caching), plus three new cached fields.
  space-tape wrote `Identity` into `_matrixAsmb`/`_matrixAsmb2Parent` **to invalidate** them — on 5117
  that asserts a cached identity transform instead, collapsing the part transform with no build
  error. → all three sites now call the public `Part.ResetCachedPosMatrixValues()`; the reflection is
  retired. flexo's similar-looking `HingeController` path was **never** exposed (it touches property
  setters, which invalidate correctly). `scope/part-editor-and-robotics.md`

**BEHAVIORAL (compile-clean, no symbol moved — needs a live pass)**
- ⚠️ **space-tape `<EVADoor>` writer vs `EVADoorTemplate.SeatId`** (rev 5085). `SeatId` is new and is
  what links a door to an `IVASeat`; the EVA button now only appears when that seat is occupied.
  space-tape emits no `SeatId`, so **authored EVA doors will be inert**. (Its existing `ConnectorId`
  attribute was never an `EVADoorTemplate` member on 5018 either — pre-existing silent no-op.)
  **Open — not fixed here.** `scope/part-editor-and-robotics.md`
- ⚠️ **doh `KittenSpawner` vs the roster rework** (revs 5074/5083/5085/5102/5103). doh replicates the
  *old* `EVADoor.CreateKittenEva()` flow; the game now sources the kitten from `Universe.KittenRoster`
  via the door's aligned `IVASeat`, and `Vehicle` disposal finalizes kitten mission stats. The
  `KittenEva` ctor is signature-identical so doh still runs, but it spawns **roster-less** kittens.
  `scope/character-and-materials.md`
- ⚠️ **con-man vs `GaugeCanvas.PlaceBesideActiveBurnGizmo()`** (revs 5092/5113). New game code
  **writes `_customOffset`** — the exact private field con-man reflects and owns — to park the burn
  window beside the burn gizmo. The game can now overwrite con-man's saved layout offsets.
  `scope/ui-customization.md`
- ⚠️ **garrys-torch / kiwis-marbles vs vehicle structural destruction** (rev 5115). Vehicles can now be
  destroyed by exceeding a structural g-limit or dynamic-pressure limit. Torch teleports the vehicle
  every frame; marbles rewrites orbits. `scope/vehicle-physics.md`, `scope/celestial-and-lights.md`
- ⚠️ **`NavBallData.ThrustWeightRatio` changed meaning** (rev 5114) — now ambient-corrected and
  propellant-aware. average-twr's readings shift with no code change. `scope/telemetry.md`
- **Watch item — `EditorTag` gained `Booster`/`Coupling`/`Cargo`.** parts-now's `BuiltInEditorTags`
  still lists six. Harmless today (the three are neither registered in `VehicleEditor._editorTagLookup`
  nor declared in `PartGameData.xml`). `scope/part-editor-and-robotics.md`

**VERIFIED CLEAN across 5018 → 5117**
- **All 33 Harmony patch-target signatures byte-identical**, including the shared chokepoints
  `GameSettings.OnKeyAll`, `Universe.ExecuteNextVehicleSolvers` (still one overload),
  `Program.DrawProgramMenusHook`, all three `*Module.UpdateRenderData`, `PartModel(.Dynamic).AddInstance`,
  `SuperMeshRenderSystem.RenderMainPass`, `ShaderModuleUtils.FromFile`, both controller `OnFrame`s.
- **The entire §4 reflection watchlist resolves**, including all 7 `GaugeCanvas` fields,
  `Camera._fovRadians`, the KittenEva→`CharacterCore.Scale` chain, `LightModule+TemplateData.ColorRgb`,
  all six parts-now `ModLibrary.All*` registries, `SerializedCollection<T>._collection`, and
  `VehicleEditor._editorTagLookup`. (`ModLibrary.cs`'s diff is **only** log line-number churn.)
- **Byte-identical files:** `PartModel`, `PartModelDynamic`, `MaterialData`, `GpuMaterialSystem`,
  `SuperMeshRenderSystem`, `PartModelRenderer`, `LightModule`, `LightSwitch`, `CatExpressionAnim`,
  `CharacterAvatar`, `CharacterCore`, `GenericGizmo`, `OrbitLinePass`, `Controller`,
  `KeyframeAnimationModule`, `DeviceMeshInterleaved`, `ShaderModuleUtils`, `Situation`,
  `KinematicMeasurements`, `KSAColor`, `MusicPlayList`.
- **humble-arteest's GLSL anchors survive.** `MeshIndirect.frag`/`MeshIndirectRaytraced.frag` changed
  (rev 5100 cloud shadows) but only inside `getLightColor()`; the `vec3 sampledColor` anchor
  (`:114`/`:156`) and the `inStateFlags` varying (`:30`/`:20`) are intact, and the new `GetCloudShadow`
  resolves from `Common/Lighting.glsl:52` through the game's own include callback, which the mod
  passes straight through. **Still needs a live render pass.**
- 🔶 **`PerInstanceData` layout unchanged and `StateBitFlag` bits 11..31 still free** (the game uses
  ≤ bit 6 in the mesh shaders). humble-arteest Vehicle Paint's standing invariant **holds**.
- 🔶 **parts-now's load-order invariant holds:** `ModLibrary.LoadAll()` (`KSA/Program.cs:965`) still
  precedes `ModLibrary.Bind()` (`:994`).
- **thug-life:** `UnlitMesh*` shaders unchanged and both shader ids still in `DefaultAssets.xml`. The
  MSAA / alpha-to-coverage work (revs 5057/5058) is absorbed because the mod reads
  `Program.OffScreenPass.SampleCount` dynamically rather than hard-coding it. **Live pass still required.**
- **mesh-deform:** `MeshIndirect.vert` byte-identical → still self-disabled, unchanged.
- Assets removed in revs 5077/5096 (`IconSymmetry*`, `Icon*Gizmo`, `IconAngleSnapping*`,
  `PlanetMeshVertexDataComp`) are referenced by **no** mod in this repo.

---

### 4750 → 5018 (previous span)

Every table row whose 5018 status was BROKEN or CHANGED. The 4750→5018 span is large (433 decomp
files, ~40k inserted lines) and dominated by three game-side rewrites: **combustion → reaction
model**, **fuel/resource feed wiring**, and **staging → resource groups**.

**CHANGED (compile breaks against 5018 — all three now FIXED)**
- `PartTemplate.Tank` **removed**; tanks are now `Tank.TemplateData` entries in the generic
  `PartTemplate.Components` list (a part may have several). → `space-tape.lib/PartImporter.cs`
  now iterates `Components`. `scope/part-editor-and-robotics.md`
- `SubstanceLibrary.TryGetCombustionProcess` **removed** along with `CombustionObject`,
  `CombustionProcess`, `CombustionProcessTemplate`, `CombustionTable`. Replaced by
  `TryGetReaction` → `Reaction`/`FixedReaction`/`MixtureReaction`, and `Tank.ConfigureFor` now takes
  a `ReactantMix`. Mixture ratio left the asset id (`MMH_NTO_1.6` → `MMH_NTO` + `DefaultMixtureRatio`
  1.65). → `doh.lib/Spawning/KittenSpawner.cs`. `scope/character-and-materials.md`
- `RocketCore.ResourceManager` **moved down** to the `Combustor` subclass (`SolidMotor` cores have
  none); the base now exposes the feed-wiring model (`FeedConnectors`, `ResolvedConsumerFeeds`,
  `TryPrepareDrain`/`TryAccumulateDrain`). → `blinky.lib/BlinkySubmod.cs`. `scope/pixel-grids-and-render.md`

**CHANGED (silent — byte layout; guarded, no live break)**
- `PartModel.PerInstanceData.packing2` → **`public float Wetness`**, and
  `PartModelDynamic.PerInstanceData.packing1` → **`public float Wetness`**. New `ENABLE_WETNESS`
  shader variant (`MeshIndirect.vert/.frag`, gated on `GameSettings.Current.Graphics.VesselWater`).
  The game now **uses** a slot humble-arteest Vehicle Paint and mesh-deform write into. Both are
  already inert behind their `ShadersActive` probe, so nothing writes there today — but the hazard
  deepened. humble-arteest **Engine Emissive** is unaffected (it writes only `Temperature`/
  `TfiThickness`, whose offsets are unchanged). `scope/character-and-materials.md`, `scope/standalone-mods.md`

**BEHAVIORAL (compile-clean, no symbol moved — needs a live pass)**
- **Gauge/HUD rework vs con-man + marque.** Revs 4919/4940/4959/5003 moved the sequence UI, burn UI
  and all pop-ups into the GaugeCanvas system, added an `AlwaysEnabled` canvas flag, and **relocated
  the gauge enable/disable toggles from the View dropdown to a new Hud dropdown** that also ships a
  native **HudLayouts** save/load feature — i.e. a first-party re-implementation of con-man's whole
  feature, plus a move of the menu marque injects into. All 7 reflected fields still resolve.
  `scope/ui-customization.md`, `scope/standalone-mods.md`
- **`KeyframeAnimationModule.TimeGoal` now fans out to mirrored parts** (`ApplyToMirroredParts`), so a
  single write can move symmetry-partner parts too. → red-alert. `scope/celestial-and-lights.md`
- **Animation pipeline reworked**: `IAnimProcessor` gained `UpdateLocalPose(…, Span<TransformTRS>, …)`,
  `CatExpressionAnim.MixPose` → `MixPoseLocal`, `AnimatedRenderable` now builds a pose buffer.
  kitten-animations does **not** implement the interface and its `_expressionPose` cache-bust target
  is intact, so it still works — but this is prime suspect for the `ISSUES.md` "kitten animations
  always the same expression" report. `scope/character-and-materials.md`
- **Rev 4914 control-module lockout** (engine/thruster Active checkboxes, Decouple, staging key locked
  out with no control module) is implemented in the **UI layer only** — `EngineController.SetIsActive`
  and `ThrusterController.SetIsActive` are byte-identical to 4750, so blinky and its-so-shiny drive
  pixel engines unaffected. `scope/vehicle-physics.md`
- **Staging rewrite**: `StageList.cs` and `Staging.cs` deleted in favour of `ResourceGroups`/
  `ResourceGroupList`/`SequencePerformance*`. `Part.Stage` and `Part.SetStage(int)` are unchanged, so
  blinky/its-so-shiny stage alignment still compiles and reads correctly — but rev 4873 changed
  `SetStage`'s cost/rebuild behavior. `scope/pixel-grids-and-render.md`

**CLOSED — previously-recorded breaks confirmed already fixed in-repo (re-verified against 5018)**
- `Controller.___Transform` field injector (camera-controller-override) — **fixed**. The prefix reads
  `__instance.Camera` directly (`CameraControllerOverridePatches.cs:54`); 5018 still exposes
  `public Camera Camera` on `Controller` and still has no `Transform` field, so the fix remains
  correct. → `scope/camera.md`
- `LightModule.TemplateData."Color"` (zippo) — **fixed**. `LightController.cs:59,80` now read
  `"ColorRgb"`, which is the 5018 field name. → `scope/celestial-and-lights.md`
- unscience supermod `IvaForceRender.Patch` — **fixed**. Now wired at `unscience/Patcher.cs:66`
  (with a matching `Unpatch` at :100). → `scope/00-architecture-and-abstractions.md`

**BROKEN — genuinely still dead, pre-existing, NOT caused by 5018**
- `MeshIndirect.vert`/`.frag` GLSL anchors (humble-arteest **Vehicle Paint**, **mesh-deform**) — dead
  by design change since rev 4693 and still dead: 5018's `MeshIndirect.vert` still carries the
  `#ifdef ENABLE_*` feature gates (now extended with `ENABLE_WETNESS`/`ENABLE_FROST`), so both mods'
  content probes correctly self-disable. Reviving paint is a redesign, not a patch.
  → `scope/character-and-materials.md`, `scope/standalone-mods.md`

**VERIFIED CLEAN against 5018** — every Harmony patch target signature (`RenderMainPass`,
the three `*Module.UpdateRenderData`, `PartModel(.Dynamic).AddInstance`,
`PartModelRenderer.UpdateRenderData(Viewport,int)`, `Universe.ExecuteNextVehicleSolvers`,
`Program.DrawProgramMenusHook`, `Orbit`/`FlyController.OnFrame`, `Camera.ChangeFieldOfView`/
`UpdateProjection`, `Vehicle.GetWorldMatrix`/`UpdateRenderData`, `GameSettings.OnKeyAll`,
`GaugeCanvas.OnDrawMenuBar`); the full §4 watchlist; `MaterialData` layout (`AlbedoColor` @16,
stride 80) and `CharacterAvatar`/`CharacterCore` (still a **struct** with `public float Scale`);
`UnlitMesh.*`, `MaterialSet.glsl` and `ModelPbr.frag` (byte-identical → thug-life and doh Kitten
Color safe); the `#ifdef ENABLE_TEMPERATURE` LUT (Engine Emissive).

**parts-now (new since 5018)** — written against this build, so every row above is `OK` by
construction rather than by diff. It adds **no Harmony patch** (only the mandatory `HotkeyGuard`),
nine string-reflection names (§4) and two new game-DLL references (`Brutal.Vulkan.Vma`,
`Planet.Render.Core`). Its risk is concentrated in the 🔶 standing invariants **U1**–**U7** in
[`part-editor-and-robotics.md`](part-editor-and-robotics.md) → parts-now → *Update-risk findings*,
none of which the compiler or a symbol grep can check.
