# Unladen Swallow — Implementation Plan

This plan implements the `unladen-swallow` mod: an HTTP RPC server embedded in a KSA mod that exposes other mods' functionality over HTTP. The initial feature exposed is camera FOV control from `glass.lib`.

## Architecture Overview

```
unladen-swallow (mod)
  ├─ ImGui window: enable/disable server checkbox
  ├─ OnBeforeUi: drains GameThread queue
  └─ references unladen-swallow.lib

unladen-swallow.lib
  ├─ SwallowServer (GenHTTP host, start/stop)
  ├─ FovEndpoint (GET/POST camera FOV)
  ├─ ApiTypes (shared response/request records)
  ├─ references glass.lib (FOV control)
  └─ references ksa-abstractions.lib (GameThread scheduler)

glass.lib
  └─ FovController (static FOV state + apply logic)

ksa-abstractions.lib
  └─ GameThread / GameStateQueue / IGameStateScheduler
```

Server hardcoded to `0.0.0.0:7887`. Mod window toggled with F11.

---

## Task 1: Add GameStateScheduler to ksa-abstractions.lib

**Goal:** Port the thread-safe game-state scheduler pattern from KROC's `game-state-updater` project into `ksa-abstractions.lib` so any mod in this workspace can reuse it.

### Reference

The source implementation lives in the KROC workspace at `kitten-remote-operations-control/game-state-updater/`. There are three files to port:

- `IGameStateScheduler.cs` — interface with `Task Schedule(Action)` and `Task<T> Schedule<T>(Func<T>)`
- `GameStateQueue.cs` — `ConcurrentQueue<WorkItem>`-based implementation with `DrainOnGameThread()`
- `GameThread.cs` — static singleton exposing `Scheduler` property and `DrainOnGameThread()` method

### Files to Create

#### `ksa-abstractions.lib/IGameStateScheduler.cs`

```csharp
using System;
using System.Threading.Tasks;

namespace MeowSci.KsaAbstractions;

public interface IGameStateScheduler
{
    Task Schedule(Action action);
    Task<T> Schedule<T>(Func<T> func);
}
```

#### `ksa-abstractions.lib/GameStateQueue.cs`

Copy the implementation from KROC's `GameStateQueue.cs` but change the namespace to `MeowSci.KsaAbstractions`. Preserve the `WorkItem` nested class, `ConcurrentQueue<WorkItem>`, and the `DrainOnGameThread()` method exactly. Required usings: `System`, `System.Collections.Concurrent`, `System.Threading.Tasks`.

#### `ksa-abstractions.lib/GameThread.cs`

```csharp
namespace MeowSci.KsaAbstractions;

public static class GameThread
{
    private static readonly GameStateQueue _instance = new();
    public static IGameStateScheduler Scheduler => _instance;
    public static void DrainOnGameThread() => _instance.DrainOnGameThread();
}
```

### Files to Modify

No modifications needed to `ksa-abstractions.lib.csproj` — `System.Collections.Concurrent` is part of the base class library.

### Verification

Run `dotnet build ksa-abstractions.lib/ksa-abstractions.lib.csproj` — must compile cleanly.

---

## Task 2: Refactor glass.lib — Extract FOV Control API

**Goal:** Move the FOV state and control logic out of `glass/Patcher.cs` static fields into `glass.lib` so that other projects (like `unladen-swallow.lib`) can control camera FOV by referencing `glass.lib` without depending on the `glass` mod itself.

### Context

Currently `glass/Patcher.cs` holds two static fields that the Harmony patches and `Mod.cs` both reference:

```csharp
internal static bool IsOverrideActive = false;
internal static float OverrideFovDegrees = 50f;
```

And `glass/Mod.cs` calls `Program.GetCamera().SetFieldOfView(clampedFov)` directly in `OnAfterUi`.

All of this state and the apply logic must move into `glass.lib/FovController.cs`.

### Files to Create

#### `glass.lib/FovController.cs`

```csharp
using System;
using KSA;

namespace MeowSci.GlassLib;

public static class FovController
{
    public const float MinFov = 1f;
    public const float MaxFov = 179f;
    public const float DefaultFov = 50f;

    public static bool IsOverrideActive { get; set; } = false;
    public static float OverrideFovDegrees { get; set; } = DefaultFov;

    public static void SetFov(float degrees)
    {
        OverrideFovDegrees = MathF.Max(MinFov, MathF.Min(MaxFov, degrees));
        IsOverrideActive = true;
    }

    public static void ResetToDefault()
    {
        OverrideFovDegrees = DefaultFov;
        IsOverrideActive = true;
    }

    public static void DisableOverride()
    {
        IsOverrideActive = false;
    }

    public static float GetCurrentFovDegrees()
    {
        float currentFovRad = Program.GetCamera().GetFieldOfView();
        return currentFovRad * (180f / MathF.PI);
    }

    /// <summary>
    /// Must be called on the game thread (e.g. in OnAfterUi) to apply the
    /// current override FOV to the camera.
    /// </summary>
    public static void ApplyFov()
    {
        if (!IsOverrideActive) return;
        float clampedFov = MathF.Max(MinFov, MathF.Min(MaxFov, OverrideFovDegrees));
        Program.GetCamera().SetFieldOfView(clampedFov);
    }
}
```

### Files to Delete

#### `glass.lib/GlassLib.cs`

Delete this placeholder file. It contains only a stub `FixmeModLib` class that is unused.

### Files to Modify

#### `glass.lib/glass.lib.csproj`

Add a KSA reference so `FovController` can call `Program.GetCamera()`:

```xml
<ItemGroup>
  <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
    <HintPath>$(KSAFolder)KSA.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

### Verification

Run `dotnet build glass.lib/glass.lib.csproj` — must compile cleanly.

---

## Task 3: Update glass Mod to Use glass.lib

**Goal:** Rewire the `glass` mod to use `FovController` from `glass.lib` instead of managing FOV state in its own static fields. The mod must behave identically after this refactor.

### Files to Modify

#### `glass/glass.csproj`

Add a `ProjectReference` to `glass.lib`:

```xml
<ItemGroup>
  <ProjectReference Include="..\glass.lib\glass.lib.csproj" />
</ItemGroup>
```

#### `glass/Patcher.cs`

1. Add `using MeowSci.GlassLib;` at the top
2. **Remove** the two static fields:
   ```csharp
   // DELETE these lines:
   internal static bool IsOverrideActive = false;
   internal static float OverrideFovDegrees = 50f;
   ```
3. In `ChangeFieldOfView_Prefix`, change `IsOverrideActive` → `FovController.IsOverrideActive`
4. In `UpdateProjection_Prefix`, change `IsOverrideActive` → `FovController.IsOverrideActive` and `OverrideFovDegrees` → `FovController.OverrideFovDegrees`

The resulting Patcher.cs should look like:

```csharp
using System;
using HarmonyLib;
using Brutal.Numerics;
using KSA;
using MeowSci.GlassLib;

namespace MeowSci.Glass;

[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("glass");

    private static System.Reflection.FieldInfo? _fovRadiansField;

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            _fovRadiansField = AccessTools.Field(typeof(Camera), "_fovRadians");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            _harmony?.UnpatchAll("glass");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"glass: Error removing patches: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Camera), "ChangeFieldOfView")]
    [HarmonyPrefix]
    private static bool ChangeFieldOfView_Prefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive) return true;
        return false;
    }

    [HarmonyPatch(typeof(Camera), "UpdateProjection")]
    [HarmonyPrefix]
    private static void UpdateProjection_Prefix(Camera __instance)
    {
        if (!FovController.IsOverrideActive) return;
        if (_fovRadiansField == null) return;
        float targetRadians = (float)(FovController.OverrideFovDegrees * (Math.PI / 180.0));
        _fovRadiansField.SetValue(__instance, targetRadians);
    }
}
```

#### `glass/Mod.cs`

1. Add `using MeowSci.GlassLib;` at the top
2. In `OnAfterUi`, replace the inline FOV application block:
   ```csharp
   // REPLACE this:
   if (Patcher.IsOverrideActive)
   {
       try
       {
           float clampedFov = MathF.Max(1f, MathF.Min(179f, Patcher.OverrideFovDegrees));
           Program.GetCamera().SetFieldOfView(clampedFov);
       }
       catch ...
   }
   // WITH:
   try { FovController.ApplyFov(); }
   catch (Exception ex) { Console.WriteLine($"glass: Error applying FOV override: {ex.Message}"); }
   ```
3. In `RenderWindow`, replace all references to `Patcher.OverrideFovDegrees` → `FovController.OverrideFovDegrees` and `Patcher.IsOverrideActive` → `FovController.IsOverrideActive`:
   - Preset radio buttons: `FovController.OverrideFovDegrees = Presets[i].Fov; FovController.IsOverrideActive = true;`
   - Manual mode checkbox: `FovController.OverrideFovDegrees = _manualFov; FovController.IsOverrideActive = true;`
   - Manual drag float: `FovController.OverrideFovDegrees = _manualFov; FovController.IsOverrideActive = true;`
   - Reset button: `FovController.OverrideFovDegrees = 50f; FovController.IsOverrideActive = true;`

### Verification

Run `dotnet build glass/glass.csproj` — must compile cleanly. The mod must behave identically to before; the refactor is purely structural.

---

## Task 4: Set Up unladen-swallow.lib — HTTP Server + FOV Endpoint

**Goal:** Build the HTTP server and FOV endpoint into `unladen-swallow.lib`. This is the core of the unladen-swallow feature.

### Files to Modify

#### `unladen-swallow.lib/unladen-swallow.lib.csproj`

Replace the entire content with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.UnladenSwallowLib</AssemblyName>
    <RootNamespace>MeowSci.UnladenSwallowLib</RootNamespace>
    <Description>HTTP RPC server for unladen-swallow mod</Description>
    <PackageId>MeowSci.UnladenSwallowLib</PackageId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
    <ProjectReference Include="..\glass.lib\glass.lib.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="GenHTTP.Core" Version="10.5.0" />
    <PackageReference Include="GenHTTP.Modules.Functional" Version="10.5.0" />
    <PackageReference Include="GenHTTP.Modules.Layouting" Version="10.5.0" />
    <PackageReference Include="GenHTTP.Modules.Practices" Version="10.5.0" />
    <PackageReference Include="GenHTTP.Modules.Security" Version="10.5.0" />
    <PackageReference Include="GenHTTP.Modules.ErrorHandling" Version="10.5.0" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="KSA" Condition="Exists('$(KSAFolder)KSA.dll')">
      <HintPath>$(KSAFolder)KSA.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### Files to Delete

#### `unladen-swallow.lib/UnladenSwallowLib.cs`

Delete this placeholder file.

### Files to Create

#### `unladen-swallow.lib/ApiTypes.cs`

Shared request/response record types.

```csharp
namespace MeowSci.UnladenSwallowLib;

public record ApiResponse<T>(string Status, T? Data);

public record FovRequest(float Fov);

public record FovState(float CurrentFovDegrees, float OverrideFovDegrees, bool IsOverrideActive);
```

#### `unladen-swallow.lib/FovEndpoint.cs`

HTTP endpoint that wraps `glass.lib`'s `FovController`.

Two routes:
- `GET /fov` — returns current FOV state (current camera FOV, override value, override active flag)
- `POST /fov` — accepts `FovRequest` JSON body `{ "fov": 30.0 }`, calls `FovController.SetFov(fov)` on the game thread

**Implementation notes:**
- Both GET and POST must use `GameThread.Scheduler.Schedule(...)` to interact with game state on the game thread
- Must call `.Serializers(Serialization.Default())` on the `Inline.Create()` builder
- Returns `ApiResponse<FovState>` for both endpoints
- POST with `fov` value of `0` or negative should call `FovController.DisableOverride()` (returns override to game default)

```csharp
using System;
using GenHTTP.Api.Content;
using GenHTTP.Api.Protocol;
using GenHTTP.Modules.Conversion;
using GenHTTP.Modules.Functional;
using MeowSci.GlassLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallowLib;

public static class FovEndpoint
{
    public static IHandler Create()
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Get(async () =>
            {
                var state = await GameThread.Scheduler.Schedule(() =>
                    new FovState(
                        FovController.GetCurrentFovDegrees(),
                        FovController.OverrideFovDegrees,
                        FovController.IsOverrideActive));

                return (object)new ApiResponse<FovState>("ok", state);
            })
            .Post(async (FovRequest body) =>
            {
                var state = await GameThread.Scheduler.Schedule(() =>
                {
                    if (body.Fov <= 0)
                        FovController.DisableOverride();
                    else
                        FovController.SetFov(body.Fov);

                    return new FovState(
                        FovController.GetCurrentFovDegrees(),
                        FovController.OverrideFovDegrees,
                        FovController.IsOverrideActive);
                });

                return (object)new ApiResponse<FovState>("ok", state);
            })
            .Build();
    }
}
```

#### `unladen-swallow.lib/SwallowServer.cs`

GenHTTP server host. Modelled on KROC's `KrocServer.cs` but simplified (no config file, no module interface — just hardcoded routes).

```csharp
using System;
using System.Net;
using System.Threading.Tasks;
using GenHTTP.Api.Content;
using GenHTTP.Api.Infrastructure;
using GenHTTP.Api.Protocol;
using GenHTTP.Engine.Internal;
using GenHTTP.Modules.ErrorHandling;
using GenHTTP.Modules.Functional;
using GenHTTP.Modules.IO;
using GenHTTP.Modules.Layouting;
using GenHTTP.Modules.Practices;
using GenHTTP.Modules.Security;

namespace MeowSci.UnladenSwallowLib;

public sealed class SwallowServer
{
    private IServerHost? _host;

    public bool IsRunning => _host is not null;

    public async Task StartAsync()
    {
        if (_host is not null) return;

        Console.WriteLine("unladen-swallow: server starting...");

        var api = Layout.Create();

        // /health
        var health = Inline.Create()
            .Get(() => new { status = "ok" });
        api.Add("health", health);

        // /fov
        api.Add("fov", FovEndpoint.Create());

        // CORS
        api.Add(CorsPolicy.Permissive());

        // JSON error responses
        api.Add(ErrorHandler.From(new JsonErrorMapper()));

        _host = await Host.Create()
            .Handler(api)
            .Bind(IPAddress.Parse("0.0.0.0"), 7887)
            .Defaults(compression: false)
            .Console()
            .Development()
            .StartAsync();

        Console.WriteLine("unladen-swallow: server listening on http://0.0.0.0:7887");
    }

    public async Task StopAsync()
    {
        if (_host is null) return;
        await _host.StopAsync();
        _host = null;
        Console.WriteLine("unladen-swallow: server stopped.");
    }

    private sealed class JsonErrorMapper : IErrorMapper<Exception>
    {
        public ValueTask<IResponse?> GetNotFound(IRequest request, IHandler handler)
        {
            var response = request.Respond()
                .Status(ResponseStatus.NotFound)
                .Content("{\"error\":\"not found\"}")
                .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
                .Build();
            return new ValueTask<IResponse?>(response);
        }

        public ValueTask<IResponse?> Map(IRequest request, IHandler handler, Exception error)
        {
            Console.WriteLine($"unladen-swallow: unhandled exception: {error}");

            var status = error is ProviderException pe
                ? pe.Status
                : ResponseStatus.InternalServerError;

            var escaped = error.Message.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var response = request.Respond()
                .Status(status)
                .Content($"{{\"error\":\"{escaped}\"}}")
                .Type(FlexibleContentType.Get(ContentType.ApplicationJson))
                .Build();
            return new ValueTask<IResponse?>(response);
        }
    }
}
```

### Verification

Run `dotnet build unladen-swallow.lib/unladen-swallow.lib.csproj` — must compile cleanly.

---

## Task 5: Update unladen-swallow Mod — Server Lifecycle + UI

**Goal:** Wire the HTTP server into the mod's lifecycle and replace the placeholder ImGui window with a simple enable/disable checkbox.

### Files to Modify

#### `unladen-swallow/unladen-swallow.csproj`

Add project references to `unladen-swallow.lib` and `ksa-abstractions.lib`:

```xml
<ItemGroup>
  <ProjectReference Include="..\unladen-swallow.lib\unladen-swallow.lib.csproj" />
  <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
</ItemGroup>
```

Also add GenHTTP package references (needed at runtime since the mod is the entry assembly that loads everything):

```xml
<ItemGroup>
  <PackageReference Include="GenHTTP.Core" Version="10.5.0" />
  <PackageReference Include="GenHTTP.Modules.Functional" Version="10.5.0" />
  <PackageReference Include="GenHTTP.Modules.Layouting" Version="10.5.0" />
  <PackageReference Include="GenHTTP.Modules.Practices" Version="10.5.0" />
  <PackageReference Include="GenHTTP.Modules.Security" Version="10.5.0" />
  <PackageReference Include="GenHTTP.Modules.ErrorHandling" Version="10.5.0" />
</ItemGroup>
```

#### `unladen-swallow/Mod.cs`

Replace the entire `Mod.cs` content. The new implementation:

1. **Fields:**
   - `SwallowServer _server` — the HTTP server instance
   - `bool _serverEnabled` — tracks checkbox state
   - `bool _windowVisible` — F11 toggle

2. **`OnFullyLoaded`:** Create `SwallowServer` instance (do NOT auto-start). Apply Harmony patches.

3. **`OnBeforeUi`:** Call `GameThread.DrainOnGameThread()` every frame to execute any pending HTTP→game-thread work items.

4. **`OnAfterUi`:** F11 toggles window. Render simple ImGui window with:
   - Title: "Unladen Swallow"
   - Checkbox: "Enable HTTP Server" — toggling this starts/stops the server
   - Status text: "Server: Running on http://0.0.0.0:7887" or "Server: Stopped"

5. **`Unload`:** Stop the server if running. Unpatch Harmony.

Full replacement `Mod.cs`:

```csharp
using System;
using Brutal.Numerics;
using Brutal.ImGuiApi;
using StarMap.API;
using KSA;
using MeowSci.UnladenSwallowLib;
using MeowSci.KsaAbstractions;

namespace MeowSci.UnladenSwallow;

[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;

    private bool _isInitialized = false;
    private bool _isDisposed = false;
    private bool _windowVisible = false;
    private bool _serverEnabled = false;
    private SwallowServer? _server;

    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        try
        {
            Patcher.Patch();
            _server = new SwallowServer();
            _isInitialized = true;
            Console.WriteLine("unladen-swallow: initialized.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during initialization: {ex.Message}");
        }
    }

    [StarMapBeforeGui]
    public void OnBeforeUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        GameThread.DrainOnGameThread();
    }

    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        try
        {
            if (!_isInitialized || _isDisposed) return;

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
                _windowVisible = !_windowVisible;

            if (_windowVisible)
                RenderWindow();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error in OnAfterUi: {ex.Message}");
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        try
        {
            if (_server is not null && _server.IsRunning)
                _server.StopAsync().GetAwaiter().GetResult();
            Patcher.Unload();
            _isDisposed = true;
            Console.WriteLine("unladen-swallow: unloaded.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"unladen-swallow: Error during unload: {ex.Message}");
        }
    }

    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(400, 150), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Unladen Swallow", ref _windowVisible))
        {
            ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "Unladen Swallow — HTTP RPC");
            ImGui.Separator();

            if (ImGui.Checkbox("Enable HTTP Server", ref _serverEnabled))
            {
                try
                {
                    if (_serverEnabled)
                        _server?.StartAsync().GetAwaiter().GetResult();
                    else
                        _server?.StopAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"unladen-swallow: Error toggling server: {ex.Message}");
                    _serverEnabled = false;
                }
            }

            if (_serverEnabled && _server?.IsRunning == true)
                ImGui.TextColored(new float4(0.0f, 1.0f, 0.0f, 1.0f), "Server: Running on http://0.0.0.0:7887");
            else
                ImGui.TextColored(new float4(1.0f, 0.4f, 0.4f, 1.0f), "Server: Stopped");
        }
        ImGui.End();
    }
}
```

### Verification

Run `dotnet build unladen-swallow/unladen-swallow.csproj` — must compile cleanly.

---

## Task 6: Update Documentation

**Goal:** Update `REPOSITORY_INDEX.md` and README files per repository maintenance rules.

### Files to Modify

#### `REPOSITORY_INDEX.md`

Add/update entries for:
- **unladen-swallow** — HTTP RPC server mod. Embeds a GenHTTP server (0.0.0.0:7887) that exposes game mod functionality over HTTP. ImGui window with enable/disable checkbox (F11). Currently exposes camera FOV control via glass.lib.
- **unladen-swallow.lib** — Server infrastructure and HTTP endpoints for unladen-swallow. Contains `SwallowServer` (GenHTTP host), `FovEndpoint` (GET/POST /fov), and shared API types. References glass.lib and ksa-abstractions.lib.
- **glass.lib** — Update entry to note it now contains `FovController` for programmatic camera FOV control (used by glass mod and unladen-swallow.lib).
- **ksa-abstractions.lib** — Update entry to note it now contains `GameThread`/`GameStateQueue`/`IGameStateScheduler` for thread-safe game-state mutation scheduling.

#### `unladen-swallow/README.md`

Replace with documentation covering:
- Overview: HTTP RPC server mod for KSA
- Features: enable/disable server, camera FOV control endpoint
- API endpoints: `GET /health`, `GET /fov`, `POST /fov`
- Request/response examples
- Architecture: mod ↔ lib ↔ glass.lib ↔ ksa-abstractions.lib

#### `glass/README.md`

Update the Architecture section to note that FOV state now lives in `glass.lib/FovController.cs` and can be controlled programmatically by other projects.

### Verification

Review that all documentation accurately reflects the implemented code.

---

## Task 7: Full Solution Build

**Goal:** Verify the entire solution compiles cleanly.

### Steps

1. Run `dotnet build ksa-mod-experiments.slnx` from the repository root
2. Fix any compilation errors
3. Verify all modified projects compile: `ksa-abstractions.lib`, `glass.lib`, `glass`, `unladen-swallow.lib`, `unladen-swallow`

### Success Criteria

Zero errors, zero warnings from all modified projects.

---

## API Reference (for verification)

### `GET /health`
```
Response: { "status": "ok" }
```

### `GET /fov`
```
Response:
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
```
Request:  { "fov": 30.0 }
Response:
{
  "status": "ok",
  "data": {
    "currentFovDegrees": 30.0,
    "overrideFovDegrees": 30.0,
    "isOverrideActive": true
  }
}
```

### `POST /fov` (disable override)
```
Request:  { "fov": 0 }
Response:
{
  "status": "ok",
  "data": {
    "currentFovDegrees": 50.0,
    "overrideFovDegrees": 50.0,
    "isOverrideActive": false
  }
}
```

---

## Task Dependency Order

```
Task 1 (GameStateScheduler in ksa-abstractions.lib)
  ↓
Task 2 (Refactor glass.lib with FovController)
  ↓
Task 3 (Update glass mod to use glass.lib)
  ↓
Task 4 (unladen-swallow.lib: server + FOV endpoint)
  ↓
Task 5 (unladen-swallow mod: lifecycle + UI)
  ↓
Task 6 (Documentation updates)
  ↓
Task 7 (Full solution build verification)
```

Tasks 1 and 2 have no dependency on each other and can be done in parallel. Task 3 depends on Task 2. Task 4 depends on Tasks 1 and 2. Task 5 depends on Task 4. Tasks 6 and 7 depend on all prior tasks.
