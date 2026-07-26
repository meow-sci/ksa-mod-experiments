# PARTS NOW — Runtime Part / SubPart Loading for KSA

> **Status:** IMPLEMENTED. Shipped as `parts-now/` + `parts-now.lib/`.
> See [`parts-now.lib/README.md`](../../parts-now.lib/README.md) for the as-built documentation and
> [`scope/part-editor-and-robotics.md`](../../scope/part-editor-and-robotics.md) → `## parts-now` for
> the game-integration map. **Read the as-built corrections below before trusting the body of this
> document** — several of its claims about the game turned out to be wrong.
> **Target game build:** `2026.7.9.5018` (see `scope/FULL_SCOPE.md` → Version baseline)
> **Decomp root (source of truth):** `C:\Users\Alex\repos\meow-sci\ksa-game-assemblies\current\decomp`
> — referred to below as `<decomp>`
> **New projects:** `parts-now/` + `parts-now.lib/`

---

## As-built corrections (added on completion)

Everything below this section is the plan **as written before implementation**. These are the places
where the game turned out not to match it. The code and `parts-now.lib/README.md` are authoritative.

**Facts about KSA 5018 the plan got wrong**

1. **§T9.2 — registry deltas cannot all be read after `OnDataLoad`.** `AllParts`,
   `AllPartGameDataReferences`, `AllMaterials` and `ModLibrary.Loaders` gain entries during
   `RegisterBundles`, but `AllFiles`, `AllMeshes` and `ModLibrary.Binders` only gain them during
   `RunLoaders` (`FileReference.Load()` registers itself and *then* calls `DoLoad()`). Capturing
   everything at one point would have left unload silently incomplete.
2. **§8 V10 — `<Combustion>` does not exist.** Reactions are `<Reaction Id>` inside `<Combustor>` /
   `<SolidMotor>`. The GrainGeometry *reference* element is `<Grain Id>`; `<GrainGeometry>` is only
   ever a definition, which V8 rejects. `ModLibrary.TryGet<SoundBehavior>` can never succeed (its
   branch is `IsSubclassOf`-only), so the sound check goes through `Get<T>` in a try/catch.
3. **§8 V2 — `<Texture Id="X"/>` with no `Path` is legal** (a reference to an already-loaded
   texture, as in `DefaultAssets.xml`), so the rule requires Id **or** Path for file references.
4. **§T11.2 step 4 — model instances must be purged by object identity, not `Template.Id`.**
   `ModuleBase.TemplateDataBase.Id` is an optional, non-unique XML attribute. `PartModelGlassModule
   .Template.RayTracers` also exists and is purged; the plan named only `PartModelModule`'s.
5. **`ImageViewEx.Dispose()` NREs on a default instance** (null captured `Device`), so a
   `<Thumbnail>` that came from XML must never be disposed. The plan's replace-the-thumbnail
   sequence would also have dropped the declared `<ModelTransform>`; parts-now carries it across.
6. **`Camera.Unfollow()` defaults to `changeControl: true`**, which nulls
   `Program.ControlledVehicle`. Thumbnail generation must pass `false`.
7. **`Brutal.Gltf.dll` is referenced by no project in this repo**, so V6 reads mesh-atlas node names
   from the GLB JSON chunk directly instead of using `GltfLoader`.

**Deliberate design changes**

8. **§T9.3** — loader success is verified by post-conditions (`IsReference()` cleared, atlas
   `Meshes` non-empty, texture present in the new binder set), not id lookups. A demotion whose
   winner is a file this same job registered is a legal share, not a failure: naming one texture from
   two material channels is a pattern KSA itself uses.
9. **§T11.4** — rollback does not record a leak; the cursor rewind reclaims those bytes. A failure
   *after* the first bind purges with leak accounting instead, because a bound mesh has already
   copied into an absolute range.
10. **§T11.2** — gained a step 0 clearing `VehicleEditor.DynamicThumbnail`'s hover preview, whose
    `AddPart` call sits outside `ThumbnailDynamic.Render`'s try/catch.
11. **§5/T1.4** — `GameRegistry` separates fatal problems (the six registries + the removal path,
    which disable loading) from degraded ones (`VehicleEditor._editorTagLookup`, which only narrows
    editor-tag validation and is reported as a warning).
12. **§T12.3** — an existing `mod.toml` is merged rather than replaced, so `File.Move` uses
    `overwrite: true` when the destination is already there.
13. **A failed paste-install deletes the folder it created**, otherwise `ModIdValidator` would refuse
    that mod id for the rest of the session.
14. **§T10.2** — the debug readback is a `PartThumbnailGenerator` property, not a UI toggle: the
    generator is owned and disposed per job, so there is nothing persistent to bind a toggle to.
15. **Test T13** ("missing texture blocked by V11 before anything registers") only holds for the
    folder flow. In the paste flow the folder does not exist at validation time, so V6 and V11's
    existence checks degrade to warnings and the miss is caught by `RunLoaders`' post-conditions.
16. **§16 limitation 5** applies to unload as well as reload.

---

## 0. Executive summary

**Verdict: yes, this is achievable, and without Harmony patching the game.**

KSA loads Parts and SubParts from XML into a set of static registries (`ModLibrary.AllParts`,
`AllMeshes`, `AllFiles`, `AllMaterials`, `AllPartGameDataReferences`) during startup, then renders a
thumbnail per top-level Part into a per-`PartTemplate` Vulkan image. Every stage of that pipeline is
reachable at runtime:

| Startup stage | `<decomp>` location | Runtime reachable? |
|---|---|---|
| Parse XML → `AssetBundle` | `KSA/XmlLoader.cs`, `KSA/XmlHelper.cs` | ✅ `XmlHelper.Serializers[typeof(AssetBundle)]` is `public static` |
| Register templates | `KSA/AssetBundle.cs` `OnDataLoad` | ✅ `public` |
| Read GLB/KTX2 from disk | `KSA/FileReference.cs` `Load()` via `ModLibrary.Loaders` | ✅ `ModLibrary.Loaders` is `public static List<ILoader>` |
| Upload to GPU | `KSA/MeshReference.cs`, `KSA/TextureReference.cs` `Bind()` via `ModLibrary.Binders` | ✅ `ModLibrary.Binders` is `public static List<IBinder>` |
| Merge GameData onto Parts | `KSA/ModLibrary.cs` `AttachGameData()` | ⚠️ must be re-implemented incrementally (stock version is not idempotent) |
| Render thumbnails | `KSA.Rendering/ThumbnailCreator.cs` | ✅ all helpers are `public static`; **and we already do this at runtime in `space-tape.lib/Thumbnails/`** |
| Part browser listing | `KSA/VehicleEditor.cs` `PartWindow.OnDrawUi` | ✅ iterates `ModLibrary.AllParts.GetList()` **live every frame** — new parts appear with no refresh call |

There are **six hard constraints** that a naive implementation will hit (§2). All six have concrete
mitigations described here. The largest one — the single pre-sized shared vertex/index buffer — is
solved by reserving headroom in `[StarMapAllModsLoaded]`, which StarMap fires as a Harmony **postfix
on `ModLibrary.LoadAll()`**, i.e. *before* the game allocates that buffer.

**Design decisions locked by the user (do not re-litigate):**

1. New `parts-now` + `parts-now.lib` project pair; standard repo pattern; no coupling to `space-tape`.
2. Pasted XML is **always** materialised into a real mod folder. The user supplies a **mod id** in an
   ImGui form; the id is used for both the folder name and the KSA mod id; it **must not already
   exist**. The folder location must be the **discovered** mods path (`ModLibrary.LocalModsFolderPath`),
   never a hardcoded string.
3. **Full reload including binaries** is in scope (unregister → `WaitIdle` → reload → rebind →
   regenerate thumbnails), gated behind safety checks.
4. **No new registries.** Substances, Reactions, GrainGeometry and unknown EditorTags are **rejected by
   validation**. Everything must reference ids that already exist.

---

## 1. How KSA loads Parts today (verified against 5018)

### 1.1 Boot sequence

`<decomp>/KSA/Program.cs` — constructor, in order:

```
 913  ModLibrary.PrepareAll()          // read manifest.toml + each mod.toml -> Mod objects
 914  ModLibrary.PreloadAssetBundles()
 955  ModLibrary.LoadEditorTags()      // -> VehicleEditor.MarkEditorTagDefinitionsLoaded()
 956  ModLibrary.LoadAll()             // XML parse + OnDataLoad, then Parallel.ForEachAsync over Loaders
      ==> StarMap fires [StarMapAllModsLoaded] HERE (Harmony postfix on LoadAll)
 957  ModLibrary.AssignDefaults()
 959- build viewports; viewport 1 == ThumbnailViewport, size = ThumbnailRenderer.SIZE
 985  ModLibrary.Bind(_renderer)       // Parallel.ForEachAsync over Binders -> GPU upload
                                       //   first MeshReference.Bind() calls
                                       //   DeviceMeshInterleaved.Shared.Build() -> allocates the
                                       //   single shared vertex+index buffer, ONCE, forever
 992  ModLibrary.AttachGameData()      // PartGameDataReference -> PartTemplate.ApplyGameData
1208- foreach PartTemplate: PartModel.Get / PartModelGlass.Get / PartModelDynamic.Get  (pre-warm only)
1244  PreparePartThumbnails(loading)   // ThumbnailCreator.Initialize + PreparePartThumbnails
1285  VehicleEditor.Initialize()
```

**The `[StarMapAllModsLoaded]` timing is the single most important fact in this document.**
`<decomp>` has no StarMap source; see `unscience/decomp/starmap/StarMap.Core/Patches/ModLibraryPatches.cs`:
it is a `[HarmonyPostfix]` on `ModLibrary.LoadAll`. So at `OnFullyLoaded()` time:

* all `DeviceMeshInterleaved` objects exist and `Shared.RunningVertexBufferSize` /
  `RunningIndexBufferSize` hold their **final startup watermark**, and
* `DeviceMeshInterleaved.Shared.Build()` **has not run yet**.

That window is what makes headroom reservation possible (§6).

### 1.2 Per-frame ordering (matters for GPU submits)

`<decomp>/KSA/Program.cs` `OnFrame`:

```
2071  ImGuiBackend.NewFrame(); ImGui.NewFrame();
2163  OnDrawUiFrame(dt)        <-- [StarMapBeforeGui] prefix  == ISubmod.Update(dt) via unscience
2164  OnDrawUiViewports(dt)    <-- [StarMapAfterGui] postfix  == ISubmod.RenderContent()
2180  ImGui.Render()
2211  OnPreRender(dt)  -> 2280 TryAcquireNextFrame
                       -> 2288 Editor.OnPreRender -> VehicleEditor.DynamicThumbnail.Render()
2292  Render(...)      -> 2308 UpdateShaderData(all viewports) -> 2313 UpdateRenderingResources
2228  renderer.TrySubmitFrame(...)
```

Both StarMap hooks run **before** `TryAcquireNextFrame` — no swapchain image is acquired and no
main-loop command buffer is recording. Submitting our own command buffers to `renderer.Graphics`, and
even `Device.WaitIdle()`, are safe at those points. This is the same position `space-tape.lib` already
submits thumbnail work from (`SpaceTapeSubmod.Update(dt)` → `SubpartThumbnailGenerator.Update()`).

### 1.3 The registries

`<decomp>/KSA/ModLibrary.cs`. Note the visibility split — this dictates what needs reflection:

| Member | Visibility | Use |
|---|---|---|
| `Loaders` (`List<ILoader>`), `Binders` (`List<IBinder>`) | **public static** | append + run incrementally |
| `RegisterLoader`, `RegisterBinder`, all `Register(...)` overloads, `Get<T>`, `TryGet<T>`, `Has<T>`, `Find(string)` | **public static** | direct calls |
| `Bind(Renderer)`, `AttachGameData()` | **public static** | reference implementations — do **not** call them (they operate on the whole list) |
| `LocalModsFolderPath`, `LocalManifestPath`, `Manifest` | **public static** | mod folder + manifest |
| `AllParts`, `AllMeshes`, `AllFiles`, `AllMaterials`, `AllPartGameDataReferences`, `AllEditorTagDefinitions`, `Lookup` | **internal static readonly** | **reflection required** — but the *type* `SerializedCollection<T>` is public, so cast and use its public API |

`SerializedCollection<T>` (`<decomp>/KSA/SerializedCollection.cs`) has `Register`, `Find(KeyHash)`,
`GetList()` — and **no remove**. `GetList()` returns the live backing `List<T>` (not a copy), so
removing from the list works directly; the parallel `ConcurrentDictionary<KeyHash,T> _collection`
private field needs reflection.

---

## 2. Hard constraints (the six landmines)

### C1 — `DeviceMeshInterleaved.Shared` is one fixed-size buffer pair

`<decomp>/KSA/DeviceMeshInterleaved.cs`. Every part mesh's vertices and indices live in **one**
`Shared.VertexAllocation` / `Shared.IndexAllocation` buffer pair, sized at
`Shared.RunningVertexBufferSize` / `RunningIndexBufferSize` when `Shared.Build()` first runs.
`Build()` is CAS-guarded and runs exactly once. Each `new DeviceMeshInterleaved(...)` atomically bumps
those counters and records its own offset.

Creating a mesh **after** `Build()` therefore yields an offset past the end of the allocation, and
`Bind()` will `vkCmdCopyBuffer` out of range → validation error / GPU fault / silent corruption.

**Mitigation:** reserve headroom in `OnFullyLoaded()` before `Build()` runs (§6), plus a hard
pre-bind budget check that aborts cleanly.
**Do not** use `Shared.Rebuild()` to grow — it copies `VertexAllocation.BufferSize` bytes *from the old
buffer*, which over-reads if the new buffer is larger.

### C2 — `Loading.Current` is never nulled after boot

`<decomp>/KSA/Loading.cs`: `Current` is set in the ctor and has a `private set`; nothing clears it.
`FileReference.Load()` (`<decomp>/KSA/FileReference.cs:73`) calls `Loading.Task(LocalPath)` →
`Loading.PushTask` → `Current.OnFrame()`, which runs a **complete ImGui frame + swapchain acquire +
submit**. Calling that from inside the game's own ImGui frame will corrupt the frame.

`Loading.OnFrame()` early-returns when `!Program.IsMainThread()`.

**Mitigation:** run all `ILoader.Load()` calls on a **background thread**. This is exactly what the game
does (`ModLibrary.LoadAll` uses `Parallel.ForEachAsync`). Do **not** try to null `Loading.Current` —
`LoadTask`'s ctor throws `InvalidOperationException` when it is null, and that throw escapes
`FileReference.Load()`'s try block.

### C3 — `vkQueueSubmit` is externally synchronised

`IBinder.Bind` creates a `StagingPool`, whose `Dispose()` submits and blocks. Submitting from a worker
thread while the main thread submits the frame is a race.

**Mitigation:** loaders off-thread (file I/O + CPU decode only), **binders on the game thread**, inside
`ISubmod.Update(dt)`.

### C4 — `ModLibrary.AttachGameData()` is not idempotent

`PartTemplate.ApplyGameData` (`<decomp>/KSA/PartTemplate.cs:231`) is **additive** (`AddRange` on
connectors, masses, rockets, components…). Re-running the stock `AttachGameData()` would double every
already-attached part.

**Mitigation:** implement an incremental attach over only the newly registered `PartGameDataReference`s
(§9.5).

### C5 — duplicate ids are silently dropped

`SerializedCollection.Register` returns `false` on a duplicate `KeyHash`; every caller treats that as
"this is a reference to the existing one". For `FileReference` it means `Load()` returns without
reading the file — so a **changed GLB at the same path will not reload**.

**Mitigation:** validation rejects duplicate ids for a fresh load (§8); full reload purges first (§11).

### C6 — `ThumbnailRenderResources.AddDraw` dereferences material channels with no null check

`<decomp>/KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs:138-140` reads
`Material.DiffuseReference.BindlessHandle`, `Material.NormalReference.BindlessHandle`,
`Material.PBRMap.BindlessHandle` unconditionally. `PartModel.WriteInstancesToGpu`
(`<decomp>/KSA/PartModel.cs:414-416`) does the same. A `<PbrMaterial>` missing any of
`<Diffuse>`/`<Normal>`/`<AoRoughMetal>`, or a `<PartModel>` with no `<Material>`, **crashes the game**.

**Mitigation:** validation makes all three channels mandatory (§8, rule V9).

### Secondary notes (not blockers, but must be honoured)

* **Bindless texture pool is 1024 slots.** `Program.cs:850` `new BindlessTextureLibrary(_renderer, 1024, …)`.
  It uses `UpdateAfterBind` + `PartiallyBound` + a free list, so runtime add/remove is *designed for*.
  `FreeListIndexPool` is constructed with `allowResize: false` — check headroom before loading and
  surface `BindlessTextures.TextureCount` / `MaxTextures` in the UI.
* **`TextureReference.Dispose(Device)` frees `BindlessHandle` unconditionally.** Handle `0` is the
  library's *empty* texture (allocated in the ctor). Never dispose a `TextureReference` whose
  `BindlessHandle <= 0`.
* **`VehicleEditor` locks its editor-tag list after boot.** `MarkEditorTagDefinitionsLoaded()` sets
  `_editorTagDefinitionsLoaded = true`; after that `RegisterTag` logs a warning and does **not** add to
  `_editorTags`, so a new tag gets no category button. This is why unknown tags are rejected (§8, V7).
* **`VehicleEditor.PartWindow._diameterCache`** is built lazily/on toggle. Call the public
  `VehicleEditor.ResetPartDiameterCache()` after a load so diameter filters include the new parts.

---

## 3. Architecture

```
parts-now/                          standalone StarMap mod (F-key window)
  Mod.cs                            StarMap lifecycle -> PartsNowSubmod
  Patcher.cs                        Harmony instance + HotkeyGuard (mandatory per CLAUDE.md)
  mod.toml
  README.md

parts-now.lib/                      all logic; also the ISubmod used by unscience
  PartsNowSubmod.cs                 ISubmod: Update(dt) drives the job state machine; RenderContent()
  Ui/
    PastePanel.cs                   mod-id form + XML tabs + Install button
    ModFolderPanel.cs               folder scan, filter, select, Load / Reload / Unload
    StatusPanel.cs                  progress, budget gauges, per-part results, error log
  Runtime/
    GameRegistry.cs                 reflection accessors for ModLibrary internals + SerializedCollection removal
    MeshBudget.cs                   shared vertex/index buffer headroom reservation + accounting
    BundleValidator.cs              all validation rules (§8); pure, unit-testable
    RuntimeModLoader.cs             the load state machine (§9)
    RuntimeModRegistry.cs           LoadedModRecord tracking (what we registered, for purge)
    RuntimeModUnloader.cs           purge / rollback (§11)
    PartThumbnailGenerator.cs       incremental thumbnail rendering against Program.ThumbnailViewport (§10)
    EditorRefresh.cs                post-load editor refresh (§10.4)
  Io/
    ModFolderScanner.cs             enumerate mods dir, read mod.toml, classify
    ModFolderWriter.cs              create <mods>/<id>/ + mod.toml + XML files
    ModIdValidator.cs               kebab-case + collision rules
  README.md
```

**Threading rule (repeat it in every file header):**
*Everything runs on the game thread except `RuntimeModLoader`'s loader step, which runs on a
`Task.Run` worker. The worker touches only `ILoader.Load()`. Completion is polled from `Update(dt)`.*
Do **not** use `MeowSci.KsaAbstractions.GameThread` — its queue is only drained when
`unladen-swallow.lib` is present, and `parts-now` must work standalone.

---

## 4. Phase 0 — Scaffolding

### T0.1 Generate the project pair

```
bun run mkmod.ts parts-now PartsNow
```
Produces `parts-now/` and `parts-now.lib/` from the `fixme-mod-name` template with placeholders
replaced. Verify: `parts-now/mod.toml` has `EntryAssembly = "MeowSci.PartsNow"`, and
`parts-now/parts-now.csproj` has `<DistDir>$(SelectedDistModDir)parts-now\</DistDir>`.

### T0.2 Add the required game references to `parts-now.lib.csproj`

The generated `.lib` csproj has no references. Copy the whole `<ItemGroup>` of `<Reference>` elements
from `parts-now/parts-now.csproj` (Brutal.Core.Common, Brutal.Core.Numerics, Brutal.ImGui,
Brutal.ImGui.Abstractions, Brutal.Core.Strings, KSA) and add these additional ones — all are needed for
the Vulkan/thumbnail work and are already used by `space-tape.lib` (copy that csproj's list verbatim if
in doubt):

* `Brutal.Vulkan`, `Brutal.Vulkan.Abstractions`, `Brutal.Core.Collections`, `Brutal.Core.Memory`

Also add `<ProjectReference Include="..\ksa-abstractions.lib\ksa-abstractions.lib.csproj" />`
(for `KsaPaths` and `SubmodUI`).

> **Verification:** `dotnet build parts-now.lib/parts-now.lib.csproj` must succeed before any logic is
> written. If a `Brutal.*` type is unresolved, find the owning DLL by grepping
> `<decomp>` for the namespace and add that `<Reference>`.

### T0.3 Register in the solution + supermod

* `ksa-mod-experiments.slnx`: add
  `<Project Path="parts-now/parts-now.csproj" />` and `<Project Path="parts-now.lib/parts-now.lib.csproj" />`.
* `unscience/unscience.csproj`: add `<ProjectReference Include="..\parts-now.lib\parts-now.lib.csproj" />`.
* `unscience/Mod.cs`: `using MeowSci.PartsNowLib;` and `_submods.Add(new PartsNowSubmod());`
  (keep the list alphabetical — insert between `KiwisMarblesSubmod` and `RedAlertSubmod`).

### T0.4 Hotkey guard (mandatory)

`parts-now/Patcher.cs` — already correct from the template. Confirm it calls
`HotkeyGuard.Patch(_harmony)` in `Patch()` and `HotkeyGuard.Unpatch(_harmony)` in `Unload()`.
`parts-now` has heavy text input, so this is not optional.

**Exit criteria:** solution builds green; the mod loads and shows an empty window on its hotkey.

---

## 5. Phase 1 — `GameRegistry` (reflection layer)

All reflection lives in exactly one file: `parts-now.lib/Runtime/GameRegistry.cs`. Nothing else in the
mod may call `GetField`/`GetMethod`. Every accessor is resolved **once** in a static ctor and throws a
descriptive exception if a member is missing (that is the game-update tripwire).

### T1.1 Collection accessors

```csharp
private static SerializedCollection<T> Collection<T>(string field)
    where T : ILibraryData, IListable
{
    var f = typeof(ModLibrary).GetField(field,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                   $"parts-now: ModLibrary.{field} not found — KSA internals changed.");
    return (SerializedCollection<T>)f.GetValue(null)!;
}

public static SerializedCollection<PartTemplate>            AllParts        { get; }
public static SerializedCollection<MeshReference>           AllMeshes       { get; }
public static SerializedCollection<FileReference>           AllFiles        { get; }
public static SerializedCollection<PbrMaterialReference>    AllMaterials    { get; }
public static SerializedCollection<PartGameDataReference>   AllPartGameData { get; }
public static SerializedCollection<EditorTagDefinition>     AllEditorTagDefs{ get; }
```

Field names, exactly: `"AllParts"`, `"AllMeshes"`, `"AllFiles"`, `"AllMaterials"`,
`"AllPartGameDataReferences"`, `"AllEditorTagDefinitions"`.
(Working precedent: `space-tape.lib/Thumbnails/SubpartThumbnailGenerator.cs:395-412`.)

### T1.2 Removal from a `SerializedCollection<T>`

```csharp
public static bool Unregister<T>(SerializedCollection<T> collection, T item)
    where T : ILibraryData, IListable
{
    // _all is the live list returned by GetList(); remove directly.
    collection.GetList().Remove(item);

    // _collection is a private ConcurrentDictionary<KeyHash, T>; both type args are public.
    var f = typeof(SerializedCollection<T>).GetField("_collection",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("parts-now: SerializedCollection._collection not found.");
    var dict = (ConcurrentDictionary<KeyHash, T>)f.GetValue(collection)!;
    return dict.TryRemove(item.Hash, out _);
}
```

`ILibraryData` exposes `Hash`. If it does not, take the hash from the concrete type (`SerializedId.Hash`)
or recompute with `KeyHash.Make(item.Id.AsSpan())` — both are public.

> Only ever call `Unregister` from the game thread while no load is in flight. `SerializedCollection`
> has a private `Lock` we deliberately do not take; single-threaded access makes that safe.

### T1.3 Editor-tag introspection (read only)

For validation rule V7 we need the set of tags KSA knows about. Read
`VehicleEditor._editorTagLookup` (`private static Dictionary<uint, string>`) via reflection and expose
`IReadOnlyCollection<string> KnownEditorTags`. Cross-check against
`AllEditorTagDefs.GetList().Select(d => d.Id)` and union the two.
Field name exactly: `"_editorTagLookup"` (`<decomp>/KSA/VehicleEditor.cs:399`).

### T1.4 Self-test

Add `GameRegistry.SelfTest()` returning a `List<string>` of problems, called once in
`PartsNowSubmod.Initialize()`. Log each problem with `Console.WriteLine` and disable the mod's Load
buttons if any accessor failed. This turns a future KSA rename into a clear message instead of a crash.

---

## 6. Phase 2 — `MeshBudget` (shared buffer headroom)

This is the only part of the plan that must run at a specific lifecycle moment. Get it wrong and
runtime mesh loading corrupts GPU memory.

### T2.1 Reserve headroom in `OnFullyLoaded()`

`parts-now/Mod.cs` `[StarMapAllModsLoaded]` **and** `PartsNowSubmod.Initialize()` (which unscience
calls from its own `OnFullyLoaded`) must both route to `MeshBudget.Reserve()`, which is idempotent.

```csharp
// MeshBudget.Reserve() — MUST be called from [StarMapAllModsLoaded].
// StarMap fires that as a Harmony postfix on ModLibrary.LoadAll(), which is Program.cs:956 —
// BEFORE ModLibrary.Bind() at :985, which is where DeviceMeshInterleaved.Shared.Build() allocates.
if (_reserved) return;
_watermarkVertexBytes = DeviceMeshInterleaved.Shared.RunningVertexBufferSize;
_watermarkIndexBytes  = DeviceMeshInterleaved.Shared.RunningIndexBufferSize;
DeviceMeshInterleaved.Shared.RunningVertexBufferSize = _watermarkVertexBytes + VertexHeadroomBytes;
DeviceMeshInterleaved.Shared.RunningIndexBufferSize  = _watermarkIndexBytes  + IndexHeadroomBytes;
_reserved = true;
_armed    = true;
Console.WriteLine($"parts-now: reserved mesh headroom {VertexHeadroomBytes/1024/1024} MiB vtx / " +
                  $"{IndexHeadroomBytes/1024/1024} MiB idx (startup watermark " +
                  $"{_watermarkVertexBytes/1024/1024} / {_watermarkIndexBytes/1024/1024} MiB)");
```

Defaults: `VertexHeadroomBytes = 48 * 1024 * 1024`, `IndexHeadroomBytes = 12 * 1024 * 1024`.
Persist overrides in the mod's config TOML under
`Path.Combine(ModLibrary.LocalModsFolderPath, "parts-now", "parts-now.toml")`; they take effect on the
next launch and the UI must say so.

### T2.2 Restore the watermark on the first frame

```csharp
// MeshBudget.OnFirstFrame() — call unconditionally from the FIRST PartsNowSubmod.Update(dt).
// By the first real UI frame, ModLibrary.Bind() (Program.cs:985) has long since run and
// DeviceMeshInterleaved.Shared.Build() has allocated the enlarged buffers.
// The loading screen (KSA/Loading.cs) does NOT go through Program.OnDrawUiFrame, so the
// StarMap hooks never fire during boot — the first Update(dt) is guaranteed to be post-Bind.
if (!_armed) return;
DeviceMeshInterleaved.Shared.RunningVertexBufferSize = _watermarkVertexBytes;
DeviceMeshInterleaved.Shared.RunningIndexBufferSize  = _watermarkIndexBytes;
_armed = false;
```

Net effect: the buffers are allocated at `watermark + headroom` bytes, while the allocation cursor is
back at `watermark`. Runtime meshes now consume the headroom at correct offsets.

Optionally assert `DeviceMeshInterleaved.Shared.IsBuilt` first — but if that field's decompiled type
(`bool`, manipulated via `Interlocked` in the decompile) does not compile as written, drop the assert
rather than fighting it.

### T2.3 Budget query + guard

```csharp
public static uint AllocatedVertexBytes => (uint)(ulong)DeviceMeshInterleaved.Shared.VertexAllocation.BufferSize;
public static uint AllocatedIndexBytes  => (uint)(ulong)DeviceMeshInterleaved.Shared.IndexAllocation.BufferSize;
public static uint UsedVertexBytes      => DeviceMeshInterleaved.Shared.RunningVertexBufferSize;
public static uint UsedIndexBytes       => DeviceMeshInterleaved.Shared.RunningIndexBufferSize;
public static bool WithinBudget         => UsedVertexBytes <= AllocatedVertexBytes
                                        && UsedIndexBytes  <= AllocatedIndexBytes;
```

Read the allocated size from the buffer itself, not from `watermark + headroom` — it is authoritative.
Adjust the `ByteSize` → integer conversion to whatever the `ByteSize` API actually offers (there are
existing `(int)` casts on `ByteSize` in `<decomp>/KSA/PartModel.cs:402`); verify at compile time.

### T2.4 Overflow abort

`RuntimeModLoader` snapshots `UsedVertexBytes`/`UsedIndexBytes` before the loader step. After the
loader step, if `!WithinBudget`:

1. **Do not bind anything.** Nothing has been uploaded yet; the offending `DeviceMeshInterleaved`
   objects only hold bad offsets.
2. Purge everything this load registered (§11).
3. Restore the cursors to the snapshot values (they are plain `public static uint`).
4. Fail the job with: *"Mesh headroom exhausted (needed X MiB, Y MiB free). Increase
   `vertexHeadroomMiB` in parts-now.toml and restart the game."*

### T2.5 Leak accounting

A full reload orphans the previous mesh data inside the shared buffer — the allocator is a monotonic
bump pointer with no free. Track `LeakedVertexBytes`/`LeakedIndexBytes` per reload, show them in the UI,
and warn once they exceed 50 % of the headroom.

---

## 7. Phase 3 — Bundle parsing

### T3.1 Parse without touching disk

```csharp
// XmlHelper.Serializers is `public static Dictionary<Type, XmlSerializer>` and already contains
// a serializer for AssetBundle built with the XmlAttributeOverrides that map <PartModel>, <Tank>,
// <Collider>, <Light>, ... onto PartTemplate.Components / PartInstance.Components.
// Constructing our own `new XmlSerializer(typeof(AssetBundle))` would MISS those overrides
// and silently drop every Component. Always use the game's instance.
if (!XmlHelper.Serializers.TryGetValue(typeof(AssetBundle), out var serializer))
    throw new InvalidOperationException("parts-now: no AssetBundle serializer registered by KSA.");

using var reader = new StringReader(xmlText);
var bundle = serializer.Deserialize(reader) as AssetBundle;
```

Parsing is for **validation only** — it must not have side effects. Do **not** call
`bundle.OnDataLoad(mod)` during validation; that is what registers things.

### T3.2 Bundle model

```csharp
public sealed record ParsedBundle(
    string  SourceName,           // file name or "Assets"/"Part"/"GameData" tab label
    string  Xml,
    AssetBundle Bundle);
```

`AssetBundle.Assets` is `List<SerializedId>`; classify entries with pattern matching. Note the type
hierarchy traps:

* `SubPartTemplate : PartTemplate` (`IsSubPart = true`)
* `PartGameDataReference : PartTemplate` (`_isGameData = true`)
* `SubPartGameDataReference : PartGameDataReference`
* `MeshAtlasFileReference : FileReference`, `MeshFileReference : FileReference`,
  `TextureReference : FileReference`

So `is PartTemplate` matches **all four** part-ish types. Always test most-derived first:
`SubPartGameDataReference` → `PartGameDataReference` → `SubPartTemplate` → `PartTemplate`.

---

## 8. Phase 4 — `BundleValidator`

Pure functions over `ParsedBundle[]` plus a snapshot of the current registries. Returns
`List<ValidationIssue> { Severity(Error|Warning), Rule, Message, ElementId }`. **Any `Error` blocks
the load.** Every rule below is mandatory.

| # | Rule | Why |
|---|---|---|
| V1 | Root element must be `<Assets>` and deserialize without exception. | `XmlLoader` swallows parse errors and returns null; we must not. |
| V2 | Every `<Part>`, `<SubPart>`, `<PbrMaterial>`, `<MeshAtlas>`/`<MeshFile>`, `<Texture>` must have a non-empty `Id` (except file refs, which may omit it — they default to their path). | `SerializedId.OnDataLoad` sets `IsReferenceable = !string.IsNullOrEmpty(Id)`; unreferenceable parts are never registered. |
| V3 | No `Id` may collide with an already-registered id of the same kind (`AllParts`, `AllMaterials`, `AllMeshes`, `AllFiles`, `AllPartGameData`). **Exception:** ids owned by the mod currently being reloaded. | C5 — duplicates are silently dropped. |
| V4 | No duplicate ids *within* the submitted bundles. | Same. |
| V5 | Every `<SubPart InstanceOf="X">` inside a `<Part>` must resolve to either a SubPart declared in this bundle set or an existing `ModLibrary.AllParts` entry. | `PartInstance.GetTemplate()` → `ModLibrary.Get<PartTemplate>` throws `NullReferenceException` at spawn/thumbnail time. |
| V6 | Every `<Mesh Id="X"/>` must resolve to a mesh declared by a `<MeshAtlas>`/`<MeshFile>` in this bundle set (by GLB node name) or an existing `AllMeshes` entry. For a `<MeshAtlas>`, read the GLB's mesh node names to check — the atlas file must exist on disk at validation time. | Same failure mode as V5. |
| V7 | Every `<EditorTag Value="T"/>` must already exist in `GameRegistry.KnownEditorTags`. | New tags get no category button after boot (§2, secondary notes). **Error**, with the message listing the valid tags. |
| V8 | Reject any `<Substance>`, `<MixtureReaction>`, `<FixedReaction>`, `<ThermalReaction>`, `<GrainGeometry>`, `<Situation>`, `<EditorTagDef>` element. | Out of scope by decision 4; `SubstanceLibrary.LoadAll`/`GrainGeometryLibrary.LoadAll` use `Dictionary.Add` and are not re-runnable. |
| V9 | Every `<PbrMaterial>` referenced by a `<PartModel>`/`<PartModelDynamic>` must declare **all three** of `<Diffuse>`, `<Normal>`, `<AoRoughMetal>`. Every `<PartModel>` must have a `<Material>`. | **C6 — otherwise the game crashes** at thumbnail or first render. |
| V10 | Every `<Combustion>`/`<Reaction>` id must resolve via `SubstanceLibrary.TryGetReaction(KeyHash.Make(id))`; every `<GrainGeometry>` reference via `GrainGeometryLibrary.TryGet(...)`; every `<VolumetricExhaust Id>` and `<SoundEvent SoundId>` must exist. | These resolve lazily at part spawn and throw there — far from the load site. |
| V11 | Every `Path=` attribute must resolve to an existing file under the mod folder and must not escape it (`..`, absolute paths, rooted paths → Error). | Security + `FileReference.Load` failure is only logged, never surfaced. |
| V12 | Warn if any `<SubPart>` lacks a `<MeshView>`. | Editor picking degrades. |
| V13 | Warn if a top-level `<Part>` has no matching `<PartGameData>` (no mass → massless part). | Documented pitfall in the `ksa-add-part` skill. |
| V14 | For a **fresh** install, reject a `<PartGameData Id="X">` whose id already exists in `AllPartGameData`. | Its `OnDataLoad` would merge into the existing reference and our incremental attach would never see it. |
| V15 | Texture count check: `bundle texture count + BindlessTextures.TextureCount <= MaxTextures - 16`. | `FreeListIndexPool(maxTextures, allowResize: false)`. |

Render issues in the UI grouped by severity, each with the offending element id and the rule number.

---

## 9. Phase 5 — `RuntimeModLoader` (the load state machine)

One job at a time. `PartsNowSubmod.Update(dt)` calls `RuntimeModLoader.Step()` once per frame. Each
state does a bounded amount of work so the game never stalls for more than a frame or two, except where
noted.

```
Idle
 ├─ Validate            game thread, sync
 ├─ WriteModFolder      game thread, sync   (paste flow only)
 ├─ CreateMod           game thread, sync
 ├─ RegisterBundles     game thread, sync
 ├─ RunLoaders          BACKGROUND Task; polled
 ├─ CheckMeshBudget     game thread, sync
 ├─ Bind                game thread, N binders per frame
 ├─ AttachGameData      game thread, sync
 ├─ WarmModels          game thread, sync
 ├─ Thumbnails          game thread, N parts per frame
 ├─ RefreshEditor       game thread, sync
 └─ Done | Failed(rollback)
```

Every state transition appends to a `List<string> Log` shown in the UI and mirrored to
`Console.WriteLine` with a `parts-now: ` prefix.

### T9.1 `CreateMod`

```csharp
// Reuse the existing Mod object if KSA already loaded this mod id at boot (reload case),
// otherwise build one from the folder's mod.toml.
Mod mod = ModLibrary.Find(modId)
       ?? Mod.MakeUsing(modId, Path.Combine(modDir, "mod.toml"));
```

`Mod.MakeUsing` is `public static`; it Tomlet-parses `mod.toml`, sets `Id`, calls `OnDataLoad`, and
sets `DirectoryPath` (with separators corrected). `Mod.Preload` defaults to `false`, which is required —
`FileReference.OnDataLoad` only calls `RegisterLoader` when `!mod.Preload`.

**Do not** register the `Mod` into `ModLibrary.Lookup`. Nothing after boot iterates it, and staying out
of it avoids any chance of a double load.

### T9.2 `RegisterBundles`

```csharp
var loaderMark = ModLibrary.Loaders.Count;
var binderMark = ModLibrary.Binders.Count;
var partsMark  = GameRegistry.AllParts.GetList().Count;
var gdMark     = GameRegistry.AllPartGameData.GetList().Count;
var meshMark   = GameRegistry.AllMeshes.GetList().Count;
var fileMark   = GameRegistry.AllFiles.GetList().Count;
var matMark    = GameRegistry.AllMaterials.GetList().Count;
record.VertexBytesBefore = MeshBudget.UsedVertexBytes;
record.IndexBytesBefore  = MeshBudget.UsedIndexBytes;

foreach (var parsed in bundles)
    parsed.Bundle.OnDataLoad(mod);     // registers templates, queues loaders
```

`AssetBundle.OnDataLoad` (`<decomp>/KSA/AssetBundle.cs:74`) walks `Assets` and calls `OnDataLoad` on
each; because `mod.Preload == false` it takes the plain `asset.OnDataLoad(mod)` branch. This is cheap
and does **no** mesh/texture file I/O — `FileReference.OnDataLoad` only calls
`ModLibrary.RegisterLoader(this)`.

Immediately after, capture the delta ranges into `record` (§T9.8). These slices are what a rollback or
unload will remove.

> Marks must be taken as **counts**, and the deltas read as `list.GetRange(mark, list.Count - mark)`.
> Nothing else appends to these lists at runtime, but re-read the counts rather than assuming.

### T9.3 `RunLoaders` (background)

```csharp
var newLoaders = ModLibrary.Loaders.GetRange(loaderMark, ModLibrary.Loaders.Count - loaderMark);
_loaderTask = Task.Run(() =>
{
    // MUST be off the main thread: FileReference.Load() -> Loading.Task() -> Loading.PushTask()
    // -> Loading.Current.OnFrame(), which renders a whole ImGui frame. Loading.OnFrame()
    // early-returns when !Program.IsMainThread(), so a worker thread makes it a no-op.
    foreach (var loader in newLoaders)
        loader.Load();
});
```

`Step()` polls `_loaderTask.IsCompleted`; on faulted, fail the job. Serial (not `Parallel.ForEachAsync`)
is fine and keeps ordering deterministic; a handful of files is not worth the concurrency risk.

`FileReference.Load()` catches and **logs** its own exceptions rather than throwing, so a missing file
produces a silent partial load. After the task completes, verify each expected mesh/texture actually
registered (`GameRegistry.AllMeshes.Find(hash) != null`) and fail loudly if not. This is why V11 exists.

### T9.4 `Bind`

```csharp
// Game thread only — StagingPool.Dispose() submits to renderer.Graphics and blocks.
// The stock path (ModLibrary.Bind) binds EVERY binder; we must only bind the new ones.
var renderer = Program.GetRenderer();
foreach (var binder in newBindersBatch)          // e.g. 4 per frame
{
    using var pool = renderer.Allocator.CreateStagingPool(renderer.Graphics, 1);
    binder.Bind(renderer, pool);
}
```

That mirrors `ModLibrary.Bind`'s per-binder body (`<decomp>/KSA/ModLibrary.cs:1734-1739`) minus the
`Parallel.ForEachAsync`. Batch a few per frame to keep the hitch small; each
`StagingPool.Dispose()` waits on a fence.

`MeshReference.Bind` calls `DeviceMeshInterleaved.Bind()` → `Shared.Build()` (already built; the CAS
guard makes it a no-op) → copies into the headroom. `TextureReference.Bind` allocates a
`SimpleVkTexture` and calls `Program.Instance.BindlessTextures.AddTexture(...)`, which is
`UpdateAfterBind` and safe here.

### T9.5 `AttachGameData` (incremental — replaces `ModLibrary.AttachGameData`)

```csharp
var allParts = GameRegistry.AllParts.GetList();
var touched  = new HashSet<PartTemplate>();

// Only the game data registered by THIS load.
foreach (var gd in record.NewGameData)
{
    var target = GameRegistry.AllParts.Find(gd.Hash);   // matches by Id hash
    if (target == null) { Warn($"PartGameData '{gd.Id}' matches no Part"); continue; }
    target.ApplyGameData(gd);                            // public, additive
    touched.Add(target);
}

// ResolveConsumerFeedPoints() clears and rebuilds ConsumerFeeds, so it IS idempotent.
foreach (var t in record.NewParts.Concat(touched))
    if (!t.IsSubPart) t.ResolveConsumerFeedPoints();
```

`PartTemplate.ApplyGameData` also calls `ExpandSymmetryGroups()` internally, so symmetry is handled.
Do **not** call `ModLibrary.AttachGameData()` — C4.

### T9.6 `WarmModels`

Mirror `Program.cs:1208-1233` for the new templates only:

```csharp
foreach (var t in record.NewParts)
    foreach (var c in t.Components)
        switch (c)
        {
            case PartModelModule.Template pm:        PartModel.Get(pm); break;
            case PartModelGlassModule.Template pg:   PartModelGlass.Get(pg); break;
            case PartModelDynamicModule.Template pd: PartModelDynamic.Get(pd); break;
        }
```

`PartModel.Get` is lazily called by `PartModelModule.CreateComponents` at spawn time anyway, but
warming here surfaces an unresolvable `<Mesh Id>` as a catchable exception at load time rather than as
a crash the first time the player clicks the part. Record each failure against the owning part and mark
that part as degraded.

### T9.7 `Thumbnails`, `RefreshEditor` — see §10.

### T9.8 `LoadedModRecord`

```csharp
public sealed class LoadedModRecord
{
    public string ModId = "";
    public string ModDir = "";
    public Mod?   Mod;
    public bool   CreatedByPaste;

    public readonly List<PartTemplate>          NewParts     = new(); // includes SubPartTemplates
    public readonly List<PartGameDataReference> NewGameData  = new();
    public readonly List<MeshReference>         NewMeshes    = new();
    public readonly List<FileReference>         NewFiles     = new(); // atlases + textures
    public readonly List<PbrMaterialReference>  NewMaterials = new();
    public readonly List<ILoader>               NewLoaders   = new();
    public readonly List<IBinder>               NewBinders   = new();

    public uint VertexBytesBefore, IndexBytesBefore;
    public uint VertexBytesUsed  => MeshBudget.UsedVertexBytes - VertexBytesBefore; // snapshot at Done
    public readonly HashSet<string> PartIds          = new();  // every NewParts[i].Id — "is this ours?"
    public readonly HashSet<string> ModelTemplateIds = new();  // PartModel/Glass/Dynamic Template ids
    public DateTime LoadedAtUtc;
}
```

Persist nothing; this is session state. `RuntimeModRegistry` holds
`Dictionary<string, LoadedModRecord>` keyed by mod id.

---

## 10. Phase 6 — Thumbnails

### T10.1 Render against the dedicated thumbnail viewport

> **This is the approach. Implement exactly this — do not build an alternative path, and do not add a
> toggle between approaches.** If it misbehaves, debug it with §T10.2; do not swap strategies.

It is the union of two things the game already does at runtime: `ThumbnailDynamic.Render()`
(`<decomp>/KSA.Rendering.Thumbnails/ThumbnailDynamic.cs`, which runs every frame while the part browser
is open) and `ThumbnailCreator.PreparePartThumbnails()`'s framing.

`Program.ThumbnailViewport` (viewport index 1) is created at boot purely for thumbnails, is marked
`IsOffscreen = true` / `ShouldRenderGizmos = false` (`<decomp>/KSA/Program.cs:964-967`), and its camera
is never driven by the player. Consequences that make this the right choice:

* **No camera save/restore.** Nothing in the game reads this camera's transform between frames.
* **No viewport resize.** It was already created at `new int2(ThumbnailRenderer.SIZE)`.
* **No follow-alert spam.** `TimedAlert` is only raised from `Camera.SetFollow(..., alert: true)`; we
  never call `SetFollow`, and this camera has no follow target to begin with. (That bug —
  `plans/done/LOAD_SUBPARTS_LOGS_ANALYSIS.md` — is a property of hijacking the *live* camera, which we
  do not do.)
* **No `UpdateShaderData` / `UpdateRenderingResources` call**, so we never perturb the frame index or
  the `DeviceHostSharedMemoryDebug` flags. We write only this viewport's camera UBO slice, exactly as
  `ThumbnailDynamic` does.

Setup once per job:

```csharp
var renderer = Program.GetRenderer();
_thumbRenderer = new ThumbnailRenderer(renderer);           // owns pipeline; dispose at job end
_pool = renderer.Device.CreateCommandPool(new VkCommandPoolCreateInfo {
    QueueFamilyIndex = renderer.Graphics.Family,
    Flags = VkCommandPoolCreateFlags.TransientBit | VkCommandPoolCreateFlags.ResetCommandBufferBit
}, null);
_viewport = Program.ThumbnailViewport;                      // public static, viewport index 1
_camera   = _viewport.GetCamera();
_root     = new ThumbnailPart(_camera);
```

> `ThumbnailCreator.Initialize` uses `Graphics.Index` and `space-tape` uses `Graphics.Family`; both
> work in practice. Use `.Family` (semantically the queue-family index). If it does not compile, use
> `.Index`.

Per frame, before rendering a batch:

```csharp
_camera.Unfollow(changeControl: false);
_camera.LocalPosition = double3.Zero;
_camera.LocalRotation = doubleQuat.Identity;
_camera.LocalScale    = double3.One;
_camera.OnFrame(1.0 / 60.0);
ThumbnailDynamic.UpdateGlobalCameraData(_viewport, _camera);   // public static — writes the camera UBO
```

Per part (`N = 2` per frame; make it a const):

```csharp
ThumbnailCreator.ResetRootPart(_root);
ThumbnailCreator.AddPart(_root, template);                     // one ThumbnailPart per SubPartInstance
if (_root.Children is null or { Count: 0 }) { Skip(template, "no sub-parts"); return; }

template.Thumbnail?.Dispose();
template.Thumbnail = ThumbnailCreator.CreateThumbnailReference(renderer, "Thumbnail_" + template.Id);
ThumbnailCreator.MoveRootPart(_root, template.Thumbnail, _camera);   // honours <Thumbnail><ModelTransform>

var res = new ThumbnailRenderResources(renderer,
              _thumbRenderer.PerInstanceDataDescriptorSetLayout,
              _thumbRenderer.PerDrawDataDescriptorSetLayout,
              _thumbRenderer.Sampler,
              ThumbnailRenderer.SIZE);
ThumbnailCreator.CollectDraws(_root, res);
if ((int)res.DrawCommandVector.ElementCount == 0) { res.Dispose(); Skip(template, "no draws"); return; }

res.UpdateDescriptorSets();

var cb = renderer.Device.AllocateCommandBuffer(new VkCommandBufferAllocateInfo {
    CommandPool = _pool, Level = VkCommandBufferLevel.Primary });
cb.Begin(new VkCommandBufferBeginInfo { Flags = VkCommandBufferUsageFlags.OneTimeSubmitBit });
_thumbRenderer.RecordPartRender(cb, template.Thumbnail, res, _viewport, template.Id);
cb.End();

var fence = renderer.Device.CreateFence(new VkFenceCreateInfo(), null);
var cbRef = cb;
renderer.Graphics.Submit(default, default, new Span<CommandBuffer>(ref cbRef), default, fence);
renderer.Device.WaitForFence(fence, -1);
renderer.Device.DestroyFence(fence, null);
renderer.Device.FreeCommandBuffers(_pool, new ReadOnlySpan<CommandBuffer>(in cbRef));
res.Dispose();
```

**Invariant: never move this camera — move the root part.** `ThumbnailCreator.MoveRootPart` positions
and rotates the `ThumbnailPart` in front of a camera parked at origin/identity, and
`ThumbnailDynamic.Render` assumes the same. The per-batch block above only *re-asserts* origin/identity;
it must never set a non-zero camera transform.

**Sharing the viewport with `ThumbnailDynamic`.** The part browser's hover preview
(`VehicleEditor.DynamicThumbnail`) uses this same viewport and camera. There is no conflict:
our batch runs in `Update(dt)` (`Program.OnDrawUiFrame`, frame line 2163) and `ThumbnailDynamic.Render`
runs later in the same frame (`Editor.OnPreRender`, line 2288). Each writes the camera UBO immediately
before its own submit and each waits on its own fence, so neither observes the other's state. Keep it
that way — do not defer our submit to a later frame phase.

Only top-level parts get thumbnails (`!template.IsSubPart`), matching
`ThumbnailCreator.PreparePartThumbnails` (`<decomp>/KSA.Rendering/ThumbnailCreator.cs:79`).

Wrap each part in try/catch — an unresolvable `<Mesh Id>` throws `NullReferenceException` out of
`ModLibrary.Get<MeshReference>`. Record the failure and continue.

At job end: dispose `_root`, `_thumbRenderer`, destroy `_pool`.

### T10.2 Troubleshooting a bad render

Symptoms and where to look. Work through these in order; **none of them is a reason to change
approach.**

| Symptom | Cause to check |
|---|---|
| Image is uniformly transparent `(0,0,0,0)` | `res.DrawCommandVector.ElementCount == 0` — `CollectDraws` found no `PartModel`/`PartModelDynamic`, i.e. the part's SubPart templates carry no `<PartModel>` or the mesh id did not resolve. Log the count per part. |
| Part is off-centre, clipped, or a dot | `MoveRootPart` framing. It uses `camera.GetFieldOfView()` and `camera.NearPlane`, and honours `<Thumbnail><ModelTransform>` if the part declares one. Verify `ComputeBoundingSphereRadius` returns non-zero (it reads `MeshReference.Get().HostMesh`, which requires the loader step to have run). |
| Geometry is right, shading is wrong/black | The camera UBO write. Confirm `ThumbnailDynamic.UpdateGlobalCameraData(_viewport, _camera)` is called **after** `_camera.OnFrame(...)` and **before** the submit, and that `_viewport.Index` is 1 (`RecordPartRender` binds `GlobalShaderBindings.DynamicOffset(viewport.Index)`). |
| Textures are wrong or garbage | Bindless handles. `ThumbnailRenderResources.AddDraw` reads `Material.*.BindlessHandle`, which is only valid after that `TextureReference`'s `Bind()` ran. Thumbnails must never be generated before the `Bind` state completes. |
| Nothing renders and the log is silent | The fence wait. `WaitForFence(fence, -1)` must return `VkResult.Success`; check it and log otherwise. |

Add a debug-only readback (blit the image to a host-visible buffer, report the fraction of non-zero
texels) behind a UI toggle in the results panel — it turns all of the above into a one-click answer.

For reference while debugging, `space-tape.lib/Thumbnails/SubpartThumbnailGenerator.cs` performs the
same record/submit/fence sequence and is known to work on this game build. Read it for comparison, but
**do not add a project reference to `space-tape.lib`** and do not copy its camera-hijack setup —
`parts-now` deliberately owns a viewport that needs none of it.

### T10.3 Thumbnail size mismatch

`ThumbnailRenderer.SIZE` reads `GameSettings.Current.Graphics.PartThumbnailSize` live, while
`Program.ThumbnailViewport` was created at boot with the then-current value. If the player changed the
setting mid-session they differ. Both are square so the projection is still correct; just log a warning.
**Do not** mutate `GameSettings.Current.Graphics.PartThumbnailSize` (space-tape does, and must resize
the viewport to match — we avoid the whole issue).

### T10.4 `RefreshEditor`

```csharp
VehicleEditor.ResetPartDiameterCache();   // public static; rebuilds PartWindow._diameterCache
```

Nothing else is needed: `PartWindow.OnDrawUi` iterates `ModLibrary.AllParts.GetList()` fresh every
frame (`<decomp>/KSA/VehicleEditor.cs:265`), so new parts appear immediately under **All** and under
their (pre-existing, per V7) category.

---

## 11. Phase 7 — Unload / reload

### T11.1 Safety gate (all must pass, else refuse with a specific message)

1. No `Part` in any live vehicle uses one of `record.PartIds`:
   walk `Universe.CurrentSystem?.Vehicles.GetList()` → `vehicle.Parts.Parts` → `part.Template.Id`
   (and recurse `SubParts`; `MeowSci.KsaAbstractions.PartHelpers` has the recursion helper).
2. The vehicle editor, if open, contains none of them: `Program.Editor?.EditingSpace` parts and
   `UnattachedPartTrees`. Simplest sufficient rule: **require the editor to be closed or empty.**
3. No load job is in flight.

### T11.2 Purge order (strict)

```
1.  renderer.Device.WaitIdle()                      // no in-flight frame may reference what we free
2.  foreach PartTemplate t in record.NewParts:
       t.Dispose()                                  // disposes Thumbnail: RemoveTexture + image
       GameRegistry.Unregister(AllParts, t)
3.  foreach PartGameDataReference gd: Unregister(AllPartGameData, gd)
4.  Purge model instances whose Template.Id is in record.ModelTemplateIds
    (collected during RegisterBundles: every ModuleBase.TemplateDataBase in every
     record.NewParts[i].Components that is a PartModel/Glass/Dynamic Template — take its .Id):
       PartModel.Instances        / PartModel.InstancesRayTrace
       PartModelGlass.Instances   / PartModelGlass.InstancesRayTrace
       PartModelDynamic.Instances
       PartModelModule.Template.RayTracers            // public static List<Template>
    (all are public static Lists — RemoveAll(x => ids.Contains(x.Template.Id)),
     and for RayTracers: RemoveAll(t => ids.Contains(t.Id)))
5.  foreach TextureReference tex in record.NewFiles.OfType<TextureReference>():
       if (tex.BindlessHandle > 0) tex.Dispose(renderer.Device);   // NEVER dispose handle 0
       Unregister(AllFiles, tex)
6.  foreach MeshReference m in record.NewMeshes:
       m.Dispose()                                   // frees SimpleVkMesh device primitives
       Unregister(AllMeshes, m)
       // NOTE: the mesh's slice of the shared interleaved buffer is NOT reclaimed — it leaks
       // until restart. Add its VerticesSize/IndicesSize to MeshBudget.Leaked*.
7.  foreach FileReference f in record.NewFiles (atlases, mesh files): Unregister(AllFiles, f)
8.  foreach PbrMaterialReference mat: Unregister(AllMaterials, mat)
9.  ModLibrary.Loaders.RemoveAll(record.NewLoaders.Contains)
    ModLibrary.Binders.RemoveAll(record.NewBinders.Contains)
10. VehicleEditor.ResetPartDiameterCache()
11. RuntimeModRegistry.Remove(modId)
```

`PartTemplate.Dispose()` only disposes the thumbnail (`<decomp>/KSA/PartTemplate.cs:226`).
`ThumbnailReference.Dispose()` calls `ImGuiBackend.Vulkan.RemoveTexture` then disposes the image view
and image — hence the `WaitIdle` at step 1 and the requirement that the part browser is not mid-draw
(it isn't; we run in `Update`, before `ImGui.Render`).

### T11.3 Reload = purge + load

`Reload(modId)` = `Unload(modId)` then `Load(modDir)`. Because the purge removes every id from every
registry, the fresh load sees no duplicates and `FileReference.Load()` re-reads the changed GLB/KTX2
(C5 resolved). Show the leak counter delta in the result.

### T11.4 Rollback

A failure in any state runs the same purge over whatever `record` has accumulated so far, plus the
`MeshBudget` cursor restore from T2.4. `record` must be populated **incrementally** as each state
completes, never only at the end.

---

## 12. Phase 8 — Mod folder I/O

### T12.1 `ModIdValidator`

Rules (all Errors):

* Matches `^[a-z0-9]+(?:-[a-z0-9]+)*$` (same regex as `mkmod.ts`), length 3–48.
* `Directory.Exists(Path.Combine(ModLibrary.LocalModsFolderPath, id))` must be **false**.
* `Directory.Exists(Path.Combine("Content", id))` must be **false** (core mod collision).
* `ModLibrary.Find(id) == null`.
* `ModLibrary.Manifest.Mods.All(m => m.Id != id)`.
* Not a reserved name: `Core`, `Sample`, `parts-now`, `unscience`.

Show the resolved absolute target path in the form so the user can see exactly where it will be written.

### T12.2 Mods directory discovery

```csharp
// ALWAYS use the game's own discovered path. Never hardcode.
// ModLibrary.LocalModsFolderPath == Path.Combine(Constants.DocumentsFolderPath, "mods")
//   where Constants.DocumentsFolderPath ==
//     Path.Combine(Environment.GetFolderPath(SpecialFolder.Personal), "My Games", "Kitten Space Agency")
string modsDir = ModLibrary.LocalModsFolderPath;
```

Sanity-check it against `MeowSci.KsaAbstractions.KsaPaths.UserDataDir + "\\mods"` and log a warning if
they differ (they should not), but **use KSA's value**.

### T12.3 `ModFolderWriter`

Given `modId`, `displayName`, `author`, `version`, and up to three XML documents (Assets / Part /
GameData — flexo's export tabs), write:

```
<mods>/<modId>/
    mod.toml
    <modId>-assets.xml      (only if the Assets tab was non-empty)
    <modId>-part.xml
    <modId>-gamedata.xml
```

`mod.toml` (Tomlet reads `Mod`'s `[TomlField]`s; `assets` is the array KSA iterates in
`Mod.LoadAssetBundles`):

```toml
name = "<displayName>"
description = "Created in-game by parts-now"
version = "<version>"
author = "<author>"

assets = [
    "<modId>-assets.xml",
    "<modId>-part.xml",
    "<modId>-gamedata.xml",
]
```

Order matters only for readability — `PartGameData` for a not-yet-loaded `Part` is held and merged
later within a single load pass (and our incremental attach runs after all bundles are registered).

Write with `Encoding.UTF8` **without BOM** and `\n` line endings. Write to `*.tmp` then
`File.Move(..., overwrite: false)` so a partial write never leaves a half-valid mod folder.

`space-tape.lib/PartModWriter.cs` already does a version of this (creates the dir, maintains the
`mod.toml` `assets` list) — read it for the TOML-rewrite details, but **implement fresh** in
`parts-now.lib` rather than referencing it.

### T12.4 Manifest entry

So the mod also loads normally on the next launch (critical — a vehicle saved with these parts will not
resolve otherwise):

```csharp
if (ModLibrary.Manifest.Mods.All(m => m.Id != modId))
{
    ModLibrary.Manifest.Mods.Add(new ModEntry { Id = modId, Enabled = true, New = false });
    ModLibrary.Manifest.Save();   // writes ModLibrary.LocalManifestPath
}
```

Do **not** use `new ModEntry(id, count)` — that ctor sets `Enabled = false, New = true`, which triggers
the `ConfirmMods` popup at next boot (`Program.cs:904`).

### T12.5 `ModFolderScanner`

Enumerate `Directory.GetDirectories(ModLibrary.LocalModsFolderPath)`, keep those containing `mod.toml`,
and for each report:

* id (folder name), display name, version, author (Tomlet-parsed)
* `assets[]` entries and whether each file exists
* **Kind:** `Content` (has a non-empty `assets` array) / `StarMap` (has `[StarMap] EntryAssembly`) /
  `Both` / `Empty`
* **State:** `LoadedAtBoot` (`ModLibrary.Find(id) != null` and not in our registry) /
  `LoadedByPartsNow` / `NotLoaded`

Only `Content`/`Both` mods with at least one existing asset file are loadable. `StarMap`-only folders
must be listed but disabled with the reason shown. Never offer to load or reload a `LoadedAtBoot` mod —
unloading it would purge parts we did not register and cannot safely account for; grey it out with
*"loaded at startup — restart the game to reload"*.

---

## 13. Phase 9 — UI

Single `RenderContent()` (no `Begin`/`End`) plus optional floating windows via
`RenderFloatingWindows()`. Follow the `imgui-design` skill and the layout conventions in
`space-tape.lib/PartEditorUi.cs`.

### T13.1 Header / status strip

* Mesh budget: two `ImGui.ProgressBar`s — vertex and index — showing `Used / Allocated` with the
  leaked portion called out.
* Bindless textures: `TextureCount / MaxTextures`.
* `GameRegistry.SelfTest()` result — a red banner if any accessor failed, with Load disabled.
* Current job state + progress + a Cancel button (only honoured between states, never mid-Vulkan).

### T13.2 "Paste XML" panel

Form fields (all `ImInputString`, see `garrys-torch.lib/GarrysTorchSubmod.cs` for the idiom):

| Field | Default | Validation |
|---|---|---|
| Mod Id | empty | §T12.1, live-validated, red text + reason under the field |
| Display Name | mirrors Mod Id | non-empty |
| Author | `"parts-now"` | non-empty |
| Version | `"1.0.0"` | non-empty |

Three tabbed XML inputs matching flexo's export tabs — **Assets**, **Part**, **GameData**. Each has:

* `ImGui.InputTextMultiline` bound to an `ImInputString` (capacity **262144**; XML gets long)
* a **Paste from clipboard** button — `ImGui.GetClipboardText()` returns `ImString`; this is the
  primary input path, typing into the box is the fallback
* a **Clear** button and a character counter

Buttons: **Validate** (runs §8, shows the issue list) and **Install & Load** (disabled until mod id is
valid and validation is clean). Installing runs: write folder → manifest entry → full load pipeline.

After success, show the created path with a **Copy path** button, and the list of parts now available.

### T13.3 "Mod folder" panel

* Path label = `ModLibrary.LocalModsFolderPath` (+ **Copy path**)
* **Rescan** button
* `ImInputString` filter + a table of scanned mods (id, name, kind, state, asset count)
* Selection detail: asset file list with existence ticks; buttons **Load** / **Reload** / **Unload**
  enabled per §T12.5 state rules and §T11.1 safety gate — with the blocking reason shown as a tooltip
  when disabled.
* A **destructive-action confirm** modal for Reload and Unload naming the mod id and part count.

### T13.4 Results panel

Per load: table of Part id → thumbnail (`ThumbnailReference.GetOrCreateImGuiTexture(Program.LinearClampedSampler)`,
drawn at 64×64 exactly as `VehicleEditor.PartWindow.DrawPartImageButton` does) → status
(OK / degraded + reason). Plus the scrollable log with a **Copy log** button.

### T13.5 Standalone window

`parts-now/Mod.cs` renders `PartsNowSubmod.RenderContent()` inside its own `ImGui.Begin`, toggled by a
hotkey. **Check the repo for a free key before choosing** — F11 and F9 are already taken by several
mods. Make it configurable in `parts-now.toml`.

---

## 14. Phase 10 — Documentation (mandatory, same change)

Per `CLAUDE.md` these are not optional:

1. **`REPOSITORY_INDEX.md`** — add a `### [parts-now](parts-now) / [parts-now.lib](parts-now.lib)`
   entry. Place it next to `space-tape` and `flexo` under the part-authoring group. Summarise:
   runtime Part/SubPart XML loading, mod-folder install/load/reload, incremental thumbnail generation,
   mesh-budget headroom.
2. **`parts-now/README.md`** and **`parts-now.lib/README.md`** — features, usage, the mod-id rules, the
   reload safety gate, the headroom setting, and the known limitations from §16.
3. **`scope/part-editor-and-robotics.md`** — add a `## parts-now` section in the same shape as the
   existing `space-tape` / `flexo` sections, listing every game touchpoint: `ModLibrary` internals
   accessed by reflection (with exact field-name strings), `DeviceMeshInterleaved.Shared`
   (`RunningVertexBufferSize` / `RunningIndexBufferSize` / `VertexAllocation` / `IndexAllocation` /
   `IsBuilt`), `XmlHelper.Serializers`, `AssetBundle.OnDataLoad`, `Mod.MakeUsing`,
   `PartTemplate.ApplyGameData` / `ResolveConsumerFeedPoints` / `Dispose`, `ThumbnailCreator.*`,
   `ThumbnailRenderer`, `ThumbnailRenderResources`, `ThumbnailPart`,
   `ThumbnailDynamic.UpdateGlobalCameraData`, `Program.ThumbnailViewport`,
   `VehicleEditor.ResetPartDiameterCache` / `_editorTagLookup`, `PartModel(.Glass/.Dynamic).Instances`,
   `PartModelModule.Template.RayTracers`, `BindlessTextureLibrary`, `ModLibrary.Manifest` / `ModEntry`.
4. **`scope/game-integration-surface.md`** — add every string-reflection entry above to the
   *String-based reflection watchlist* (these fail **silently at runtime**, which is exactly what that
   list exists for), and register the **`[StarMapAllModsLoaded]` fires before `ModLibrary.Bind`**
   ordering as a standing invariant to re-verify each game update.
5. **`scope/FULL_SCOPE.md`** — add `parts-now` to the ToC row for `part-editor-and-robotics.md` and to
   the status summary.
6. Move this plan to `plans/done/` when complete.

---

## 15. Test matrix

Run every row in-game; there are no automated tests for the Vulkan paths.

| # | Scenario | Expected |
|---|---|---|
| T1 | Paste a Part+GameData that reuses only Core SubParts; no `<Assets>` tab. | Folder created; part appears in browser with a correct thumbnail; spawns; attaches; mass/tooltip correct. |
| T2 | Same, but a duplicate Part id. | Blocked by V3 with the colliding id named. No folder written. |
| T3 | Paste with an `<EditorTag Value="Nonsense"/>`. | Blocked by V7, valid tags listed. |
| T4 | Paste a `<PbrMaterial>` missing `<AoRoughMetal>`. | Blocked by V9. **Confirm the game does not crash.** |
| T5 | Load a mod folder with its own GLB + 3 KTX2 atlases, not loaded at boot. | Meshes + textures bind; thumbnails render; part renders correctly in flight. |
| T6 | Reload T5's mod after editing the GLB (move a vertex visibly). | New geometry visible; thumbnail regenerated; leak counter increases; no validation error. |
| T7 | Reload while a vehicle in flight uses one of its parts. | Refused, naming the vehicle and part. Nothing purged. |
| T8 | Unload T5's mod, then re-load it. | Clean round trip; part count returns to the same value. |
| T9 | Load enough meshes to exceed the headroom. | Clean abort before any bind; cursors restored; actionable message; game still playable; other mods unaffected. |
| T10 | Restart the game after T1. | Part loads normally at boot from the written folder; a vehicle saved with it in T1 still loads. |
| T11 | Load a `StarMap`-only mod folder (e.g. `unscience`). | Listed, disabled, reason shown. |
| T12 | Load with the vehicle editor **open** on the part-browser tab. | New parts appear without closing the editor; no flicker; diameter filter includes them. |
| T13 | Load a mod whose XML references a missing texture file. | Blocked by V11 before anything registers. |
| T14 | Load 3 different mods in one session, unload the middle one. | Only the middle one's parts disappear; the other two are untouched. |
| T15 | Run with `--vulkan-validation` (or the game's validation flag) through T5 + T6. | **Zero** new validation errors — especially no buffer-overrun or descriptor-in-use errors. |
| T16 | Load a 20-part mod while following a vehicle in flight, camera moving. | Live camera never jumps or stutters; **no "Following &lt;x&gt;" alerts**; `Program.ControlledVehicle` unchanged; thumbnails all correct. Confirms §T10.1 leaves the player camera alone. |
| T17 | Load with the part browser open and hovering a part (`DynamicThumbnail` actively rendering). | Both renderers share `Program.ThumbnailViewport`'s camera; the hover preview must not corrupt or be corrupted. If it flickers, serialise: skip our batch on frames where `Program.Editor?.ShowPartWindow` is true, or run our batch first in `Update(dt)` (which is already before `Editor.OnPreRender`). |

---

## 16. Known limitations (put these in the README verbatim)

1. **Mesh memory is never reclaimed.** Each reload permanently consumes headroom in the shared
   interleaved buffer until the game restarts. Reload budget ≈ headroom ÷ mod mesh size.
2. **Headroom is fixed at launch.** Changing it in `parts-now.toml` requires a restart.
3. **New EditorTags, Substances, Reactions and GrainGeometry are rejected.** Parts must reference
   ids that already exist. (Implementable later: tags need reflection into `VehicleEditor._editorTags` /
   `_editorTagLookup` / `_editorTagDefinitionsLoaded` and the six whitelist/blacklist lists;
   substances/reactions/grains need reflection into `SubstanceLibrary`'s and
   `GrainGeometryLibrary`'s private dictionaries plus a call to each template's `Create()`.)
4. **Mods loaded at boot cannot be reloaded** — only mods `parts-now` itself loaded this session.
5. **Reload requires the parts to be unused** — no live vehicle, editor closed or empty.
6. **Raytracing (IVA) is untested.** With `GameSettings.Current.Graphics.IVARayTracing` on, the shared
   buffer is allocated through `RaytraceAllocator` and BLASes reference it. Headroom still works
   (the buffer is simply bigger), but verify T15 with raytracing on before claiming support; if it
   misbehaves, disable loading while raytracing is active.
7. **Saved vehicles depend on the mod folder staying put.** Deleting `<mods>/<id>/` will break any
   vehicle that used its parts.

---

## Appendix A — Game API surface used

### Public (direct calls)

| Member | File |
|---|---|
| `XmlHelper.Serializers` (`public static Dictionary<Type, XmlSerializer>`) | `KSA/XmlHelper.cs` |
| `AssetBundle` + `OnDataLoad(Mod)` | `KSA/AssetBundle.cs` |
| `Mod.MakeUsing(string, string)`, `Mod.LoadAssetBundles()`, `Mod.DirectoryPath`, `Mod.GetPath(string)` | `KSA/Mod.cs` |
| `ModLibrary.Loaders`, `Binders`, `RegisterLoader`, `RegisterBinder`, `Register(...)`, `Get<T>`, `TryGet<T>`, `Find(string)`, `LocalModsFolderPath`, `LocalManifestPath`, `Manifest` | `KSA/ModLibrary.cs` |
| `ModManifest.Save()`, `ModEntry` | `KSA/ModManifest.cs`, `KSA/ModEntry.cs` |
| `SerializedCollection<T>.Register/Find/GetList` | `KSA/SerializedCollection.cs` |
| `PartTemplate.ApplyGameData`, `ResolveConsumerFeedPoints`, `Dispose`, `Thumbnail`, `IsSubPart`, `IsHidden`, `Components`, `SubPartInstances`, `EditorTags` | `KSA/PartTemplate.cs` |
| `PartModel.Get/Instances/InstancesRayTrace`; `PartModelGlass.*`; `PartModelDynamic.*`; `PartModelModule.Template.RayTracers` | `KSA/PartModel*.cs` |
| `ThumbnailCreator.Viewport/BaseRotation/ViewRotation/CreateThumbnailReference/AddPart/MoveRootPart/CollectDraws/ResetRootPart` | `KSA.Rendering/ThumbnailCreator.cs` |
| `ThumbnailRenderer` (`.SIZE`, `.ColorFormat`, ctor, `RecordPartRender`, layouts, `Sampler`) | `KSA.Rendering.Thumbnails/ThumbnailRenderer.cs` |
| `ThumbnailRenderResources` (ctor, `UpdateDescriptorSets`, `AddDraw`, `DrawCommandVector`, `Dispose`) | `KSA.Rendering.Thumbnails/ThumbnailRenderResources.cs` |
| `ThumbnailPart` (ctors, `AddChild`, `ComputeBoundingSphereRadius`, `Dispose`) | `KSA.Rendering.Thumbnails/ThumbnailPart.cs` |
| `ThumbnailReference` (`CreateImageView`, `GetOrCreateImGuiTexture`, `Dispose`) | `KSA.Rendering.Thumbnails/ThumbnailReference.cs` |
| `ThumbnailDynamic.UpdateGlobalCameraData(Viewport, Camera)` | `KSA.Rendering.Thumbnails/ThumbnailDynamic.cs` |
| `DeviceMeshInterleaved.Shared.{RunningVertexBufferSize, RunningIndexBufferSize, VertexAllocation, IndexAllocation, IsBuilt}` | `KSA/DeviceMeshInterleaved.cs` |
| `Program.{Instance, GetRenderer, ThumbnailViewport, MainViewport, LinearClampedSampler, Editor, IsMainThread, BindlessTextures}` | `KSA/Program.cs` |
| `VehicleEditor.ResetPartDiameterCache()` | `KSA/VehicleEditor.cs:6187` |
| `SubstanceLibrary.TryGetReaction`, `GrainGeometryLibrary.TryGet` (validation V10) | `KSA/SubstanceLibrary.cs`, `KSA/GrainGeometryLibrary.cs` |
| `KeyHash.Make(ReadOnlySpan<char>)` | `KSA/KeyHash.cs` |

### Reflection (all in `GameRegistry.cs`)

| Target | Kind | Purpose |
|---|---|---|
| `ModLibrary.AllParts` | `internal static readonly SerializedCollection<PartTemplate>` | enumerate/unregister parts |
| `ModLibrary.AllMeshes` | `internal static readonly SerializedCollection<MeshReference>` | unregister meshes |
| `ModLibrary.AllFiles` | `internal static readonly SerializedCollection<FileReference>` | unregister atlases/textures |
| `ModLibrary.AllMaterials` | `internal static readonly SerializedCollection<PbrMaterialReference>` | unregister materials |
| `ModLibrary.AllPartGameDataReferences` | `internal static readonly SerializedCollection<PartGameDataReference>` | incremental attach + unregister |
| `ModLibrary.AllEditorTagDefinitions` | `internal static readonly SerializedCollection<EditorTagDefinition>` | validation V7 |
| `SerializedCollection<T>._collection` | `private readonly ConcurrentDictionary<KeyHash,T>` | removal |
| `VehicleEditor._editorTagLookup` | `private static Dictionary<uint,string>` | validation V7 |

---

## Appendix B — Key `<decomp>` references

```
KSA/Program.cs                                   boot order 913-1290; frame order 2071-2320; IsMainThread 520
KSA/ModLibrary.cs                                registries 66-134; LoadAll 466; Bind 1732; AttachGameData 1746
KSA/Mod.cs                                       MakeUsing 102; LoadAssetBundles 156
KSA/AssetBundle.cs                               element->type map 11-67; OnDataLoad 74
KSA/XmlLoader.cs                                 Load<T>/Deserialize
KSA/XmlHelper.cs                                 Serializers + AttributeOverrides (Components mapping)
KSA/SerializedCollection.cs                      Register/Find/GetList; no remove
KSA/SerializedId.cs                              Id/Hash/Mod/IsReferenceable
KSA/PartTemplate.cs                              OnDataLoad 130; ApplyGameData 231; ResolveConsumerFeedPoints 379
KSA/SubPartTemplate.cs, PartGameDataReference.cs, SubPartGameDataReference.cs
KSA/PartInstance.cs                              GetTemplate 94
KSA/FileReference.cs                             OnDataLoad 38; Load 66 (Loading.Task at 73)
KSA/MeshAtlasFileReference.cs                    DoLoad -> GltfLoader, per-node MeshReference
KSA/MeshReference.cs                             Load 76; Bind 120
KSA/TextureReference.cs                          DoLoad 81; Bind 122; Dispose 74
KSA/PbrMaterialReference.cs                      OnDataLoad 56
KSA/DeviceMeshInterleaved.cs                     Shared.Build/Rebuild/BindBuffers; ctor bump; Bind
KSA/PartModel.cs                                 Get 333; WriteInstancesToGpu 393
KSA/PartModelModule.cs                           Template 17; CreateComponents 63
KSA/ColliderModule.cs                            CreateComponents (per-Part, no global bake)
KSA/Loading.cs                                   Current 23; OnFrame 90 (IsMainThread guard)
KSA/LoadTask.cs                                  ctor throws when Loading.Current is null
KSA/VehicleEditor.cs                             PartWindow 45-366; SpawnPart 5152; tag registry 6058-6173
KSA/EditorTag.cs, EditorTagDefinition.cs
KSA/Constants.cs:260                             DocumentsFolderPath
KSA.Rendering/ThumbnailCreator.cs                PreparePartThumbnails 54; helpers 123-241
KSA.Rendering.Thumbnails/ThumbnailRenderer.cs    SIZE 31; RecordPartRender 111
KSA.Rendering.Thumbnails/ThumbnailDynamic.cs     Render 167; UpdateGlobalCameraData 272
RenderCore.Systems/BindlessTextureLibrary.cs     AddTexture/FreeTexture; 1024 slots, no resize
Brutal.VulkanApi.Abstractions/StagingPool.cs     Dispose = Submit + Wait

unscience/decomp/starmap/StarMap.Core/Patches/ModLibraryPatches.cs   [StarMapAllModsLoaded] == postfix on LoadAll
unscience/decomp/starmap/StarMap.Core/Patches/ProgramPatcher.cs      BeforeGui/AfterGui hook points

space-tape.lib/Thumbnails/SubpartThumbnailGenerator.cs   READ-ONLY reference: working runtime record/submit/fence
                                                         sequence + the ModLibrary.AllParts reflection helper.
                                                         Do NOT copy its camera-hijack setup (§T10.1).
space-tape.lib/PartModWriter.cs                          mod.toml assets-list maintenance
plans/done/LOAD_SUBPARTS_LOGS_ANALYSIS.md                background: why hijacking the LIVE camera causes
                                                         "Following <x>" alert spam — a problem T10.1 avoids
```
