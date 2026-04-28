# doh.lib — DOH Library

Headless library providing programmatic kitten spawning and per-kitten GPU material customization for KSA. Designed for use by the `doh` mod UI and future RPC endpoints via `unladen-swallow`.

## Modules

### Materials (`Materials/`)

- **`MaterialSystemAccessor`** — Static reflection bridge to `GpuMaterialSystem` and `GpuTextureSystem`. Discovers `Program.Instance` at runtime, caches reflection handles for `CreateObject`, `GetOrLoad`, `AssetMap`, `BigBuffer`, and `DeviceCtx`. Provides:
  - `Initialize()` — one-time discovery of GPU systems
  - `CreateMaterial(name, data)` — registers a new `MaterialData` in the GPU buffer
  - `GetMaterialHandle(name)` / `GetExistingMaterialHandle(name)` — resolve material handles
  - `GetTextureBindlessHandle(name)` — resolve texture bindless handles
  - `WriteAlbedoColor(handle, color)` — staged Vulkan upload to modify AlbedoColor on an existing material
  - `Cleanup()` — reset cached state on unload

- **`MaterialFactory`** — Creates unique `KittenMaterialSet` instances per kitten. Resolves character texture references (`PbrMaterialReference` → `TextureReference` → bindless handles) via reflection, constructs `MaterialData` structs with custom `AlbedoColor`, and registers them in `GpuMaterialSystem`.

- **`KittenMaterialSet`** — Holds per-kitten GPU material handles (body, head, eye, fur) with a tint color. `UpdateTint()` writes directly to the GPU buffer for live recoloring.

### DohSubmod (`DohSubmod.cs`)

- **`DohSubmod`** — `ISubmod` implementation for unscience supermod integration. Encapsulates the full spawning UI (vehicle/character selection, offset, batch count, color picker, kitten list with live recoloring and despawn). Also used by standalone `doh/Mod.cs` to avoid code duplication.

### Spawning (`Spawning/`)

- **`KittenSpawner`** — Core spawning engine replicating `EVADoor.CreateKittenEva()`. Supports:
  - Vehicle-relative positioning with body-frame offset
  - Absolute orbital state positioning (position + velocity + parent body)
  - Batch spawning with chain offsets
  - Optional per-kitten material customization
  - `Despawn(id)` / `DespawnAll()` — remove spawned kittens
  - `RecolorKitten(id, color)` — live tint update
  - `GetAvailableCharacters()` — enumerate character IDs from `ModLibrary`

- **`SpawnRequest`** — Input DTO for spawn operations. Key fields: `ReferenceVehicleId`, `OffsetBodyFrame`, `Count`, `CharacterId`, `TintColor`, `UniqueMaterialsPerKitten`, `PerKittenColors`.

- **`SpawnResult`** / **`SpawnedKittenInfo`** — Result DTOs with per-kitten spawn details.

- **`SpawnedKittenRegistry`** — Dictionary-based tracker for all mod-spawned kittens. Supports register, unregister, get, get-all, and clear operations.

## Thread Safety

All `KittenSpawner` methods MUST run on the game thread. When invoked via RPC, callers must use `GameThread.Scheduler.Schedule()` for marshalling.

## RPC Integration (Future)

The library exposes a clean API surface for `unladen-swallow` RPC endpoints:

| Endpoint | Method |
|---|---|
| `POST /doh/spawn` | `KittenSpawner.Spawn(SpawnRequest)` |
| `DELETE /doh/despawn` | `KittenSpawner.Despawn(kittenId)` |
| `DELETE /doh/despawn-all` | `KittenSpawner.DespawnAll()` |
| `PUT /doh/recolor` | `KittenSpawner.RecolorKitten(kittenId, color)` |
| `GET /doh/kittens` | `SpawnedKittenRegistry.GetAll()` |
| `GET /doh/characters` | `KittenSpawner.GetAvailableCharacters()` |

## Dependencies

- `ksa-abstractions.lib` — VehicleProvider, CelestialProvider
- KSA game DLLs: KSA.dll, Planet.Core.dll, Planet.Render.Core.dll, Brutal.Core.Numerics.dll, Brutal.Vulkan.dll, Brutal.VulkanApi.Abstractions.dll, CommunityToolkit.HighPerformance.dll
