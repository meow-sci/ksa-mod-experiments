# Unladen Swallow � HTTP RPC Server Mod

An HTTP RPC server embedded in a KSA mod that exposes game mod functionality over a REST API. Named after Monty Python and the Holy Grail ("what is the airspeed velocity of an unladen swallow?").

Provides an ImGui control window (F11) with an enable/disable checkbox to start and stop the embedded GenHTTP server on `http://0.0.0.0:7887`.

## Features

- **ImGui control window** (F11 toggle)
- **Enable/disable HTTP server** via checkbox � start/stop without restarting the game
- **Live status indicator** � shows Running/Stopped with the server URL
- **Camera FOV control** via `GET /fov` and `POST /fov`
- **Health check** via `GET /health`

## API Endpoints

### `GET /health`

Server liveness probe.

```json
{ "status": "ok" }
```

### `GET /fov`

Returns the current camera FOV state.

```json
{
  "status": "ok",
  "data": {
    "currentFovDegrees": 50.0,
    "overrideFovDegrees": 50.0,
    "isOverrideActive": false
  }
}
```

### `POST /fov`

Sets the camera FOV override. Send `fov` > 0 to activate override, or `fov` <= 0 to disable it and return control to the game.

**Request:**
```json
{ "fov": 30.0 }
```

**Response:**
```json
{
  "status": "ok",
  "data": {
    "currentFovDegrees": 30.0,
    "overrideFovDegrees": 30.0,
    "isOverrideActive": true
  }
}
```

**Disable override:**
```json
{ "fov": 0 }
```

## Architecture

```
unladen-swallow (mod)
  -- ImGui window: F11 toggle, enable/disable checkbox, status display
  -- OnBeforeUi: drains GameThread queue (HTTP -> game thread work items)
  -- references unladen-swallow.lib

unladen-swallow.lib
  -- SwallowServer: GenHTTP host on 0.0.0.0:7887
  -- FovEndpoint: GET/POST /fov (game-thread-safe via GameThread.Scheduler)
  -- ApiTypes: ApiResponse<T>, FovRequest, FovState records
  -- references glass.lib (FovController)
  -- references ksa-abstractions.lib (GameThread)

glass.lib
  -- FovController: static FOV state + SetFov/ApplyFov/DisableOverride

ksa-abstractions.lib
  -- GameThread / GameStateQueue / IGameStateScheduler
```

### Thread Safety

HTTP request handlers run on GenHTTP worker threads. All game state interactions must happen on the game thread. `GameThread.Scheduler.Schedule(...)` enqueues a work item and returns a `Task<T>` that resolves when the game thread executes it in `OnBeforeUi`.

### Server Lifecycle

The server is NOT auto-started. Use the ImGui checkbox to start/stop. On mod unload, the server is stopped if running.

## Files

| File | Purpose |
|------|---------|
| `Mod.cs` | Mod entry point: lifecycle, game-thread draining, ImGui window |
| `Patcher.cs` | Harmony setup (no patches currently needed) |
| `unladen-swallow.csproj` | Mod project |
| `mod.toml` | StarMap mod descriptor |
