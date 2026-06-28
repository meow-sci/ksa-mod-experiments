# RPC / HTTP Server (`unladen-swallow`) — Game Integration Scope

Permanent reference for detecting when KSA game updates break the **unladen-swallow** HTTP RPC
mod (`unladen-swallow/` + `unladen-swallow.lib/`). This mod has very little *direct* game
integration: almost every endpoint **delegates** to another feature lib whose game touchpoints are
catalogued in that lib's own `scope/` file. The two things unladen-swallow owns directly are
(a) the **game-thread marshaling** seam (off-thread HTTP work scheduled onto the game thread) and
(b) the **vehicle engine ignite/shutdown** endpoints, which call a typed KSA API.

**Verified game versions**

- NEW decomp `2026.6.9.4750` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
- OLD decomp `2026.6.8.4680` root: `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies_2026.6.8.4680\current\decomp`

Paths in the **Decomp path (NEW)** column are relative to the NEW decomp root (e.g. `KSA/Vehicle.cs`).
**Mod code** paths are relative to the repo root `C:\Users\Alex\repos\meow-sci\unscience`.

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
- **Bundled in unscience** — `unscience/Mod.cs:84` adds `new UnladenSwallowSubmod()` to the
  supermod's 22-submod list (`unscience.csproj:32` references `unladen-swallow.lib`). The supermod's
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
| 1 | StarMap lifecycle attrs | `unladen-swallow/Mod.cs:9,19,22,39,46,61` | `[StarMapMod]`/`ImmediateLoad`/`AllModsLoaded`/`BeforeGui`/`AfterGui`/`Unload` → game hooks `Program.OnDrawUiFrame(double)` (BeforeGui) & `Program.OnDrawUiViewports(double)` (AfterGui) | `KSA/Program.cs:2639`, `:2666` | Yes | None (OLD `:2582`/`:2609`) | StarMap.API seam (string-named hooks owned by StarMap, not this mod). See `scope/00-architecture-and-abstractions.md`. |
| 2 | Game-thread **drain** | `unladen-swallow.lib/UnladenSwallowSubmod.cs:23` (`GameThread.DrainOnGameThread()` in `Update`) | none — pure C# queue drain, runs inside `[StarMapBeforeGui]` | n/a | n/a | n/a | The single point where queued HTTP work executes on the game thread. No game member. |
| 3 | Off-thread **enqueue** | every endpoint, e.g. `FovEndpoint.cs:23,33`, `ActionIgnite.cs:26` (`GameThread.Scheduler.Schedule(...)`) | none — pure C# (`Task<T>` + ConcurrentQueue) | n/a | n/a | n/a | Marshals HTTP-worker work onto the game thread. No game member. |
| 4 | Harmony (shared, required) | `unladen-swallow/Patcher.cs:19` (Patch), `:31` (Unpatch); `:18` `PatchAll` finds **no** own patches | `MeowSci.KsaAbstractions.HotkeyGuard` → `GameSettings.OnKeyAll(GlfwKeyEvent)` | `KSA/GameSettings.cs:2379` | Yes | None (OLD `:2347`) | Mod declares zero Harmony patches of its own; only HotkeyGuard. See HotkeyGuard in `scope/00-…`. |
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
| **torch** `GET/POST/DELETE /torch/welds`, `POST /torch/welds/{modify,animate}`, `GET/POST/DELETE /torch/presets` | `TorchWelds/Modify/Animate/Presets Endpoint.cs` | `garrys-torch.lib` → `GarrysTorchSubmod.Instance` (`Welds`, `CreateWeld`, `RemoveWeld`, `GetPreset`, `WeldEntry`) | `scope/vehicle-physics.md` → *garrys-torch* |
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
| 1 | Direct typed API (method) | `ActionIgnite.cs:34`, `ActionShutdown.cs:34` | `KSA.Vehicle.SetEnum(Enum? enumValue)` — `public void` | `KSA/Vehicle.cs:4838` | Yes | None (OLD `:4776`, identical sig) | Compile-time bound (`using KSA;`). The `VehicleEngine` branch dispatches to private `SetAction`. |
| 2 | Direct typed API (enum) | `ActionIgnite.cs:34` (`MainIgnite`), `ActionShutdown.cs:34` (`MainShutdown`) | `KSA.VehicleEngine` — `public enum VehicleEngine : byte { MainIgnite, MainShutdown }` | `KSA/VehicleEngine.cs:3-6` | Yes | None (OLD identical) | Two-member enum; both members present/unchanged. |
| 3 | Effect path (downstream, not called directly) | (reached via #1) | `KSA.Vehicle.SetAction(VehicleEngine)` — `private void`, sets `_manualControlInputs.EngineOn = true/false` | `KSA/Vehicle.cs:4912` | Yes | None — body byte-identical (OLD `:4850`) | **No `IsControllable` gate on this path** (see findings). |
| 4 | Direct API (provider + read) | `ActionIgnite.cs:28-29`, `ActionShutdown.cs:28-29`, `BlinkyGridScanEndpoint.cs:88`, `BlinkyEngineDeactivateEndpoint.cs:52`, `BlinkyGridsEndpoint.cs:142`, `ShinyGridScanEndpoint.cs:80`, `ShinyGridsEndpoint.cs:150` | `VehicleProvider.GetAllVehicles()` → `KSA.Vehicle.Id` (read) — resolve vehicle by id | seam: `scope/00-…`; `Vehicle.Id` → `KSA/Astronomical.cs:85` | Yes | None | All vehicle lookups funnel through the ksa-abstractions seam, not raw `Universe`. |
| 5 | Game type reference only | `BlinkyGridScanEndpoint.cs:58` (`new List<Part>()`), plus `Vehicle` typing in the 7 `using KSA;` endpoints | `KSA.Part`, `KSA.Vehicle` (types passed to blinky/shiny libs) | `KSA/Part.cs`, `KSA/Vehicle.cs:28` | Yes | None | Types referenced only to hold/pass values into the libs; no further member calls. |

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
- **Delegated-lib risk dominates.** Because endpoints delegate, an RPC group breaking after a game
  update almost always means the *delegated lib* broke. Trace via the cross-reference table:
  fov→`scope/camera.md` (glass), blinky/shiny→`scope/pixel-grids-and-render.md`,
  camera→`scope/camera.md`, torch→`scope/vehicle-physics.md` (garrys-torch),
  zippo→`scope/celestial-and-lights.md`, vehicle-resolution→`scope/00-architecture-and-abstractions.md`.
  Camera's pre-existing `___Transform` field-injector defect (see `scope/camera.md`) would surface
  through `POST /camera/animate` as an inert animation, not an HTTP error.
