# Unladen Swallow � HTTP RPC Server Mod

An HTTP RPC server embedded in a KSA mod that exposes game mod functionality over a REST API. Named after Monty Python and the Holy Grail ("what is the airspeed velocity of an unladen swallow?").

Provides an ImGui control window (F11) with an enable/disable checkbox to start and stop the embedded GenHTTP server on `http://0.0.0.0:7887`.

## Features

- **ImGui control window** (F11 toggle)
- **Enable/disable HTTP server** via checkbox
- **Camera animation sequencing** via `POST /camera/animate`, `GET /camera/status`, `DELETE /camera/stop` � start/stop without restarting the game
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

## Camera Animation Endpoints

### `POST /camera/animate`

Starts a camera animation sequence. Accepts a list of steps executed sequentially. Each step is a single animation or a group of animations that play simultaneously. If an animation is already playing it is stopped first.

Optionally include `returnToStart` to animate the camera back to its starting position after the sequence finishes.

**Request:**
```json
{
  "sequence": [
    {
      "orbit": {
        "degrees": 360,
        "durationSeconds": 10.0,
        "easing": "easeInOut"
      }
    }
  ],
  "returnToStart": {
    "durationSeconds": 3.0,
    "easing": "easeInOut"
  }
}
```

**Multi-step (zoom out → orbit → zoom in):**
```json
{
  "sequence": [
    { "zoomOut": { "speedMetersPerSecond": 10.0, "durationSeconds": 3.0, "easing": "easeOut" } },
    { "orbit": { "degrees": 180, "durationSeconds": 8.0, "easing": "easeInOut" } },
    { "zoomIn": { "speedMetersPerSecond": 10.0, "durationSeconds": 3.0, "easing": "easeIn" } }
  ]
}
```

**Group step (simultaneous orbit + zoom out):**
```json
{
  "sequence": [
    {
      "group": [
        { "orbit": { "degrees": 360, "durationSeconds": 12.0, "easing": "linear" } },
        { "zoomOut": { "speedMetersPerSecond": 3.0, "durationSeconds": 12.0, "easing": "easeIn" } }
      ]
    }
  ]
}
```

Available animation types: `zoomOut`, `zoomIn`, `zoomInToOffset`, `orbit`, `loopyOrbit`, `spiralZoomIn`, `spiralZoomOut`, `shake`, `pan`, `rotate`.

All animations share: `durationSeconds` (required), `easing` (`linear`/`easeIn`/`easeOut`/`easeInOut`), `easingPowerStart`, `easingPowerEnd`.

**Response:**
```json
{
  "status": "ok",
  "data": { "keyframeCount": 1, "totalDurationSeconds": 10.0, "returnToStartEnabled": true }
}
```

### `GET /camera/status`

Returns current playback state.

```json
{
  "status": "ok",
  "data": {
    "state": "Playing",
    "isReturningToStart": false,
    "currentKeyframeIndex": 0,
    "totalKeyframes": 1,
    "totalElapsedTime": 3.2,
    "totalDurationSeconds": 10.0
  }
}
```

### `DELETE /camera/stop`

Stops any running animation and returns the previous state.

```json
{ "status": "ok", "data": { "previousState": "Playing" } }
```

> **Requires:** `camera-controller-override` mod to be loaded. Returns 503 if it is not.

## Garry's Torch Weld Endpoints

Control the vehicle welding system from Garry's Torch remotely.

Scale is returned as `{ "x", "y", "z" }`, with each axis constrained to 0.05–20.0.
Requests should use that vector form; a legacy numeric value is still accepted and expanded uniformly.

### `GET /torch/welds`

Returns all active welds.

```json
{
  "status": "ok",
  "data": {
    "welds": [
      {
        "sourceVehicleId": "my-lander",
        "targetVehicleId": "station-core",
        "position": { "x": 0, "y": 0, "z": 2.5 },
        "rotation": { "x": 0, "y": 0, "z": 0 },
        "scale": { "x": 1.0, "y": 0.75, "z": 1.25 },
        "lockRotation": true
      }
    ]
  }
}
```

### `POST /torch/welds`

Create a new weld. Provide either `data` (inline config) or `presetName` (not both).

**With inline data:**
```json
{
  "sourceVehicleId": "my-lander",
  "targetVehicleId": "station-core",
  "data": {
    "position": { "x": 0, "y": 0, "z": 2.5 },
    "rotation": { "x": 0, "y": 0, "z": 0 },
    "scale": { "x": 1.0, "y": 0.75, "z": 1.25 },
    "lockRotation": true
  }
}
```

**With preset:**
```json
{
  "sourceVehicleId": "my-lander",
  "targetVehicleId": "station-core",
  "presetName": "Docking Position"
}
```

### `DELETE /torch/welds`

Remove a weld (unweld the source vehicle).

```json
{ "sourceVehicleId": "my-lander" }
```

### `POST /torch/welds/modify`

Immediately modify an existing weld. Only provided fields are updated; omit fields to leave them unchanged.

```json
{
  "sourceVehicleId": "my-lander",
  "position": { "x": 0, "y": 0, "z": 5.0 },
  "scale": { "x": 0.75, "y": 1.0, "z": 1.5 }
}
```

### `POST /torch/welds/animate`

Smoothly interpolate a weld to a new state over a specified duration. Animations are queued if one is already running.

```json
{
  "sourceVehicleId": "my-lander",
  "durationSeconds": 3.0,
  "data": {
    "position": { "x": 0, "y": 0, "z": 5.0 },
    "rotation": { "x": 0, "y": 180, "z": 0 },
    "scale": { "x": 0.5, "y": 0.75, "z": 1.0 },
    "lockRotation": true
  },
  "easing": {
    "easing": "easeInOut",
    "easingPowerStart": 3.0,
    "easingPowerEnd": 3.0
  }
}
```

**Easing types**: `linear`, `easeIn`, `easeOut`, `easeInOut`

### `GET /torch/presets`

List all saved weld presets.

### `POST /torch/presets`

Save or update a named preset.

```json
{
  "name": "Docking Position",
  "data": {
    "position": { "x": 0, "y": 0, "z": 2.5 },
    "rotation": { "x": 0, "y": 0, "z": 0 },
    "scale": { "x": 1.0, "y": 1.0, "z": 1.0 },
    "lockRotation": true
  }
}
```

### `DELETE /torch/presets`

Delete a preset by name.

```json
{ "name": "Docking Position" }
```

## Architecture

```
unladen-swallow (mod)
  -- ImGui window: F11 toggle, enable/disable checkbox, status display
  -- OnBeforeUi: drains GameThread queue (HTTP -> game thread work items)
  -- references unladen-swallow.lib

unladen-swallow.lib
  -- SwallowServer: GenHTTP host on 0.0.0.0:7887
  -- FovEndpoint: GET/POST /fov
  -- BlinkyAnimateEndpoint, BlinkyStaticEndpoint, BlinkyOffEndpoint, BlinkyListEndpoint
  -- CameraAnimateEndpoint: POST /camera/animate
  -- CameraStatusEndpoint: GET /camera/status
  -- CameraStopEndpoint: DELETE /camera/stop
  -- ApiTypes: all request/response records
  -- references glass.lib, blinky.lib, camera-controller-override.lib, ksa-abstractions.lib

glass.lib
  -- FovController: static FOV state + SetFov/ApplyFov/DisableOverride

camera-controller-override.lib
  -- CameraControllerOverrideSubmod.Instance: static accessor for RPC
  -- KeyframeSequencePlayer: animation playback engine

ksa-abstractions.lib
  -- GameThread / GameStateQueue / IGameStateScheduler
```

### Thread Safety

HTTP request handlers run on GenHTTP worker threads. All game state mutations must happen on the game thread.   `GameThread.Scheduler.Schedule(...)` enqueues a work item and returns a `Task<T>` that resolves when the game thread executes it in `OnBeforeUi`.

Reading game state DOES NOT need to run on a game thread and can run on the the web server thread handling the request

### Server Lifecycle

The server is NOT auto-started. Use the ImGui checkbox to start/stop. On mod unload, the server is stopped if running.

## Files

| File | Purpose |
|------|---------|
| `Mod.cs` | Mod entry point: lifecycle, game-thread draining, ImGui window |
| `Patcher.cs` | Harmony setup (no patches currently needed) |
| `unladen-swallow.csproj` | Mod project |
| `mod.toml` | StarMap mod descriptor |
