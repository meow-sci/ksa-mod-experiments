---
name: rpc
description: details about the rpc mechanism in unladen swallow
---

# Unladen Swallow RPC

Unladen Swallow is an HTTP RPC server embedded in a KSA mod. It uses **GenHTTP** (not ASP.NET) to expose game mod functionality over HTTP endpoints. Server listens on `0.0.0.0:7887`.

## Architecture

```
unladen-swallow (mod)          ← StarMap mod, ImGui window, drains game thread queue
  └─ unladen-swallow.lib       ← SwallowServer + endpoint handlers
       ├─ references mod.lib projects (glass.lib, etc.) for game functionality
       └─ references ksa-abstractions.lib for GameThread scheduler

ksa-abstractions.lib           ← GameThread / GameStateQueue / IGameStateScheduler
```

- **`unladen-swallow`** — The mod entry point. Creates `SwallowServer`, has an ImGui enable/disable checkbox, and calls `GameThread.DrainOnGameThread()` every frame in `OnBeforeUi`.
- **`unladen-swallow.lib`** — Contains `SwallowServer`, all endpoint classes, and `ApiTypes`. This is where new endpoints go.
- **`ksa-abstractions.lib`** — Thread-safe scheduler for marshalling HTTP thread work onto the game thread.

## Critical Rules

### 1. MUST use `Serialization.Default()` on every endpoint

GenHTTP's `Inline.Create()` does NOT include serializers by default. Without `.Serializers(Serialization.Default())`, it will try to resolve serializers from ASP.NET which is NOT available in the KSA runtime. Every endpoint **must** chain this call:

```csharp
Inline.Create()
    .Serializers(Serialization.Default())   // ← MANDATORY — without this, runtime crash
    .Get(...)
    .Post(...)
    .Build();
```

### 2. MUST use GameThread.Scheduler for all game-state access

GenHTTP handlers run on HTTP worker threads. KSA game state is NOT thread-safe. All reads and writes of game state MUTATIONS (vehicles, parts, camera, universe, etc.) **must** be scheduled onto the game thread:

```csharp
var result = await GameThread.Scheduler.Schedule(() =>
{
    // This runs on the game thread during OnBeforeUi
    return SomeGameStateRead();
});
```

The mod's `OnBeforeUi` calls `GameThread.DrainOnGameThread()` each frame to execute queued work items.

### 3. MUST return `(object)` cast for response types

GenHTTP's functional API requires endpoint lambdas to return `object`. Always cast:

```csharp
return (object)new ApiResponse<MyData>("ok", data);
```

## How to Add a New Endpoint

### Step 1: Define request/response types in `ApiTypes.cs`

```csharp
// unladen-swallow.lib/ApiTypes.cs
public record MyRequest(string SomeParam);
public record MyResult(string Info);
```

Use C# records for request and response DTOs. Keep them in the shared `ApiTypes.cs` file.

### Step 2: Create the endpoint handler class

Create a new file `unladen-swallow.lib/MyEndpoint.cs`:

```csharp
using System;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class MyEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())       // ← MANDATORY
            .Get(async () =>
            {
                var data = await GameThread.Scheduler.Schedule(() =>
                {
                    // read game state here
                    return new MyResult("value");
                });
                return (object)new ApiResponse<MyResult>("ok", data);
            })
            .Post(async (MyRequest body) =>
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(body.SomeParam))
                    throw new ProviderException(ResponseStatus.BadRequest, "Missing someParam.");

                var data = await GameThread.Scheduler.Schedule(() =>
                {
                    // mutate game state here
                    return new MyResult("done");
                });
                return (object)new ApiResponse<MyResult>("ok", data);
            })
            .Build();
    }
}
```

### Step 3: Register the route in `SwallowServer.RegisterRoutes()`

```csharp
// In SwallowServer.cs → RegisterRoutes()
api.Add("my-route", MyEndpoint.Create());
```

For nested routes use `Layout.Create()`:

```csharp
api.Add("vehicle", Layout.Create()
    .Add("actions", Layout.Create()
        .Add("ignite", ActionIgnite.Create())
        .Add("shutdown", ActionShutdown.Create())));
```

### Step 4: If new game functionality is needed, expose it via a `.lib` project

Endpoints should call into `.lib` projects (e.g. `glass.lib`) rather than directly manipulating game internals. If the functionality isn't already in a `.lib`:

1. Extract the logic into the relevant `mod.lib` project
2. Add a `<ProjectReference>` from `unladen-swallow.lib` to that `.lib`
3. Call the `.lib` API from within the `GameThread.Scheduler.Schedule(...)` block

## Error Handling Pattern

Use `ProviderException` for HTTP error responses — GenHTTP maps them to the correct status code:

```csharp
// 400 Bad Request
throw new ProviderException(ResponseStatus.BadRequest, "Missing required field.");

// 404 Not Found
throw new ProviderException(ResponseStatus.NotFound, $"Vehicle not found: {id}.");

// 500 Internal Server Error (catch unexpected exceptions)
catch (Exception ex)
{
    throw new ProviderException(ResponseStatus.InternalServerError,
        "Unexpected error.", ex);
}
```

The `JsonErrorMapper` in `SwallowServer` converts unhandled exceptions to JSON `{"status":"error","message":"..."}` responses.

For endpoints that interact with game objects, use this pattern to re-throw `ProviderException` while wrapping unexpected errors:

```csharp
try
{
    var result = await GameThread.Scheduler.Schedule(() =>
    {
        // game logic that may throw ProviderException
    });
    return (object)new ApiResponse<MyResult>("ok", result);
}
catch (ProviderException) { throw; }
catch (Exception ex)
{
    throw new ProviderException(ResponseStatus.InternalServerError,
        "Unexpected error.", ex);
}
```

## API Response Envelope

All endpoints return `ApiResponse<T>`:

```csharp
public record ApiResponse<T>(string Status, T? Data);
```

- `Status` is `"ok"` on success
- `Data` contains the typed payload

## GenHTTP Imports Cheat Sheet

```csharp
using GenHTTP.Api.Content;         // IHandler
using GenHTTP.Api.Protocol;        // ResponseStatus, ProviderException, IRequest, IResponse
using GenHTTP.Modules.Conversion;  // Serialization (MANDATORY for .Serializers())
using GenHTTP.Modules.Functional;  // Inline
using GenHTTP.Modules.Layouting;   // Layout
using GenHTTP.Modules.IO;          // FlexibleContentType, ContentType
using GenHTTP.Modules.Security;    // CorsPolicy
using GenHTTP.Modules.ErrorHandling; // ErrorHandler, IErrorMapper
using GenHTTP.Modules.Practices;   // .Defaults(), .Console(), .Development()
using GenHTTP.Engine.Internal;     // Host
using GenHTTP.Api.Infrastructure;  // IServerHost
```

## NuGet Packages Required

Both `unladen-swallow.lib.csproj` and `unladen-swallow.csproj` need these packages (version `10.5.0`):

```
GenHTTP.Core
GenHTTP.Modules.Conversion
GenHTTP.Modules.Functional
GenHTTP.Modules.Layouting
GenHTTP.Modules.Practices
GenHTTP.Modules.Security
GenHTTP.Modules.ErrorHandling
GenHTTP.Modules.IO
```

The mod `.csproj` must duplicate these because it is the entry assembly that loads everything at runtime.

## Existing Endpoints Reference

| Route | Method | Handler | Description |
|---|---|---|---|
| `/health` | GET | inline in `SwallowServer` | Returns `{ "status": "ok" }` |
| `/fov` | GET | `FovEndpoint` | Returns current camera FOV state |
| `/fov` | POST | `FovEndpoint` | Sets camera FOV override (`{ "fov": 30.0 }`, `fov <= 0` disables) |
| `/vehicle/actions/ignite` | POST | `ActionIgnite` | Ignites engines (`{ "vehicleId": "..." }`) |
| `/vehicle/actions/shutdown` | POST | `ActionShutdown` | Shuts down engines (`{ "vehicleId": "..." }`) |

## Vehicle Lookup Pattern

When an endpoint needs to find a vehicle by ID:

```csharp
var vehicles = Universe.CurrentSystem?.Vehicles.GetList() ?? Enumerable.Empty<Vehicle>();
var vehicle = vehicles.FirstOrDefault(v => v.Id == body.VehicleId);
if (vehicle is null)
    throw new ProviderException(ResponseStatus.NotFound, $"Vehicle not found: {body.VehicleId}.");
```

This must run inside `GameThread.Scheduler.Schedule(...)`.

