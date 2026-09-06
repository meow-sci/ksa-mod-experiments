# unladen-swallow.lib

Library project that hosts the embedded GenHTTP RPC server used by the unladen-swallow mod.

## Purpose

- Exposes game and mod functionality over HTTP JSON endpoints.
- Uses a strict thread-safety model: handlers run on HTTP worker threads, game state access runs through `GameThread.Scheduler.Schedule(...)`.
- Provides a shared API envelope and request/response DTOs in `ApiTypes.cs`.

## Endpoint Surface

All responses use:

```json
{ "status": "ok", "data": { } }
```

### Core

- `GET /health`
- `GET /fov`
- `POST /fov`

### Vehicle Actions

- `POST /vehicle/actions/ignite`
- `POST /vehicle/actions/shutdown`

### Blinky Grid Management

- `GET /blinky/grids`
- `POST /blinky/grids`
- `DELETE /blinky/grids?vehicleId=...&gridName=...`
- `POST /blinky/grids/scan`
- `POST /blinky/grids/scan-all`
- `POST /blinky/grids/repair` — re-wires a registered grid's propellant feed (`{ vehicleId, gridName }`) so its engines can light; needed for grids found by scanning

### Blinky Display Control

- `POST /blinky/animate`
- `DELETE /blinky/animate?vehicleId=...&gridName=...`
- `POST /blinky/animate/builtin`
- `POST /blinky/static`
- `POST /blinky/pattern`
- `POST /blinky/off`

### Blinky Settings and Engine Control

- `GET /blinky/render`
- `POST /blinky/render`
- `POST /blinky/engines/deactivate`

### Its-So-Shiny Grid Management

- `GET /shiny/grids`
- `POST /shiny/grids`
- `DELETE /shiny/grids?vehicleId=...&gridName=...`
- `POST /shiny/grids/scan`
- `POST /shiny/grids/scan-all`

### Its-So-Shiny Display Control

- `POST /shiny/animate`
- `DELETE /shiny/animate?vehicleId=...&gridName=...`
- `POST /shiny/static`
- `POST /shiny/pattern`
- `POST /shiny/off`

### Its-So-Shiny Appearance

- `GET /shiny/appearance?vehicleId=...&gridName=...`
- `POST /shiny/appearance`

### Camera Animation

- `POST /camera/animate`
- `GET /camera/status`
- `DELETE /camera/stop`

### Garry's Torch Welds

Weld create/modify/animate/preset payloads support independent X/Y/Z scale factors. Responses
always emit an XYZ object; legacy numeric request values remain accepted as uniform scale for
backwards compatibility.

- `GET /torch/welds`
- `POST /torch/welds`
- `DELETE /torch/welds`
- `POST /torch/welds/modify`
- `POST /torch/welds/animate`
- `GET /torch/presets`
- `POST /torch/presets`
- `DELETE /torch/presets`

## Dependencies

- `ksa-abstractions.lib` for game-thread scheduling and providers.
- `blinky.lib` for LCD engine grid and animation operations.
- `its-so-shiny.lib` for LCD light grid and animation operations.
- `glass.lib` for FOV controls.
- `camera-controller-override.lib` for camera sequencing.
- `garrys-torch.lib` for weld management.

## Development Notes

- Add or update routes in `SwallowServer.cs`.
- Keep endpoint handlers small and focused.
- Validate request inputs on HTTP thread before scheduling game-thread work when possible.
