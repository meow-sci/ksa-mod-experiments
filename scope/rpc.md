# RPC / HTTP Server (`unladen-swallow`) — Game Integration Scope

Permanent reference for detecting when KSA game updates break the **unladen-swallow** HTTP RPC
mod (`unladen-swallow/` + `unladen-swallow.lib/`). This mod has very little *direct* game
integration: almost every endpoint **delegates** to another feature lib whose game touchpoints are
catalogued in that lib's own `scope/` file. The two things unladen-swallow owns directly are
(a) the **game-thread marshaling** seam (off-thread HTTP work scheduled onto the game thread) and
(b) the **vehicle engine ignite/shutdown** endpoints, which call a typed KSA API.

**Verified game versions**

- NEW decomp `2026.9.7.5402` root: `~/repos/meow-sci/ksa-game-assemblies/current/decomp`
- OLD decomp `2026.8.22.5348` root: `~/repos/meow-sci/ksa-game-assemblies_prev/current/decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root (e.g. `KSA/Vehicle.cs`);
line numbers are **@5402** unless a cell says otherwise. **Mod code** paths are relative to the repo
root `~/repos/meow-sci/unscience`.

---

## Purpose

An embedded **GenHTTP** server (3rd-party HTTP library — **not** a game API) bound to
`0.0.0.0:7887` that exposes other mods' functionality over a REST/JSON API. Named for Monty Python
("airspeed velocity of an unladen swallow"). It lets external tools drive FOV, LCD pixel grids
(blinky), LCD light grids (its-so-shiny), cinematic camera animation, vehicle welds (garry's torch),
vehicle lights (zippo), and engine ignite/shutdown over HTTP. Server is **not** auto-started; an
ImGui checkbox (F11 window) starts/stops it.

---

## Unscience integration

Two host paths exist; both load in the same game process and share the same lib assemblies, so the
sibling submods' static `.Instance` accessors are visible to the RPC endpoints.

- **Standalone mod** — `unladen-swallow/Mod.cs` is its own `[StarMapMod]`. It news a
  `UnladenSwallowSubmod`, applies `Patcher.Patch()`, and drains the game-thread queue from its own
  `[StarMapBeforeGui]` (`Mod.cs:39-44` → `_submod.Update(dt)`).
- **Bundled in unscience** — `unscience/Mod.cs:92` adds `new UnladenSwallowSubmod()` to the
  supermod's 26-submod list (`unscience.csproj:34` references `unladen-swallow.lib`). The supermod's
  per-frame submod loop calls the same `Update(dt)` → same drain.
- **Delegation reach.** `unladen-swallow.lib.csproj:13-19` references exactly seven libs:
  `ksa-abstractions.lib`, `glass.lib`, `blinky.lib`, `its-so-shiny.lib`,
  `camera-controller-override.lib`, `garrys-torch.lib`, `zippo.lib`. Camera/torch/zippo endpoints
  additionally require the sibling submod's `.Instance` to be set (in that submod's `Initialize()`,
  e.g. `CameraControllerOverrideSubmod.cs:116`). When unladen-swallow runs **bundled in unscience**
  those siblings are loaded → `.Instance` is non-null. If it ran **without** them, camera/torch/zippo
  return HTTP **503 ServiceUnavailable** (fov/blinky/shiny still work — they use *static* lib classes).
- **HotkeyGuard** is applied per the repo rule (`Patcher.cs:19`/`:31`).

---

## Server lifecycle & game-thread marshaling

**Lifecycle.** `StartAsync()`/`StopAsync()` (`SwallowServer.cs:31`,`:49`) build a GenHTTP `Host`
(`Host.Create().Handler(api).Bind(0.0.0.0, 7887).Defaults(...).Console().Development().StartAsync()`,
`SwallowServer.cs:38-44`). Routes registered in `RegisterRoutes` (`SwallowServer.cs:62-163`). The
ImGui checkbox toggles start/stop (`UnladenSwallowSubmod.cs:44-69`) using **blocking**
`.GetAwaiter().GetResult()` on the UI/game thread (`:50`,`:62`). Unhandled exceptions are mapped to
JSON by `JsonErrorMapper` (`SwallowServer.cs:167-195`); `ProviderException.Status` controls the code.

**Marshaling pattern (the core game-facing dependency).** GenHTTP handlers run on HTTP worker
threads; KSA game state is not thread-safe. Endpoints call
`GameThread.Scheduler.Schedule(() => …)` (ksa-abstractions.lib) to enqueue a work item returning a
`Task<T>`; the game thread executes the queue via `GameThread.DrainOnGameThread()`
(`UnladenSwallowSubmod.cs:23`, inside `Update` → `[StarMapBeforeGui]`). The scheduler trio
(`GameThread`/`GameStateQueue`/`IGameStateScheduler`) is **pure C#** (ConcurrentQueue +
TaskCompletionSource) with **no game API surface** — catalogued in
`scope/00-architecture-and-abstractions.md`. (Pure reads may run off-thread per README, but all
mutations are scheduled.)

### Integration-points table (lifecycle + marshaling)

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | StarMap lifecycle attrs | `unladen-swallow/Mod.cs:9,19,22,39,46,61` | `[StarMapMod]`/`ImmediateLoad`/`AllModsLoaded`/`BeforeGui`/`AfterGui`/`Unload` → game hooks `Program.OnDrawUiFrame(double)` (BeforeGui) & `Program.OnDrawUiViewports(double)` (AfterGui) | `KSA/Program.cs:3021`, `:3051` | Yes | Signatures none (OLD `:2892`/`:2921`); `OnDrawUiViewports` body now iterates `ViewportRegistry.GameViews` (5402) — irrelevant to a postfix | StarMap.API seam (string-named hooks owned by StarMap, not this mod). See `scope/00-architecture-and-abstractions.md`. |
| 2 | Game-thread **drain** | `unladen-swallow.lib/UnladenSwallowSubmod.cs:23` (`GameThread.DrainOnGameThread()` in `Update`) | none — pure C# queue drain, runs inside `[StarMapBeforeGui]` | n/a | n/a | n/a | The single point where queued HTTP work executes on the game thread. No game member. |
| 3 | Off-thread **enqueue** | every endpoint, e.g. `FovEndpoint.cs:23,33`, `ActionIgnite.cs:26` (`GameThread.Scheduler.Schedule(...)`) | none — pure C# (`Task<T>` + ConcurrentQueue) | n/a | n/a | n/a | Marshals HTTP-worker work onto the game thread. No game member. |
| 4 | Harmony (shared, required) | `unladen-swallow/Patcher.cs:19` (Patch), `:31` (Unpatch); `:18` `PatchAll` finds **no** own patches | `MeowSci.KsaAbstractions.HotkeyGuard` → `GameSettings.OnKeyAll(GlfwKeyEvent)` | `KSA/GameSettings.cs:3301` | Yes | None (OLD `:3301`; file byte-identical) | Mod declares zero Harmony patches of its own; only HotkeyGuard. See HotkeyGuard in `scope/00-…`. |
| 5 | ImGui toggle | `unladen-swallow/Mod.cs:52` (`ImGui.IsKeyPressed(ImGuiKey.F11)`), `:81` (`ImGui.Begin`) | Brutal.ImGuiApi only — not a KSA game member | `Brutal.ImGuiApi/*` | Yes | None observed | F11 toggles the control window. Rides Brutal packages (rev-4729 watch). |
| 6 | GenHTTP host bind | `unladen-swallow.lib/SwallowServer.cs:38-44` (`Host.Create()…Bind(0.0.0.0,7887)…StartAsync()`) | none — GenHTTP 10.5.0 (3rd-party) + OS socket | n/a | n/a | n/a | NOT a game API. Breakage here = library/port/firewall, not a game update. |

---

## Endpoint → delegated-lib cross-reference

Makes RPC breakage traceable to the delegated lib. "Scope file" is where that lib's game touchpoints
are verified — re-check it when an endpoint group misbehaves after a game update.

| Endpoint group (routes) | Mod handler(s) | Delegated lib API (namespace) | Game touch lives in scope file |
|---|---|---|---|
| **health** `GET /health` | inline in `SwallowServer.cs:65` | none — returns `{status:"ok"}` | — (no game touch) |
| **fov** `GET/POST /fov` | `FovEndpoint.cs` | `glass.lib` → `FovController` (`GetCurrentFovDegrees`, `OverrideFovDegrees`, `IsOverrideActive`, `SetFov`, `DisableOverride`) | `scope/camera.md` → *glass* |
| **vehicle actions** `POST /vehicle/actions/{ignite,shutdown}` | `ActionIgnite.cs`, `ActionShutdown.cs` | **DIRECT game API** `Vehicle.SetEnum(VehicleEngine.*)` + `VehicleProvider` (ksa-abstractions) — not a feature lib | engines: `scope/vehicle-physics.md`; resolve: `scope/00-architecture-and-abstractions.md` (VehicleProvider). See *Direct game touchpoints* below. |
| **blinky** `/blinky/grids[/scan,/scan-all]`, `/blinky/animate[/builtin]`, `/blinky/{static,pattern,off,render}`, `/blinky/engines/deactivate` | `Blinky*Endpoint.cs` (12 files) | `blinky.lib` → `BlinkyGridManager`, `PixelGrid`, `LcdGridBuilder`, `BlinkyPixelGrid`, `NonLcdEngineCache`, render settings | `scope/pixel-grids-and-render.md` → *blinky* |
| **shiny** `/shiny/grids[/scan,/scan-all]`, `/shiny/{animate,static,pattern,off,appearance}` | `Shiny*Endpoint.cs` (9 files) | `its-so-shiny.lib` → shiny grid manager / builder / appearance | `scope/pixel-grids-and-render.md` → *its-so-shiny* |
| **camera** `POST /camera/animate`, `GET /camera/status`, `DELETE /camera/stop` | `CameraAnimateEndpoint.cs`, `CameraStatusEndpoint.cs`, `CameraStopEndpoint.cs` | `camera-controller-override.lib` → `CameraControllerOverrideSubmod.Instance.SequencePlayer` (`KeyframeSequencePlayer`, animation types) | `scope/camera.md` → *camera-controller-override* |
| **torch** `GET/POST/DELETE /torch/welds`, `POST /torch/welds/{modify,animate}`, `GET/POST/DELETE /torch/presets` | `TorchWelds/Modify/Animate/Presets Endpoint.cs` | `garrys-torch.lib` → `GarrysTorchSubmod.Instance` (`Welds`, `CreateWeld`, `ModifyWeld`, `AnimateWeld`, presets); scale is explicit XYZ in responses/new requests, with legacy numeric request values expanded uniformly | `scope/vehicle-physics.md` → *garrys-torch* |
| **zippo** `GET /zippo/lights`, `POST /zippo/lights/state`, `POST/DELETE /zippo/animate` | `ZippoLights/LightState/Animate Endpoint.cs` | `zippo.lib` → `ZippoSubmod.Instance` (`GetLightPartInfos`, set state, queue/clear animations) | `scope/celestial-and-lights.md` → *zippo* |

> camera/torch/zippo handlers resolve the sibling submod via its static `.Instance`; if null they
> throw `ProviderException(ServiceUnavailable)` → **HTTP 503**. fov/blinky/shiny use *static* lib
> classes and need no `.Instance`.

---

## Direct game touchpoints

Beyond delegating to libs, unladen-swallow's endpoints touch the game directly in only three ways.
Vehicle **resolution** goes through `VehicleProvider.GetAllVehicles()` (the ksa-abstractions seam —
its internal `Universe.CurrentSystem → CelestialSystem.All → LookupCollection.UnsafeAsList →
OfType<Vehicle>` chain is verified in `scope/00-architecture-and-abstractions.md`). The endpoints do
**not** call `Universe.*` themselves. The only *typed KSA member call* unique to this mod is engine
ignite/shutdown.

| # | Kind | Mod code (file:line) | Game target (Type.Member + signature) | Decomp path (NEW) | In NEW? | Δ vs OLD | Risk/notes |
|---|------|----------------------|----------------------------------------|-------------------|---------|----------|------------|
| 1 | Direct typed API (method) | `ActionIgnite.cs:34`, `ActionShutdown.cs:34` | `KSA.Vehicle.SetEnum(Enum? enumValue)` — `public void` | `KSA/Vehicle.cs:6096` | Yes | None (OLD `:5879`, identical sig; `VehicleEngine` branch unchanged) | Compile-time bound (`using KSA;`). The `VehicleEngine` branch dispatches to private `SetAction`. |
| 2 | Direct typed API (enum) | `ActionIgnite.cs:34` (`MainIgnite`), `ActionShutdown.cs:34` (`MainShutdown`) | `KSA.VehicleEngine` — `public enum VehicleEngine : byte { MainIgnite, MainShutdown }` | `KSA/VehicleEngine.cs:3-6` | Yes | None (file byte-identical) | Two-member enum; both members present/unchanged. |
| 3 | Effect path (downstream, not called directly) | (reached via #1) | `KSA.Vehicle.SetAction(VehicleEngine)` — `private void`, sets `_manualControlInputs.EngineOn = true/false` | `KSA/Vehicle.cs:6184` | Yes | None — body byte-identical (OLD `:5967`) | **No `IsControllable` gate on this path** (`IsControllable` itself is at `:588`; see findings). |
| 4 | Direct API (provider + read) | `ActionIgnite.cs:28-29`, `ActionShutdown.cs:28-29`, `BlinkyGridScanEndpoint.cs`, `BlinkyEngineDeactivateEndpoint.cs`, `BlinkyGridsEndpoint.cs`, `ShinyGridScanEndpoint.cs`, `ShinyGridsEndpoint.cs` (each `VehicleProvider.GetAllVehicles().FirstOrDefault(v => v.Id == …)`) | `VehicleProvider.GetAllVehicles()` → `KSA.Vehicle.Id` (read) — resolve vehicle by id | seam: `scope/00-…`; `Vehicle.Id` → `KSA/Astronomical.cs:104` | Yes | None (OLD `:104`) | All vehicle lookups funnel through the ksa-abstractions seam, not raw `Universe`. |
| 5 | Game type reference only | `BlinkyGridScanEndpoint.cs:58`, `ShinyGridScanEndpoint.cs:48` (`new List<Part>()`), plus `Vehicle` typing in the 7 `using KSA;` endpoints | `KSA.Part`, `KSA.Vehicle` (types passed to blinky/shiny libs) | `KSA/Part.cs`, `KSA/Vehicle.cs:28` | Yes | None | Types referenced only to hold/pass values into the libs; no further member calls. |

---

## Update-risk findings (4680 → 4750)

- **No typed/compile breaks. The whole project builds clean against NEW (4750)** (recon task #7), so
  every typed game member the endpoints touch (`Vehicle.SetEnum`, `VehicleEngine.MainIgnite/MainShutdown`,
  `Vehicle.Id`, `Vehicle`/`Part` types) still resolves with an identical signature. Only line numbers
  moved (`Vehicle.SetEnum` OLD `:4776` → NEW `:4838`).
- **rev 4699 `Vehicle.IsControllable` — does NOT block the RPC ignite/shutdown path (downstream
  watch only).** `SetEnum(VehicleEngine.*)` → private `SetAction` (`KSA/Vehicle.cs:4912`, byte-identical
  to OLD `:4850`) writes `_manualControlInputs.EngineOn` **unconditionally** — no `IsControllable`
  check. The new `IsControllable` gate (`KSA/Vehicle.cs:526`, backed by `PartTree.Controls`, absent in
  OLD) is confined to UI `Hovered(...)` tooltip helpers (`:5182`,`:5197`,`:5232`) and FlightComputer
  warp/burn actions (`:4861`,`:4883`,`:5033`) — **not** the engine ignite/shutdown mutation. So the RPC
  call still flips `EngineOn` regardless of controllability; the only residual, unverified behavioral
  question is whether `EngineOn` yields thrust on a Control-Module-less vehicle. (This refines the
  task's hypothesis: the *API* is ungated; any no-op would be a deeper solver behavior, not an API break.)
- **rev 4729 Brutal package bump (secondary watch).** unladen-swallow's only ImGui surface is the F11
  control window (`Mod.cs`, `UnladenSwallowSubmod.RenderContent`) and `SubmodUI`. It builds clean
  against the 4750 Brutal DLLs, so no used signature shifted; flag for re-check on each Brutal bump.
- **GenHTTP 10.5.0 (3rd-party) and the marshaling seam carry no game-update risk.** Server bind,
  routing, serialization, and the `GameThread` scheduler trio touch no KSA member; their breakage
  modes are library/version/port, not a game update.
- **`Microsoft.Extensions.ObjectPool` is a game-shipped assembly — bind to the game's copy, not
  GenHTTP's.** GenHTTP pulls it in transitively at **10.x**, but KSA ships **11.x**
  (`Microsoft.Extensions.ObjectPool.dll`, 11.0.0-preview.5) and that is what
  `Brutal.Core.Strings`/`Brutal.Core.Logging` reference — so it is already loaded in the game process
  before any mod runs. Compiling against the package version produced an unresolvable **MSB3277**
  10.0.0.0-vs-11.0.0.0 conflict in `unladen-swallow.lib`, `unladen-swallow` and `unscience`, and
  shipped a stale 10.x copy into each mod folder that could never win at runtime.
  **Resolution (all three projects):** a `<PackageReference … ExcludeAssets="all">` to drop the
  package's assets, plus a `<Reference>` to `$(KSAFolder)Microsoft.Extensions.ObjectPool.dll` with
  `<Private>false</Private>` — the same pattern already used for every `Brutal.*`/`KSA` assembly. The
  explicit copy of the DLL was also removed from the `GenHttpTransitiveDeps` deploy list.
  ⚠ **On a game update, re-check this version.** If KSA ever ships an ObjectPool *older* than what
  GenHTTP requires, the assembly load would fail at runtime rather than at build time.
- **Delegated-lib risk dominates.** Because endpoints delegate, an RPC group breaking after a game
  update almost always means the *delegated lib* broke. Trace via the cross-reference table:
  fov→`scope/camera.md` (glass), blinky/shiny→`scope/pixel-grids-and-render.md`,
  camera→`scope/camera.md`, torch→`scope/vehicle-physics.md` (garrys-torch),
  zippo→`scope/celestial-and-lights.md`, vehicle-resolution→`scope/00-architecture-and-abstractions.md`.
  Camera's then-current `___Transform` field-injector defect (see `scope/camera.md`; **retired @5261**,
  the prefix now reads `__instance.Camera`) surfaced through `POST /camera/animate` as an inert
  animation, not an HTTP error.

---

## Area summary — Update-risk findings (5261 → 5348)

- ✅ **unladen-swallow has no direct game reads and none of its delegated libs broke.** Every endpoint's
  target surface is unchanged this span: blinky (`PartTree.CreateFromNewPartTree`,
  `EngineController.SetIsActive`), its-so-shiny, glass (`Camera._fovRadians` — `Camera.cs` is
  byte-identical), camera-controller-override (`OrbitController`/`FlyController.OnFrame`), garrys-torch
  (`JobSystems.VehicleSolver.Wait()`, `Vehicle.Teleport`), eternal-flame
  (`Vehicle.RefillConsumables`, `Battery.Refill`) and kiwis-marbles (`Celestial.SetOrbit`).
- ✅ **`GameThread` marshaling clean.** The StarMap hooks the queue drains on
  (`Program.OnDrawUiFrame` / `OnFrame`) are unchanged.
- ⚠️ **`POST /camera/animate` is now genuinely live.** It was inert while
  camera-controller-override's `___Transform` field injector was broken; that was fixed at 5261 and
  remains fixed at 5348. Worth exercising over HTTP as part of the live pass — it is the quickest way to
  drive the camera mods without UI clicking.
- ℹ️ **Transport is game-free.** GenHTTP, the routing table and the DTOs couple to nothing in KSA and can
  only break via package churn. No GenHTTP bump this span.

---

## Area summary — Update-risk findings (5348 → 5402)

Span note: only rev **5401** ("Fixed crash for incorrect data stride for thumbnail rendering") is
logged; revisions **5349–5400 are unlogged**, so the source diff is the only evidence. The span's
headline change — `Viewport` → `IViewport`/`IGameViewport`/`ViewportRegistry` — does not touch
anything unladen-swallow calls directly.

- ✅ **No direct game touchpoint moved.** `Vehicle.SetEnum(Enum?)` (`KSA/Vehicle.cs:6096`) and the
  private `SetAction(VehicleEngine)` it dispatches to (`:6184`) are byte-identical to 5348
  (`:5879`/`:5967`); `EngineOn` is still written unconditionally (no `IsControllable` gate, `:588`).
  `KSA/VehicleEngine.cs` byte-identical. `Vehicle.Id` → `Astronomical.Id` (`:104`) unchanged.
- ✅ **Vehicle resolution seam clean.** `VehicleProvider.GetAllVehicles()` → `Universe.CurrentSystem`
  (`:94`) → `CelestialSystem.All` (`:64`) → `LookupCollection.UnsafeAsList()` (`:210`, file identical).
  `Program.ControlledVehicle` (`:503`) is a property with a `ClearHeldPlayerInput()` setter — it already
  was at 5348; the provider only reads it.
- ✅ **StarMap hooks / `GameThread` drain unchanged.** `Program.OnDrawUiFrame` (`:3021`) and
  `OnDrawUiViewports` (`:3051`) keep `private void (double)`; the latter's body now walks
  `ViewportRegistry.GameViews` and draws only `HasUi` secondary viewports, which is irrelevant to a
  postfix. `[StarMapBeforeGui]` still drains the queue every frame while the HUD is visible; the
  hidden-HUD (F2) replay via `HiddenUiFrameHook` → `Program.OnDrawUiConsole` (`:3009`, called `:2201`)
  also survives (see `scope/00-…`).
- ✅ **HotkeyGuard clean.** `GameSettings.cs` byte-identical (`OnKeyAll` `:3301`). `Program.OnKey`
  (`:1718`) split its guard chain into two `if`s (`:1723`, `:1727`), but `OnKeyAll` is still the first
  game term and the guard's forced `true` still returns before camera/controller handling.
- ⚠️ **`Microsoft.Extensions.ObjectPool` version — unverifiable from the macOS snapshot.** Neither
  `ksa-game-assemblies/current/dll` nor `_prev` contains `Microsoft.Extensions.ObjectPool.dll`, and
  `Directory.Build.props:52` points `KSAFolder` at that directory on this machine, so the
  `<Reference Condition="Exists(…)">` in both `unladen-swallow*.csproj` is false here (pre-existing;
  the build still passes because the GenHTTP package assets are excluded, not because the game copy
  is bound). Re-check the shipped version on a Windows install before trusting the 11.x-vs-10.x
  binding note above.
- ✅ **Delegated libs:** the only compile break in the suite this span was the `Viewport`→`IViewport`
  retype (IvaForceRender, i-feel-seen, dont-stifle-me, graffiti, parts-now, thug-life-adjacent) — none
  on an unladen-swallow endpoint path except via glass/blinky/its-so-shiny/camera/torch/zippo, which are
  re-verified in their own scope files. GenHTTP 10.5.0 unchanged.
- **Live pass wanted:** `POST /vehicle/actions/ignite` on a controlled vehicle; `POST /camera/animate`
  (still the quickest end-to-end check of the camera mods after the viewport rework).
