# DOH Mod — Dynamically Originating Hominids

## 1. Problem Statement

We need a new mod called **"doh"** (Dynamically Originating Hominids) that allows programmatic spawning of kittens in the KSA game with **per-kitten material customization**. Currently, the game can only spawn kittens via the EVADoor interaction, and material changes (humble-arteest) apply globally to all kittens because they share GPU material handles. This mod will:

1. Spawn kittens programmatically at arbitrary positions
2. Create **unique GPU material instances per kitten** at runtime so each kitten can have an individually-colored material tint
3. Expose all functionality through both ImGui UI and RPC-ready headless API
4. Follow the established mod/mod.lib architecture pattern

---

## 2. Feasibility Analysis: Runtime Per-Kitten Materials

### 2.1 Why Current Materials Are Global

The game's `GpuMaterialSystem` stores all `MaterialData` structs in a single GPU storage buffer. Materials are registered by name in a `ConcurrentDictionary<AssetName, GpuObjectAssetRef>` called `AssetMap`. When the game loads character models, it calls `MaterialSystem.GetOrLoad(pbrMaterial.Id)` which returns the **same shared handle** for all kittens using the same character. In `AnimatedRenderable.Draw()`, each mesh uses `MaterialIndices[i]` (which holds the shared handle) to tell the shader which buffer slot to read.

### 2.2 Runtime Material Creation IS Fully Feasible

**Evidence from game source:** `CharacterRenderResources.CreateFurMaterial()` (line 46-62 of `CharacterRenderResources.cs`) already creates materials at runtime:

```csharp
MeshRenderSystem.MaterialSystem.CreateObject(assetName, new MaterialData { ... });
return MeshRenderSystem.MaterialSystem.GetOrLoad(assetName);
```

**The `GpuObjectSystem<T>.CreateObject()` API** (from `GpuObjectSystem.cs` line 45-55):
```csharp
public bool CreateObject(AssetName id, T element)
{
    int handle = SendToBuffer(element);  // Uploads to GPU, returns slot index
    GpuObjectAssetRef ref = new GpuObjectAssetRef(id, this, handle);
    bool flag = TryAdd(ref);  // Adds to AssetMap
    if (!flag) ref.Dispose();
    return flag;
}
```

**Strategy for per-kitten materials:**
1. Resolve the character's `PbrMaterialReference` textures to bindless handles via `TextureSystem.GetOrLoad()`
2. Construct a new `MaterialData` struct with the **same texture handles** but a **custom `AlbedoColor`**
3. Call `MaterialSystem.CreateObject("doh_kitten_{id}_{meshType}", materialData)` to register it
4. After spawning a kitten, **replace entries in `AnimatedRenderable.MaterialIndices[]`** (via reflection) with the new handle values

Each kitten's `AnimatedRenderable` has its own `MaterialIndices` array (an `int[]`), so modifying individual elements gives us per-kitten material assignment without affecting other kittens.

### 2.3 Capacity Constraints

`GpuObjectSystem` uses `FreeListIndexPool` with `allowResize: false`. The game initializes with `capacity` slots (typically 1024). Each spawned kitten with unique materials consumes ~4 additional slots (body, head, eye, fur). With 1024 capacity minus ~50 used by the base game, we can support ~240 uniquely-colored kittens. This is more than sufficient.

Materials can also be freed when kittens are despawned via `GpuObjectAssetRef.Dispose()` → `Source.Free(Handle)`.

---

## 3. Architecture

### 3.1 Project Structure

```
doh/                          # Main mod (UI + lifecycle)
├── doh.csproj
├── Mod.cs                    # StarMapMod lifecycle, ImGui window
├── Patcher.cs                # Harmony + HotkeyGuard
├── mod.toml                  # Mod metadata
└── README.md

doh.lib/                      # Headless logic library (no ImGui)
├── doh.lib.csproj
├── DohLib.cs                 # Library marker/entry
├── Spawning/
│   ├── KittenSpawner.cs      # Core spawning engine
│   ├── SpawnRequest.cs       # Request DTOs
│   ├── SpawnResult.cs        # Result DTOs
│   └── SpawnedKittenRegistry.cs  # Tracks spawned kittens
├── Materials/
│   ├── MaterialFactory.cs    # Runtime material creation
│   ├── KittenMaterialSet.cs  # Per-kitten material handle set
│   └── MaterialSystemAccessor.cs # Reflection access to GpuMaterialSystem
└── README.md
```

### 3.2 Dependency Graph

```
doh (mod)
├── doh.lib (library)
│   └── ksa-abstractions.lib (shared utilities)
├── ksa-abstractions.lib
├── StarMap.API (NuGet)
├── Lib.Harmony (NuGet)
└── Game DLLs (KSA, Brutal, RenderCore)
```

### 3.3 Separation of Concerns

```
┌──────────────────────────────────────────────────┐
│  doh.lib (Headless Core Logic)                   │
│  ├── KittenSpawner      (spawn/despawn logic)    │
│  ├── MaterialFactory    (material creation)       │
│  ├── MaterialSystemAccessor (reflection bridge)   │
│  └── SpawnedKittenRegistry (state tracking)       │
└──────────────────────────────────────────────────┘
                       ▲
            ┌──────────┴──────────┐
            │                     │
    ┌───────▼────────┐   ┌───────▼────────┐
    │ doh/Mod.cs     │   │ Future RPC     │
    │ (ImGui UI)     │   │ (unladen-      │
    │                │   │  swallow       │
    │                │   │  endpoint)     │
    └────────────────┘   └────────────────┘
```

All game-state-mutating logic lives in `doh.lib`. The `doh` mod project only handles ImGui rendering and the mod lifecycle. This ensures the functionality can be invoked via HTTP RPC (unladen-swallow) without any UI dependency.

---

## 4. Detailed File Specifications

### 4.1 `doh.lib/Materials/MaterialSystemAccessor.cs`

**Purpose:** Provides reflection-based access to the game's `GpuMaterialSystem` for creating and managing materials at runtime. This centralizes all reflection code so other components don't need to know about game internals.

**Namespace:** `MeowSci.DohLib.Materials`

**Pattern:** Follows the same reflection approach as `humble-arteest.lib/KittenColor.cs` (lines 48-86).

```csharp
public static class MaterialSystemAccessor
{
    // State
    private static bool _initialized;
    private static string? _lastError;
    
    // Cached reflection handles (same pattern as KittenColor.cs)
    private static object? _materialSystem;       // GpuMaterialSystem instance
    private static object? _textureSystem;        // GpuTextureSystem instance
    private static IDictionary? _assetMap;         // MaterialSystem.AssetMap
    private static PropertyInfo? _bigBufferProp;   // MaterialSystem.BigBuffer
    private static FieldInfo? _deviceCtxField;     // MaterialSystem.DeviceCtx (in base GpuObjectSystem)
    private static MethodInfo? _createObjectMethod; // MaterialSystem.CreateObject(AssetName, MaterialData)
    private static MethodInfo? _getOrLoadMethod;   // MaterialSystem.GetOrLoad(AssetName)
    private static MethodInfo? _textureGetOrLoad;  // TextureSystem.GetOrLoad(AssetName)
    
    public static bool IsInitialized => _initialized && _materialSystem != null;
    public static string? LastError => _lastError;
    
    // --- Public API ---
    
    public static bool Initialize();
    // Discovers Program.Instance → MaterialSystem, TextureSystem, caches all reflection handles.
    // Implementation details:
    //   1. typeof(Part).Assembly.GetType("KSA.Program") → programType
    //   2. programType.GetProperty("Instance", Public|Static) → instanceProp
    //   3. instanceProp.GetValue(null) → programInstance
    //   4. GetFieldOrProp(programType, programInstance, "MaterialSystem") → _materialSystem
    //   5. Walk hierarchy for AssetMap, BigBuffer, DeviceCtx (same as KittenColor.cs)
    //   6. Get "SuperMeshRenderSystem" from programInstance → get "TextureSystem" → _textureSystem
    //   7. Cache MethodInfo for CreateObject via:
    //      _materialSystem.GetType().GetMethod("CreateObject", BindingFlags.Public|Instance)
    //   8. Cache MethodInfo for GetOrLoad via:
    //      _materialSystem.GetType().GetMethod("GetOrLoad", BindingFlags.Public|Instance)
    //   9. Cache TextureSystem.GetOrLoad method
    
    public static bool CreateMaterial(string assetName, MaterialData data);
    // Invokes _createObjectMethod on _materialSystem with the provided AssetName and MaterialData.
    // Returns true if successful.
    // Implementation: _createObjectMethod.Invoke(_materialSystem, new object[] { (AssetName)assetName, data });
    
    public static int GetMaterialHandle(string assetName);
    // Calls GetOrLoad on _materialSystem, returns the GpuObjectAssetRef.Handle.
    // Implementation: var ref = _getOrLoadMethod.Invoke(_materialSystem, new object[] { (AssetName)assetName });
    //   return (int)ref.GetType().GetField("Handle", Public|Instance).GetValue(ref);
    
    public static int GetTextureBindlessHandle(string textureName);
    // Calls TextureSystem.GetOrLoad(textureName), returns .BindlessHandle.
    // Used to resolve texture references when constructing MaterialData.
    
    public static int GetExistingMaterialHandle(string materialName);
    // Looks up materialName in AssetMap, returns handle if found, -1 otherwise.
    
    public static int GetDefaultSamplerHandle();
    // Returns TextureSystem.SamplerRepeatHandle via reflection.
    
    public static int GetDefaultBlackTextureHandle();
    // Returns TextureSystem.DefaultBlackTexture.BindlessHandle via reflection.
    
    public static int GetDefaultWhiteTextureHandle();
    // Returns TextureSystem.DefaultWhiteTexture.BindlessHandle via reflection.
    
    public static int GetBlankMaterialTextureHandle();
    // Returns GltfSystem.BlankMaterialTexture.BindlessHandle via reflection.
    
    public static bool WriteAlbedoColor(int handle, float4 color);
    // Same implementation as KittenColor.WriteAlbedoColor() — writes AlbedoColor 
    // at the correct offset in the GPU buffer for a given material handle.
    // Used to update colors on already-created materials.
    
    public static void Cleanup();
    // Resets all cached state. Called on mod unload.
    
    // --- Reflection helpers (private, same as KittenColor.cs) ---
    private static object? GetFieldOrProp(Type type, object instance, string name);
    private static object? FindFieldInHierarchy(object instance, string fieldName);
    private static FieldInfo? FindFieldInfoInHierarchy(Type? type, string fieldName);
}
```

**Key Reflection Paths (from analysis):**
- `Program.Instance` → `typeof(Part).Assembly.GetType("KSA.Program")` → static property `Instance`
- `Program.Instance.MaterialSystem` → field/property `MaterialSystem` (type `GpuMaterialSystem` which extends `GpuObjectSystem<MaterialData>`)
- `Program.Instance.SuperMeshRenderSystem` → field/property (contains TextureSystem, GltfSystem, etc.)
- `GpuMaterialSystem.AssetMap` → inherited from `AssetManager<GpuObjectAssetRef>`, type `ConcurrentDictionary<AssetName, GpuObjectAssetRef>`
- `GpuMaterialSystem.BigBuffer` → inherited from `GpuObjectSystem<MaterialData>`, type `BufferEx`
- `GpuMaterialSystem.DeviceCtx` → inherited protected field, type `IVulkanContext`
- `GpuMaterialSystem.CreateObject(AssetName, MaterialData)` → public method on `GpuObjectSystem<T>`
- `GpuMaterialSystem.GetOrLoad(AssetName)` → inherited from `AssetManager<GpuObjectAssetRef>`
- `GpuTextureAssetRef.BindlessHandle` → public property/field (int)
- `TextureSystem.SamplerRepeatHandle` → public property (int)

**Required Assembly References:**
- `KSA.dll` (for `Part`, `MaterialData`, `GpuObjectAssetRef`, etc.)
- `Brutal.Core.Numerics.dll` (for `float4`, `ByteSize`)
- `Brutal.Vulkan.dll` (for `BufferEx`, `IVulkanContext`, `VkUtils`)
- `RenderCore.dll` (for `GpuTextureAssetRef`)

---

### 4.2 `doh.lib/Materials/KittenMaterialSet.cs`

**Purpose:** Holds the GPU material handles for a single kitten's custom material set.

**Namespace:** `MeowSci.DohLib.Materials`

```csharp
/// <summary>
/// Holds per-kitten GPU material handles created by MaterialFactory.
/// Each handle points to a unique slot in the GpuMaterialSystem buffer
/// with a custom AlbedoColor tint.
/// </summary>
public sealed class KittenMaterialSet
{
    /// <summary>Unique identifier for this material set (e.g., "doh_kitten_0042").</summary>
    public string Id { get; }
    
    /// <summary>The AlbedoColor tint applied to body/head materials.</summary>
    public float4 TintColor { get; private set; }
    
    /// <summary>GPU material handle for the body mesh (CharacterBodyMaterial).</summary>
    public int BodyMaterialHandle { get; init; }
    
    /// <summary>GPU material handle for the head mesh (CharacterHeadMaterial).</summary>
    public int HeadMaterialHandle { get; init; }
    
    /// <summary>GPU material handle for the eye mesh (CharacterEyeMaterial). Usually untinted.</summary>
    public int EyeMaterialHandle { get; init; }
    
    /// <summary>GPU material handle for the fur mesh. Created via CreateFurMaterial pattern.</summary>
    public int FurMaterialHandle { get; init; }
    
    /// <summary>Whether this material set was successfully created and all handles are valid.</summary>
    public bool IsValid => BodyMaterialHandle >= 0 && HeadMaterialHandle >= 0 
                         && EyeMaterialHandle >= 0;
    
    /// <summary>
    /// Updates the AlbedoColor tint on body and head materials.
    /// Writes directly to the GPU buffer for immediate visual update.
    /// </summary>
    public bool UpdateTint(float4 newColor);
    // Implementation:
    //   TintColor = newColor;
    //   bool ok = MaterialSystemAccessor.WriteAlbedoColor(BodyMaterialHandle, newColor);
    //   ok &= MaterialSystemAccessor.WriteAlbedoColor(HeadMaterialHandle, newColor);
    //   return ok;
}
```

---

### 4.3 `doh.lib/Materials/MaterialFactory.cs`

**Purpose:** Creates unique `KittenMaterialSet` instances with custom tint colors for each spawned kitten.

**Namespace:** `MeowSci.DohLib.Materials`

```csharp
/// <summary>
/// Creates unique GPU material instances for each spawned kitten.
/// 
/// Each material is a clone of the base character material with a custom
/// AlbedoColor tint. Materials are registered in the GpuMaterialSystem
/// with unique names to prevent conflicts.
/// 
/// Reference: CharacterAvatar.InitalizeFromCharacterRef() (CharacterAvatar.cs:382-475)
/// shows how the game loads character materials. We replicate this pattern
/// but with custom AlbedoColor values.
/// </summary>
public sealed class MaterialFactory
{
    private int _nextMaterialId;
    private readonly List<KittenMaterialSet> _createdSets = new();
    
    public IReadOnlyList<KittenMaterialSet> CreatedSets => _createdSets;
    
    /// <summary>
    /// Creates a new material set for a kitten with the specified tint color.
    /// </summary>
    /// <param name="characterId">
    ///   The character reference ID (e.g., "Calico", "Tabby"). 
    ///   Used to resolve the correct PbrMaterialReference textures.
    ///   Get available IDs from ModLibrary.Get&lt;CharacterReference&gt;().
    /// </param>
    /// <param name="tintColor">
    ///   The AlbedoColor to apply. float4(1,1,1,1) = no tint (white).
    ///   The shader multiplies: finalColor = textureColor * AlbedoColor.
    ///   So float4(1,0,0,1) = red tint, float4(0,1,0,1) = green, etc.
    /// </param>
    /// <returns>A KittenMaterialSet with unique handles, or null if creation failed.</returns>
    public KittenMaterialSet? CreateTintedMaterialSet(string characterId, float4 tintColor);
    // Implementation approach:
    //
    // 1. Generate unique prefix: $"doh_{_nextMaterialId++:D4}"
    //
    // 2. Resolve the character's base material textures:
    //    a. Get CharacterReference from ModLibrary:
    //       var charRef = ModLibrary.Get<CharacterReference>(characterId);
    //    b. Get texture references:
    //       - charRef.CharacterTextures.Get().CharacterBodyMaterial → PbrMaterialReference
    //       - charRef.CharacterTextures.Get().CharacterHeadMaterial → PbrMaterialReference
    //       - charRef.CharacterTextures.Get().CharacterEyeMaterial → PbrMaterialReference
    //    c. For each PbrMaterialReference, resolve texture bindless handles:
    //       - DiffuseReference → TextureSystem.GetOrLoad(id) → .BindlessHandle → albedoTexture
    //       - NormalReference → TextureSystem.GetOrLoad(id) → .BindlessHandle → normalTexture
    //       - PBRMap → TextureSystem.GetOrLoad(id) → .BindlessHandle → roughMetallicAOTexture
    //       - If any reference is null, use defaults:
    //         albedo → DefaultWhiteTexture, normal → BlankNormalTexture, PBR → BlankMaterialTexture
    //
    //    NOTE: The reflection path to get PbrMaterialReference textures:
    //       - ModLibrary is a static class accessible via typeof(Part).Assembly
    //       - ModLibrary.Get<CharacterReference>(id) returns the character data
    //       - CharacterTextures is a SubAssetReference<CharacterTexturesReference>
    //       - .Get() resolves it from ModLibrary
    //       - Each PbrMaterialReference has DiffuseReference, NormalReference, PBRMap
    //       - These are TextureReference/TexturePowerReference with .Id property
    //       - TextureSystem.GetOrLoad(id) returns GpuTextureAssetRef with .BindlessHandle
    //
    // 3. Create body MaterialData:
    //    var bodyMat = new MaterialData {
    //        AlbedoTexture = bodyAlbedoHandle,
    //        NormalTexture = bodyNormalHandle,
    //        RoughMetallicAOTexture = bodyPbrHandle,
    //        Sampler = MaterialSystemAccessor.GetDefaultSamplerHandle(),
    //        AlbedoColor = tintColor,
    //        RoughnessMetalScale = float4.One,
    //        EmissiveTexture = MaterialSystemAccessor.GetDefaultBlackTextureHandle()
    //    };
    //    MaterialSystemAccessor.CreateMaterial($"{prefix}_body", bodyMat);
    //    int bodyHandle = MaterialSystemAccessor.GetMaterialHandle($"{prefix}_body");
    //
    // 4. Repeat for head material (same as body but with head textures)
    //
    // 5. Eye material: create with float4.One AlbedoColor (eyes shouldn't be tinted)
    //    OR use the original shared eye material handle (no tinting needed)
    //
    // 6. Fur material: Use the CharacterRenderResources.CreateFurMaterial() pattern
    //    but with our custom AlbedoColor. The fur material needs special ExtraData
    //    for FurTexture, FurSampler, and CatFurMaskTexture handles.
    //    Alternatively, create a standard material and just set AlbedoColor —
    //    the fur renderer uses a different shader (FurFrag) but still reads AlbedoColor.
    //
    // 7. Return new KittenMaterialSet { Id, TintColor, BodyHandle, HeadHandle, EyeHandle, FurHandle }
    
    /// <summary>
    /// Simplified method: creates materials using only a tint color without
    /// needing to resolve character textures. Instead, it clones an existing
    /// material by reading its data from the GPU buffer.
    /// 
    /// This is the FALLBACK approach if texture resolution proves too complex.
    /// It finds the EXISTING shared material handles from the AssetMap, reads
    /// back their MaterialData, and creates new copies with modified AlbedoColor.
    /// </summary>
    public KittenMaterialSet? CreateTintedFromExisting(string baseMaterialName, float4 tintColor);
    // Implementation:
    // 1. Look up baseMaterialName in AssetMap → get handle
    // 2. Read MaterialData from GPU buffer at handle offset:
    //    - Get BigBuffer, create staging pool for read-back
    //    - Or maintain a CPU-side cache of MaterialData when creating
    // 3. Modify AlbedoColor
    // 4. CreateObject() with unique name
    // Note: GPU read-back is complex. Better to use the texture resolution approach above.
    
    /// <summary>
    /// Disposes all material sets created by this factory.
    /// Frees GPU buffer slots for reuse.
    /// </summary>
    public void Cleanup();
}
```

**Critical Implementation Detail — Resolving Texture Handles via Reflection:**

The `CharacterAvatar.GetMaterial()` method (CharacterAvatar.cs line 568-575) shows the resolution chain:
```csharp
private GpuObjectAssetRef? GetMaterial(PbrMaterialReference pbrMaterial)
{
    return Program.Instance.CharacterRenderSystem._meshRenderSystem.MaterialSystem.GetOrLoad(pbrMaterial.Id);
}
```

For our purposes, we need the **texture handles** inside the material, not the material handle itself. The textures are resolved during `GltfPbrSystem` loading. The most robust approach:

1. Get the EXISTING shared material's `GpuObjectAssetRef` from `MaterialSystem.GetOrLoad(pbrMaterialRef.Id)` → this gives us the handle
2. We KNOW the material was already created and uploaded to GPU
3. Rather than reading back from GPU, we can resolve textures from `PbrMaterialReference` directly:
   - `pbrMaterialRef.DiffuseReference.Get()` → `TextureReference` with `.Id`
   - `TextureSystem.GetOrLoad(textureRef.Id)` → `GpuTextureAssetRef` with `.BindlessHandle`

**Alternative Simpler Approach — CPU-Side Material Cache:**

Maintain a `Dictionary<string, MaterialData>` in `MaterialFactory` that caches the `MaterialData` struct whenever we create one. When cloning, copy from the cache instead of reading back from GPU. For the INITIAL set of base materials (the ones the game creates), we'd need to reconstruct them once from the `PbrMaterialReference` chain.

---

### 4.4 `doh.lib/Spawning/SpawnRequest.cs`

**Purpose:** Defines the input parameters for spawning kittens.

**Namespace:** `MeowSci.DohLib.Spawning`

```csharp
/// <summary>
/// Parameters for spawning one or more kittens.
/// Supports two positioning modes:
///   1. Relative to a reference vehicle (with offset)
///   2. Absolute orbital state (position + velocity in CCI frame)
/// </summary>
public sealed class SpawnRequest
{
    // ---- Positioning Mode 1: Relative to vehicle ----
    
    /// <summary>
    /// Reference vehicle ID (name). The kitten will be spawned near this vehicle.
    /// If null, AbsolutePosition must be provided.
    /// </summary>
    public string? ReferenceVehicleId { get; init; }
    
    /// <summary>
    /// Offset in the reference vehicle's BODY frame (meters).
    /// X = right, Y = up, Z = forward (relative to vehicle orientation).
    /// Default: (0, 0, 10) = 10 meters ahead.
    /// </summary>
    public double3 OffsetBodyFrame { get; init; } = new double3(0, 0, 10);
    
    // ---- Positioning Mode 2: Absolute orbital state ----
    
    /// <summary>
    /// Absolute position in CCI (Centered Inertial) frame of the parent body (meters).
    /// Used when ReferenceVehicleId is null.
    /// </summary>
    public double3? PositionCci { get; init; }
    
    /// <summary>
    /// Absolute velocity in CCI frame (m/s).
    /// Used when ReferenceVehicleId is null.
    /// </summary>
    public double3? VelocityCci { get; init; }
    
    /// <summary>
    /// Parent celestial body name for absolute positioning (e.g., "Caturn", "Gael").
    /// Required when using PositionCci/VelocityCci.
    /// </summary>
    public string? ParentBodyName { get; init; }
    
    // ---- Orientation ----
    
    /// <summary>
    /// Body-to-CCE rotation as Euler angles (radians). 
    /// If null, inherits from reference vehicle orientation.
    /// </summary>
    public double3? Body2CceEuler { get; init; }
    
    /// <summary>
    /// Angular velocity (rad/s). If null, inherits from reference vehicle.
    /// </summary>
    public double3? BodyRates { get; init; }
    
    // ---- Batch Spawning ----
    
    /// <summary>
    /// Number of kittens to spawn. Each subsequent kitten is offset from the
    /// previous by OffsetBodyFrame, creating a chain with no overlap.
    /// Default: 1.
    /// </summary>
    public int Count { get; init; } = 1;
    
    // ---- Character & Material ----
    
    /// <summary>
    /// Character reference ID (e.g., "Calico"). If null, a random character is selected.
    /// Available characters can be enumerated from ModLibrary.
    /// </summary>
    public string? CharacterId { get; init; }
    
    /// <summary>
    /// Custom material tint color as RGBA float4.
    /// float4(1,1,1,1) = no tint (default white).
    /// If null, no custom materials are created (uses shared defaults).
    /// </summary>
    public float4? TintColor { get; init; }
    
    /// <summary>
    /// When spawning multiple kittens (Count > 1), whether each kitten 
    /// gets its own material set. If false, they share one custom material set.
    /// Default: false (share one set for efficiency).
    /// </summary>
    public bool UniqueMaterialsPerKitten { get; init; }
    
    /// <summary>
    /// When spawning multiple kittens with UniqueMaterialsPerKitten=true,
    /// provides per-kitten colors. If this list is shorter than Count,
    /// remaining kittens use TintColor.
    /// </summary>
    public float4[]? PerKittenColors { get; init; }
}
```

---

### 4.5 `doh.lib/Spawning/SpawnResult.cs`

**Purpose:** Return value from spawn operations.

**Namespace:** `MeowSci.DohLib.Spawning`

```csharp
/// <summary>Result of a single kitten spawn operation.</summary>
public sealed class SpawnedKittenInfo
{
    /// <summary>The kitten's unique vehicle ID in the game (e.g., "Kitten_42").</summary>
    public string KittenId { get; init; } = "";
    
    /// <summary>The character reference ID used (e.g., "Calico").</summary>
    public string CharacterId { get; init; } = "";
    
    /// <summary>Material set ID if custom materials were created, null otherwise.</summary>
    public string? MaterialSetId { get; init; }
    
    /// <summary>The tint color applied, or null if using default materials.</summary>
    public float4? TintColor { get; init; }
    
    /// <summary>Position in CCI frame after spawn (meters).</summary>
    public double3 PositionCci { get; init; }
    
    /// <summary>Velocity in CCI frame after spawn (m/s).</summary>
    public double3 VelocityCci { get; init; }
    
    /// <summary>Name of the parent celestial body.</summary>
    public string ParentBodyName { get; init; } = "";
}

/// <summary>Result of a batch spawn operation.</summary>
public sealed class SpawnResult
{
    /// <summary>Whether the overall operation succeeded.</summary>
    public bool Success { get; init; }
    
    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; init; }
    
    /// <summary>Info about each kitten that was successfully spawned.</summary>
    public SpawnedKittenInfo[] SpawnedKittens { get; init; } = Array.Empty<SpawnedKittenInfo>();
    
    /// <summary>Total number of kittens spawned.</summary>
    public int Count => SpawnedKittens.Length;
}
```

---

### 4.6 `doh.lib/Spawning/SpawnedKittenRegistry.cs`

**Purpose:** Tracks all kittens spawned by this mod for management (despawn, recolor, list).

**Namespace:** `MeowSci.DohLib.Spawning`

```csharp
/// <summary>
/// Registry of all kittens spawned by the doh mod.
/// Maintains references for despawning, recoloring, and enumeration.
/// </summary>
public sealed class SpawnedKittenRegistry
{
    private readonly Dictionary<string, SpawnedKittenEntry> _kittens = new();
    
    /// <summary>All tracked kitten IDs.</summary>
    public IReadOnlyCollection<string> KittenIds => _kittens.Keys;
    
    /// <summary>Number of tracked kittens.</summary>
    public int Count => _kittens.Count;
    
    /// <summary>Registers a newly spawned kitten.</summary>
    public void Register(string kittenId, string characterId, KittenMaterialSet? materialSet);
    
    /// <summary>Unregisters a kitten (after despawn).</summary>
    public void Unregister(string kittenId);
    
    /// <summary>Gets entry for a specific kitten.</summary>
    public SpawnedKittenEntry? Get(string kittenId);
    
    /// <summary>Lists all tracked kittens.</summary>
    public IReadOnlyList<SpawnedKittenEntry> GetAll();
    
    /// <summary>Clears all entries.</summary>
    public void Clear();
}

public sealed class SpawnedKittenEntry
{
    public string KittenId { get; init; } = "";
    public string CharacterId { get; init; } = "";
    public KittenMaterialSet? MaterialSet { get; init; }
}
```

---

### 4.7 `doh.lib/Spawning/KittenSpawner.cs`

**Purpose:** Core spawning engine. Replicates and extends the `EVADoor.CreateKittenEva()` logic.

**Namespace:** `MeowSci.DohLib.Spawning`

```csharp
/// <summary>
/// Spawns kitten entities (KittenEva) programmatically.
///
/// Replicates the game's EVADoor.CreateKittenEva() spawn flow
/// (EVADoor.cs lines 65-81) but with additional features:
///   - Arbitrary positioning (vehicle-relative or absolute orbital)
///   - Batch spawning with offset chains
///   - Optional per-kitten material customization
///
/// MUST be called on the game thread (not from HTTP handlers directly).
/// When used via RPC, the caller must schedule via GameThread.Scheduler.
/// </summary>
public sealed class KittenSpawner
{
    private readonly MaterialFactory _materialFactory;
    private readonly SpawnedKittenRegistry _registry;
    private int _nextKittenIndex;
    
    public KittenSpawner(MaterialFactory materialFactory, SpawnedKittenRegistry registry);
    
    /// <summary>Spawns kitten(s) according to the request parameters.</summary>
    public SpawnResult Spawn(SpawnRequest request);
    // Implementation steps:
    //
    // STEP 1: Validate request
    //   - Either ReferenceVehicleId or (PositionCci + VelocityCci + ParentBodyName) must be provided
    //   - Count must be >= 1 and <= 100 (safety cap)
    //   - If ReferenceVehicleId provided, resolve it:
    //       var vehicles = VehicleProvider.GetAllVehicles();
    //       var refVehicle = vehicles.FirstOrDefault(v => v.Id == request.ReferenceVehicleId);
    //       if (refVehicle == null) return error;
    //
    // STEP 2: Resolve positioning
    //   IF vehicle-relative:
    //     a. Get reference vehicle's orbital state:
    //        StateVectors sv = refVehicle.Orbit.StateVectors;
    //        doubleQuat body2Cci = refVehicle.GetBody2Cci();  // via GetAsmb2Cci or Body2Cce * Cce2Cci
    //        IParentBody parent = refVehicle.Parent;
    //     b. Transform offset from body frame to CCI:
    //        double3 offsetCci = request.OffsetBodyFrame.Transform(body2Cci);
    //     c. Calculate spawn position:
    //        double3 spawnPosCci = sv.PositionCci + offsetCci;
    //        double3 spawnVelCci = sv.VelocityCci;  // inherit velocity
    //     d. Orientation: use request.Body2CceEuler if provided, else refVehicle.Body2Cce
    //     e. Body rates: use request.BodyRates if provided, else refVehicle.BodyRates
    //
    //   IF absolute:
    //     a. Resolve parent body:
    //        Find parent from CelestialProvider or Universe.CurrentSystem by name
    //     b. Use provided PositionCci, VelocityCci directly
    //     c. Body2Cce: from request.Body2CceEuler or doubleQuat.Identity
    //     d. BodyRates: from request.BodyRates or double3.Zero
    //
    // STEP 3: Resolve character
    //   string charId = request.CharacterId;
    //   if (charId == null) charId = GetRandomCharacterId();
    //   // GetRandomCharacterId(): Use ModLibrary to enumerate CharacterReference entries
    //   // Same approach as EVADoor.TryGetRandomCharacter()
    //
    // STEP 4: Spawn loop (for Count kittens)
    //   var results = new List<SpawnedKittenInfo>();
    //   for (int i = 0; i < request.Count; i++)
    //   {
    //     a. Generate unique name:
    //        string kittenName = GenerateUniqueName();
    //        // Check Universe.CurrentSystem for name collisions
    //        // Pattern: "Kitten_doh_{_nextKittenIndex++}"
    //
    //     b. Create backpack part (same as EVADoor.GetBackPackPart()):
    //        var partTemplate = ModLibrary.AllParts.Find(KeyHash.Make("KittenBackPackPart".AsSpan()));
    //        var part = new Part(partTemplate.Id, partTemplate);
    //        part.Tree.ReinitializeDerivedValues();
    //        // Fill propellant tanks:
    //        var mix = SubstanceLibrary.TryGetCombustionProcess(KeyHash.Make("MMH_NTO_1.6".AsSpan()));
    //        foreach (var tank in part.SubtreeModules.Get<Tank>())
    //            tank.ConfigureFor(mix);
    //        part.Tree.RefillConsumables();
    //
    //     c. Create KittenEva:
    //        var kittenEva = new KittenEva(
    //            Universe.CurrentSystem,
    //            charId,
    //            body2Cce,
    //            bodyRates,
    //            parent,
    //            kittenName,
    //            part,
    //            orbit  // temporary, will be replaced by Teleport
    //        );
    //
    //     d. Calculate this kitten's specific position (with chain offset):
    //        double3 thisOffsetCci = offsetCci * (i + 1);  // or additive
    //        // For vehicle-relative: each kitten gets OffsetBodyFrame * (i+1) in body frame
    //        //   double3 chainOffset = (request.OffsetBodyFrame * (i + 1)).Transform(body2Cci);
    //        //   double3 kittenPosCci = sv.PositionCci + chainOffset;
    //        // For absolute: each kitten gets OffsetBodyFrame * i added to base position
    //
    //     e. Create orbit and teleport:
    //        Orbit newOrbit = Orbit.CreateFromStateCci(
    //            parent, simTime, kittenPosCci, spawnVelCci, IndexedColor.Amber);
    //        kittenEva.Teleport(newOrbit, null, null);
    //
    //     f. Register with parent body:
    //        parent.Children.Add(kittenEva);
    //        kittenEva.UpdatePerFrameData();
    //
    //     g. Apply custom materials (if requested):
    //        if (request.TintColor != null || request.PerKittenColors != null)
    //        {
    //            float4 color = request.PerKittenColors?[i] ?? request.TintColor ?? float4.One;
    //            KittenMaterialSet? matSet = null;
    //            
    //            if (request.UniqueMaterialsPerKitten || i == 0)
    //                matSet = _materialFactory.CreateTintedMaterialSet(charId, color);
    //            else
    //                matSet = previousMatSet;  // Reuse first kitten's material set
    //            
    //            if (matSet != null)
    //                ApplyMaterialSetToKitten(kittenEva, matSet);
    //        }
    //
    //     h. Track in registry:
    //        _registry.Register(kittenName, charId, matSet);
    //
    //     i. Add to results list
    //   }
    //
    // STEP 5: Return SpawnResult
    
    /// <summary>Despawns a kitten by ID. Removes from parent body and frees materials.</summary>
    public bool Despawn(string kittenId);
    // Implementation:
    //   1. Look up in registry
    //   2. Find KittenEva in Universe.CurrentSystem by ID
    //   3. Remove from parent.Children
    //   4. Dispose material set if owned
    //   5. Unregister from registry
    
    /// <summary>Despawns all kittens spawned by this mod.</summary>
    public void DespawnAll();
    
    /// <summary>
    /// Updates the tint color on a previously spawned kitten's materials.
    /// </summary>
    public bool RecolorKitten(string kittenId, float4 newColor);
    // Implementation:
    //   1. Look up in registry → get SpawnedKittenEntry
    //   2. If entry.MaterialSet != null:
    //      entry.MaterialSet.UpdateTint(newColor);
    //   3. Else: create material set now and apply
    
    /// <summary>Lists all available character IDs from ModLibrary.</summary>
    public string[] GetAvailableCharacters();
    // Uses reflection: ModLibrary enumeration of CharacterReference entries
    
    // --- Private helpers ---
    
    /// <summary>
    /// Applies a KittenMaterialSet to a KittenEva by replacing the
    /// MaterialIndices entries on its CharacterAvatar's AnimatedRenderable.
    /// </summary>
    private static void ApplyMaterialSetToKitten(KittenEva kittenEva, KittenMaterialSet matSet);
    // Implementation:
    //   1. Access kittenEva._renderable via reflection (private field)
    //      KittenRenderable has _characterAvatar (private field)
    //   2. Get CharacterAvatar.Core.CharacterModel (AnimatedRenderable)
    //   3. Access AnimatedRenderable.MaterialIndices (protected readonly int[])
    //      via reflection: typeof(AnimatedRenderable).GetField("MaterialIndices", 
    //          BindingFlags.NonPublic | BindingFlags.Instance)
    //   4. Replace array elements:
    //      - From CharacterAvatar.InitalizeFromCharacterRef() we know:
    //        MaterialIndices mapping:
    //          index 0 → eye material (from GltfAssetRef.MaterialIndices[0])
    //          index 1 → head material (from GltfAssetRef.MaterialIndices[1])
    //          index 2 → body material (from GltfAssetRef.MaterialIndices[2])
    //          index 3 → head material again (from GltfAssetRef.MaterialIndices[3])
    //        But the ACTUAL indices depend on GltfAssetRef.MaterialIndices mapping.
    //        The customMaterials array is indexed by the GLTF material index, and
    //        then GltfAssetRef.MaterialIndices[meshIdx] maps mesh → material.
    //
    //        From CharacterAvatar.cs:415-420:
    //          array[0] = GetMaterial(CharacterEyeMaterial);   // GLTF material slot 0 = eye
    //          array[1] = GetMaterial(CharacterHeadMaterial);  // GLTF material slot 1 = head
    //          array[2] = GetMaterial(CharacterBodyMaterial);  // GLTF material slot 2 = body
    //          array[3] = GetMaterial(CharacterHeadMaterial);  // GLTF material slot 3 = head again
    //
    //        Then in AnimatedRenderable constructor (line 84):
    //          MaterialIndices[meshIdx] = array[GltfAssetRef.MaterialIndices[meshIdx]].Handle;
    //
    //        So MaterialIndices maps: meshIndex → materialHandle
    //        where the materialHandle came from customMaterials[gltfMaterialIndex].Handle
    //
    //      APPROACH: Iterate MaterialIndices, for each entry:
    //        - If the current handle matches any known shared body material → replace with matSet.BodyMaterialHandle
    //        - If matches shared head material → replace with matSet.HeadMaterialHandle
    //        - If matches shared eye material → optionally leave or replace with matSet.EyeMaterialHandle
    //
    //      To find the shared handles, we can look them up via MaterialSystem.GetOrLoad()
    //      using the PbrMaterialReference.Id from the character reference.
    //
    //   5. Also update fur material if applicable:
    //      Access CharacterAvatar.Fur.CatFurRenderable → its internal material reference
    
    private static string GenerateUniqueName();
    // Pattern: "Kitten_doh_{_nextKittenIndex++}"
    // Verify no collision with Universe.CurrentSystem
}
```

**Critical Reflection Chain for Applying Materials:**

```
KittenEva instance
  └─ Field: _renderable (KittenRenderable, private)
       └─ Field: _characterAvatar (CharacterAvatar, private)
            └─ Property: Core (CharacterCore struct)
                 └─ Field: CharacterModel (AnimatedRenderable)
                      └─ Field: MaterialIndices (protected readonly int[])
                           └─ Array element modification: MaterialIndices[i] = newHandle
```

Each step requires reflection since the fields are private/protected. Use `BindingFlags.NonPublic | BindingFlags.Instance`.

---

### 4.8 `doh/Mod.cs`

**Purpose:** StarMapMod lifecycle and ImGui window for the doh mod.

**Namespace:** `MeowSci.Doh`

```csharp
[StarMapMod]
public class Mod
{
    public bool ImmediateUnload => false;
    
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _windowVisible;
    
    // Core systems (from doh.lib)
    private MaterialFactory? _materialFactory;
    private SpawnedKittenRegistry? _registry;
    private KittenSpawner? _spawner;
    
    // UI state
    private string _selectedVehicleId = "";
    private float[] _offset = { 0f, 0f, 10f };
    private int _spawnCount = 1;
    private float[] _tintColor = { 1f, 1f, 1f, 1f };
    private bool _useCustomColor;
    private bool _uniquePerKitten;
    private string? _statusMessage;
    private string? _errorMessage;
    
    [StarMapImmediateLoad]
    public void OnImmediateLoad() { }
    
    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        Patcher.Patch();
        
        // Initialize library systems
        if (!MaterialSystemAccessor.Initialize())
        {
            Console.WriteLine($"doh: Failed to init MaterialSystem: {MaterialSystemAccessor.LastError}");
        }
        
        _materialFactory = new MaterialFactory();
        _registry = new SpawnedKittenRegistry();
        _spawner = new KittenSpawner(_materialFactory, _registry);
        
        _isInitialized = true;
        Console.WriteLine("doh: Initialized");
    }
    
    [StarMapBeforeGui]
    public void OnBeforeUi(double dt) { }
    
    [StarMapAfterGui]
    public void OnAfterUi(double dt)
    {
        if (!_isInitialized || _isDisposed) return;
        
        // Toggle window with F8 (pick an available key)
        if (ImGui.IsKeyPressed(ImGuiKey.F8))
            _windowVisible = !_windowVisible;
        
        if (_windowVisible)
            RenderWindow();
    }
    
    [StarMapUnload]
    public void Unload()
    {
        _spawner?.DespawnAll();
        _materialFactory?.Cleanup();
        MaterialSystemAccessor.Cleanup();
        Patcher.Unload();
        _isDisposed = true;
    }
    
    private void RenderWindow()
    {
        ImGui.SetNextWindowSize(new float2(450, 500), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("DOH - Kitten Spawner###doh-window", ref _windowVisible))
        {
            ImGui.End();
            return;
        }
        
        // -- Status section --
        if (_errorMessage != null) ImGui.TextColored(new float4(1,0.3f,0.3f,1), _errorMessage);
        if (_statusMessage != null) ImGui.TextColored(new float4(0.3f,1,0.3f,1), _statusMessage);
        ImGui.Text($"Spawned: {_registry?.Count ?? 0} kittens");
        ImGui.Separator();
        
        // -- Vehicle selection --
        // ComboBox listing all vehicles from VehicleProvider.GetAllVehicles()
        // Sets _selectedVehicleId
        
        // -- Offset --
        ImGui.InputFloat3("Offset (body frame)", ref _offset);
        
        // -- Spawn count --
        ImGui.SliderInt("Count", ref _spawnCount, 1, 20);
        
        // -- Material options --
        ImGui.Checkbox("Custom Color", ref _useCustomColor);
        if (_useCustomColor)
        {
            ImGui.ColorEdit4("Tint Color", ref _tintColor);
            if (_spawnCount > 1)
                ImGui.Checkbox("Unique per kitten", ref _uniquePerKitten);
        }
        
        // -- Spawn button --
        if (ImGui.Button("Spawn Kitten(s)"))
        {
            var request = new SpawnRequest
            {
                ReferenceVehicleId = _selectedVehicleId,
                OffsetBodyFrame = new double3(_offset[0], _offset[1], _offset[2]),
                Count = _spawnCount,
                TintColor = _useCustomColor ? new float4(_tintColor) : null,
                UniqueMaterialsPerKitten = _uniquePerKitten,
            };
            var result = _spawner!.Spawn(request);
            _statusMessage = result.Success ? $"Spawned {result.Count} kitten(s)" : null;
            _errorMessage = result.Error;
        }
        
        ImGui.Separator();
        
        // -- Spawned kittens list --
        // Table showing ID, character, color, with Despawn and Recolor buttons
        
        // -- Despawn All button --
        if (ImGui.Button("Despawn All"))
        {
            _spawner!.DespawnAll();
            _statusMessage = "All kittens despawned";
        }
        
        ImGui.End();
    }
}
```

---

### 4.9 `doh/Patcher.cs`

**Purpose:** Standard Harmony patcher with HotkeyGuard. Follows `fixme-mod-name/Patcher.cs` pattern exactly.

**Namespace:** `MeowSci.Doh`

```csharp
[HarmonyPatch]
internal static class Patcher
{
    private static Harmony? _harmony = new Harmony("doh");

    public static void Patch()
    {
        try
        {
            _harmony?.PatchAll(typeof(Patcher).Assembly);
            if (_harmony != null) HotkeyGuard.Patch(_harmony);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error applying patches: {ex.Message}");
        }
    }

    public static void Unload()
    {
        try
        {
            if (_harmony != null) HotkeyGuard.Unpatch(_harmony);
            _harmony?.UnpatchAll("doh");
            _harmony = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"doh: Error removing patches: {ex.Message}");
        }
    }
}
```

---

### 4.10 `doh/mod.toml`

```toml
name = "doh"
description = "Dynamically Originating Hominids — Programmatic kitten spawning with per-kitten material customization"
version = "0.1.0"
author = "meow sci"

[StarMap]
EntryAssembly = "MeowSci.Doh"
```

---

### 4.11 `.csproj` Files

#### `doh/doh.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.Doh</AssemblyName>
    <DistDir>$(SelectedDistModDir)doh\</DistDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StarMap.API" Version="0.3.6" PrivateAssets="all" />
    <PackageReference Include="Lib.Harmony" Version="2.4.2" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\doh.lib\doh.lib.csproj" />
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <!-- Game DLL references: same pattern as fixme-mod-name.csproj -->
  <!-- Include: KSA.dll, Brutal.Core.Numerics.dll, Brutal.Core.Common.dll, 
       Brutal.ImGui.dll, Brutal.Vulkan.dll, RenderCore.dll, etc. -->
  <!-- All with Condition="Exists(...)" and Private=false -->
</Project>
```

#### `doh.lib/doh.lib.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <AssemblyName>MeowSci.DohLib</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />
  </ItemGroup>

  <!-- Game DLL references needed for library code:
       KSA.dll (for Part, MaterialData, KittenEva, Vehicle, Orbit, etc.)
       Brutal.Core.Numerics.dll (for float4, double3, doubleQuat, ByteSize)
       Brutal.Core.Common.dll (for AssetName, KeyHash)
       Brutal.Vulkan.dll (for BufferEx, IVulkanContext, VkUtils)
       Brutal.Vulkan.Abstractions.dll (for IVulkanContext)
       RenderCore.dll (for GpuTextureAssetRef, StagingPool)
       CommunityToolkit.HighPerformance (for Span extensions)
  -->
</Project>
```

---

## 5. RPC Endpoint Design (for unladen-swallow integration)

The doh.lib logic is structured so RPC endpoints can be added to `unladen-swallow/SwallowServer.cs` when ready:

```
POST /doh/spawn          → KittenSpawner.Spawn(SpawnRequest)
DELETE /doh/despawn       → KittenSpawner.Despawn(kittenId)
DELETE /doh/despawn-all   → KittenSpawner.DespawnAll()
PUT /doh/recolor          → KittenSpawner.RecolorKitten(kittenId, color)
GET /doh/kittens          → SpawnedKittenRegistry.GetAll()
GET /doh/characters       → KittenSpawner.GetAvailableCharacters()
```

**Example endpoint (future):**
```csharp
public static class DohSpawnEndpoint
{
    public static IHandler Create(KittenSpawner spawner)
    {
        return Inline.Create()
            .Serializers(Serialization.Default())
            .Post(async (SpawnRequest body) =>
            {
                var result = await GameThread.Scheduler.Schedule(() => spawner.Spawn(body));
                if (!result.Success)
                    throw new ProviderException(ResponseStatus.BadRequest, result.Error!);
                return (object)new ApiResponse<SpawnResult>("ok", result);
            })
            .Build();
    }
}
```

All `KittenSpawner` methods are synchronous and MUST run on the game thread. The RPC layer handles thread marshalling via `GameThread.Scheduler.Schedule()`.

---

## 6. Key Implementation Risks & Mitigations

### Risk 1: MaterialData Struct Layout Mismatch
**Issue:** If the decompiled `MaterialData` struct doesn't exactly match the runtime layout, GPU uploads will corrupt data.
**Mitigation:** Use `Marshal.SizeOf<MaterialData>()` at runtime to verify size matches expected (64 bytes with `Pack = 1`). Log a warning if mismatched.

### Risk 2: GpuObjectSystem Buffer Full
**Issue:** `FreeListIndexPool.TryAdd()` fails when buffer is at capacity.
**Mitigation:** Check capacity before spawning. Log error and fall back to shared materials if allocation fails. Free materials on despawn.

### Risk 3: Reflection Path Changes
**Issue:** Game updates may rename/move fields accessed via reflection.
**Mitigation:** Wrap all reflection in try/catch with descriptive error messages. MaterialSystemAccessor.Initialize() returns false with error details. UI shows initialization status.

### Risk 4: KittenEva Constructor Access
**Issue:** `KittenEva` is a public class but its constructor parameters require internal game types.
**Mitigation:** We have direct access to `KSA.dll` types. The constructor is public. No reflection needed for instantiation itself.

### Risk 5: Thread Safety
**Issue:** Material creation involves GPU uploads which aren't inherently thread-safe.
**Mitigation:** All spawn/material operations happen on the game thread (via `OnBeforeUi`/`OnAfterUi` for UI, via `GameThread.Scheduler` for RPC). The GPU buffer allocator uses locks internally.

### Risk 6: AnimatedRenderable.MaterialIndices Reflection
**Issue:** `MaterialIndices` is `protected readonly int[]`. We need reflection to access it.
**Mitigation:** The `readonly` modifier only prevents reassigning the field reference, not modifying array elements. Once we get the array via `FieldInfo.GetValue()`, we can modify elements directly. This is a well-established C# pattern.

---

## 7. Implementation Task List

### Phase 1: Project Scaffolding
1. **TASK-SCAFFOLD**: Run `bun run mkmod.ts doh Doh` to create the doh/doh.lib project structure from the fixme-mod-name template.
2. **TASK-SOLUTION**: Add both projects to `ksa-mod-experiments.slnx`.
3. **TASK-CSPROJ**: Update both `.csproj` files with the correct game DLL references (copy pattern from `humble-arteest.csproj` / `humble-arteest.lib.csproj` since they need the same rendering/material types).

### Phase 2: Material System (doh.lib/Materials/)
4. **TASK-MATERIAL-ACCESSOR**: Implement `MaterialSystemAccessor.cs` — reflection bridge to `GpuMaterialSystem` and `GpuTextureSystem`. This is the foundation everything else depends on.
5. **TASK-MATERIAL-SET**: Implement `KittenMaterialSet.cs` — simple data class holding per-kitten material handles.
6. **TASK-MATERIAL-FACTORY**: Implement `MaterialFactory.cs` — runtime material creation with custom tint colors.

### Phase 3: Spawning System (doh.lib/Spawning/)
7. **TASK-SPAWN-DTOS**: Implement `SpawnRequest.cs` and `SpawnResult.cs` — request/response DTOs.
8. **TASK-REGISTRY**: Implement `SpawnedKittenRegistry.cs` — state tracking for spawned kittens.
9. **TASK-SPAWNER**: Implement `KittenSpawner.cs` — core spawn/despawn/recolor engine.

### Phase 4: Mod UI (doh/)
10. **TASK-PATCHER**: Implement `Patcher.cs` with HotkeyGuard.
11. **TASK-MOD**: Implement `Mod.cs` with ImGui window for spawning controls.
12. **TASK-MOD-TOML**: Create `mod.toml` with correct metadata.

### Phase 5: Documentation & Index
13. **TASK-README-LIB**: Write `doh.lib/README.md` with API documentation.
14. **TASK-README-MOD**: Write `doh/README.md` with usage documentation.
15. **TASK-REPO-INDEX**: Update `REPOSITORY_INDEX.md` with doh mod entry.

### Phase 6: Build & Verification
16. **TASK-BUILD**: Run `dotnet build` to verify compilation.
17. **TASK-REVIEW**: Code review pass for quality, edge cases, and consistency.

---

## 8. Decompiled Source Reference Index

| Source File | Location | Relevance |
|---|---|---|
| `EVADoor.cs` | `decomp/ksa/KSA/` | **PRIMARY** — `CreateKittenEva()` is the spawning template to replicate |
| `KittenEva.cs` | `decomp/ksa/KSA/` | **PRIMARY** — KittenEva class, constructor, factory methods |
| `KittenRenderable.cs` | `decomp/ksa/KSA/` | **PRIMARY** — Rendering setup, CharacterAvatar usage, material override points |
| `CharacterAvatar.cs` | `decomp/ksa/KSA/` | **PRIMARY** — `InitalizeFromCharacterRef()` shows material resolution chain |
| `AnimatedRenderable.cs` | `decomp/ksa/KSA/` | **PRIMARY** — `MaterialIndices[]`, `Draw()`, custom materials constructor |
| `GpuObjectSystem.cs` | `decomp/ksa/KSA/` | **PRIMARY** — `CreateObject()`, `SendToBuffer()`, `Free()` |
| `GpuMaterialSystem.cs` | `decomp/ksa/KSA/` | Material system (extends GpuObjectSystem) |
| `MaterialData.cs` | `decomp/ksa/KSA/` | MaterialData struct layout |
| `CharacterRenderResources.cs` | `decomp/ksa/KSA/` | `CreateFurMaterial()` — runtime material creation example |
| `AssetManager.cs` | `decomp/ksa/KSA/` | `GetOrLoad()`, `TryAdd()`, `AssetMap` |
| `GpuObjectAssetRef.cs` | `decomp/ksa/KSA/` | Handle reference class, `Dispose()` |
| `Vehicle.cs` | `decomp/ksa/KSA/` | `Teleport()`, `SetFlightPlan()`, positioning |
| `Orbit.cs` | `decomp/ksa/KSA/` | `CreateFromStateCci()` |
| `CharacterTexturesReference.cs` | `decomp/ksa/KSA/` | Character material definitions |
| `PbrMaterialReference.cs` | `decomp/ksa/KSA/` | PBR material texture references |

---

## 9. Existing Mod Code References

| File | Relevance |
|---|---|
| `humble-arteest.lib/KittenColor.cs` | **Reflection pattern** — accessing MaterialSystem, AssetMap, BigBuffer, Vulkan upload |
| `garrys-torch.lib/WeldEngine.cs` | **Vehicle positioning** — offset calculations in body frame, `Orbit.CreateFromStateCci()`, `Teleport()` |
| `ksa-abstractions.lib/VehicleProvider.cs` | **Vehicle enumeration** — `GetAllVehicles()`, `GetControlledVehicle()` |
| `ksa-abstractions.lib/CelestialProvider.cs` | **Parent body resolution** |
| `ksa-abstractions.lib/HotkeyGuard.cs` | **Required** — must be applied in Patcher |
| `fixme-mod-name/` | **Template** — canonical mod structure |
| `unladen-swallow.lib/` | **RPC pattern** — endpoint structure, GameThread scheduler |
